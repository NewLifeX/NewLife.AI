using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using NewLife.AI.Handlers;
using NewLife.AI.Tools;
using NewLife.ChatAI.Entity;
using NewLife.ChatAI.Handlers;
using Xunit;

namespace XUnitTest.Handlers;

/// <summary>SkillActivationHandler 单元测试：OnBefore/OnAfter 空值保护、system 消息注入逻辑</summary>
[DisplayName("SkillActivationHandler 测试")]
public class SkillActivationHandlerTests
{
    #region OnBefore — skillService 为 null 时立即返回

    [Fact]
    [DisplayName("OnBefore—skillService 为 null 时立即返回")]
    public async Task OnBefore_NullSkillService_ReturnsImmediately()
    {
        var handler = new SkillActivationHandler([], null);
        var ctx = BuildContext();

        await handler.OnBefore(ctx, CancellationToken.None);

        // 未注入任何消息
        Assert.Empty(ctx.ContextMessages);
    }

    [Fact]
    [DisplayName("OnBefore—非 MessageFlowContext 且 skillService=null 时正常返回")]
    public async Task OnBefore_NotFlowContext_SkillServiceNull_ReturnsImmediately()
    {
        var handler = new SkillActivationHandler([], null);
        // SkillActivationHandler 先检查 skillService == null，再检查 MessageFlowContext
        await handler.OnBefore(new FakeContext(), CancellationToken.None);
    }

    #endregion

    #region OnAfter — 早返回保护

    [Fact]
    [DisplayName("OnAfter—skillService 为 null 时立即返回")]
    public async Task OnAfter_NullSkillService_ReturnsImmediately()
    {
        var handler = new SkillActivationHandler([], null);
        var ctx = BuildContext();
        ctx.SkillId = 1;
        ctx.UserId = 100;

        await handler.OnAfter(ctx, CancellationToken.None);
    }

    [Fact]
    [DisplayName("OnAfter—SkillId == 0 时立即返回（无技能激活）")]
    public async Task OnAfter_SkillIdZero_ReturnsImmediately()
    {
        var handler = new SkillActivationHandler([], null);
        var ctx = BuildContext();
        ctx.SkillId = 0;
        ctx.UserId = 100;

        await handler.OnAfter(ctx, CancellationToken.None);
    }

    [Fact]
    [DisplayName("OnAfter—UserId == 0 时立即返回（匿名用户）")]
    public async Task OnAfter_UserIdZero_ReturnsImmediately()
    {
        var handler = new SkillActivationHandler([], null);
        var ctx = BuildContext();
        ctx.SkillId = 5;
        ctx.UserId = 0;

        await handler.OnAfter(ctx, CancellationToken.None);
    }

    [Fact]
    [DisplayName("OnAfter—HasError=true 时立即返回")]
    public async Task OnAfter_HasError_ReturnsImmediately()
    {
        var handler = new SkillActivationHandler([], null);
        var ctx = BuildContext();
        ctx.SkillId = 5;
        ctx.UserId = 100;
        ctx.HasError = true;

        await handler.OnAfter(ctx, CancellationToken.None);
    }

    #endregion

    #region ResolveSkillByContent — 基类实现为空操作

    [Fact]
    [DisplayName("ResolveSkillByContent（基类）—不修改 SkillId")]
    public void ResolveSkillByContent_BaseClass_NoOp()
    {
        // 通过可访问的子类包装暴露 protected 方法
        var handler = new ExposedSkillActivationHandler([], null);
        var ctx = BuildContext();
        ctx.SkillId = 0;

        handler.ExposedResolveSkillByContent(ctx, "任意消息内容");

        Assert.Equal(0, ctx.SkillId);
    }

    #endregion

    // ── 工厂方法 ──────────────────────────────────────────────────────────

    private static MessageFlowContext BuildContext() => new()
    {
        Conversation = new Conversation(),
        ModelConfig = new ModelConfig(),
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
        public Object? this[String key]
        {
            get => Items.TryGetValue(key, out var v) ? v : null;
            set => Items[key] = value;
        }
    }

    /// <summary>暴露 protected ResolveSkillByContent 供测试调用</summary>
    private sealed class ExposedSkillActivationHandler(
        IEnumerable<IToolProvider> toolProviders,
        SkillService? skillService)
        : SkillActivationHandler(toolProviders, skillService)
    {
        public void ExposedResolveSkillByContent(IChatContext context, String? content)
            => ResolveSkillByContent(context, content);
    }
}
