using System.Runtime.CompilerServices;
using NewLife.AI.Models;

namespace NewLife.AI.Providers;

/// <summary>用量追踪中间件客户端。对话完成后触发回调，用于统计 Token 消耗</summary>
/// <remarks>
/// 此中间件不依赖数据库，通过回调函数接入业务统计逻辑，适合基础设施层的轻量聚合。
/// 如需携带 userId / conversationId / appKey 等业务上下文，建议在应用服务层（如 DbChatApplicationService）
/// 直接注入并调用 UsageService，而非通过此中间件传递上下文。
/// 使用示例：
/// <code>
/// var client = new ChatClientBuilder(provider, options)
///     .UseUsageTracking((usage, model) => usageService.Record(model, usage.PromptTokens, usage.CompletionTokens))
///     .Build();
/// </code>
/// </remarks>
public class UsageTrackingChatClient : DelegatingChatClient
{
    #region 属性

    private readonly Action<ChatUsage, String?> _onUsage;

    #endregion

    #region 构造

    /// <summary>初始化用量追踪客户端</summary>
    /// <param name="innerClient">内层客户端</param>
    /// <param name="onUsage">用量回调，参数为（用量统计, 模型编码）</param>
    public UsageTrackingChatClient(IChatClient innerClient, Action<ChatUsage, String?> onUsage) : base(innerClient)
    {
        if (onUsage == null) throw new ArgumentNullException(nameof(onUsage));
        _onUsage = onUsage;
    }

    #endregion

    #region 方法

    /// <summary>非流式对话完成，附加用量回调</summary>
    /// <param name="request">对话请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>完整的对话响应</returns>
    public override async Task<ChatCompletionResponse> CompleteAsync(ChatCompletionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await base.CompleteAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.Usage != null)
            _onUsage(response.Usage, response.Model);
        return response;
    }

    /// <summary>流式对话完成，流结束后触发用量回调（仅最后一个含 usage 的 chunk 有效）</summary>
    /// <param name="request">对话请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>流式响应块的异步枚举</returns>
    public override async IAsyncEnumerable<ChatCompletionResponse> CompleteStreamingAsync(
        ChatCompletionRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ChatUsage? lastUsage = null;
        String? model = null;
        await foreach (var chunk in base.CompleteStreamingAsync(request, cancellationToken).ConfigureAwait(false))
        {
            if (chunk.Usage != null) lastUsage = chunk.Usage;
            if (chunk.Model != null) model = chunk.Model;
            yield return chunk;
        }
        if (lastUsage != null)
            _onUsage(lastUsage, model);
    }

    #endregion
}
