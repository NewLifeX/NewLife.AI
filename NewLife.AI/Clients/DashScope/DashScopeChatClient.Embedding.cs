using NewLife.AI.Embedding;

namespace NewLife.AI.Clients.DashScope;

public partial class DashScopeChatClient
{
    #region 嵌入向量（IEmbeddingClient 实现）
    /// <summary>生成嵌入向量。始终使用兼容模式端点，与对话原生端点隔离</summary>
    /// <remarks>
    /// DashScope 嵌入 API 仅在兼容模式下可用：POST /compatible-mode/v1/embeddings<br/>
    /// 无论全局端点配置为何，嵌入请求均自动路由到兼容模式端点。
    /// </remarks>
    /// <param name="request">嵌入请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>嵌入响应</returns>
    public override async Task<EmbeddingResponse> GenerateAsync(EmbeddingRequest request, CancellationToken cancellationToken = default)
    {
        // Embedding 始终使用兼容模式端点，临时切换 endpoint 后委托基类
        var saved = _options.Endpoint;
        _options.Endpoint = GetCompatibleBaseUrl();
        try
        {
            return await base.GenerateAsync(request, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _options.Endpoint = saved;
        }
    }
    #endregion
}
