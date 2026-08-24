namespace NewLife.ChatAI.Handlers;

/// <summary>上下文轮数限制处理器（已废弃）。原用于在 OnBefore 阶段统计会话轮数并拒绝超限对话</summary>
/// <remarks>
/// <para><b>已移除硬性拒绝</b>：对比豆包 / ChatGPT / Claude / Copilot 等主流 AI 产品均不限制对话轮数——轮数不是资源，Token 才是。
/// 现由滑动窗口（每次携带最近 N 轮历史）+ 上下文压缩（<c>CompactionFilter</c>）+ Token 硬兜底（<c>TokenBudgetFilter</c>）共同管理，对话永不中断。</para>
/// <para>用户级 <see cref="UserSetting.ContextRounds"/> 语义已迁移为「每次请求携带的历史轮数（滑动窗口）」，解析逻辑见 <c>MessageFlow.ResolveContextRounds</c>。</para>
/// <para>此类保留仅为决策记录，禁止重新注册启用。原拦截逻辑曾设置 <c>context_rounds_exceeded</c> 错误事件。</para>
/// </remarks>
[Obsolete("会话轮数硬限制已移除，对齐竞品改为滑动窗口 + 上下文压缩 + TokenBudgetFilter 兜底。保留此类作为决策记录，勿重新启用")]
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
    /// <remarks>已改为空操作：会话轮数硬限制已移除（见类注释）。不拒绝任何对话，早期历史由滑动窗口自然淡出。</remarks>
    public override Task OnBefore(IChatContext context, CancellationToken cancellationToken) => Task.CompletedTask;
}
