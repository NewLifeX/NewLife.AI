namespace NewLife.AI.Handlers;

/// <summary>对话处理器流控信号。由 <see cref="IChatContext"/> 的 <see cref="IChatContext.FlowControl"/> 属性承载，
/// 在 <see cref="IChatHandler.OnBefore"/> 阶段控制后续处理器与 LLM 核心阶段的执行策略</summary>
/// <remarks>
/// 三种信号互斥，调度器 <c>MessageFlow</c> 按如下规则解释：
/// <list type="table">
/// <listheader><term>值</term><description>后续 Before 处理器 | LLM 核心阶段 | 客户端推送</description></listheader>
/// <item><term><see cref="Continue"/></term><description>全部运行 | 运行 | 正常内容流</description></item>
/// <item><term><see cref="SkipRemaining"/></term><description>跳过 | 运行 | 正常内容流</description></item>
/// <item><term><see cref="Cancel"/></term><description>跳过 | 跳过 | error 事件</description></item>
/// </list>
/// 无论哪种信号，<b>OnAfter 调用规则不变</b>：After-only 处理器无条件调用；
/// Before+After 处理器仅当其 OnBefore 确实执行过时才调用。
/// </remarks>
public enum ChatFlowControl
{
    /// <summary>继续执行。默认值，后续 Before 处理器与 LLM 核心阶段均正常运行</summary>
    Continue = 0,

    /// <summary>跳过后续 Before 处理器，但仍执行 LLM 核心阶段并向客户端推送正常内容流。
    /// 适用于某处理器已锁定参数（如命中缓存前置数据），不希望后续处理器覆盖，但仍需完成 LLM 调用的场景</summary>
    SkipRemaining = 1,

    /// <summary>完全取消执行。跳过后续 Before 处理器与整个 LLM 核心阶段，向客户端推送 error 事件。
    /// 配合 <see cref="IChatContext.CancelCode"/> / <see cref="IChatContext.CancelMessage"/> 填写原因，
    /// 便于客户端识别（如 quota_exceeded、content_blocked 等）</summary>
    Cancel = 2,
}
