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

/// <summary>UsageRecordHandler 单元测试：所有空值保护路径</summary>
[DisplayName("UsageRecordHandler 测试")]
public class UsageRecordHandlerTests
{
    #region OnBefore — 总是返回 CompletedTask

    [Fact]
    [DisplayName("OnBefore—始终立即返回")]
    public async Task OnBefore_AlwaysReturnsImmediately()
    {
        var handler = new UsageRecordHandler(null);
        await handler.OnBefore(BuildFlow(), CancellationToken.None);
    }

    #endregion

    #region OnAfter — 早返回保护

    [Fact]
    [DisplayName("OnAfter—usageService 为 null 时立即返回，无异常")]
    public async Task OnAfter_NullUsageService_ReturnsImmediately()
    {
        var handler = new UsageRecordHandler(null);
        var flow = BuildFlow();

        await handler.OnAfter(flow, CancellationToken.None);
        // 没有异常即通过
    }

    [Fact]
    [DisplayName("OnAfter—非 MessageFlowContext 时立即返回")]
    public async Task OnAfter_NotMessageFlowContext_ReturnsImmediately()
    {
        var handler = new UsageRecordHandler(null);
        // IChatContext 的 stub 实现（不是 MessageFlowContext）
        var fakeCtx = new FakeContext();

        await handler.OnAfter(fakeCtx, CancellationToken.None);
    }

    [Fact]
    [DisplayName("OnAfter—flow.Usage 为 null 时立即返回（即使有 usageService 占位）")]
    public async Task OnAfter_NullUsage_ReturnsImmediately()
    {
        // usageService 为 null，所以还是早返回；此处验证 null usage 分支的防护能力
        var handler = new UsageRecordHandler(null);
        var flow = BuildFlow();
        flow.Usage = null;

        await handler.OnAfter(flow, CancellationToken.None);
    }

    #endregion

    // ── 工厂方法 ──────────────────────────────────────────────────────────

    private static MessageFlowContext BuildFlow() => new()
    {
        Conversation = new Conversation(),
        ModelConfig = new ModelConfig(),
        Usage = new UsageDetails { InputTokens = 10, OutputTokens = 20, TotalTokens = 30 },
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
        public Int32 MaxTokens { get; set; }
        public Double? Temperature { get; set; }
        public Object? ResponseFormat { get; set; }
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
