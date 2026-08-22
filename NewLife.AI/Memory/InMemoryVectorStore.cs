using System.Collections.Concurrent;

namespace NewLife.AI.Memory;

/// <summary>内存实现的向量存储。适合开发测试与小规模 RAG 场景</summary>
/// <remarks>线程安全：集合注册表使用 ConcurrentDictionary，GetOrAdd 原子创建，无显式锁。</remarks>
public sealed class InMemoryVectorStore : IVectorStore
{
    private readonly ConcurrentDictionary<String, InMemoryVectorStoreCollection> _collections = new();

    /// <summary>获取指定集合。集合不存在时懒创建空集合</summary>
    /// <param name="name">集合名称</param>
    /// <returns>集合对象，可反复使用</returns>
    public IVectorStoreCollection GetCollection(String name)
    {
        if (String.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));

        return _collections.GetOrAdd(name, key => new InMemoryVectorStoreCollection(key));
    }

    /// <summary>列出存储中全部集合名称</summary>
    /// <param name="cancellationToken">取消令牌</param>
    public Task<IList<String>> ListCollectionNamesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IList<String>>([.. _collections.Keys]);
    }

    /// <summary>删除整个集合及其全部记录。集合不存在时静默成功</summary>
    /// <param name="name">集合名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task RemoveCollectionAsync(String name, CancellationToken cancellationToken = default)
    {
        _collections.TryRemove(name, out _);

        return TaskEx.CompletedTask;
    }
}

/// <summary>内存实现的向量集合。使用余弦相似度进行向量检索，适合开发测试和小数据集场景</summary>
/// <remarks>线程安全：记录表使用 ConcurrentDictionary，各操作原子且无显式锁。</remarks>
public sealed class InMemoryVectorStoreCollection(String name) : IVectorStoreCollection
{
    private readonly ConcurrentDictionary<String, VectorRecord> _records = new();

    /// <summary>集合名称</summary>
    public String Name { get; } = name;

    /// <summary>新增或更新记录。若 Id 已存在则覆盖</summary>
    /// <param name="record">向量记录</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task UpsertAsync(VectorRecord record, CancellationToken cancellationToken = default)
    {
        if (record == null) throw new ArgumentNullException(nameof(record));
        if (String.IsNullOrEmpty(record.Id)) throw new ArgumentException("VectorRecord.Id 不能为空", nameof(record));

        _records[record.Id] = record;

        return TaskEx.CompletedTask;
    }

    /// <summary>批量新增或更新。忽略 null 与空 Id 记录</summary>
    /// <param name="records">向量记录列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task UpsertAsync(IEnumerable<VectorRecord> records, CancellationToken cancellationToken = default)
    {
        if (records == null) throw new ArgumentNullException(nameof(records));

        foreach (var r in records)
        {
            if (r != null && !String.IsNullOrEmpty(r.Id))
                _records[r.Id] = r;
        }
        return TaskEx.CompletedTask;
    }

    /// <summary>按 Id 获取记录</summary>
    /// <param name="id">记录 Id</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>找到则返回记录，否则返回 null</returns>
    public Task<VectorRecord?> GetAsync(String id, CancellationToken cancellationToken = default)
    {
        _records.TryGetValue(id, out var record);
        return Task.FromResult<VectorRecord?>(record);
    }

    /// <summary>Top-K 相似度检索。使用余弦相似度计算，返回 Top-K 结果</summary>
    /// <param name="queryVector">查询向量</param>
    /// <param name="top">返回条数（0 表示返回全部）</param>
    /// <param name="minScore">最低相似度门槛</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>按相似度降序的检索结果</returns>
    public Task<IList<VectorSearchResult>> SearchAsync(Single[] queryVector, Int32 top = 5, Double minScore = 0, CancellationToken cancellationToken = default)
    {
        if (queryVector == null || queryVector.Length == 0) throw new ArgumentNullException(nameof(queryVector));

        // ConcurrentDictionary.Values 返回快照，弱一致即可（检索为尽力而为的排序）
        var results = _records.Values
            .Where(r => r.Vector != null && r.Vector.Length > 0)
            .Select(r => new VectorSearchResult { Record = r, Score = CosineSimilarity(queryVector, r.Vector!) })
            .Where(x => x.Score >= minScore)
            .OrderByDescending(x => x.Score)
            .ToList();
        IList<VectorSearchResult> topResults = top > 0 && results.Count > top
            ? results.GetRange(0, top)
            : results;

        return Task.FromResult(topResults);
    }

    /// <summary>删除指定记录。不存在时静默成功</summary>
    /// <param name="id">记录 Id</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task DeleteAsync(String id, CancellationToken cancellationToken = default)
    {
        _records.TryRemove(id, out _);

        return TaskEx.CompletedTask;
    }

    /// <summary>批量删除记录。忽略不存在的 Id</summary>
    /// <param name="ids">记录 Id 列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task DeleteAsync(IEnumerable<String> ids, CancellationToken cancellationToken = default)
    {
        if (ids == null) throw new ArgumentNullException(nameof(ids));

        foreach (var id in ids)
            _records.TryRemove(id, out _);

        return TaskEx.CompletedTask;
    }

    /// <summary>获取记录总数</summary>
    /// <param name="cancellationToken">取消令牌</param>
    public Task<Int64> CountAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult((Int64)_records.Count);
    }

    #region 辅助
    /// <summary>计算两个向量的余弦相似度</summary>
    /// <param name="a">向量 a</param>
    /// <param name="b">向量 b</param>
    /// <returns>余弦相似度（-1 ~ 1，归一化向量返回 0 ~ 1）</returns>
    private static Double CosineSimilarity(Single[] a, Single[] b)
    {
        var len = Math.Min(a.Length, b.Length);
        Double dot = 0, normA = 0, normB = 0;
        for (var i = 0; i < len; i++)
        {
            dot += (Double)a[i] * b[i];
            normA += (Double)a[i] * a[i];
            normB += (Double)b[i] * b[i];
        }
        var denom = Math.Sqrt(normA) * Math.Sqrt(normB);
        if (denom < 1e-10) return 0;
        return dot / denom;
    }
    #endregion
}
