using System.Runtime.CompilerServices;
using NewLife.AI.Models;
using NewLife.Log;

namespace NewLife.AI.Providers;

/// <summary>分布式追踪中间件客户端。使用 NewLife ITracer 为每次对话创建追踪 Span</summary>
/// <remarks>通过 <see cref="ChatClientBuilderExtensions.UseTracing"/> 加入管道。
/// Span 名称格式为 <c>chat/complete/{model}</c> 或 <c>chat/streaming/{model}</c>，
/// 完成后记录 TotalTokens 为 Span 的 Value。</remarks>
public class TracingChatClient : DelegatingChatClient
{
    #region 属性

    private readonly ITracer? _tracer;

    #endregion

    #region 构造

    /// <summary>初始化追踪客户端</summary>
    /// <param name="innerClient">内层客户端</param>
    /// <param name="tracer">追踪器，为 null 时不追踪</param>
    public TracingChatClient(IChatClient innerClient, ITracer? tracer = null) : base(innerClient)
        => _tracer = tracer;

    #endregion

    #region 方法

    /// <summary>非流式对话完成，附加追踪 Span</summary>
    /// <param name="request">对话请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>完整的对话响应</returns>
    public override async Task<ChatCompletionResponse> CompleteAsync(ChatCompletionRequest request, CancellationToken cancellationToken = default)
    {
        using var span = _tracer?.NewSpan($"chat/complete/{request.Model}", request.Model);
        try
        {
            var response = await base.CompleteAsync(request, cancellationToken).ConfigureAwait(false);
            if (span != null && response.Usage != null)
            {
                span.Value = response.Usage.TotalTokens;
                span.AppendTag($" prompt={response.Usage.PromptTokens} completion={response.Usage.CompletionTokens}");
            }
            return response;
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            throw;
        }
    }

    /// <summary>流式对话完成，附加追踪 Span</summary>
    /// <param name="request">对话请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>流式响应块的异步枚举</returns>
    public override async IAsyncEnumerable<ChatCompletionResponse> CompleteStreamingAsync(
        ChatCompletionRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var span = _tracer?.NewSpan($"chat/streaming/{request.Model}", request.Model);
        ChatUsage? lastUsage = null;
        await foreach (var chunk in base.CompleteStreamingAsync(request, cancellationToken).ConfigureAwait(false))
        {
            if (chunk.Usage != null) lastUsage = chunk.Usage;
            yield return chunk;
        }
        if (span != null && lastUsage != null)
        {
            span.Value = lastUsage.TotalTokens;
            span.AppendTag($" prompt={lastUsage.PromptTokens} completion={lastUsage.CompletionTokens}");
        }
    }

    #endregion
}
