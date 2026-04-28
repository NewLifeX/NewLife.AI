using NewLife.AI.Models;

namespace NewLife.AI.Services;

/// <summary>对话事件流委托。调用链中的下一个节点（最末端为 <c>MessageFlow</c> 内核 LLM 调用），返回事件流</summary>
/// <param name="cancellationToken">取消令牌</param>
/// <returns>事件流</returns>
public delegate IAsyncEnumerable<ChatStreamEvent> ChatNextDelegate(CancellationToken cancellationToken);

/// <summary>对话流拦截器（around 中间件）。<b>仅当处理器需要干预核心事件流</b>（如推荐缓存命中后直接回放缓存内容）时才实现此接口。
/// 实现此接口的类<b>必须同时实现 <see cref="IChatHandler"/></b>，DI 仅以 <see cref="IChatHandler"/> 注册一次即可，
/// <c>MessageFlow</c> 装配 Interceptor 链时通过 <c>OfType&lt;IChatInterceptor&gt;()</c> 过滤</summary>
/// <remarks>
/// <para>典型实现结构：
/// <code>
/// async IAsyncEnumerable&lt;ChatStreamEvent&gt; InvokeAsync(IChatContext ctx, ChatNextDelegate next, CancellationToken ct)
/// {
///     // 命中条件由 OnBefore 阶段写入 ctx.Items 标记，此处仅检查
///     if (ctx.Items["MyHit"] is String cached)
///     {
///         // 短路：不调用 next，直接 yield 自定义事件流
///         yield return new ChatStreamEvent { Type = "message", Content = cached };
///         yield return new ChatStreamEvent { Type = "message_done" };
///         yield break;
///     }
///     // 透传：未命中则照常调用下游
///     await foreach (var ev in next(ct).ConfigureAwait(false))
///         yield return ev;
/// }
/// </code>
/// </para>
/// <para>多个 Interceptor 按 <see cref="IChatHandler"/> 注册顺序串成洋葱：先注册 = 最外层。</para>
/// <para><b>异常</b>：实现者抛出的异常将向上传播，<b>不要</b>静默吞噬。</para>
/// </remarks>
public interface IChatInterceptor
{
    /// <summary>处理对话事件流。可在调用 <paramref name="next"/> 前后插入逻辑，或不调用 <paramref name="next"/> 实现短路</summary>
    /// <param name="context">对话上下文</param>
    /// <param name="next">链中下一个节点（最末端为 <c>MessageFlow</c> 内核 LLM 调用）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>事件流</returns>
    IAsyncEnumerable<ChatStreamEvent> InvokeAsync(IChatContext context, ChatNextDelegate next, CancellationToken cancellationToken);
}
