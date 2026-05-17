using NewLife.AI.Models;

namespace NewLife.AI.Handlers;

/// <summary>对话事件流委托。调用链中的下一个节点（最末端为 <c>MessageFlow</c> 内核 LLM 调用），返回事件流</summary>
/// <param name="cancellationToken">取消令牌</param>
/// <returns>事件流</returns>
public delegate IAsyncEnumerable<ChatStreamEvent> ChatNextDelegate(CancellationToken cancellationToken);

/// <summary>对话处理器能力标志。声明当前处理器实现了哪些调度阶段</summary>
/// <remarks>调度器 <c>MessageFlow</c> 根据此标志决定是否调用对应方法，避免对空实现的无效调用。
/// 支持多标志组合，如 <c>Before | After | Interceptor</c></remarks>
[Flags]
public enum ChatHandlerCapabilities
{
    /// <summary>无任何能力（占位，通常不用）</summary>
    None = 0,

    /// <summary>事前处理能力。声明后调度器将调用 <see cref="IChatHandler.OnBefore"/></summary>
    Before = 1,

    /// <summary>事后处理能力。声明后调度器将调用 <see cref="IChatHandler.OnAfter"/></summary>
    After = 2,

    /// <summary>事件流拦截能力。声明后调度器将 <see cref="IChatHandler.InvokeAsync"/> 纳入洋葱链</summary>
    Interceptor = 4,
}

/// <summary>对话处理器（三段式：事前 / 事件流拦截 / 事后）。<b>注册顺序即执行顺序</b>：OnBefore 与 OnAfter 均按注册正序执行</summary>
/// <remarks>
/// <para>通过 <see cref="Capabilities"/> 属性声明本处理器实现了哪些阶段，调度器据此跳过未声明阶段的调用。</para>
/// <para><b>OnAfter 调用规则</b>：After-only（无 <see cref="ChatHandlerCapabilities.Before"/>）的处理器<b>无条件</b>执行 OnAfter；
/// 同时声明 Before+After 的处理器仅当其 OnBefore 确实被调用过时才执行 OnAfter（短路时未执行的 OnBefore 对应的 OnAfter 不会被调用）。</para>
/// <para><b>事前流控</b>：在 <see cref="OnBefore"/> 中设置 <see cref="IChatContext.FlowControl"/>，<c>MessageFlow</c> 将按如下规则执行：<br/>
/// • <see cref="ChatFlowControl.SkipRemaining"/>: 跳过后续 Handler 的 OnBefore，但仍执行 LLM 核心阶段，客户端收到正常内容流。<br/>
/// • <see cref="ChatFlowControl.Cancel"/>: 跳过后续 Handler 的 OnBefore 与整个 LLM 核心阶段，客户端收到 error 事件；
/// 配合 <see cref="IChatContext.CancelCode"/> / <see cref="IChatContext.CancelMessage"/> 填写原因。<br/>
/// 两种短路情形下，已运行 OnBefore 的 Handler 的 OnAfter 仍按序执行以及所有 After-only Handler 的 OnAfter。</para>
/// <para><b>异常</b>：实现者抛出的异常将向上传播。<b>不要</b>静默吞噬。</para>
/// <para><b>事件流拦截</b>（如推荐缓存命中后直接回放缓存内容）：在 <see cref="Capabilities"/> 中追加
/// <see cref="ChatHandlerCapabilities.Interceptor"/> 并覆写 <see cref="InvokeAsync"/>；
/// DI 仅以 <see cref="IChatHandler"/> 注册一次即可，<c>MessageFlow</c> 通过 Capabilities 标志识别拦截器。</para>
/// </remarks>
public interface IChatHandler
{
    /// <summary>处理器能力标志。声明当前处理器实现了哪些调度阶段，调度器据此决定是否调用对应方法</summary>
    /// <remarks>默认值 <see cref="ChatHandlerCapabilities.Before"/> | <see cref="ChatHandlerCapabilities.After"/>，
    /// 与历史行为一致。仅有 OnAfter 逻辑时应声明 <see cref="ChatHandlerCapabilities.After"/>，
    /// 需要拦截事件流时额外追加 <see cref="ChatHandlerCapabilities.Interceptor"/></remarks>
    ChatHandlerCapabilities Capabilities { get; }

    /// <summary>事前处理。可修改 <see cref="IChatContext.ContextMessages"/>、注入 SystemPrompt、配额校验、解析技能等</summary>
    /// <param name="context">对话上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步任务</returns>
    Task OnBefore(IChatContext context, CancellationToken cancellationToken);

    /// <summary>事后处理。可读取 <see cref="IChatContext.ContentBuilder"/> / <see cref="IChatContext.Usage"/> 等收集结果，
    /// 用于持久化、配额扣减、统计累加、用量入库等。<b>按注册正序执行</b>
    /// （After-only 处理器无条件执行；Before+After 处理器仅当 OnBefore 确实运行过时才执行）</summary>
    /// <param name="context">对话上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步任务</returns>
    Task OnAfter(IChatContext context, CancellationToken cancellationToken);

    /// <summary>处理对话事件流。可在调用 <paramref name="next"/> 前后插入逻辑，或不调用 <paramref name="next"/> 实现短路。
    /// 仅当 <see cref="Capabilities"/> 含 <see cref="ChatHandlerCapabilities.Interceptor"/> 时，调度器才将本方法纳入洋葱链</summary>
    /// <param name="context">对话上下文</param>
    /// <param name="next">链中下一个节点（最末端为 <c>MessageFlow</c> 内核 LLM 调用）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>事件流</returns>
    IAsyncEnumerable<ChatStreamEvent> InvokeAsync(IChatContext context, ChatNextDelegate next, CancellationToken cancellationToken);
}

/// <summary>示例空实现。仅供测试或占位，实际处理器请根据需要实现对应方法并声明能力标志</summary>
public abstract class ChatHandlerBase : IChatHandler
{
    /// <summary>处理器能力标志。声明当前处理器实现了哪些调度阶段，调度器据此决定是否调用对应方法</summary>
    /// <remarks>默认值 <see cref="ChatHandlerCapabilities.Before"/> | <see cref="ChatHandlerCapabilities.After"/>，
    /// 与历史行为一致。仅有 OnAfter 逻辑时应声明 <see cref="ChatHandlerCapabilities.After"/>，
    /// 需要拦截事件流时额外追加 <see cref="ChatHandlerCapabilities.Interceptor"/></remarks>
    public virtual ChatHandlerCapabilities Capabilities => ChatHandlerCapabilities.Before | ChatHandlerCapabilities.After;

    /// <summary>事前处理。可修改 <see cref="IChatContext.ContextMessages"/>、注入 SystemPrompt、配额校验、解析技能等</summary>
    /// <param name="context">对话上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步任务</returns>
    public virtual Task OnBefore(IChatContext context, CancellationToken cancellationToken) => TaskEx.CompletedTask;

    /// <summary>事后处理。可读取 <see cref="IChatContext.ContentBuilder"/> / <see cref="IChatContext.Usage"/> 等收集结果，
    /// 用于持久化、配额扣减、统计累加、用量入库等。<b>按注册正序执行</b>
    /// （After-only 处理器无条件执行；Before+After 处理器仅当 OnBefore 确实运行过时才执行）</summary>
    /// <param name="context">对话上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步任务</returns>
    public virtual Task OnAfter(IChatContext context, CancellationToken cancellationToken) => TaskEx.CompletedTask;

    /// <summary>处理对话事件流。可在调用 <paramref name="next"/> 前后插入逻辑，或不调用 <paramref name="next"/> 实现短路。
    /// 仅当 <see cref="Capabilities"/> 含 <see cref="ChatHandlerCapabilities.Interceptor"/> 时，调度器才将本方法纳入洋葱链</summary>
    /// <param name="context">对话上下文</param>
    /// <param name="next">链中下一个节点（最末端为 <c>MessageFlow</c> 内核 LLM 调用）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>事件流</returns>
    public virtual IAsyncEnumerable<ChatStreamEvent> InvokeAsync(IChatContext context, ChatNextDelegate next, CancellationToken cancellationToken) => next(cancellationToken);
}