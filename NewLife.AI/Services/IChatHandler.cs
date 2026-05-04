using NewLife.AI.Models;

namespace NewLife.AI.Services;

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

/// <summary>对话处理器（三段式：事前 / 事件流拦截 / 事后）。<b>注册顺序即执行顺序</b>：OnBefore 按注册正序；OnAfter 按注册倒序</summary>
/// <remarks>
/// <para>通过 <see cref="Capabilities"/> 属性声明本处理器实现了哪些阶段，调度器据此跳过未声明阶段的调用。</para>
/// <para><b>事前短路</b>：在 <see cref="OnBefore"/> 中将 <see cref="IChatContext.Cancel"/> 置 true 并填充
/// <see cref="IChatContext.CancelCode"/> / <see cref="IChatContext.CancelMessage"/>，<c>MessageFlow</c> 将：
/// 跳过后续 Handler 的 OnBefore、跳过整个核心阶段（含 LLM 调用），但仍按注册倒序执行 OnAfter（便于资源回收/扣减回滚）。</para>
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
    ChatHandlerCapabilities Capabilities => ChatHandlerCapabilities.Before | ChatHandlerCapabilities.After;

    /// <summary>事前处理。可修改 <see cref="IChatContext.ContextMessages"/>、注入 SystemPrompt、配额校验、解析技能等</summary>
    /// <param name="context">对话上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步任务</returns>
    Task OnBefore(IChatContext context, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>事后处理。可读取 <see cref="IChatContext.ContentBuilder"/> / <see cref="IChatContext.Usage"/> 等收集结果，
    /// 用于持久化、配额扣减、统计累加、用量入库等。<b>按注册倒序执行</b></summary>
    /// <param name="context">对话上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步任务</returns>
    Task OnAfter(IChatContext context, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>处理对话事件流。可在调用 <paramref name="next"/> 前后插入逻辑，或不调用 <paramref name="next"/> 实现短路。
    /// 仅当 <see cref="Capabilities"/> 含 <see cref="ChatHandlerCapabilities.Interceptor"/> 时，调度器才将本方法纳入洋葱链</summary>
    /// <param name="context">对话上下文</param>
    /// <param name="next">链中下一个节点（最末端为 <c>MessageFlow</c> 内核 LLM 调用）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>事件流</returns>
    IAsyncEnumerable<ChatStreamEvent> InvokeAsync(IChatContext context, ChatNextDelegate next, CancellationToken cancellationToken) => next(cancellationToken);
}
