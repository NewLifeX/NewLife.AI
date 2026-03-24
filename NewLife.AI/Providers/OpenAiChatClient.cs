using System.Diagnostics;
using System.Runtime.CompilerServices;
using NewLife.AI.Models;
using NewLife.Log;

namespace NewLife.AI.Providers;

/// <summary>对话协议内部委托接口。解耦 OpenAiChatClient 与公共 IAiProvider，对外不可见</summary>
/// <remarks>
/// OpenAiProvider / AnthropicProvider / GeminiProvider 均实现此接口，
/// 由各自的 CreateClient 将 this 注入 OpenAiChatClient，实现函数调度不变的同时让 IAiProvider 保持纯粹。
/// </remarks>
internal interface IAiChatProtocol
{
    /// <summary>服务商名称（用于元数据）</summary>
    String Name { get; }

    /// <summary>默认 API 地址（用于元数据）</summary>
    String DefaultEndpoint { get; }

    /// <summary>非流式对话执行</summary>
    /// <param name="request">内部对话请求</param>
    /// <param name="options">连接选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<ChatResponse> ChatAsync(ChatRequest request, AiProviderOptions options, CancellationToken cancellationToken = default);

    /// <summary>流式对话执行</summary>
    /// <param name="request">内部对话请求</param>
    /// <param name="options">连接选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    IAsyncEnumerable<ChatResponse> ChatStreamAsync(ChatRequest request, AiProviderOptions options, CancellationToken cancellationToken = default);
}

/// <summary>内部对话客户端实现。绑定协议执行器与连接选项，实际 HTTP 通信由 IAiChatProtocol 完成</summary>
/// <remarks>通过 <see cref="IAiProvider.CreateClient"/> 创建，请勿直接实例化。</remarks>
internal sealed class OpenAiChatClient(IAiChatProtocol protocol, AiProviderOptions options) : IChatClient, ILogFeature, ITracerFeature
{
    private readonly AiProviderOptions _options = options;

    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>追踪器</summary>
    public ITracer? Tracer { get; set; }

    /// <summary>非流式对话完成</summary>
    /// <param name="request">内部对话请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>完整的对话响应</returns>
    public async Task<ChatResponse> GetResponseAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        request.Model ??= _options.Model;

        var model = request.Model;
        using var span = Tracer?.NewSpan($"chat:{model}", request.Messages?.FirstOrDefault()?.Content);
        try
        {
            var response = await protocol.ChatAsync(request, _options, cancellationToken).ConfigureAwait(false);
            if (span != null && response.Usage != null)
                span.Value = response.Usage.TotalTokens;

            return response;
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            Log.Error("[ChatClient] CompleteAsync error! {0}", ex.Message);
            throw;
        }
    }

    /// <summary>流式对话完成</summary>
    /// <param name="request">内部对话请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>流式响应块的异步枚举</returns>
    public async IAsyncEnumerable<ChatResponse> GetStreamingResponseAsync(ChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        request.Model ??= _options.Model;

        var model = request.Model;
        using var span = Tracer?.NewSpan($"chat:streaming:{model}", model);

        UsageDetails? lastUsage = null;
        var chunks = 0;
        await foreach (var chunk in protocol.ChatStreamAsync(request, _options, cancellationToken).ConfigureAwait(false))
        {
            if (chunk.Usage != null) lastUsage = chunk.Usage;
            chunks++;
            yield return chunk;
        }

        if (span != null && lastUsage != null)
            span.Value = lastUsage.TotalTokens;
    }

    /// <summary>释放资源（协议层不持有独立资源，HttpClient 由各 Provider 静态持有）</summary>
    public void Dispose() { }
}
