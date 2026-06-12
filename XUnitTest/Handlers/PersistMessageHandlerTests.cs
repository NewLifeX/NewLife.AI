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

/// <summary>PersistMessageHandler 单元测试：OnAfter 早返回保护 + 字段写入逻辑</summary>
[DisplayName("PersistMessageHandler 测试")]
public class PersistMessageHandlerTests
{
    #region OnBefore — 总是返回 CompletedTask

    [Fact]
    [DisplayName("OnBefore—始终立即返回")]
    public async Task OnBefore_AlwaysReturnsImmediately()
    {
        var handler = new PersistMessageHandler(ChatSetting.Current);
        await handler.OnBefore(BuildFlow(withAssistantMessage: false), CancellationToken.None);
    }

    #endregion

    #region OnAfter — 早返回保护

    [Fact]
    [DisplayName("OnAfter—非 MessageFlowContext 时立即返回")]
    public async Task OnAfter_NotFlowContext_ReturnsImmediately()
    {
        var handler = new PersistMessageHandler(ChatSetting.Current);
        await handler.OnAfter(new FakeContext(), CancellationToken.None);
    }

    [Fact]
    [DisplayName("OnAfter—AssistantMessage 为 null 时立即返回")]
    public async Task OnAfter_NullAssistantMessage_ReturnsImmediately()
    {
        var handler = new PersistMessageHandler(ChatSetting.Current);
        var flow = BuildFlow(withAssistantMessage: false);

        await handler.OnAfter(flow, CancellationToken.None);
        // 无异常即通过
    }

    #endregion

    #region OnAfter — 字段写入（通过子类截断 Update 调用）

    [Fact]
    [DisplayName("OnAfter—ThinkingBuilder 有内容时写入 ThinkingContent")]
    public async Task OnAfter_ThinkingBuilderHasContent_WrittenToThinkingContent()
    {
        var handler = new PersistMessageHandler(ChatSetting.Current);
        var flow = BuildFlow(withAssistantMessage: true);
        flow.ThinkingBuilder.Append("这是思考内容");
        flow.ContentBuilder.Append("回复正文");

        // 注意：OnAfter 最终会调用 assistantMsg.Update()，这需要 DB。
        // 此处测试提前检查，通过捕获异常后验证字段已写入
        try { await handler.OnAfter(flow, CancellationToken.None); }
        catch { /* DB 未就绪时忽略持久化异常，仍然验证字段赋值发生 */ }

        Assert.Equal("回复正文", flow.AssistantMessage.Content);
        Assert.Equal("这是思考内容", flow.AssistantMessage.ThinkingContent);
    }

    [Fact]
    [DisplayName("OnAfter—HasError=true 且内容为空时写入 [生成失败]")]
    public async Task OnAfter_HasErrorAndEmptyContent_WritesFailurePlaceholder()
    {
        var handler = new PersistMessageHandler(ChatSetting.Current);
        var flow = BuildFlow(withAssistantMessage: true);
        flow.HasError = true;
        // ContentBuilder 为空

        try { await handler.OnAfter(flow, CancellationToken.None); }
        catch { /* 忽略 DB 异常 */ }

        Assert.Equal("[生成失败]", flow.AssistantMessage.Content);
    }

    [Fact]
    [DisplayName("OnAfter—HasError=true 且有错误详情时附加到内容末尾")]
    public async Task OnAfter_HasErrorWithDetail_AppendsToContent()
    {
        var handler = new PersistMessageHandler(ChatSetting.Current);
        var flow = BuildFlow(withAssistantMessage: true);
        flow.HasError = true;
        flow.ContentBuilder.Append("部分回复");
        flow.DeferredError = new ChatStreamEvent { Type = "error", Error = "Rate limit exceeded" };

        try { await handler.OnAfter(flow, CancellationToken.None); }
        catch { /* 忽略 DB 异常 */ }

        var content = flow.AssistantMessage.Content;
        Assert.Contains("部分回复", content);
        Assert.Contains("Rate limit exceeded", content);
    }

    [Fact]
    [DisplayName("OnAfter—Usage 有值时写入 Token 字段")]
    public async Task OnAfter_WithUsage_WritesTokenFields()
    {
        var handler = new PersistMessageHandler(ChatSetting.Current);
        var flow = BuildFlow(withAssistantMessage: true);
        flow.ContentBuilder.Append("回复内容");
        flow.Usage = new UsageDetails { InputTokens = 15, OutputTokens = 30, TotalTokens = 45, ElapsedMs = 500 };

        try { await handler.OnAfter(flow, CancellationToken.None); }
        catch { /* 忽略 DB 异常 */ }

        Assert.Equal(15, flow.AssistantMessage.InputTokens);
        Assert.Equal(30, flow.AssistantMessage.OutputTokens);
        Assert.Equal(45, flow.AssistantMessage.TotalTokens);
    }

    [Fact]
    [DisplayName("OnAfter—ToolCalls 有值时写入 ToolNames 字段")]
    public async Task OnAfter_WithToolCalls_WritesToolNames()
    {
        var handler = new PersistMessageHandler(ChatSetting.Current);
        var flow = BuildFlow(withAssistantMessage: true);
        flow.ContentBuilder.Append("使用了工具");
        flow.ToolCalls.Add(new ToolCallDto("tool-1", "search_web", ToolCallStatus.Done));
        flow.ToolCalls.Add(new ToolCallDto("tool-2", "read_file", ToolCallStatus.Done));

        try { await handler.OnAfter(flow, CancellationToken.None); }
        catch { /* 忽略 DB 异常 */ }

        Assert.Contains("search_web", flow.AssistantMessage.ToolNames);
        Assert.Contains("read_file", flow.AssistantMessage.ToolNames);
    }

    #endregion

    // ── 工厂方法 ──────────────────────────────────────────────────────────

    private static MessageFlowContext BuildFlow(Boolean withAssistantMessage = true)
    {
        var flow = new MessageFlowContext
        {
            Conversation = new Conversation { Id = 1 },
            ModelConfig = new ModelConfig { Code = "test-model" },
        };

        if (withAssistantMessage)
            flow.AssistantMessage = new DbChatMessage { Id = 1 };

        return flow;
    }

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
        public IList<String> SystemSegments { get; } = new List<String>();
        public IList<String> TailSegments { get; } = new List<String>();
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
        public IDictionary<String, Object?> Items { get; } = new Dictionary<String, Object?>(StringComparer.OrdinalIgnoreCase);
        public IList<ISkill> ActivatedSkills => [];
        public Object? this[String key]
        {
            get => Items.TryGetValue(key, out var v) ? v : null;
            set => Items[key] = value;
        }
    }
}
