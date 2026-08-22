namespace NewLife.AI.Memory;

/// <summary>向量存储接口。管理多个向量集合，支持 Top-K 相似度检索</summary>
/// <remarks>
/// 对标 Microsoft.Extensions.VectorData.VectorStore（存储级），做非泛型简化：
/// <list type="bullet">
/// <item>集合（Collection）隔离：不同知识库/用户可使用独立集合，检索以集合为边界</item>
/// <item>记录固定为 <see cref="VectorRecord"/>，不引入 TKey/TRecord 泛型</item>
/// <item>向量由外部生成后传入，不依赖具体 Embedding 实现</item>
/// <item>集合记录操作见 <see cref="IVectorStoreCollection"/>，本接口只负责获取集合与集合生命周期</item>
/// </list>
/// </remarks>
public interface IVectorStore
{
    /// <summary>获取指定集合。集合不存在时懒创建空集合</summary>
    /// <param name="name">集合名称</param>
    /// <returns>集合对象，可反复使用</returns>
    IVectorStoreCollection GetCollection(String name);

    /// <summary>列出存储中全部集合名称</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>集合名称列表</returns>
    Task<IList<String>> ListCollectionNamesAsync(CancellationToken cancellationToken = default);

    /// <summary>删除整个集合及其全部记录。集合不存在时静默成功</summary>
    /// <param name="name">集合名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task RemoveCollectionAsync(String name, CancellationToken cancellationToken = default);
}

/// <summary>向量集合接口。管理同一集合内的高维向量记录，支持 Top-K 相似度检索</summary>
/// <remarks>
/// 对标 Microsoft.Extensions.VectorData.VectorStoreCollection，做非泛型简化：
/// <list type="bullet">
/// <item>集合内记录共享同一向量维度与距离度量，检索以集合为边界</item>
/// <item>记录固定为 <see cref="VectorRecord"/>，不引入 TKey/TRecord 泛型</item>
/// <item>批量操作用同名方法重载实现（与官方 UpsertAsync/DeleteAsync 重载一致）</item>
/// <item>向量由外部生成后传入，不依赖具体 Embedding 实现</item>
/// </list>
/// </remarks>
public interface IVectorStoreCollection
{
    /// <summary>集合名称</summary>
    String Name { get; }

    /// <summary>新增或更新记录。若 Id 已存在则覆盖</summary>
    /// <param name="record">向量记录</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UpsertAsync(VectorRecord record, CancellationToken cancellationToken = default);

    /// <summary>批量新增或更新。忽略 null 与空 Id 记录</summary>
    /// <param name="records">向量记录列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UpsertAsync(IEnumerable<VectorRecord> records, CancellationToken cancellationToken = default);

    /// <summary>按 Id 获取记录</summary>
    /// <param name="id">记录 Id</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>找到则返回记录，否则返回 null</returns>
    Task<VectorRecord?> GetAsync(String id, CancellationToken cancellationToken = default);

    /// <summary>Top-K 相似度检索。返回向量最接近查询向量的前 top 条</summary>
    /// <param name="queryVector">查询向量</param>
    /// <param name="top">返回条数（默认 5，0 表示返回全部）</param>
    /// <param name="minScore">最低相似度门槛（0–1，默认 0）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>按相似度降序排列的检索结果</returns>
    Task<IList<VectorSearchResult>> SearchAsync(Single[] queryVector, Int32 top = 5, Double minScore = 0, CancellationToken cancellationToken = default);

    /// <summary>删除指定记录。不存在时静默成功</summary>
    /// <param name="id">记录 Id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task DeleteAsync(String id, CancellationToken cancellationToken = default);

    /// <summary>批量删除记录。忽略不存在的 Id</summary>
    /// <param name="ids">记录 Id 列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task DeleteAsync(IEnumerable<String> ids, CancellationToken cancellationToken = default);

    /// <summary>获取集合内记录总数</summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task<Int64> CountAsync(CancellationToken cancellationToken = default);
}
