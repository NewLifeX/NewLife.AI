using NewLife.AI.Clients.OpenAI;

namespace NewLife.AI.Clients;

/// <summary>模型列表能力接口。查询当前账号/密钥可用的模型清单</summary>
/// <remarks>
/// 已实现：OpenAI、DashScope、Gemini、Ollama、NewLifeAI；其余按官方文档支持情况扩展。
/// </remarks>
public interface IModelListClient
{
    /// <summary>获取该服务商当前可用的模型列表</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>模型列表，服务不可用时返回 null</returns>
    Task<OpenAiModelListResponse?> ListModelsAsync(CancellationToken cancellationToken = default);
}
