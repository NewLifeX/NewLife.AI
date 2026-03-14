namespace NewLife.AI.Memory;

/// <summary>内存实现的向量存储。适合开发测试与小规模 RAG 场景</summary>
/// <remarks>线程安全，使用 lock 保护字典操作。</remarks>
public sealed class InMemoryVectorStore : IVectorStore
{
    private readonly Dictionary<String, VectorRecord> _records = [];
    private readonly Object _lock = new();

    /// <summary>新增或更新向量记录</summary>
    /// <param name="record">向量记录</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task UpsertAsync(VectorRecord record, CancellationToken cancellationToken = default)
    {
        if (record == null) throw new ArgumentNullException(nameof(record));
        if (String.IsNullOrEmpty(record.Id)) throw new ArgumentException("VectorRecord.Id 不能为空", nameof(record));

        lock (_lock)
            _records[record.Id] = record;

        return Task.CompletedTask;
    }

    /// <summary>批量新增或更新</summary>
    /// <param name="records">向量记录列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task UpsertBatchAsync(IEnumerable<VectorRecord> records, CancellationToken cancellationToken = default)
    {
        if (records == null) throw new ArgumentNullException(nameof(records));

        lock (_lock)
        {
            foreach (var r in records)
            {
                if (r != null && !String.IsNullOrEmpty(r.Id))
                    _records[r.Id] = r;
            }
        }
        return Task.CompletedTask;
    }

    /// <summary>按 Id 获取记录</summary>
    /// <param name="id">记录 Id</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task<VectorRecord?> GetAsync(String id, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _records.TryGetValue(id, out var record);
            return Task.FromResult<VectorRecord?>(record);
        }
    }

    /// <summary>Top-K 相似度检索</summary>
    /// <param name="queryVector">查询向量</param>
    /// <param name="topK">返回数量</param>
    /// <param name="minScore">最低相似度门槛</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task<IList<VectorSearchResult>> SearchAsync(Single[] queryVector, Int32 topK = 5, Double minScore = 0, CancellationToken cancellationToken = default)
    {
        if (queryVector == null || queryVector.Length == 0) throw new ArgumentNullException(nameof(queryVector));

        List<VectorRecord> snap;
        lock (_lock)
            snap = [.. _records.Values];

        var results = new List<VectorSearchResult>(snap.Count);
        foreach (var r in snap)
        {
            if (r.Vector == null || r.Vector.Length == 0) continue;
            var score = CosineSimilarity(queryVector, r.Vector);
            if (score >= minScore)
                results.Add(new VectorSearchResult { Record = r, Score = score });
        }

        results.Sort((a, b) => b.Score.CompareTo(a.Score));
        IList<VectorSearchResult> top = topK > 0 && results.Count > topK
            ? results.GetRange(0, topK)
            : results;

        return Task.FromResult(top);
    }

    /// <summary>删除指定记录</summary>
    /// <param name="id">记录 Id</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task DeleteAsync(String id, CancellationToken cancellationToken = default)
    {
        lock (_lock)
            _records.Remove(id);

        return Task.CompletedTask;
    }

    /// <summary>获取记录总数</summary>
    public Task<Int64> CountAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
            return Task.FromResult((Int64)_records.Count);
    }

    #region 辅助
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
