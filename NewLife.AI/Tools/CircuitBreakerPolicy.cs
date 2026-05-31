namespace NewLife.AI.Tools;

/// <summary>工具提供者熔断状态</summary>
public enum CircuitBreakerState
{
    /// <summary>闭合——正常放行</summary>
    Closed,
    /// <summary>断开——冷却中，拒绝调用</summary>
    Open,
    /// <summary>半开——冷却到期，允许一次探测</summary>
    HalfOpen,
}

/// <summary>单工具提供者三态熔断策略。线程安全，使用原子操作（CAS）实现状态转换，零锁竞争</summary>
/// <remarks>
/// 状态转换：
/// <list type="bullet">
/// <item>Closed → Open：累计失败次数达到 <see cref="FailureThreshold"/></item>
/// <item>Open → HalfOpen：冷却窗口（<see cref="CooldownSeconds"/>）到期后，第一个到来的调用获得探测权</item>
/// <item>HalfOpen → Closed：探测调用成功</item>
/// <item>HalfOpen → Open：探测调用失败，重置冷却计时</item>
/// </list>
/// </remarks>
public sealed class CircuitBreakerPolicy
{
    #region 属性

    /// <summary>触发熔断所需的连续失败次数。默认 5</summary>
    public Int32 FailureThreshold { get; }

    /// <summary>熔断冷却秒数。冷却到期后允许一次 HalfOpen 探测。默认 60</summary>
    public Int32 CooldownSeconds { get; }

    /// <summary>当前熔断器状态（供监控/日志使用）</summary>
    public CircuitBreakerState State
    {
        get
        {
            var openUntil = Volatile.Read(ref _openUntilTick);
            if (openUntil == 0) return CircuitBreakerState.Closed;
            if (Runtime.TickCount64 < openUntil) return CircuitBreakerState.Open;
            return CircuitBreakerState.HalfOpen;
        }
    }

    /// <summary>剩余冷却秒数（Open 状态时有效，其余状态返回 0）</summary>
    public Int32 RemainingCooldownSeconds
    {
        get
        {
            var openUntil = Volatile.Read(ref _openUntilTick);
            if (openUntil == 0) return 0;
            var remaining = (Int32)((openUntil - Runtime.TickCount64) / 1000);
            return remaining > 0 ? remaining : 0;
        }
    }

    /// <summary>当前累计失败次数（仅 Closed 状态有意义）</summary>
    public Int32 FailureCount => Volatile.Read(ref _failureCount);

    #endregion

    #region 字段

    private Int32 _failureCount;
    private Int64 _openUntilTick;   // TickCount64 毫秒时间戳；0 = Closed
    private Int32 _halfOpenSlot;    // 0 = 探测权可领取；1 = 探测进行中

    #endregion

    #region 构造

    /// <summary>初始化熔断器</summary>
    /// <param name="failureThreshold">失败阈值，默认 5</param>
    /// <param name="cooldownSeconds">冷却秒数，默认 60</param>
    public CircuitBreakerPolicy(Int32 failureThreshold = 5, Int32 cooldownSeconds = 60)
    {
        FailureThreshold = failureThreshold > 0 ? failureThreshold : 5;
        CooldownSeconds = cooldownSeconds > 0 ? cooldownSeconds : 60;
    }

    #endregion

    #region 方法

    /// <summary>尝试获取调用权。返回 true 时可执行调用；返回 false 时熔断中，应直接返回降级响应</summary>
    public Boolean TryAcquire()
    {
        var openUntil = Volatile.Read(ref _openUntilTick);

        // Closed 状态：直接放行
        if (openUntil == 0) return true;

        // Open 状态（冷却未到期）：拒绝
        if (Runtime.TickCount64 < openUntil) return false;

        // 冷却到期→尝试领取 HalfOpen 探测权（CAS）
        return Interlocked.CompareExchange(ref _halfOpenSlot, 1, 0) == 0;
    }

    /// <summary>记录一次成功。转回 Closed 状态，重置所有计数器</summary>
    public void RecordSuccess()
    {
        Interlocked.Exchange(ref _failureCount, 0);
        Volatile.Write(ref _openUntilTick, 0);
        Interlocked.Exchange(ref _halfOpenSlot, 0);
    }

    /// <summary>记录一次失败。连续失败达到阈值时转为 Open；HalfOpen 探测失败时重置冷却计时</summary>
    public void RecordFailure()
    {
        // 释放 HalfOpen 探测槽（无论是否是 HalfOpen，幂等操作无副作用）
        Interlocked.Exchange(ref _halfOpenSlot, 0);

        var count = Interlocked.Increment(ref _failureCount);
        if (count >= FailureThreshold)
            Volatile.Write(ref _openUntilTick, Runtime.TickCount64 + (Int64)CooldownSeconds * 1000);
    }

    /// <summary>手动重置熔断器为 Closed 状态（运维干预用）</summary>
    public void Reset()
    {
        Interlocked.Exchange(ref _failureCount, 0);
        Volatile.Write(ref _openUntilTick, 0);
        Interlocked.Exchange(ref _halfOpenSlot, 0);
    }

    #endregion
}
