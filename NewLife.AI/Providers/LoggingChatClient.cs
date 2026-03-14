using System.Diagnostics;
using System.Runtime.CompilerServices;
using NewLife.AI.Models;
using NewLife.Log;

namespace NewLife.AI.Providers;

/// <summary>日志中间件客户端。记录每次对话的请求模型、耗时与 Token 用量</summary>
/// <remarks>通过 <see cref="ChatClientBuilderExtensions.UseLogging"/> 加入管道。</remarks>
public class LoggingChatClient : DelegatingChatClient
{
    #region 属性

    private readonly ILog _log;

    #endregion

    #region 构造

    /// <summary>初始化日志客户端</summary>
    /// <param name="innerClient">内层客户端</param>
    /// <param name="log">日志实例，为 null 时使用 XTrace</param>
    public LoggingChatClient(IChatClient innerClient, ILog? log = null) : base(innerClient)
        => _log = log ?? XTrace.Log;

    #endregion

    #region 方法

    /// <summary>非流式对话完成，附加日志记录</summary>
    /// <param name="request">对话请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>完整的对话响应</returns>
    public override async Task<ChatCompletionResponse> CompleteAsync(ChatCompletionRequest request, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        _log.Debug("[ChatClient] CompleteAsync model={0} messages={1}", request.Model, request.Messages?.Count);
        try
        {
            var response = await base.CompleteAsync(request, cancellationToken).ConfigureAwait(false);
            sw.Stop();
            _log.Debug("[ChatClient] CompleteAsync done elapsed={0}ms prompt={1} completion={2}",
                sw.ElapsedMilliseconds, response.Usage?.PromptTokens, response.Usage?.CompletionTokens);
            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _log.Error("[ChatClient] CompleteAsync error elapsed={0}ms {1}", sw.ElapsedMilliseconds, ex.Message);
            throw;
        }
    }

    /// <summary>流式对话完成，附加日志记录</summary>
    /// <param name="request">对话请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>流式响应块的异步枚举</returns>
    public override async IAsyncEnumerable<ChatCompletionResponse> CompleteStreamingAsync(
        ChatCompletionRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        _log.Debug("[ChatClient] CompleteStreamingAsync model={0} messages={1}", request.Model, request.Messages?.Count);
        var chunks = 0;
        await foreach (var chunk in base.CompleteStreamingAsync(request, cancellationToken).ConfigureAwait(false))
        {
            chunks++;
            yield return chunk;
        }
        sw.Stop();
        _log.Debug("[ChatClient] CompleteStreamingAsync done elapsed={0}ms chunks={1}", sw.ElapsedMilliseconds, chunks);
    }

    #endregion
}
