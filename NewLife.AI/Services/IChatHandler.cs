using NewLife.AI.Models;

namespace NewLife.AI.Services;

/// <summary>对话处理器委托。调用链中的下一个处理器，返回事件流</summary>
/// <param name="cancellationToken">取消令牌</param>
/// <returns>事件流</returns>
public delegate IAsyncEnumerable<ChatStreamEvent> ChatHandlerDelegate(CancellationToken cancellationToken);

/// <summary>对话处理器（around 中间件）。<b>注册顺序即外层顺序</b>：先注册 = 最外层，事前最先执行、事后最后执行</summary>
/// <remarks>
/// <para>处理器以洋葱圈模式串联，<see cref="InvokeAsync"/> 内部典型结构：
/// <code>
/// async IAsyncEnumerable&lt;ChatStreamEvent&gt; InvokeAsync(IChatContext ctx, ChatHandlerDelegate next, CancellationToken ct)
/// {
///     // 事前逻辑：可修改 ctx.ContextMessages / 写入 Items / 等
///     await foreach (var ev in next(ct).ConfigureAwait(false))
///         yield return ev;
///     // 事后逻辑：可读取 ctx.ContentBuilder / Usage / AssistantMessage 等收集结果
/// }
/// </code>
/// </para>
/// <para><b>短路</b>：处理器若不调用 <paramref name="next"/>，则下游全部跳过（含核心 LLM 调用）。
/// 适用于推荐缓存命中、内容安全拦截等场景。</para>
/// <para><b>异常</b>：实现者抛出的异常将向上传播，由消息流入口统一捕获并 yield 错误事件。
/// <b>不要</b>在循环中静默吞噬异常。</para>
/// <para><b>事后倒序</b>：来自 <c>await foreach</c> 栈展开的天然顺序，无需额外机制。</para>
/// </remarks>
public interface IChatHandler
{
    /// <summary>处理对话流</summary>
    /// <param name="context">对话上下文</param>
    /// <param name="next">链中下一个处理器（最末端为核心 LLM 调用）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>事件流</returns>
    IAsyncEnumerable<ChatStreamEvent> InvokeAsync(IChatContext context, ChatHandlerDelegate next, CancellationToken cancellationToken);
}
