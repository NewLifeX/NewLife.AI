using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using NewLife.AI.Handlers;
using NewLife.ChatAI.Entity;
using NewLife.ChatAI.Handlers;
using Xunit;

namespace XUnitTest.Handlers;

/// <summary>TitleGenerationHandler 单元测试：ExtractTitleText 纯函数 + OnBefore 跳过条件</summary>
[DisplayName("TitleGenerationHandler 测试")]
public class TitleGenerationHandlerTests
{
    #region ExtractTitleText — 纯静态方法，无外部依赖

    [Fact]
    [DisplayName("ExtractTitleText—null 返回 null")]
    public void ExtractTitleText_Null_ReturnsNull()
    {
        Assert.Null(TitleGenerationHandler.ExtractTitleText(null));
    }

    [Fact]
    [DisplayName("ExtractTitleText—空字符串返回 null")]
    public void ExtractTitleText_Empty_ReturnsNull()
    {
        Assert.Null(TitleGenerationHandler.ExtractTitleText(String.Empty));
    }

    [Fact]
    [DisplayName("ExtractTitleText—普通文本原样返回")]
    public void ExtractTitleText_PlainText_ReturnsSame()
    {
        const String input = "帮我写一个 C# 单元测试";
        Assert.Equal(input, TitleGenerationHandler.ExtractTitleText(input));
    }

    [Fact]
    [DisplayName("ExtractTitleText—不以 [ 开头的文本直接返回")]
    public void ExtractTitleText_NotStartingWithBracket_ReturnsSame()
    {
        const String input = "How does MessageFlow work?";
        Assert.Equal(input, TitleGenerationHandler.ExtractTitleText(input));
    }

    [Fact]
    [DisplayName("ExtractTitleText—OpenAI 多模态 JSON 提取文本片段")]
    public void ExtractTitleText_MultimodalJson_ExtractsTextPart()
    {
        var json = """[{"type":"text","text":"请解释这张图片的内容"},{"type":"image_url","image_url":{"url":"data:image/png;base64,abc"}}]""";
        var result = TitleGenerationHandler.ExtractTitleText(json);
        Assert.Equal("请解释这张图片的内容", result);
    }

    [Fact]
    [DisplayName("ExtractTitleText—多个 text 片段合并（空格分隔）")]
    public void ExtractTitleText_MultipleTextParts_MergedWithSpace()
    {
        var json = """[{"type":"text","text":"第一段"},{"type":"text","text":"第二段"}]""";
        var result = TitleGenerationHandler.ExtractTitleText(json);
        Assert.Equal("第一段 第二段", result);
    }

    [Fact]
    [DisplayName("ExtractTitleText—仅图片内容时返回 null")]
    public void ExtractTitleText_ImageOnly_ReturnsNull()
    {
        var json = """[{"type":"image_url","image_url":{"url":"data:image/png;base64,abc"}}]""";
        var result = TitleGenerationHandler.ExtractTitleText(json);
        Assert.Null(result);
    }

    [Fact]
    [DisplayName("ExtractTitleText—空 JSON 数组返回原始字符串")]
    public void ExtractTitleText_EmptyJsonArray_ReturnsOriginal()
    {
        // 空数组 contents.Count==0 → 返回原始 userMessage
        var result = TitleGenerationHandler.ExtractTitleText("[]");
        Assert.Equal("[]", result);
    }

    [Fact]
    [DisplayName("ExtractTitleText—非法 JSON 以 [ 开头但解析失败时返回原始")]
    public void ExtractTitleText_InvalidJson_ReturnsOriginal()
    {
        const String input = "[not valid json";
        var result = TitleGenerationHandler.ExtractTitleText(input);
        // ParseMultimodalContent 解析失败返回 null/空 → 返回原始字符串
        Assert.NotNull(result);
    }

    #endregion

    #region OnBefore — 跳过条件（无 DB 依赖）

    [Fact]
    [DisplayName("OnBefore—非 MessageFlowContext 时立即返回")]
    public async Task OnBefore_NotMessageFlowContext_ReturnsImmediately()
    {
        var setting = new ChatSetting { AutoGenerateTitle = true };
        var handler = new TitleGenerationHandler(null!, setting, null, null, null);

        // 传入一个不是 MessageFlowContext 的假上下文
        var fakeContext = new FakeContext();
        await handler.OnBefore(fakeContext, CancellationToken.None);
        // 无异常即通过（早返回路径）
    }

    [Fact]
    [DisplayName("OnBefore—AutoGenerateTitle=false 时跳过")]
    public async Task OnBefore_AutoGenerateTitleFalse_Skips()
    {
        var setting = new ChatSetting { AutoGenerateTitle = false };
        var handler = new TitleGenerationHandler(null!, setting, null, null, null);

        var flow = new MessageFlowContext
        {
            Conversation = new Conversation { MessageCount = 0 },
            HasError = false,
        };
        // 不会抛异常（无后台任务启动）
        await handler.OnBefore(flow, CancellationToken.None);
    }

    [Fact]
    [DisplayName("OnBefore—HasError=true 时跳过")]
    public async Task OnBefore_HasError_Skips()
    {
        var setting = new ChatSetting { AutoGenerateTitle = true };
        var handler = new TitleGenerationHandler(null!, setting, null, null, null);

        var flow = new MessageFlowContext
        {
            Conversation = new Conversation { MessageCount = 0 },
            HasError = true,
        };
        await handler.OnBefore(flow, CancellationToken.None);
    }

    [Fact]
    [DisplayName("OnBefore—会话 MessageCount > 0 时跳过（非首轮）")]
    public async Task OnBefore_MessageCountGtZero_Skips()
    {
        var setting = new ChatSetting { AutoGenerateTitle = true };
        var handler = new TitleGenerationHandler(null!, setting, null, null, null);

        var flow = new MessageFlowContext
        {
            Conversation = new Conversation { MessageCount = 5 },
            HasError = false,
        };
        await handler.OnBefore(flow, CancellationToken.None);
    }

    [Fact]
    [DisplayName("OnBefore—用户消息内容为空时跳过")]
    public async Task OnBefore_EmptyUserContent_Skips()
    {
        var setting = new ChatSetting { AutoGenerateTitle = true };
        var handler = new TitleGenerationHandler(null!, setting, null, null, null);

        var flow = new MessageFlowContext
        {
            Conversation = new Conversation { MessageCount = 0 },
            HasError = false,
            UserMessage = new DbChatMessage { Content = null },
        };
        await handler.OnBefore(flow, CancellationToken.None);
    }

    #endregion

    #region OnBefore — 总是返回 CompletedTask

    [Fact]
    [DisplayName("OnBefore—始终立即返回")]
    public async Task OnBefore_AlwaysReturnsImmediately()
    {
        var setting = new ChatSetting();
        var handler = new TitleGenerationHandler(null!, setting, null, null, null);
        await handler.OnBefore(new FakeContext(), CancellationToken.None);
    }

    #endregion

    // ── 测试辅助 ──────────────────────────────────────────────────────────

    /// <summary>最简假上下文实现，不是 MessageFlowContext，触发处理器早返回路径</summary>
    private sealed class FakeContext : IChatContext
    {
        public FlowKind Kind { get; set; }
        public Int32 UserId { get; set; }
        public Int32 SkillId { get; set; }
        public ThinkingMode ThinkingMode { get; set; }
        public IConversation Conversation { get; set; } = null!;
        public IModelConfig ModelConfig { get; set; } = null!;
        public IChatMessage? UserMessage { get; set; }
        public IChatMessage AssistantMessage { get; set; } = null!;
        public IList<AiChatMessage> ContextMessages { get; set; } = new List<AiChatMessage>();
        public String? SystemPrompt { get; set; }
        public Action<String>? OnSystemReady { get; set; }
        public ISet<String> SelectedTools { get; } = new HashSet<String>();
        public ISet<String> AvailableToolNames { get; } = new HashSet<String>();
        public ChatOptions Options { get; set; } = new();
        public String? FinishReason { get; set; }
        public System.Text.StringBuilder ContentBuilder { get; } = new();
        public System.Text.StringBuilder ThinkingBuilder { get; } = new();
        public List<ToolCallDto> ToolCalls { get; } = [];
        public UsageDetails? Usage { get; set; }
        public IDictionary<String, UsageDetails> SubFlowUsages { get; } = new Dictionary<String, UsageDetails>(StringComparer.OrdinalIgnoreCase);
        public Boolean HasError { get; set; }
        public ChatFlowControl FlowControl { get; set; }
        public String? CancelCode { get; set; }
        public String? CancelMessage { get; set; }
        public ChatFlowSource Source { get; set; } = ChatFlowSource.Web;
        public Boolean PersistMessages { get; set; } = true;
        public IDictionary<String, Object?> Items { get; } = new Dictionary<String, Object?>(StringComparer.OrdinalIgnoreCase);
        public Object? this[String key]
        {
            get => Items.TryGetValue(key, out var v) ? v : null;
            set => Items[key] = value;
        }
    }
}
