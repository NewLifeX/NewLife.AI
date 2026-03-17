using NewLife.AI.Filters;
using NewLife.AI.Models;

namespace NewLife.AI.Providers;

/// <summary>带过滤器链的对话客户端。在 CompleteAsync 前后执行注册的 IChatFilter 列表</summary>
/// <remarks>
/// 添加过滤器：
/// <code>
/// var client = provider.CreateClient(options)
///     .AsBuilder()
///     .UseFilters(new LogFilter(), new ValidationFilter())
///     .Build();
/// </code>
/// </remarks>
public class FilteredChatClient : DelegatingChatClient
{
    #region 属性

    /// <summary>过滤器列表。按注册顺序依次执行（洋葱圈模型）</summary>
    public IList<IChatFilter> Filters { get; } = [];

    #endregion

    #region 构造

    /// <summary>初始化带过滤器链的客户端</summary>
    /// <param name="innerClient">内层客户端</param>
    public FilteredChatClient(IChatClient innerClient) : base(innerClient) { }

    /// <summary>初始化并注入过滤器列表</summary>
    /// <param name="innerClient">内层客户端</param>
    /// <param name="filters">过滤器列表</param>
    public FilteredChatClient(IChatClient innerClient, IEnumerable<IChatFilter> filters) : base(innerClient)
    {
        if (filters != null)
        {
            foreach (var f in filters)
            {
                if (f != null) Filters.Add(f);
            }
        }
    }

    #endregion

    #region 方法

    /// <summary>非流式对话完成。依次执行过滤器链后调用内层客户端</summary>
    /// <param name="messages">消息列表</param>
    /// <param name="options">对话选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    public override async Task<ChatCompletionResponse> CompleteAsync(IList<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (Filters.Count == 0)
            return await InnerClient.CompleteAsync(messages, options, cancellationToken).ConfigureAwait(false);

        var request = ChatCompletionRequest.Create(messages, options);
        var context = new ChatFilterContext { Request = request, IsStreaming = false };
        await ExecuteFilterChainAsync(context, 0, options, cancellationToken).ConfigureAwait(false);
        return context.Response ?? new ChatCompletionResponse();
    }

    /// <summary>流式对话完成。执行过滤器链的 before 阶段后委托给内层客户端</summary>
    /// <param name="messages">消息列表</param>
    /// <param name="options">对话选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    public override IAsyncEnumerable<ChatCompletionResponse> CompleteStreamingAsync(IList<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (Filters.Count == 0)
            return InnerClient.CompleteStreamingAsync(messages, options, cancellationToken);

        // 流式场景：先运行 before 阶段，再委托给内层流
        return RunStreamingWithFiltersAsync(messages, options, cancellationToken);
    }

    #endregion

    #region 辅助

    private async Task ExecuteFilterChainAsync(ChatFilterContext context, Int32 index, ChatOptions? options, CancellationToken cancellationToken)
    {
        if (index >= Filters.Count)
        {
            // 链末尾：调用内层客户端
            context.Response = await InnerClient.CompleteAsync(context.Request.Messages, options, cancellationToken).ConfigureAwait(false);
            return;
        }

        var filter = Filters[index];
        await filter.OnChatAsync(context, (ctx, ct) => ExecuteFilterChainAsync(ctx, index + 1, options, ct), cancellationToken).ConfigureAwait(false);
    }

    private async IAsyncEnumerable<ChatCompletionResponse> RunStreamingWithFiltersAsync(
        IList<ChatMessage> messages,
        ChatOptions? options,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // 流式只执行 before 阶段（过滤器 next 后面的代码）— 把请求作为最终状态传入
        var request = ChatCompletionRequest.Create(messages, options);
        var context = new ChatFilterContext { Request = request, IsStreaming = true };

        // 运行过滤器链的 before 阶段（不设置 Response，过滤器到达链尾时不调用 CompleteAsync）
        await RunBeforeFiltersAsync(context, 0, cancellationToken).ConfigureAwait(false);

        await foreach (var chunk in InnerClient.CompleteStreamingAsync(context.Request.Messages, options, cancellationToken).ConfigureAwait(false))
            yield return chunk;
    }

    private async Task RunBeforeFiltersAsync(ChatFilterContext context, Int32 index, CancellationToken cancellationToken)
    {
        if (index >= Filters.Count) return;

        var filter = Filters[index];
        await filter.OnChatAsync(context, (ctx, ct) => RunBeforeFiltersAsync(ctx, index + 1, ct), cancellationToken).ConfigureAwait(false);
    }

    #endregion
}
