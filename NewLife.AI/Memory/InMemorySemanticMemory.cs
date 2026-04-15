namespace NewLife.AI.Memory;

/// <summary>内存实现的语义记忆。使用余弦相似度进行向量检索，适合开发测试和小数据集场景</summary>
/// <remarks>线程安全：内部使用 lock 保护，支持多线程并发读写。</remarks>
public sealed class InMemorySemanticMemory : ISemanticMemory
{
    private readonly Dictionary<String, Dictionary<String, MemoryEntry>> _store = [];
    private readonly Object _lock = new();

    /// <summary>保存记忆条目。若 Id 已存在则覆盖</summary>
    /// <param name="entry">记忆条目</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task SaveAsync(MemoryEntry entry, CancellationToken cancellationToken = default)
    {
        if (entry == null) throw new ArgumentNullException(nameof(entry));
        if (String.IsNullOrEmpty(entry.Id)) throw new ArgumentException("Id 不能为空", nameof(entry));
        if (String.IsNullOrEmpty(entry.Collection)) throw new ArgumentException("Collection 不能为空", nameof(entry));

        lock (_lock)
        {
            if (!_store.TryGetValue(entry.Collection, out var col))
            {
                col = [];
                _store[entry.Collection] = col;
            }
            col[entry.Id] = entry;
        }
        return Task.CompletedTask;
    }

    /// <summary>按 Id 获取单条记忆</summary>
    /// <param name="collection">集合名称</param>
    /// <param name="id">条目 Id</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task<MemoryEntry?> GetAsync(String collection, String id, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_store.TryGetValue(collection, out var col) && col.TryGetValue(id, out var entry))
                return Task.FromResult<MemoryEntry?>(entry);
        }
        return Task.FromResult<MemoryEntry?>(null);
    }

    /// <summary>相似度检索。使用余弦相似度计算，返回 Top-N 结果</summary>
    /// <param name="collection">集合名称</param>
    /// <param name="queryVector">查询向量</param>
    /// <param name="topN">返回条数</param>
    /// <param name="minRelevance">最低相似度门槛</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task<IList<MemorySearchResult>> SearchAsync(String collection, Single[] queryVector, Int32 topN = 5, Double minRelevance = 0, CancellationToken cancellationToken = default)
    {
        if (queryVector == null || queryVector.Length == 0) throw new ArgumentNullException(nameof(queryVector));

        List<MemoryEntry> entries;
        lock (_lock)
        {
            if (!_store.TryGetValue(collection, out var col))
                return Task.FromResult<IList<MemorySearchResult>>([]);
            entries = [.. col.Values];
        }

        var results = new List<MemorySearchResult>(entries.Count);
        foreach (var entry in entries)
        {
            if (entry.Vector == null || entry.Vector.Length == 0) continue;
            var score = CosineSimilarity(queryVector, entry.Vector);
            if (score >= minRelevance)
                results.Add(new MemorySearchResult { Entry = entry, Relevance = score });
        }

        results.Sort((a, b) => b.Relevance.CompareTo(a.Relevance));
        IList<MemorySearchResult> topResults = topN > 0 && results.Count > topN
            ? results.GetRange(0, topN)
            : results;

        return Task.FromResult(topResults);
    }

    /// <summary>删除指定条目</summary>
    /// <param name="collection">集合名称</param>
    /// <param name="id">条目 Id</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task RemoveAsync(String collection, String id, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_store.TryGetValue(collection, out var col))
            {
                col.Remove(id);
                if (col.Count == 0) _store.Remove(collection);
            }
        }
        return Task.CompletedTask;
    }

    /// <summary>列出集合内所有条目 Id</summary>
    /// <param name="collection">集合名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task<IList<String>> ListIdsAsync(String collection, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_store.TryGetValue(collection, out var col))
                return Task.FromResult<IList<String>>([.. col.Keys]);
        }
        return Task.FromResult<IList<String>>([]);
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
