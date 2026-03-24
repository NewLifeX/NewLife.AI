using NewLife.AI.Providers;

namespace NewLife.AI.Embedding;

/// <summary>AI 嵌入向量客户端接口。将文本转换为浮点向量，用于语义搜索、相似度计算等场景</summary>
/// <remarks>
/// 设计对标 MEAI 的 IEmbeddingGenerator，简化为单方法接口。
/// 通过 <see cref="IEmbeddingProvider.CreateEmbeddingClient"/> 获取实例，
/// 或直接实例化 <see cref="OpenAiEmbeddingClient"/>。
/// </remarks>
public interface IEmbeddingClient : IDisposable
{
    /// <summary>客户端元数据</summary>
    EmbeddingClientMetadata Metadata { get; }

    /// <summary>生成嵌入向量</summary>
    /// <param name="request">嵌入请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>嵌入响应，包含每条输入对应的向量</returns>
    Task<EmbeddingResponse> GenerateAsync(EmbeddingRequest request, CancellationToken cancellationToken = default);
}

/// <summary>AI 嵌入向量客户端元数据</summary>
public class EmbeddingClientMetadata
{
    /// <summary>服务商名称</summary>
    public String ProviderName { get; init; } = null!;

    /// <summary>API 地址</summary>
    public String? Endpoint { get; init; }

    /// <summary>默认模型编码</summary>
    public String? DefaultModelId { get; init; }
}

/// <summary>嵌入请求。兼容 OpenAI Embeddings API</summary>
public class EmbeddingRequest
{
    /// <summary>输入文本列表。每条文本生成一个向量</summary>
    public IList<String> Input { get; set; } = [];

    /// <summary>模型编码，如 text-embedding-3-small</summary>
    public String? Model { get; set; }

    /// <summary>向量维度。支持降维的模型（如 text-embedding-3-*）可指定，默认使用模型最大维度</summary>
    public Int32? Dimensions { get; set; }

    /// <summary>编码格式。float（默认）或 base64</summary>
    public String? EncodingFormat { get; set; }

    /// <summary>用户标识。用于追踪和限流</summary>
    public String? User { get; set; }
}

/// <summary>嵌入响应</summary>
public class EmbeddingResponse
{
    /// <summary>模型编码</summary>
    public String? Model { get; set; }

    /// <summary>嵌入向量列表，与输入文本一一对应</summary>
    public IList<EmbeddingItem> Data { get; set; } = [];

    /// <summary>令牌用量统计</summary>
    public EmbeddingUsage? Usage { get; set; }
}

/// <summary>单条嵌入向量</summary>
public class EmbeddingItem
{
    /// <summary>序号，与输入 Input[Index] 对应</summary>
    public Int32 Index { get; set; }

    /// <summary>嵌入向量。浮点数组，维度取决于所用模型</summary>
    public Single[]? Embedding { get; set; }
}

/// <summary>嵌入令牌用量统计</summary>
public class EmbeddingUsage
{
    /// <summary>提示令牌数（输入文本消耗）</summary>
    public Int32 PromptTokens { get; set; }

    /// <summary>总令牌数</summary>
    public Int32 TotalTokens { get; set; }
}

/// <summary>支持嵌入向量的 AI 服务商接口。实现此接口的服务商可创建 IEmbeddingClient</summary>
/// <remarks>
/// OpenAI、阿里百炼等兼容 OpenAI Embeddings API 的服务商可实现此接口。
/// 不支持 Embedding 的服务商无需实现（如 Anthropic Claude）。
/// </remarks>
public interface IEmbeddingProvider
{
    /// <summary>创建已绑定连接参数的嵌入向量客户端</summary>
    /// <param name="options">连接选项（Endpoint、ApiKey 等）</param>
    /// <returns>已配置的 IEmbeddingClient 实例</returns>
    IEmbeddingClient CreateEmbeddingClient(AiClientOptions options);
}
