using System.Runtime.CompilerServices;
using System.Text;
using NewLife.AI.Filters;
using NewLife.AI.Models;

namespace NewLife.AI.Clients;

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
    /// <param name="request">内部对话请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public override async Task<IChatResponse> GetResponseAsync(IChatRequest request, CancellationToken cancellationToken = default)
    {
        if (Filters.Count == 0)
            return await InnerClient.GetResponseAsync(request, cancellationToken).ConfigureAwait(false);

        var context = new ChatFilterContext
        {
            Request = request,
            IsStreaming = false,
            UserId = request.UserId,
            ConversationId = request.ConversationId
        };
        if (request.Items != null) context.Items = request.Items;

        await ExecuteFilterChainAsync(context, 0, cancellationToken).ConfigureAwait(false);
        return context.Response ?? new ChatResponse();
    }

    /// <summary>流式对话完成。执行过滤器链的 before 阶段后委托给内层客户端，流结束后触发 OnStreamCompletedAsync</summary>
    /// <param name="request">内部对话请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public override IAsyncEnumerable<IChatResponse> GetStreamingResponseAsync(IChatRequest request, CancellationToken cancellationToken = default)
    {
        if (Filters.Count == 0)
            return InnerClient.GetStreamingResponseAsync(request, cancellationToken);

        // 流式场景：先运行 before 阶段，再委托给内层流，流结束后触发 OnStreamCompletedAsync
        return RunStreamingWithFiltersAsync(request, cancellationToken);
    }

    #endregion

    #region 辅助

    private async Task ExecuteFilterChainAsync(ChatFilterContext context, Int32 index, CancellationToken cancellationToken)
    {
        if (index >= Filters.Count)
        {
            // 链末尾：调用内层客户端
            context.Response = await InnerClient.GetResponseAsync(context.Request, cancellationToken).ConfigureAwait(false);
            return;
        }

        var filter = Filters[index];
        await filter.OnChatAsync(context, (ctx, ct) => ExecuteFilterChainAsync(ctx, index + 1, ct), cancellationToken).ConfigureAwait(false);
    }

    private async IAsyncEnumerable<IChatResponse> RunStreamingWithFiltersAsync(
        IChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var context = new ChatFilterContext
        {
            Request = request,
            IsStreaming = true,
            UserId = request.UserId,
            ConversationId = request.ConversationId
        };
        if (request.Items != null) context.Items = request.Items;

        // 运行过滤器链的 before 阶段（修改请求，例如注入记忆上下文）
        await RunBeforeFiltersAsync(context, 0, cancellationToken).ConfigureAwait(false);

        // 流式输出，同时收集最后一个有效用量、模型名和完整回复内容（用于传给 OnStreamCompletedAsync）
        UsageDetails? lastUsage = null;
        String? model = null;
        FinishReason? finishReason = null;
        var contentBuilder = new StringBuilder();
        var reasoningBuilder = new StringBuilder();
        await foreach (var chunk in InnerClient.GetStreamingResponseAsync(context.Request, cancellationToken).ConfigureAwait(false))
        {
            if (chunk.Usage != null) lastUsage = chunk.Usage;
            if (chunk.Model != null) model = chunk.Model;
            // 聚合正文与思考内容，以便 OnStreamCompletedAsync 中各过滤器（如 AgentTriggerFilter / LearningFilter）可读取完整回复
            var choice = chunk.Messages?.FirstOrDefault();
            var delta = choice?.Delta ?? choice?.Message;
            if (delta?.Content is String text && !String.IsNullOrEmpty(text))
                contentBuilder.Append(text);
            if (delta?.ReasoningContent is String reasoning && !String.IsNullOrEmpty(reasoning))
                reasoningBuilder.Append(reasoning);
            if (choice?.FinishReason != null) finishReason = choice.FinishReason;
            yield return chunk;
        }

        // 流结束后：组装包含完整回复内容的摘要响应，并以"火焰即忘"方式触发 OnStreamCompletedAsync
        var resp = new ChatResponse
        {
            Model = model,
            Usage = lastUsage,
        };
        var reasoningText = reasoningBuilder.Length > 0 ? reasoningBuilder.ToString() : null;
        resp.Add(contentBuilder.ToString(), reasoningText, finishReason);
        context.Response = resp;

        var capturedContext = context;
        var capturedFilters = Filters.ToArray();
        _ = Task.Run(async () =>
        {
            foreach (var filter in capturedFilters)
            {
                try
                {
                    await filter.OnStreamCompletedAsync(capturedContext, CancellationToken.None).ConfigureAwait(false);
                }
                catch { /* 后处理异常不应影响主响应链 */ }
            }
        });
    }

    private async Task RunBeforeFiltersAsync(ChatFilterContext context, Int32 index, CancellationToken cancellationToken)
    {
        if (index >= Filters.Count) return;

        var filter = Filters[index];
        await filter.OnChatAsync(context, (ctx, ct) => RunBeforeFiltersAsync(ctx, index + 1, ct), cancellationToken).ConfigureAwait(false);
    }

    #endregion
}
