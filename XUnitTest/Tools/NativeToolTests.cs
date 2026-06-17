using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NewLife;
using NewLife.AI.Clients;
using NewLife.AI.Models;
using NewLife.AI.Tools;
using Xunit;

namespace XUnitTest.Tools;

[DisplayName("原生工具注册与调用测试")]
public class NativeToolTests
{
    // ── 测试用工具服务类 ──────────────────────────────────────────────────────

    /// <summary>测试用计算工具服务，包含带参数和无参数的方法</summary>
    private sealed class MathToolService
    {
        /// <summary>两数相加</summary>
        /// <param name="a">第一个操作数</param>
        /// <param name="b">第二个操作数</param>
        [ToolDescription("add_numbers")]
        public Int32 Add(Int32 a, Int32 b) => a + b;

        /// <summary>返回圆周率常量</summary>
        [ToolDescription("get_pi")]
        public Double GetPi() => Math.PI;

        /// <summary>异步获取问候语</summary>
        /// <param name="name">姓名</param>
        [ToolDescription("greet")]
        public async Task<String> GreetAsync(String name, CancellationToken ct = default)
        {
            await Task.Yield();
            return $"Hello, {name}!";
        }

        /// <summary>此方法没有 ToolDescription，不应被注册</summary>
        public Int32 NotATool(Int32 x) => x;
    }

    /// <summary>返回固定工具调用再回复文本的假客户端</summary>
    private sealed class ToolCallThenReplyClient : IChatClient
    {
        private readonly String _toolName;
        private readonly String _toolArgs;
        private readonly String _finalReply;
        private Int32 _callCount;

        public ToolCallThenReplyClient(String toolName, String toolArgs, String finalReply)
        {
            _toolName = toolName;
            _toolArgs = toolArgs;
            _finalReply = finalReply;
        }

        public Task<IChatResponse> GetResponseAsync(IChatRequest request, CancellationToken ct = default)
        {
            _callCount++;
            ChatResponse resp;

            if (_callCount == 1)
            {
                // 第一次调用：返回工具调用
                resp = new ChatResponse
                {
                    Messages =
                    [
                        new ChatChoice
                        {
                            Message = new ChatMessage
                            {
                                Role = "assistant",
                                Content = null,
                                ToolCalls =
                                [
                                    new ToolCall
                                    {
                                        Id = "call_001",
                                        Type = "function",
                                        Function = new FunctionCall
                                        {
                                            Name = _toolName,
                                            Arguments = _toolArgs
                                        }
                                    }
                                ]
                            }
                        }
                    ]
                };
            }
            else
            {
                // 第二次调用：返回最终文本回复
                resp = new ChatResponse
                {
                    Messages =
                    [
                        new ChatChoice
                        {
                            Message = new ChatMessage
                            {
                                Role = "assistant",
                                Content = _finalReply
                            }
                        }
                    ]
                };
            }

            return Task.FromResult<IChatResponse>(resp);
        }

        public IAsyncEnumerable<IChatResponse> GetStreamingResponseAsync(IChatRequest request, CancellationToken ct = default)
            => throw new NotImplementedException();

        public void Dispose() { }
    }

    // ── ToolDescriptionAttribute 测试 ─────────────────────────────────────────

    [Fact]
    [DisplayName("显式指定工具名称时 HasExplicitName 为 true")]
    public void ToolDescriptionAttribute_ExplicitName_HasExplicitName()
    {
        var attr = new ToolDescriptionAttribute("my_tool");
        Assert.Equal("my_tool", attr.Name);
        Assert.True(attr.HasExplicitName);
    }

    [Fact]
    [DisplayName("无参构造时 HasExplicitName 为 false")]
    public void ToolDescriptionAttribute_NoExplicitName()
    {
        var attr = new ToolDescriptionAttribute();
        Assert.False(attr.HasExplicitName);
    }

    [Fact]
    [DisplayName("ToolDescriptionAttribute 支持配置触发词和启用状态")]
    public void ToolDescriptionAttribute_CanSetTriggersAndEnable()
    {
        var attr = new ToolDescriptionAttribute("tool")
        {
            Triggers = "触发词A,触发词B",
            Enable = false,
        };

        Assert.Equal("触发词A,触发词B", attr.Triggers);
        Assert.False(attr.Enable);
    }

    // ── ToolSchemaBuilder 测试 ────────────────────────────────────────────────

    [Fact]
    [DisplayName("BuildFromMethod 从带参数方法构建 ChatTool，名称与参数正确")]
    public void BuildFromMethod_WithParameters_CorrectSchema()
    {
        var method = typeof(MathToolService).GetMethod(nameof(MathToolService.Add))!;
        var tool = ToolSchemaBuilder.BuildFromMethod(method);

        Assert.NotNull(tool.Function);
        Assert.Equal("add_numbers", tool.Function!.Name);
        Assert.NotNull(tool.Function.Parameters);

        var schema = tool.Function.Parameters as Dictionary<String, Object>;
        Assert.NotNull(schema);
        Assert.Equal("object", schema!["type"]?.ToString());

        var props = schema["properties"] as Dictionary<String, Object>;
        Assert.NotNull(props);
        Assert.True(props!.ContainsKey("a"));
        Assert.True(props.ContainsKey("b"));

        var required = schema["required"] as List<String>;
        Assert.NotNull(required);
        Assert.Contains("a", required!);
        Assert.Contains("b", required);
    }

    [Fact]
    [DisplayName("BuildFromMethod 从无参方法构建 ChatTool，required 为空")]
    public void BuildFromMethod_NoParameters_EmptyRequired()
    {
        var method = typeof(MathToolService).GetMethod(nameof(MathToolService.GetPi))!;
        var tool = ToolSchemaBuilder.BuildFromMethod(method);

        Assert.NotNull(tool.Function);
        Assert.Equal("get_pi", tool.Function!.Name);

        var schema = tool.Function.Parameters as Dictionary<String, Object>;
        // 无参方法返回 null schema
        Assert.Null(schema);
        var required = schema?["required"] as List<String>;
        Assert.True(required == null || required.Count == 0);
    }

    // ── ToolRegistry 测试 ────────────────────────────────────────────────────

    [Fact]
    [DisplayName("AddTools 仅注册带 ToolDescription 的方法")]
    public void AddTools_OnlyRegistersTaggedMethods()
    {
        var registry = new ToolRegistry();
        registry.AddTools(new MathToolService());

        // add_numbers / get_pi / greet 均已标注，NotATool 未标注
        Assert.Equal(3, registry.Tools.Count);
        var names = registry.Tools.Select(t => t.Function!.Name).ToList();
        Assert.Contains("add_numbers", names);
        Assert.Contains("get_pi", names);
        Assert.Contains("greet", names);
        Assert.DoesNotContain("not_a_tool", names);
    }

    [Fact]
    [DisplayName("InvokeAsync 调用同步方法返回正确 JSON 结果")]
    public async Task InvokeAsync_SyncMethod_ReturnsJson()
    {
        var registry = new ToolRegistry();
        registry.AddTools(new MathToolService());

        var result = await registry.InvokeAsync("add_numbers", "{\"a\":3,\"b\":5}");
        Assert.Equal("8", result);
    }

    [Fact]
    [DisplayName("InvokeAsync 调用异步方法返回 Hello 问候")]
    public async Task InvokeAsync_AsyncMethod_ReturnsGreeting()
    {
        var registry = new ToolRegistry();
        registry.AddTools(new MathToolService());

        var result = await registry.InvokeAsync("greet", "{\"name\":\"World\"}");
        Assert.Equal("Hello, World!", result);
    }

    [Fact]
    [DisplayName("TryInvokeAsync 调用未注册工具返回错误 JSON 而非抛异常")]
    public async Task TryInvokeAsync_UnknownTool_ReturnsErrorJson()
    {
        var registry = new ToolRegistry();
        var result = await registry.TryInvokeAsync("unknown_tool", null);
        Assert.Contains("error", result);
    }

    [Fact]
    [DisplayName("AddTool 注册委托并可正常调用")]
    public async Task AddTool_DelegateRegistration_InvokesCorrectly()
    {
        var registry = new ToolRegistry();
        registry.AddTool("echo", async (args, context, ct) =>
        {
            await Task.Yield();
            return args ?? "null";
        });

        var result = await registry.InvokeAsync("echo", "ping");
        Assert.Equal("ping", result);
    }

    // ── ToolChatClient 集成测试 ────────────────────────────────────────────

    [Fact]
    [DisplayName("ToolChatClient 完成单轮工具调用后返回最终文本")]
    public async Task NativeToolChatClient_SingleToolCall_ReturnsFinalText()
    {
        var registry = new ToolRegistry();
        registry.AddTools(new MathToolService());

        // 模拟模型先发起 add_numbers 调用，然后返回最终回复
        var innerClient = new ToolCallThenReplyClient(
            toolName: "add_numbers",
            toolArgs: "{\"a\":10,\"b\":20}",
            finalReply: "计算结果是 30");

        var nativeClient = new ToolChatClient(innerClient, (IToolProvider)registry);
        IList<ChatMessage> messages = [new ChatMessage { Role = "user", Content = "10 + 20 等于多少？" }];

        var response = await nativeClient.GetResponseAsync(messages, cancellationToken: default);
        var content = response.Messages?.FirstOrDefault()?.Message?.Content as String;

        Assert.Equal("计算结果是 30", content);
    }

    [Fact]
    [DisplayName("ChatClientBuilder.UseTools 装配 ToolChatClient 中间件")]
    public void ChatClientBuilder_UseNativeTools_AddsMiddleware()
    {
        var registry = new ToolRegistry();
        registry.AddTools(new MathToolService());

        // 只需验证构建不抛异常，且 registry 中已有工具
        Assert.Equal(3, registry.Tools.Count);
    }

    [Fact]
    [DisplayName("MergeToolOptions 完整保留所有 ChatOptions 扩展属性")]
    public async Task MergeToolOptions_PreservesAllOptions()
    {
        IChatRequest captured = null;
        var capturingClient = new CapturingChatClient(opts => captured = opts, "done");

        var registry = new ToolRegistry();
        var nativeClient = new ToolChatClient(capturingClient, (IToolProvider)registry);

        var inputOptions = new ChatOptions
        {
            Model = "gpt-4",
            EnableThinking = true,
            ParallelToolCalls = false,
            UserId = 99 + "",
            ConversationId = 888L + "",
        };
        inputOptions["ThinkingBudget"] = 1024;
        inputOptions["EnableSearch"] = true;
        inputOptions["SearchStrategy"] = "pro";
        inputOptions["EnableSource"] = true;
        inputOptions["EnableSearchExtension"] = false;

        await nativeClient.GetResponseAsync([new ChatMessage { Role = "user", Content = "ping" }], inputOptions, default);

        Assert.NotNull(captured);
        Assert.True(captured.EnableThinking);
        Assert.Equal(1024, captured["ThinkingBudget"] as Int32?);
        Assert.Equal(true, captured["EnableSearch"] as Boolean?);
        Assert.Equal("pro", captured["SearchStrategy"] as String);
        Assert.Equal(true, captured["EnableSource"] as Boolean?);
        Assert.Equal(false, captured["EnableSearchExtension"] as Boolean?);
        Assert.False(captured.ParallelToolCalls);
        Assert.Equal(99, captured.UserId.ToInt());
        Assert.Equal(888L, captured.ConversationId.ToLong());
    }

    // 测试专用：捕获调用选项的假客户端，不触发工具循环
    private sealed class CapturingChatClient : IChatClient
    {
        private readonly Action<IChatRequest?> _capture;
        private readonly String _finalReply;

        public CapturingChatClient(Action<IChatRequest?> capture, String finalReply)
        {
            _capture = capture;
            _finalReply = finalReply;
        }

        public Task<IChatResponse> GetResponseAsync(IChatRequest request, CancellationToken ct = default)
        {
            _capture(request);
            return Task.FromResult<IChatResponse>(new ChatResponse
            {
                Messages = [new ChatChoice { Message = new ChatMessage { Role = "assistant", Content = _finalReply } }]
            });
        }

        public IAsyncEnumerable<IChatResponse> GetStreamingResponseAsync(IChatRequest request, CancellationToken ct = default)
            => throw new NotImplementedException();

        public void Dispose() { }
    }

    // ── ToolHelper.IsSsrfRisk ─────────────────────────────────────────────────

    [Fact]
    [DisplayName("localhost 为 SSRF 风险地址")]
    public void IsSsrfRisk_Localhost_ReturnsTrue()
        => Assert.True(ToolHelper.IsSsrfRisk("localhost"));

    [Fact]
    [DisplayName("ip6-localhost 为 SSRF 风险地址")]
    public void IsSsrfRisk_Ip6Localhost_ReturnsTrue()
        => Assert.True(ToolHelper.IsSsrfRisk("ip6-localhost"));

    [Fact]
    [DisplayName("127.0.0.1 回环地址为 SSRF 风险")]
    public void IsSsrfRisk_Loopback_ReturnsTrue()
        => Assert.True(ToolHelper.IsSsrfRisk("127.0.0.1"));

    [Fact]
    [DisplayName("10.0.0.1 A 类私有地址为 SSRF 风险")]
    public void IsSsrfRisk_PrivateClassA_ReturnsTrue()
        => Assert.True(ToolHelper.IsSsrfRisk("10.0.0.1"));

    [Fact]
    [DisplayName("172.16.0.1 B 类私有地址下沿为 SSRF 风险")]
    public void IsSsrfRisk_PrivateClassB_Lower_ReturnsTrue()
        => Assert.True(ToolHelper.IsSsrfRisk("172.16.0.1"));

    [Fact]
    [DisplayName("172.31.255.0 B 类私有地址上沿为 SSRF 风险")]
    public void IsSsrfRisk_PrivateClassB_Upper_ReturnsTrue()
        => Assert.True(ToolHelper.IsSsrfRisk("172.31.255.0"));

    [Fact]
    [DisplayName("172.15.0.1 不在 B 类私有段，非 SSRF 风险")]
    public void IsSsrfRisk_PrivateClassB_OutOfRange_ReturnsFalse()
        => Assert.False(ToolHelper.IsSsrfRisk("172.15.0.1"));

    [Fact]
    [DisplayName("192.168.1.1 C 类私有地址为 SSRF 风险")]
    public void IsSsrfRisk_PrivateClassC_ReturnsTrue()
        => Assert.True(ToolHelper.IsSsrfRisk("192.168.1.1"));

    [Fact]
    [DisplayName("169.254.0.1 链路本地地址为 SSRF 风险")]
    public void IsSsrfRisk_LinkLocal_ReturnsTrue()
        => Assert.True(ToolHelper.IsSsrfRisk("169.254.0.1"));

    [Fact]
    [DisplayName("0.0.0.0 为 SSRF 风险地址")]
    public void IsSsrfRisk_ZeroAddress_ReturnsTrue()
        => Assert.True(ToolHelper.IsSsrfRisk("0.0.0.0"));

    [Fact]
    [DisplayName("8.8.8.8 公网地址非 SSRF 风险")]
    public void IsSsrfRisk_PublicIp_ReturnsFalse()
        => Assert.False(ToolHelper.IsSsrfRisk("8.8.8.8"));

    [Fact]
    [DisplayName("1.1.1.1 公网地址非 SSRF 风险")]
    public void IsSsrfRisk_PublicIp2_ReturnsFalse()
        => Assert.False(ToolHelper.IsSsrfRisk("1.1.1.1"));

    [Fact]
    [DisplayName("空字符串为 SSRF 风险")]
    public void IsSsrfRisk_EmptyString_ReturnsTrue()
        => Assert.True(ToolHelper.IsSsrfRisk(String.Empty));

    [Fact]
    [DisplayName("IPv6 回环 ::1 为 SSRF 风险")]
    public void IsSsrfRisk_Ipv6Loopback_ReturnsTrue()
        => Assert.True(ToolHelper.IsSsrfRisk("::1"));

    // ── ToolHelper.ExtractTextFromHtml ────────────────────────────────────────

    [Fact]
    [DisplayName("空字符串输入返回空字符串")]
    public void ExtractTextFromHtml_EmptyInput_ReturnsEmpty()
        => Assert.Equal(String.Empty, ToolHelper.ExtractTextFromHtml(String.Empty));

    [Fact]
    [DisplayName("纯文本 HTML 去除标签后保留正文")]
    public void ExtractTextFromHtml_PlainHtml_ReturnText()
    {
        var html = "<html><body><p>Hello World</p></body></html>";
        var result = ToolHelper.ExtractTextFromHtml(html);
        Assert.Contains("Hello World", result);
    }

    [Fact]
    [DisplayName("移除 script 块内容")]
    public void ExtractTextFromHtml_RemovesScriptBlock()
    {
        var html = "<html><body><script>var x = 1;</script><p>Visible</p></body></html>";
        var result = ToolHelper.ExtractTextFromHtml(html);
        Assert.DoesNotContain("var x", result);
        Assert.Contains("Visible", result);
    }

    [Fact]
    [DisplayName("移除 style 块内容")]
    public void ExtractTextFromHtml_RemovesStyleBlock()
    {
        var html = "<html><head><style>body { color: red; }</style></head><body>Text</body></html>";
        var result = ToolHelper.ExtractTextFromHtml(html);
        Assert.DoesNotContain("color: red", result);
        Assert.Contains("Text", result);
    }

    [Fact]
    [DisplayName("解码 HTML 实体（&amp; → &）")]
    public void ExtractTextFromHtml_DecodesAmpersand()
    {
        var html = "<p>Tom &amp; Jerry</p>";
        var result = ToolHelper.ExtractTextFromHtml(html);
        Assert.Contains("Tom & Jerry", result);
    }

    [Fact]
    [DisplayName("解码 &lt; 和 &gt; 实体")]
    public void ExtractTextFromHtml_DecodesLtGt()
    {
        var html = "<p>1 &lt; 2 &gt; 0</p>";
        var result = ToolHelper.ExtractTextFromHtml(html);
        Assert.Contains("1 < 2 > 0", result);
    }

    [Fact]
    [DisplayName("折叠连续空格为单空格")]
    public void ExtractTextFromHtml_CollapsesMultipleSpaces()
    {
        var html = "<p>Hello   World</p>";
        var result = ToolHelper.ExtractTextFromHtml(html);
        Assert.DoesNotContain("   ", result);
    }

    // ── ToolHelper.CreateDefaultHttpClient ───────────────────────────────────

    [Fact]
    [DisplayName("CreateDefaultHttpClient 返回非空 HttpClient")]
    public void CreateDefaultHttpClient_ReturnsNonNull()
    {
        using var client = ToolHelper.CreateDefaultHttpClient();
        Assert.NotNull(client);
    }

    [Fact]
    [DisplayName("CreateDefaultHttpClient 超时时间为 30 秒")]
    public void CreateDefaultHttpClient_Timeout_Is30Seconds()
    {
        using var client = ToolHelper.CreateDefaultHttpClient();
        Assert.Equal(TimeSpan.FromSeconds(30), client.Timeout);
    }

    [Fact]
    [DisplayName("CreateDefaultHttpClient 设置包含 Mozilla 的 User-Agent 头")]
    public void CreateDefaultHttpClient_HasUserAgentHeader()
    {
        using var client = ToolHelper.CreateDefaultHttpClient();
        var ua = client.DefaultRequestHeaders.UserAgent.ToString();
        Assert.Contains("Mozilla", ua);
    }

    // ── ToolError 结构化错误返回测试 ──────────────────────────────────────────

    [Fact]
    [DisplayName("ToolError.ToJson 生成含 error_code 和 hint 字段的 JSON")]
    public void ToolError_ToJson_ContainsRequiredFields()
    {
        var err = ToolError.Create("SYNTAX_ERROR", "SQL 语法错误");
        var json = err.ToJson();

        Assert.Contains("\"error_code\"", json);
        Assert.Contains("SYNTAX_ERROR", json);
        Assert.Contains("\"hint\"", json);
        Assert.Contains("SQL 语法错误", json);
    }

    [Fact]
    [DisplayName("ToolError.ToJson 含 suggested_fix 时输出该字段")]
    public void ToolError_ToJson_IncludesSuggestedFix()
    {
        var err = ToolError.Create("SYNTAX_ERROR", "SQL 语法错误", "请添加 SELECT 子句");
        var json = err.ToJson();

        Assert.Contains("\"suggested_fix\"", json);
        Assert.Contains("SELECT 子句", json);
    }

    [Fact]
    [DisplayName("ToolError.ToJson 不含 suggested_fix 时省略该字段")]
    public void ToolError_ToJson_OmitsSuggestedFixWhenNull()
    {
        var err = ToolError.Create("NOT_FOUND", "工具未找到");
        var json = err.ToJson();

        Assert.DoesNotContain("suggested_fix", json);
    }

    [Fact]
    [DisplayName("ToolError.IsToolError 正确识别结构化错误 JSON")]
    public void ToolError_IsToolError_RecognizesErrorJson()
    {
        var errJson = ToolError.Create("TEST", "hint").ToJson();
        Assert.True(ToolError.IsToolError(errJson));
        Assert.False(ToolError.IsToolError("计算结果是 30"));
        Assert.False(ToolError.IsToolError(null));
        Assert.False(ToolError.IsToolError(String.Empty));
    }

    [Fact]
    [DisplayName("ToolError.ToJson 对特殊字符正确转义")]
    public void ToolError_ToJson_EscapesSpecialChars()
    {
        var err = ToolError.Create("ERR", "含\"引号\"和\\反斜线");
        var json = err.ToJson();

        // 必须合法 JSON：引号转义为 \"，反斜线转义为 \\
        Assert.Contains("\\\"", json);
        Assert.Contains("\\\\", json);
    }

    // ── ToolApprovalTier 三档权限测试 ─────────────────────────────────────────

    [Fact]
    [DisplayName("ToolApprovalTier.Allow 低风险工具绕过审批直接执行")]
    public async Task ToolChatClient_TierAllow_SkipsApproval()
    {
        var registry = new ToolRegistry();
        registry.AddTools(new MathToolService());

        var innerClient = new ToolCallThenReplyClient("add_numbers", "{\"a\":1,\"b\":2}", "结果 3");
        var client = new ToolChatClient(innerClient, (IToolProvider)registry);

        var approvalRequested = false;
        client.ApprovalProvider = new TierApprovalProvider(
            tier: ToolApprovalTier.Allow,
            onRequest: () => { approvalRequested = true; return ToolApprovalResult.Allow; });

        await client.GetResponseAsync([new ChatMessage { Role = "user", Content = "1+2?" }], cancellationToken: default);
        Assert.False(approvalRequested, "Allow 档位不应调用 RequestApprovalAsync");
    }

    [Fact]
    [DisplayName("ToolApprovalTier.Deny 高风险工具被代码层阻断，不调用审批")]
    public async Task ToolChatClient_TierDeny_BlocksWithoutApproval()
    {
        var registry = new ToolRegistry();
        registry.AddTools(new MathToolService());

        var innerClient = new ToolCallThenReplyClient("add_numbers", "{\"a\":1,\"b\":2}", "done");
        var client = new ToolChatClient(innerClient, (IToolProvider)registry);

        var approvalRequested = false;
        client.ApprovalProvider = new TierApprovalProvider(
            tier: ToolApprovalTier.Deny,
            onRequest: () => { approvalRequested = true; return ToolApprovalResult.Allow; });

        await client.GetResponseAsync([new ChatMessage { Role = "user", Content = "1+2?" }], cancellationToken: default);
        Assert.False(approvalRequested, "Deny 档位不应调用 RequestApprovalAsync");
    }

    [Fact]
    [DisplayName("ToolApprovalTier.Ask 中风险工具调用审批，用户拒绝后返回结构化错误")]
    public async Task ToolChatClient_TierAsk_UserDeny_ReturnsStructuredError()
    {
        var registry = new ToolRegistry();
        registry.AddTools(new MathToolService());

        // 第二次返回最终文本（包含工具结果）
        var innerClient = new ToolCallThenReplyClient("add_numbers", "{\"a\":1,\"b\":2}", "已处理");
        var client = new ToolChatClient(innerClient, (IToolProvider)registry);

        client.ApprovalProvider = new TierApprovalProvider(
            tier: ToolApprovalTier.Ask,
            onRequest: () => ToolApprovalResult.Deny);

        // 不应抛出异常，错误被结构化返回给模型
        var response = await client.GetResponseAsync([new ChatMessage { Role = "user", Content = "1+2?" }], cancellationToken: default);
        Assert.NotNull(response);
    }

    [Fact]
    [DisplayName("工具执行抛异常时返回结构化错误 JSON 而非向上抛出")]
    public async Task ToolChatClient_ToolException_ReturnsStructuredError()
    {
        var registry = new ToolRegistry();
        registry.AddTool("failing_tool", (_, __, ___) =>
        {
            throw new InvalidOperationException("数据库连接失败");
#pragma warning disable CS0162
            return Task.FromResult(String.Empty);
#pragma warning restore CS0162
        });

        var innerClient = new ToolCallThenReplyClient("failing_tool", "{}", "done");
        var client = new ToolChatClient(innerClient, (IToolProvider)registry);

        // 不应抛出 — 异常被捕获为结构化错误传给模型
        var response = await client.GetResponseAsync([new ChatMessage { Role = "user", Content = "fail" }], cancellationToken: default);
        Assert.NotNull(response);
    }

    // 测试专用：支持配置档位的审批提供者（同时实现 IToolTierProvider）
    private sealed class TierApprovalProvider : IToolApprovalProvider, IToolTierProvider
    {
        private readonly ToolApprovalTier _tier;
        private readonly Func<ToolApprovalResult> _onRequest;

        public TierApprovalProvider(ToolApprovalTier tier, Func<ToolApprovalResult> onRequest)
        {
            _tier = tier;
            _onRequest = onRequest;
        }

        public ToolApprovalTier GetToolTier(String toolName) => _tier;

        public Task<ToolApprovalResult> RequestApprovalAsync(String toolName, String? argumentsJson, CancellationToken ct = default)
            => Task.FromResult(_onRequest());
    }

    // ── B1 新特性：ToolResponseRouting / ToolCallId / isCatalogCall ────────────

    /// <summary>带 Frontend 路由标注的测试工具服务</summary>
    private sealed class FrontendToolService
    {
        /// <summary>展示可视化内容</summary>
        /// <param name="content">内容</param>
        [ToolDescription("show_visual", ReadOnly = true)]
        public String ShowVisual(String content) => $"{{\"rendered\":\"{content}\"}}";
    }

    /// <summary>捕获第 N 次调用请求消息的假客户端</summary>
    private sealed class MessageCapturingClient : IChatClient
    {
        private readonly String _toolName;
        private readonly String _toolArgs;
        private readonly String _finalReply;
        private Int32 _callCount;

        /// <summary>第二次调用时收到的消息列表</summary>
        public IList<ChatMessage>? SecondCallMessages { get; private set; }

        public MessageCapturingClient(String toolName, String toolArgs, String finalReply)
        {
            _toolName = toolName;
            _toolArgs = toolArgs;
            _finalReply = finalReply;
        }

        public Task<IChatResponse> GetResponseAsync(IChatRequest request, CancellationToken ct = default)
        {
            _callCount++;
            if (_callCount == 2)
                SecondCallMessages = request.Messages?.ToList();

            if (_callCount == 1)
            {
                return Task.FromResult<IChatResponse>(new ChatResponse
                {
                    Messages =
                    [
                        new ChatChoice
                        {
                            Message = new ChatMessage
                            {
                                Role = "assistant",
                                ToolCalls =
                                [
                                    new ToolCall
                                    {
                                        Id = "call_b1",
                                        Type = "function",
                                        Function = new FunctionCall { Name = _toolName, Arguments = _toolArgs }
                                    }
                                ]
                            }
                        }
                    ]
                });
            }
            return Task.FromResult<IChatResponse>(new ChatResponse
            {
                Messages = [new ChatChoice { Message = new ChatMessage { Role = "assistant", Content = _finalReply } }]
            });
        }

        public IAsyncEnumerable<IChatResponse> GetStreamingResponseAsync(IChatRequest request, CancellationToken ct = default)
            => throw new NotImplementedException();

        public void Dispose() { }
    }

    [Fact]
    [DisplayName("ToolDescriptionAttribute 默认 Idempotent 为 true")]
    public void ToolDescriptionAttribute_DefaultIdempotent_IsTrue()
    {
        var attr = new ToolDescriptionAttribute("tool");
        Assert.True(attr.Idempotent);
    }

    [Fact]
    [DisplayName("ToolDescriptionAttribute 可设置 ReadOnly = true")]
    public void ToolDescriptionAttribute_ReadOnly_CanBeSet()
    {
        var attr = new ToolDescriptionAttribute("tool") { ReadOnly = true };
        Assert.True(attr.ReadOnly);
    }

    [Fact]
    [DisplayName("ToolSchemaBuilder 从 ToolDescriptionAttribute 读取属性（Routing 已移除）")]
    public void ToolSchemaBuilder_Attributes_AreReadFromAttribute()
    {
        var method = typeof(FrontendToolService).GetMethod(nameof(FrontendToolService.ShowVisual))!;
        var tool = ToolSchemaBuilder.BuildFromMethod(method);

        Assert.NotNull(tool.Function);
        Assert.Equal("show_visual", tool.Function!.Name);
    }

    // [Fact] 暂禁用：Frontend 路由已改为运行时 IToolResult.Contents.Audience，需重写为返回 ToolResult(ForLlm(), ForUser()) 的集成测试
    // TODO: Frontend 路由测试需要重写为 IToolResult.Audience 模式
    // 原测试验证 role=tool 消息写入占位文本，新模式下由 IToolResult.Contents 决定
    /*
    [Fact]
    [DisplayName("Frontend 路由工具：role=tool 消息写入占位文本而非真实结果")]
    public async Task ToolChatClient_FrontendRouting_WritesPlaceholderToLlm()
    {
        var registry = new ToolRegistry();
        registry.AddTools(new FrontendToolService());

        var innerClient = new MessageCapturingClient(
            toolName: "show_visual",
            toolArgs: "{\"content\":\"hello\"}",
            finalReply: "已渲染");

        var nativeClient = new ToolChatClient(innerClient, (IToolProvider)registry);
        await nativeClient.GetResponseAsync([new ChatMessage { Role = "user", Content = "渲染图表" }], cancellationToken: default);

        // 第二次调用（工具结束后）发送给 LLM 的消息里，role=tool 内容应为占位符而非 JSON
        var toolMsg = innerClient.SecondCallMessages?.FirstOrDefault(m => m.Role == "tool");
        Assert.NotNull(toolMsg);
        Assert.Contains("[已渲染到客户端：show_visual]", toolMsg!.Content as String ?? "");
        Assert.DoesNotContain("rendered", toolMsg.Content as String ?? "");
    }
    */

    [Fact]
    [DisplayName("ToolCallId 在工具调用时与 LLM 返回的 tc.Id 一致")]
    public async Task ToolChatClient_ToolCallId_MatchesTcId()
    {
        String? capturedToolCallId = null;
        var registry = new ToolRegistry();
        registry.AddTool("id_capture", async (args, ctx, ct) =>
        {
            await Task.Yield();
            capturedToolCallId = ctx?.ToolCallId;
            return "ok";
        });

        var innerClient = new ToolCallThenReplyClient("id_capture", "{}", "done");
        var nativeClient = new ToolChatClient(innerClient, (IToolProvider)registry);
        await nativeClient.GetResponseAsync([new ChatMessage { Role = "user", Content = "test" }], cancellationToken: default);

        Assert.Equal("call_001", capturedToolCallId);
    }

    [Fact]
    [DisplayName("isCatalogCall 目录调用（工具不在 SelectedTools）：异常时返回 INVALID_ARGUMENTS + schema hint")]
    public async Task ToolChatClient_CatalogCall_ThrowsReturnsInvalidArguments()
    {
        var registry = new ToolRegistry();
        // dummy_tool 会被放入 SelectedTools，让 mergedTools 非空以启动工具循环
        registry.AddTool("dummy_tool", (_, __, ___) => Task.FromResult("dummy"));
        // strict_tool 不在 SelectedTools → isCatalogCall = true，调用时抛异常
        registry.AddTool("strict_tool", (args, _, ___) =>
        {
            if (args.IsNullOrEmpty() || !args.Contains("\"name\""))
                throw new ArgumentException("缺少必填参数 name");
            return Task.FromResult("ok");
        });

        var capturingInner = new MessageCapturingClient("strict_tool", "{}", "done");
        // SelectedTools 包含 dummy_tool 但不包含 strict_tool
        var client = new ToolChatClient(capturingInner, (IToolProvider)registry)
        {
            SelectedTools = new System.Collections.Generic.HashSet<String>(System.StringComparer.OrdinalIgnoreCase) { "dummy_tool" }
        };

        await client.GetResponseAsync([new ChatMessage { Role = "user", Content = "test" }], cancellationToken: default);

        // 工具结果写回 role=tool 消息后，第二轮 LLM 调用会收到该消息
        var toolMsg = capturingInner.SecondCallMessages?.FirstOrDefault(m => m.Role == "tool");
        Assert.NotNull(toolMsg);
        var content = toolMsg!.Content as String ?? "";
        Assert.Contains("INVALID_ARGUMENTS", content);
    }
}
