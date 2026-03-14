namespace NewLife.AI.Memory;

/// <summary>向量存储接口。独立管理高维向量记录，支持 Top-K 相似度检索</summary>
/// <remarks>
/// 与 ISemanticMemory 的区别：
/// <list type="bullet">
/// <item>IVectorStore 只负责向量的存储与检索，不关心文本语义</item>
/// <item>ISemanticMemory 是上层封装，内部可以使用 IVectorStore 作为存储后端</item>
/// <item>IVectorStore 可独立用于需要精细控制 Payload 的场景</item>
/// </list>
/// </remarks>
public interface IVectorStore
{
    /// <summary>新增或更新向量记录。若 Id 已存在则覆盖</summary>
    /// <param name="record">向量记录</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UpsertAsync(VectorRecord record, CancellationToken cancellationToken = default);

    /// <summary>批量新增或更新</summary>
    /// <param name="records">向量记录列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UpsertBatchAsync(IEnumerable<VectorRecord> records, CancellationToken cancellationToken = default);

    /// <summary>按 Id 获取记录</summary>
    /// <param name="id">记录 Id</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>找到则返回记录，否则返回 null</returns>
    Task<VectorRecord?> GetAsync(String id, CancellationToken cancellationToken = default);

    /// <summary>Top-K 相似度检索</summary>
    /// <param name="queryVector">查询向量</param>
    /// <param name="topK">返回数量</param>
    /// <param name="minScore">最低相似度门槛（0–1）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>按相似度降序的检索结果</returns>
    Task<IList<VectorSearchResult>> SearchAsync(Single[] queryVector, Int32 topK = 5, Double minScore = 0, CancellationToken cancellationToken = default);

    /// <summary>删除指定记录</summary>
    /// <param name="id">记录 Id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task DeleteAsync(String id, CancellationToken cancellationToken = default);

    /// <summary>获取存储中的记录总数</summary>
    Task<Int64> CountAsync(CancellationToken cancellationToken = default);
}
