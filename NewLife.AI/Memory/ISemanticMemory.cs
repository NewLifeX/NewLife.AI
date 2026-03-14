namespace NewLife.AI.Memory;

/// <summary>语义记忆接口。提供文本存储与相似度检索能力，对标 SK 的 ISemanticMemory</summary>
/// <remarks>
/// 设计原则：
/// <list type="bullet">
/// <item>集合（Collection）隔离：不同知识库/用户可使用独立集合</item>
/// <item>向量由外部生成后传入，不依赖具体 Embedding 实现</item>
/// <item>SearchAsync 使用余弦相似度，返回 Top-N 结果</item>
/// </list>
/// </remarks>
public interface ISemanticMemory
{
    /// <summary>保存记忆条目。若 Id 已存在则覆盖</summary>
    /// <param name="entry">记忆条目（含预生成的向量）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task SaveAsync(MemoryEntry entry, CancellationToken cancellationToken = default);

    /// <summary>按 Id 获取单条记忆</summary>
    /// <param name="collection">集合名称</param>
    /// <param name="id">条目 Id</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>找到则返回条目，否则返回 null</returns>
    Task<MemoryEntry?> GetAsync(String collection, String id, CancellationToken cancellationToken = default);

    /// <summary>相似度检索。返回向量最接近查询向量的 Top-N 条目</summary>
    /// <param name="collection">集合名称</param>
    /// <param name="queryVector">查询向量</param>
    /// <param name="topN">返回条数（默认 5）</param>
    /// <param name="minRelevance">最低相似度门槛（0–1，默认 0）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>按相似度降序排列的检索结果</returns>
    Task<IList<MemorySearchResult>> SearchAsync(String collection, Single[] queryVector, Int32 topN = 5, Double minRelevance = 0, CancellationToken cancellationToken = default);

    /// <summary>删除指定条目</summary>
    /// <param name="collection">集合名称</param>
    /// <param name="id">条目 Id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task RemoveAsync(String collection, String id, CancellationToken cancellationToken = default);

    /// <summary>列出集合内所有条目 Id</summary>
    /// <param name="collection">集合名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IList<String>> ListIdsAsync(String collection, CancellationToken cancellationToken = default);
}
