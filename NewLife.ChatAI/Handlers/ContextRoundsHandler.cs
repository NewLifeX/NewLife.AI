namespace NewLife.ChatAI.Handlers;

/// <summary>上下文轮数限制处理器。在 OnBefore 阶段统计当前会话已有的用户消息轮数，超出用户设置的 ContextRounds 上限时拒绝继续对话</summary>
/// <remarks>
/// <para>仅对 Web 来源生效（<see cref="ChatFlowSource.Web"/>），渠道/网关调用不受约束。</para>
/// <para>轮数上限优先读取 <see cref="UserSetting.ContextRounds"/>；用户无记录时回退到 <see cref="IChatSetting.DefaultContextRounds"/>；两者均为 0 时默认 10。</para>
/// <para>计数直接从 <see cref="IChatContext.ContextMessages"/> 中统计 role=user 的消息数（含本轮），无额外 DB 查询。</para>
/// <para>拦截时设置 <see cref="IChatContext.FlowControl"/> = <see cref="ChatFlowControl.Cancel"/>，客户端收到 <c>context_rounds_exceeded</c> 错误事件，不发起 LLM 调用。</para>
/// </remarks>
/// <param name="setting">系统配置，用于获取默认上下文轮数</param>
[ChatHandlerOrder(Before = 5)]
public class ContextRoundsHandler(IChatSetting setting) : ChatHandlerBase, IChatHandlerScope
{
    /// <inheritdoc/>
    /// <remarks>仅 OnBefore 有逻辑，无需注册 OnAfter</remarks>
    public override ChatHandlerCapabilities Capabilities => ChatHandlerCapabilities.Before;

    /// <inheritdoc/>
    /// <remarks>轮数限制仅适用于 Web UI 用户；API/网关调用跳过</remarks>
    public ChatFlowSource SupportedSources => ChatFlowSource.Web;

    /// <inheritdoc/>
    /// <remarks>用户保护能力，精简链下同样执行</remarks>
    public ChatHandlerTier Tier => ChatHandlerTier.Core;

    /// <inheritdoc/>
    public override Task OnBefore(IChatContext context, CancellationToken cancellationToken)
    {
        var userId = context.UserId;
        if (userId <= 0) return Task.CompletedTask;

        var maxRounds = ResolveMaxRounds(userId);
        // ContextMessages 在 BuildContextAsync 阶段已填充（早于 InvokeChainAsync），含本轮用户消息，空 assistant 占位符已被跳过
        var rounds = context.ContextMessages.Count(m => m.Role == "user");
        if (rounds <= maxRounds) return Task.CompletedTask;

        context.FlowControl = ChatFlowControl.Cancel;
        context.CancelCode = "context_rounds_exceeded";
        context.CancelMessage = $"本会话已达到最大对话轮数（{maxRounds} 轮）。\n\n请开启新会话继续提问，或前往**用户设置**调整最大轮数。";
        return Task.CompletedTask;
    }

    /// <summary>解析用户的最大对话轮数。优先读取用户设置，回退到系统默认值，最终兜底 10</summary>
    /// <param name="userId">用户编号</param>
    /// <returns>最大对话轮数</returns>
    private Int32 ResolveMaxRounds(Int32 userId)
    {
        var userSetting = UserSetting.FindByUserId(userId);
        if (userSetting?.ContextRounds > 0) return userSetting.ContextRounds;
        return setting.DefaultContextRounds > 0 ? setting.DefaultContextRounds : 10;
    }
}
