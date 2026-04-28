namespace NewLife.AI.Services;

/// <summary>对话处理器（事前/事后两阶段）。<b>注册顺序即执行顺序</b>：OnBefore 按注册正序；OnAfter 按注册倒序</summary>
/// <remarks>
/// <para>绝大多数业务扩展（配额校验、技能解析、统计累加、用量入库、内容安全等）只需要在核心 LLM 调用的事前与事后插入逻辑，
/// 无需干预流式事件序列，故采用此最简的双方法接口。</para>
/// <para><b>事前短路</b>：在 <see cref="OnBefore"/> 中将 <see cref="IChatContext.Cancel"/> 置 true 并填充
/// <see cref="IChatContext.CancelCode"/> / <see cref="IChatContext.CancelMessage"/>，<c>MessageFlow</c> 将：
/// 跳过后续 Handler 的 OnBefore、跳过整个核心阶段（含 LLM 调用），但仍按注册倒序执行 OnAfter（便于资源回收/扣减回滚）。</para>
/// <para><b>异常</b>：实现者抛出的异常将向上传播。<b>不要</b>静默吞噬。</para>
/// <para><b>需要拦截事件流</b>（如推荐缓存命中后直接回放缓存内容）的处理器，可在实现 <see cref="IChatHandler"/> 之外
/// <b>额外实现</b> <see cref="IChatInterceptor"/>；DI 仅以 <see cref="IChatHandler"/> 注册一次即可，
/// <c>MessageFlow</c> 在装配 Interceptor 链时通过 <c>OfType&lt;IChatInterceptor&gt;()</c> 过滤。</para>
/// </remarks>
public interface IChatHandler
{
    /// <summary>事前处理。可修改 <see cref="IChatContext.ContextMessages"/>、注入 SystemPrompt、配额校验、解析技能等</summary>
    /// <param name="context">对话上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步任务</returns>
    Task OnBefore(IChatContext context, CancellationToken cancellationToken);

    /// <summary>事后处理。可读取 <see cref="IChatContext.ContentBuilder"/> / <see cref="IChatContext.Usage"/> 等收集结果，
    /// 用于持久化、配额扣减、统计累加、用量入库等。<b>按注册倒序执行</b></summary>
    /// <param name="context">对话上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步任务</returns>
    Task OnAfter(IChatContext context, CancellationToken cancellationToken);
}
