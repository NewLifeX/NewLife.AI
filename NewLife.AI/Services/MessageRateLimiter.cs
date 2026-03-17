using System.Collections.Concurrent;

namespace NewLife.AI.Services;

/// <summary>聊天消息速率限制器。按用户维度限流，防止恶意或高频调用</summary>
/// <remarks>
/// 使用固定分钟桶（1-minute bucket）策略：对每个用户每分钟内的消息计数独立累加，
/// 超出上限则拒绝。后台定期清理过期桶，避免内存无限增长。
/// </remarks>
public class MessageRateLimiter
{
    // key: (userId, 分钟桶) -> 本分钟内消息计数
    private readonly ConcurrentDictionary<(Int32 UserId, Int64 Bucket), Int32> _counters = new();

    #region 方法
    /// <summary>判断当前用户本分钟内是否仍在允许范围内，并同步递增计数</summary>
    /// <param name="userId">用户编号</param>
    /// <param name="maxPerMinute">每分钟最大消息数，&lt;= 0 表示不限流</param>
    /// <returns>允许则 true，超限则 false</returns>
    public Boolean IsAllowed(Int32 userId, Int32 maxPerMinute)
    {
        if (maxPerMinute <= 0) return true;

        var bucket = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMinute;
        var key = (userId, bucket);
        var count = _counters.AddOrUpdate(key, 1, (_, c) => c + 1);

        // 每 200 次计数触发一次老桶清理，防止内存泄漏
        if (count % 200 == 1)
            CleanupOldBuckets(bucket);

        return count <= maxPerMinute;
    }
    #endregion

    #region 辅助
    /// <summary>清理两分钟前的过期计数桶</summary>
    /// <param name="currentBucket">当前分钟桶编号</param>
    private void CleanupOldBuckets(Int64 currentBucket)
    {
        foreach (var key in _counters.Keys)
        {
            if (key.Bucket < currentBucket - 2)
                _counters.TryRemove(key, out _);
        }
    }
    #endregion
}
