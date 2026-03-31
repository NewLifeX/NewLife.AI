using NewLife.AI.Clients;

namespace NewLife.AI.Filters;

/// <summary>对话过滤器接口。在 CompleteAsync/CompleteStreamingAsync 前后注入自定义逻辑</summary>
/// <remarks>
/// 调用链模式（洋葱圈）：
/// <code>
/// filter1.OnChatAsync(ctx, next1, ct)
///   └── filter2.OnChatAsync(ctx, next2, ct)
///         └── innerClient.CompleteAsync(ctx.Request, ct)  ← next2 实际执行
/// </code>
/// 实现示例：
/// <code>
/// public async Task OnChatAsync(ChatFilterContext ctx, Func&lt;ChatFilterContext, CancellationToken, Task&gt; next, CancellationToken ct)
/// {
///     // before — 修改 ctx.Request
///     await next(ctx, ct);
///     // after — 读取或修改 ctx.Response
/// }
/// </code>
/// </remarks>
public interface IChatFilter
{
    /// <summary>执行对话过滤逻辑</summary>
    /// <param name="context">过滤器上下文</param>
    /// <param name="next">调用链中的下一个处理器</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task OnChatAsync(ChatFilterContext context, Func<ChatFilterContext, CancellationToken, Task> next, CancellationToken cancellationToken = default);

    /// <summary>流式对话完成后的回调。在所有块流出完毕后异步触发，默认无操作</summary>
    /// <remarks>
    /// 此方法由 <see cref="FilteredChatClient"/> 在流式枚举结束后以"火焰即忘"方式调用。
    /// 过滤器可在此处执行不阻塞响应的后处理，例如触发自学习分析。
    /// 调用时 <see cref="ChatFilterContext.Response"/> 包含从流中聚合的摘要（Model、Usage）。
    /// </remarks>
    /// <param name="context">过滤器上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task OnStreamCompletedAsync(ChatFilterContext context, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

/// <summary>函数调用过滤器接口。在工具函数执行前后注入自定义逻辑</summary>
public interface IFunctionInvocationFilter
{
    /// <summary>执行函数调用过滤逻辑</summary>
    /// <param name="context">函数调用上下文</param>
    /// <param name="next">调用链中的下一个处理器</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task OnFunctionInvocationAsync(FunctionInvocationContext context, Func<FunctionInvocationContext, CancellationToken, Task> next, CancellationToken cancellationToken = default);
}
