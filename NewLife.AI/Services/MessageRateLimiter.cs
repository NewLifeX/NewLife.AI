using System.Collections.Concurrent;

namespace NewLife.AI.Services;

/// <summary>聊天消息速率限制器。按作用域+编号维度限流，防止恶意或高频调用</summary>
/// <remarks>
/// 使用固定分钟桶（1-minute bucket）策略：对每个作用域+编号每分钟内的计数独立累加，
/// 超出上限则拒绝。过期桶按计数触发惰性清理（每 200 次计数全表扫描一次），
/// 请求停止后过期桶会残留至下次清理触发；活跃用户量不大时影响可忽略。
/// </remarks>
public class MessageRateLimiter
{
    /// <summary>用户作用域。旧重载 IsAllowed(userId, ...) 与本作用域使用同一计数器</summary>
    public const String UserScope = "user";

    // key: (Scope, Id, 分钟桶) -> 本分钟内计数
    private readonly ConcurrentDictionary<(String Scope, Int32 Id, Int64 Bucket), Int32> _counters = new();

    #region 方法
    /// <summary>判断当前用户本分钟内是否仍在允许范围内，并同步递增计数</summary>
    /// <param name="userId">用户编号</param>
    /// <param name="maxPerMinute">每分钟最大消息数，&lt;= 0 表示不限流</param>
    /// <returns>允许则 true，超限则 false</returns>
    public Boolean IsAllowed(Int32 userId, Int32 maxPerMinute) => IsAllowed(UserScope, userId, maxPerMinute);

    /// <summary>判断指定作用域编号本分钟内是否仍在允许范围内，并同步递增计数。不同作用域之间计数独立（如 user/appkey）</summary>
    /// <param name="scope">作用域名称，区分不同维度的限流计数器</param>
    /// <param name="id">作用域内编号（用户编号/密钥编号等）</param>
    /// <param name="maxPerMinute">每分钟最大请求数，&lt;= 0 表示不限流</param>
    /// <returns>允许则 true，超限则 false</returns>
    public Boolean IsAllowed(String scope, Int32 id, Int32 maxPerMinute)
    {
        if (maxPerMinute <= 0) return true;

        var bucket = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMinute;
        var key = (scope ?? UserScope, id, bucket);
        var count = _counters.AddOrUpdate(key, 1, (_, c) => c + 1);

        // 每 200 次计数触发一次老桶清理，防止内存泄漏。
        // 原 count%200==1 在 count=1 时恒触发（每用户每分钟首条消息即全表扫描），改 count>=200 才进入清理节奏
        if (count >= 200 && count % 200 == 0)
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
