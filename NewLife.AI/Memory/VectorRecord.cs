namespace NewLife.AI.Memory;

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

    /// <summary>调试友好输出。显示记录 Id 和得分</summary>
    public override String ToString() => $"[{Record?.Id}]{{Score={Score}}}";
}
