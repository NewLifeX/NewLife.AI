#nullable enable
using System;
using System.ComponentModel;
using System.Threading;
using NewLife.AI.Tools;
using Xunit;

namespace XUnitTest.Tools;

/// <summary>CircuitBreakerPolicy 三态熔断器单元测试。验证 Closed→Open→HalfOpen→Closed 状态转换、失败阈值、冷却与探测权</summary>
public class CircuitBreakerPolicyTests
{
    [Fact]
    [DisplayName("初始 Closed：TryAcquire 放行，失败计数为 0")]
    public void Initial_Closed_Allows()
    {
        var cb = new CircuitBreakerPolicy();
        Assert.Equal(CircuitBreakerState.Closed, cb.State);
        Assert.True(cb.TryAcquire());
        Assert.Equal(0, cb.FailureCount);
    }

    [Fact]
    [DisplayName("未达阈值的失败不触发熔断，计数递增")]
    public void Failure_BelowThreshold_StaysClosed()
    {
        var cb = new CircuitBreakerPolicy(5, 60);
        for (var i = 0; i < 4; i++)
            cb.RecordFailure();

        Assert.Equal(CircuitBreakerState.Closed, cb.State);
        Assert.Equal(4, cb.FailureCount);
        Assert.True(cb.TryAcquire());
    }

    [Fact]
    [DisplayName("达到阈值触发熔断：Open 状态拒绝调用，冷却剩余>0")]
    public void Failure_ReachThreshold_Opens()
    {
        var cb = new CircuitBreakerPolicy(3, 60);
        for (var i = 0; i < 3; i++)
            cb.RecordFailure();

        Assert.Equal(CircuitBreakerState.Open, cb.State);
        Assert.False(cb.TryAcquire());
        Assert.True(cb.RemainingCooldownSeconds > 0);
    }

    [Fact]
    [DisplayName("冷却到期进入 HalfOpen：仅放行一次探测，其余拒绝")]
    public void CooldownExpired_HalfOpen_SingleProbe()
    {
        var cb = new CircuitBreakerPolicy(2, 1);
        cb.RecordFailure();
        cb.RecordFailure();  // 达到阈值 → Open
        Assert.Equal(CircuitBreakerState.Open, cb.State);

        Thread.Sleep(1100);  // 等待冷却到期

        Assert.Equal(CircuitBreakerState.HalfOpen, cb.State);
        Assert.True(cb.TryAcquire());   // 第一个获得探测权
        Assert.False(cb.TryAcquire());  // 探测权已被领取，其余拒绝
    }

    [Fact]
    [DisplayName("HalfOpen 探测成功恢复 Closed：重置计数与冷却")]
    public void HalfOpen_ProbeSuccess_Closes()
    {
        var cb = new CircuitBreakerPolicy(2, 1);
        cb.RecordFailure();
        cb.RecordFailure();
        Thread.Sleep(1100);

        Assert.True(cb.TryAcquire());
        cb.RecordSuccess();

        Assert.Equal(CircuitBreakerState.Closed, cb.State);
        Assert.Equal(0, cb.FailureCount);
        Assert.True(cb.TryAcquire());
    }

    [Fact]
    [DisplayName("HalfOpen 探测失败重新 Open：重置冷却计时")]
    public void HalfOpen_ProbeFailure_Reopens()
    {
        var cb = new CircuitBreakerPolicy(2, 1);
        cb.RecordFailure();
        cb.RecordFailure();
        Thread.Sleep(1100);

        Assert.True(cb.TryAcquire());
        cb.RecordFailure();  // 探测失败 → 重新 Open

        Assert.Equal(CircuitBreakerState.Open, cb.State);
        Assert.False(cb.TryAcquire());
        Assert.True(cb.RemainingCooldownSeconds > 0);
    }

    [Fact]
    [DisplayName("RecordSuccess 提前重置为 Closed（中途恢复场景）")]
    public void RecordSuccess_ResetsToClosed()
    {
        var cb = new CircuitBreakerPolicy(2, 60);
        cb.RecordFailure();
        cb.RecordFailure();
        Assert.Equal(CircuitBreakerState.Open, cb.State);

        cb.RecordSuccess();

        Assert.Equal(CircuitBreakerState.Closed, cb.State);
        Assert.True(cb.TryAcquire());
    }

    [Fact]
    [DisplayName("Reset 手动恢复 Closed（运维干预）")]
    public void Reset_RestoresClosed()
    {
        var cb = new CircuitBreakerPolicy(2, 60);
        cb.RecordFailure();
        cb.RecordFailure();
        Assert.Equal(CircuitBreakerState.Open, cb.State);

        cb.Reset();

        Assert.Equal(CircuitBreakerState.Closed, cb.State);
        Assert.Equal(0, cb.FailureCount);
        Assert.True(cb.TryAcquire());
    }
}
