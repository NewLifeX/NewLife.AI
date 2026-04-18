using NewLife.AI.Models;

namespace NewLife.AI.Clients.Capabilities;

/// <summary>文档重排序能力接口。RAG 检索后对候选文档按相关度重排</summary>
/// <remarks>
/// 已实现：DashScope（gte-rerank）、NewLifeAI、Bedrock（Cohere Rerank，可选）；
/// 不实现：OpenAI、DeepSeek、Anthropic、Gemini、Ollama、Azure。
/// </remarks>
public interface IRerankClient
{
    /// <summary>重排序</summary>
    /// <param name="request">重排请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>按相关度降序的结果列表</returns>
    Task<RerankResponse> RerankAsync(RerankRequest request, CancellationToken cancellationToken = default);
}
