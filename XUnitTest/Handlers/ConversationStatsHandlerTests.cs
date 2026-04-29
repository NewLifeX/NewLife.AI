using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NewLife.AI.Models;
using NewLife.AI.Services;
using NewLife.ChatAI.Entity;
using NewLife.ChatAI.Handlers;
using NewLife.ChatAI.Services;
using Xunit;

namespace XUnitTest.Handlers;

/// <summary>ConversationStatsHandler 单元测试：OnAfter Token 累加逻辑</summary>
[DisplayName("ConversationStatsHandler 测试")]
public class ConversationStatsHandlerTests
{
    #region OnBefore — 总是返回 CompletedTask

    [Fact]
    [DisplayName("OnBefore—始终立即返回")]
    public async Task OnBefore_AlwaysReturnsImmediately()
    {
        var handler = new ConversationStatsHandler(null);
        await handler.OnBefore(BuildFlow(), CancellationToken.None);
    }

    #endregion

    #region OnAfter — 早返回保护

    [Fact]
    [DisplayName("OnAfter—非 MessageFlowContext 时立即返回")]
    public async Task OnAfter_NotFlowContext_ReturnsImmediately()
    {
        var handler = new ConversationStatsHandler(null);
        await handler.OnAfter(new FakeContext(), CancellationToken.None);
    }

    [Fact]
    [DisplayName("OnAfter—Conversation 为 null 时立即返回")]
    public async Task OnAfter_NullConversation_ReturnsImmediately()
    {
        var handler = new ConversationStatsHandler(null);
        var flow = new MessageFlowContext
        {
            Conversation = null!,
            ModelConfig = new ModelConfig(),
        };

        await handler.OnAfter(flow, CancellationToken.None);
    }

    #endregion

    #region OnAfter — Token 累加（通过截断 Update 调用验证字段）

    [Fact]
    [DisplayName("OnAfter—Usage 有值时累加 Token 到 Conversation")]
    public async Task OnAfter_WithUsage_AccumulatesTokens()
    {
        var handler = new ConversationStatsHandler(null);
        var conv = new Conversation { InputTokens = 100, OutputTokens = 200, TotalTokens = 300 };
        var flow = BuildFlow(conv);
        flow.Usage = new UsageDetails { InputTokens = 10, OutputTokens = 20, TotalTokens = 30, ElapsedMs = 500 };

        try { await handler.OnAfter(flow, CancellationToken.None); }
        catch { /* DB 未就绪：忽略 Update/CountByConversationId 异常 */ }

        Assert.Equal(110, conv.InputTokens);
        Assert.Equal(220, conv.OutputTokens);
        Assert.Equal(330, conv.TotalTokens);
    }

    [Fact]
    [DisplayName("OnAfter—Usage 为 null 时不累加 Token")]
    public async Task OnAfter_NullUsage_NoTokenAccumulation()
    {
        var handler = new ConversationStatsHandler(null);
        var conv = new Conversation { InputTokens = 100, OutputTokens = 200, TotalTokens = 300 };
        var flow = BuildFlow(conv);
        flow.Usage = null;

        try { await handler.OnAfter(flow, CancellationToken.None); }
        catch { /* 忽略 DB 异常 */ }

        Assert.Equal(100, conv.InputTokens);
        Assert.Equal(200, conv.OutputTokens);
        Assert.Equal(300, conv.TotalTokens);
    }

    [Fact]
    [DisplayName("OnAfter—ModelConfig 有效时更新 ModelName")]
    public async Task OnAfter_WithModelConfig_UpdatesModelName()
    {
        var handler = new ConversationStatsHandler(null);
        var conv = new Conversation();
        var flow = BuildFlow(conv);
        flow.ModelConfig = new ModelConfig { Name = "GPT-4o" };

        try { await handler.OnAfter(flow, CancellationToken.None); }
        catch { /* 忽略 DB 异常 */ }

        Assert.Equal("GPT-4o", conv.ModelName);
    }

    #endregion

    // ── 工厂方法 ──────────────────────────────────────────────────────────

    private static MessageFlowContext BuildFlow(Conversation? conv = null) => new()
    {
        Conversation = conv ?? new Conversation(),
        ModelConfig = new ModelConfig { Name = "test-model" },
    };

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
        public ISet<String> ResolvedSkillNames { get; } = new HashSet<String>();
        public Int32 MaxTokens { get; set; }
        public Double? Temperature { get; set; }
        public String? FinishReason { get; set; }
        public System.Text.StringBuilder ContentBuilder { get; } = new();
        public System.Text.StringBuilder ThinkingBuilder { get; } = new();
        public List<ToolCallDto> ToolCalls { get; } = [];
        public UsageDetails? Usage { get; set; }
        public Boolean HasError { get; set; }
        public Boolean Cancel { get; set; }
        public String? CancelCode { get; set; }
        public String? CancelMessage { get; set; }
        public IDictionary<String, Object?> Items { get; } = new Dictionary<String, Object?>(StringComparer.OrdinalIgnoreCase);
        public Object? this[String key]
        {
            get => Items.TryGetValue(key, out var v) ? v : null;
            set => Items[key] = value;
        }
    }
}
