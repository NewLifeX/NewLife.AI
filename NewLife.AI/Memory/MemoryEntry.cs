using NewLife.AI.Models;

namespace NewLife.AI.Memory;

/// <summary>语义记忆条目。存储文本、向量及附加元数据</summary>
public class MemoryEntry
{
    /// <summary>唯一标识</summary>
    public String Id { get; set; } = String.Empty;

    /// <summary>集合名称。用于多集合隔离</summary>
    public String Collection { get; set; } = String.Empty;

    /// <summary>原始文本</summary>
    public String Text { get; set; } = String.Empty;

    /// <summary>嵌入向量。由外部 IEmbeddingClient 生成后传入</summary>
    public Single[]? Vector { get; set; }

    /// <summary>附加元数据。Key-Value 形式，可存储来源、时间戳等信息</summary>
    public Dictionary<String, String> Metadata { get; set; } = [];
}

/// <summary>记忆检索结果。含条目本身与相似度得分</summary>
public class MemorySearchResult
{
    /// <summary>匹配的记忆条目</summary>
    public MemoryEntry Entry { get; set; } = new();

    /// <summary>余弦相似度（0–1，越大越相似）</summary>
    public Double Relevance { get; set; }
}

/// <summary>向量存储记录。独立于语义记忆，专注于高维向量的存取与检索</summary>
public class VectorRecord
{
    /// <summary>唯一标识</summary>
    public String Id { get; set; } = String.Empty;

    /// <summary>向量数据</summary>
    public Single[] Vector { get; set; } = [];

    /// <summary>附加载荷。可存储任意结构化数据（序列化为 JSON 等）</summary>
    public Dictionary<String, Object?> Payload { get; set; } = [];
}

/// <summary>向量检索结果</summary>
public class VectorSearchResult
{
    /// <summary>匹配的向量记录</summary>
    public VectorRecord Record { get; set; } = new();

    /// <summary>相似度得分（余弦相似度，0–1）</summary>
    public Double Score { get; set; }
}
