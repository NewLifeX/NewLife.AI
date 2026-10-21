#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NewLife;
using NewLife.AI.Clients;
using NewLife.AI.Models;
using NewLife.AI.Tools;
using L10n = NewLife.Localization;
using Xunit;

namespace XUnitTest.Tools;

/// <summary>工具 Schema 本地化测试。验证工具描述与参数描述按当前语言翻译、契约字段保持原样、缺译文回退源文</summary>
/// <remarks>Localization 是进程级全局状态，归入串行集合，避免与其他语言测试相互干扰</remarks>
[Collection("Localization")]
public class ToolSchemaLocalizerTests
{
    private const String ToolText = "Gets the data context of the current page";
    private const String ParamText = "Form mode: add or edit";
    private const String ToolTextZh = "获取当前页面的数据上下文";
    private const String ParamTextZh = "表单模式：add 新增 / edit 编辑";
    private const String TestCulture = "xx-Cube";

    /// <summary>构造带嵌套参数 Schema 的工具定义，结构与 ToolSchemaBuilder 输出一致</summary>
    private static ChatTool CreateTool() => new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "get_data_context",
            Description = ToolText,
            Parameters = new Dictionary<String, Object?>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<String, Object?>
                {
                    ["mode"] = new Dictionary<String, Object?>
                    {
                        ["type"] = "string",
                        ["description"] = ParamText,
                        ["enum"] = new List<Object?> { "add", "edit" },
                    },
                },
                ["required"] = new List<Object?> { "mode" },
            },
        },
    };

    /// <summary>切换到测试语言并注册词表，Dispose 时还原</summary>
    private static IDisposable UseTestLanguage() => new LanguageScope();

    private sealed class LanguageScope : IDisposable
    {
        private readonly CultureInfo? _old = L10n.Culture;
        private readonly CultureInfo _culture = new(TestCulture);

        public LanguageScope()
        {
            L10n.Register(_culture, new Dictionary<String, String>
            {
                [ToolText] = ToolTextZh,
                [ParamText] = ParamTextZh,
            });
            L10n.Culture = _culture;
        }

        public void Dispose()
        {
            L10n.Culture = _old;
            L10n.Unregister(_culture);
        }
    }

    /// <summary>最小工具提供者：暴露 get_data_context 工具</summary>
    private sealed class FakeToolProvider : IToolProvider
    {
        public IList<ChatTool> GetTools(ISet<String>? filterNames = null) => [CreateTool()];

        public Task<IToolResult> CallToolAsync(String toolName, String? arguments, ToolCallContext? context = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IToolResult>(new ToolResult("{}"));
    }

    /// <summary>伪内层客户端：记录请求并返回脚本化响应</summary>
    private sealed class FakeInnerClient : IChatClient
    {
        public readonly List<IChatRequest> Requests = [];
        private readonly Queue<IChatResponse> _responses = new();

        public void EnqueueResponse(IChatResponse response) => _responses.Enqueue(response);

        public Task<IChatResponse> GetResponseAsync(IChatRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(_responses.Dequeue());
        }

        public async IAsyncEnumerable<IChatResponse> GetStreamingResponseAsync(IChatRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            yield return _responses.Dequeue();
            await Task.CompletedTask.ConfigureAwait(false);
        }

        public void Dispose() { }
    }

    [Fact]
    [DisplayName("工具描述与参数描述按当前语言翻译")]
    public void Tool_Description_Translated()
    {
        using var _ = UseTestLanguage();
        var tool = CreateTool();

        var localized = ToolSchemaLocalizer.Localize(tool);

        Assert.NotSame(tool, localized);
        Assert.Equal(ToolTextZh, localized.Function!.Description);

        var properties = (IDictionary<String, Object?>)((IDictionary<String, Object?>)localized.Function.Parameters!)["properties"]!;
        var mode = (IDictionary<String, Object?>)properties["mode"]!;
        Assert.Equal(ParamTextZh, mode["description"]);

        // 契约字段保持原样：参数名、枚举值
        Assert.Equal(new Object?[] { "add", "edit" }, ((IList<Object?>)mode["enum"]!).ToArray());
        Assert.True(properties.ContainsKey("mode"));

        // 注册表中的原始定义不被改写
        Assert.Equal(ToolText, tool.Function.Description);
        var originMode = (IDictionary<String, Object?>)((IDictionary<String, Object?>)tool.Function.Parameters!)["properties"]!;
        Assert.Equal(ParamText, ((IDictionary<String, Object?>)originMode["mode"]!)["description"]);
    }

    [Fact]
    [DisplayName("缺少译文回退源文并复用原实例")]
    public void Missing_Translation_FallsBack()
    {
        using var _ = UseTestLanguage();

        var tool = new ChatTool { Function = new FunctionDefinition { Name = "plain", Description = "Untranslated tool description" } };

        Assert.Same(tool, ToolSchemaLocalizer.Localize(tool));
    }

    [Fact]
    [DisplayName("未注册语言回退源文")]
    public void UnsupportedCulture_FallsBack()
    {
        var old = L10n.Culture;
        try
        {
            L10n.Culture = new CultureInfo("sw-KE");

            var tool = CreateTool();
            Assert.Same(tool, ToolSchemaLocalizer.Localize(tool));
        }
        finally
        {
            L10n.Culture = old;
        }
    }

    [Fact]
    [DisplayName("关闭本地化后按源文原样提交")]
    public void Disabled_ReturnsOriginal()
    {
        using var _ = UseTestLanguage();
        var tool = CreateTool();

        ToolSchemaLocalizer.Enabled = false;
        try
        {
            Assert.Same(tool, ToolSchemaLocalizer.Localize(tool));
        }
        finally
        {
            ToolSchemaLocalizer.Enabled = true;
        }
    }

    [Fact]
    [DisplayName("ToolChatClient 提交给 LLM 的工具描述已翻译")]
    public async Task ToolChatClient_InjectsLocalizedDescription()
    {
        using var _ = UseTestLanguage();

        var response = new ChatResponse { Object = "chat.completion" };
        response.Add("done", null, FinishReason.Stop);

        var inner = new FakeInnerClient();
        inner.EnqueueResponse(response);
        using var client = new ToolChatClient(inner, new FakeToolProvider());

        await client.GetResponseAsync(new ChatRequest
        {
            Model = "test-model",
            Messages = [new ChatMessage { Role = "user", Content = "hello" }],
        });

        var tools = inner.Requests[0].Tools;
        Assert.NotNull(tools);
        var tool = Assert.Single(tools!);
        Assert.Equal(ToolTextZh, tool.Function!.Description);
    }
}
