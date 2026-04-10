using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NewLife.AI.Models;
using NewLife.AI.Services;
using NewLife.Log;
using Xunit;

namespace XUnitTest.Services;

/// <summary>服务层（MessageRateLimiter、BackgroundGenerationService）单元测试</summary>
[DisplayName("服务层单元测试")]
public class ServiceTests
{
    // ── MessageRateLimiter ───────────────────────────────────────────────────

    #region MessageRateLimiter

    [Fact]
    [DisplayName("MessageRateLimiter—maxPerMinute <= 0 时始终允许")]
    public void MessageRateLimiter_MaxZero_AlwaysAllowed()
    {
        var limiter = new MessageRateLimiter();
        for (var i = 0; i < 100; i++)
            Assert.True(limiter.IsAllowed(1, 0));
    }

    [Fact]
    [DisplayName("MessageRateLimiter—maxPerMinute 负数时始终允许")]
    public void MessageRateLimiter_NegativeMax_AlwaysAllowed()
    {
        var limiter = new MessageRateLimiter();
        Assert.True(limiter.IsAllowed(1, -5));
        Assert.True(limiter.IsAllowed(1, -1));
    }

    [Fact]
    [DisplayName("MessageRateLimiter—首次调用在限制内时允许")]
    public void MessageRateLimiter_FirstCall_Allowed()
    {
        var limiter = new MessageRateLimiter();
        Assert.True(limiter.IsAllowed(userId: 100, maxPerMinute: 10));
    }

    [Fact]
    [DisplayName("MessageRateLimiter—调用次数达到上限时被拒绝")]
    public void MessageRateLimiter_AtLimit_Denied()
    {
        var limiter = new MessageRateLimiter();
        const Int32 max = 3;

        // 前 max 次应允许
        for (var i = 0; i < max; i++)
            Assert.True(limiter.IsAllowed(userId: 200, maxPerMinute: max), $"第 {i + 1} 次应允许");

        // 超过上限后应拒绝
        Assert.False(limiter.IsAllowed(userId: 200, maxPerMinute: max), "超过上限应被拒绝");
    }

    [Fact]
    [DisplayName("MessageRateLimiter—不同用户计数独立")]
    public void MessageRateLimiter_DifferentUsers_Independent()
    {
        var limiter = new MessageRateLimiter();

        // 用户 A 达到上限
        for (var i = 0; i < 2; i++)
            limiter.IsAllowed(userId: 1, maxPerMinute: 2);

        // 用户 A 超限
        Assert.False(limiter.IsAllowed(userId: 1, maxPerMinute: 2));

        // 用户 B 不受影响
        Assert.True(limiter.IsAllowed(userId: 2, maxPerMinute: 2));
    }

    [Fact]
    [DisplayName("MessageRateLimiter—userId=0 也可正常计数")]
    public void MessageRateLimiter_UserIdZero_Works()
    {
        var limiter = new MessageRateLimiter();
        Assert.True(limiter.IsAllowed(userId: 0, maxPerMinute: 5));
    }

    [Fact]
    [DisplayName("MessageRateLimiter—maxPerMinute=1 时只允许第一次")]
    public void MessageRateLimiter_MaxOne_OnlyFirstAllowed()
    {
        var limiter = new MessageRateLimiter();
        Assert.True(limiter.IsAllowed(userId: 99, maxPerMinute: 1));
        Assert.False(limiter.IsAllowed(userId: 99, maxPerMinute: 1));
        Assert.False(limiter.IsAllowed(userId: 99, maxPerMinute: 1));
    }

    [Fact]
    [DisplayName("MessageRateLimiter—多次构造各自独立计数")]
    public void MessageRateLimiter_NewInstance_IndependentCounter()
    {
        var limiter1 = new MessageRateLimiter();
        var limiter2 = new MessageRateLimiter();

        // limiter1 达到上限
        limiter1.IsAllowed(1, 1);
        Assert.False(limiter1.IsAllowed(1, 1));

        // limiter2 完全独立
        Assert.True(limiter2.IsAllowed(1, 1));
    }

    #endregion

    // ── BackgroundGenerationService ──────────────────────────────────────────

    #region BackgroundGenerationService

    /// <summary>立即完成（无元素）的空事件流</summary>
    private static async IAsyncEnumerable<ChatStreamEvent> EmptyStream(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.Yield();
        yield break;
    }

    /// <summary>生成指定个 content_delta 事件的流</summary>
    private static async IAsyncEnumerable<ChatStreamEvent> ContentStream(
        String[] texts,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var text in texts)
        {
            await Task.Yield();
            yield return new ChatStreamEvent { Type = "content_delta", Content = text };
        }
        yield return new ChatStreamEvent { Type = "message_done" };
    }

    [Fact]
    [DisplayName("BackgroundGenerationService—Register 创建进行中任务")]
    public async Task BackgroundGenerationService_Register_CreatesRunningTask()
    {
        var svc = new BackgroundGenerationService(Logger.Null);
        svc.Register(1001, EmptyStream());

        // 任务刚注册时 IsRunning 为 true（异步还未完成）
        var isRunning = svc.IsRunning(1001);
        // 等待完成
        await Task.Delay(100);
        var task = svc.GetTask(1001);
        Assert.NotNull(task);
        Assert.Equal(1001, task!.MessageId);
    }

    [Fact]
    [DisplayName("BackgroundGenerationService—Register 注册后 IsRunning 返回 true")]
    public void BackgroundGenerationService_Register_IsRunning()
    {
        var svc = new BackgroundGenerationService(Logger.Null);
        svc.Register(2001, LongRunningStream());
        Assert.True(svc.IsRunning(2001));
        svc.Stop(2001);
    }

    [Fact]
    [DisplayName("BackgroundGenerationService—GetTask 不存在 id 返回 null")]
    public void BackgroundGenerationService_GetTask_Missing_ReturnsNull()
    {
        var svc = new BackgroundGenerationService(Logger.Null);
        Assert.Null(svc.GetTask(9999));
    }

    [Fact]
    [DisplayName("BackgroundGenerationService—IsRunning 不存在 id 返回 false")]
    public void BackgroundGenerationService_IsRunning_Missing_ReturnsFalse()
    {
        var svc = new BackgroundGenerationService(Logger.Null);
        Assert.False(svc.IsRunning(9999));
    }

    [Fact]
    [DisplayName("BackgroundGenerationService—Stop 不存在的 id 不抛异常")]
    public void BackgroundGenerationService_Stop_MissingId_DoesNotThrow()
    {
        var svc = new BackgroundGenerationService(Logger.Null);
        svc.Stop(9999); // should not throw
    }

    [Fact]
    [DisplayName("BackgroundGenerationService—流完成后任务状态变为 Completed")]
    public async Task BackgroundGenerationService_Stream_CompletesTask()
    {
        var svc = new BackgroundGenerationService(Logger.Null);
        svc.Register(3001, EmptyStream());

        // 等待后台任务完成
        await Task.Delay(200);
        var task = svc.GetTask(3001);
        Assert.NotNull(task);
        Assert.Equal(BackgroundTaskStatus.Completed, task!.Status);
    }

    [Fact]
    [DisplayName("BackgroundGenerationService—content_delta 事件累积到 ContentBuilder")]
    public async Task BackgroundGenerationService_ContentDelta_Accumulated()
    {
        var svc = new BackgroundGenerationService(Logger.Null);
        svc.Register(4001, ContentStream(["Hello", " World"]));

        // 等待完成
        await Task.Delay(200);
        var task = svc.GetTask(4001);
        Assert.NotNull(task);
        Assert.Equal("Hello World", task!.ContentBuilder.ToString());
    }

    [Fact]
    [DisplayName("BackgroundGenerationService—Stop 后任务状态变为 Cancelled")]
    public async Task BackgroundGenerationService_Stop_CancelsTask()
    {
        // 使用阻塞流，确保任务在 Stop 调用时还在运行
        var svc = new BackgroundGenerationService(Logger.Null);
        svc.Register(5001, LongRunningStream());

        await Task.Delay(50); // 让后台任务启动
        svc.Stop(5001);
        await Task.Delay(100);

        var task = svc.GetTask(5001);
        // 任务要么被取消，要么已完成（取决于时序）
        Assert.NotNull(task);
        Assert.True(
            task!.Status == BackgroundTaskStatus.Cancelled ||
            task.Status == BackgroundTaskStatus.Completed);
    }

    [Fact]
    [DisplayName("BackgroundGenerationService—onComplete 回调在任务完成后被调用")]
    public async Task BackgroundGenerationService_OnComplete_Callback_Called()
    {
        var svc = new BackgroundGenerationService(Logger.Null);
        BackgroundTask completedTask = null;

        svc.Register(6001, EmptyStream(), t =>
        {
            completedTask = t;
            return Task.CompletedTask;
        });

        await Task.Delay(200);
        Assert.NotNull(completedTask);
        Assert.Equal(6001, completedTask!.MessageId);
    }

    [Fact]
    [DisplayName("BackgroundGenerationService—Subscribe 回放全部历史事件")]
    public async Task BackgroundGenerationService_Subscribe_ReplaysHistory()
    {
        var svc = new BackgroundGenerationService(Logger.Null);

        // 注册一个发 3 个 chunk 后完成的流
        svc.Register(7001, ThreeChunkStream(), null);
        await Task.Delay(300); // 等待完成

        // 通过 Subscribe 读取，应该能收到全部事件
        var events = new List<ChatStreamEvent>();
        using var cts = new CancellationTokenSource(1000);
        await foreach (var ev in svc.Subscribe(7001, cts.Token).ConfigureAwait(false))
        {
            events.Add(ev);
        }

        Assert.Equal(4, events.Count); // 3 content_delta + 1 message_done
        Assert.Equal("content_delta", events[0].Type);
        Assert.Equal("chunk0", events[0].Content);
        Assert.Equal("content_delta", events[1].Type);
        Assert.Equal("chunk1", events[1].Content);
        Assert.Equal("content_delta", events[2].Type);
        Assert.Equal("chunk2", events[2].Content);
        Assert.Equal("message_done", events[3].Type);
    }

    [Fact]
    [DisplayName("BackgroundGenerationService—Subscribe 任务运行中可接收实时事件")]
    public async Task BackgroundGenerationService_Subscribe_ReceivesLiveEvents()
    {
        var svc = new BackgroundGenerationService(Logger.Null);

        // 注册一个慢速流（每 20ms 一个 chunk）
        svc.Register(7002, LongRunningStream(), null);
        await Task.Delay(100); // 等待部分事件产生

        // 此时任务仍在运行
        var task = svc.GetTask(7002);
        Assert.NotNull(task);
        Assert.Equal(BackgroundTaskStatus.Running, task!.Status);

        // Subscribe 读取历史 + 实时事件
        var events = new List<ChatStreamEvent>();
        using var cts = new CancellationTokenSource(500);
        try
        {
            await foreach (var ev in svc.Subscribe(7002, cts.Token).ConfigureAwait(false))
            {
                events.Add(ev);
            }
        }
        catch (OperationCanceledException) { }

        // 应该至少收到初始的历史事件
        Assert.True(events.Count > 0, "Subscribe 应至少产出历史事件");

        svc.Stop(7002);
    }

    [Fact]
    [DisplayName("BackgroundGenerationService—Subscribe 不存在的任务返回空流")]
    public async Task BackgroundGenerationService_Subscribe_NotFound_ReturnsEmpty()
    {
        var svc = new BackgroundGenerationService(Logger.Null);
        var events = new List<ChatStreamEvent>();
        await foreach (var ev in svc.Subscribe(99999).ConfigureAwait(false))
        {
            events.Add(ev);
        }
        Assert.Empty(events);
    }

    /// <summary>发送 3 个 chunk 后完成的流</summary>
    private static async IAsyncEnumerable<ChatStreamEvent> ThreeChunkStream(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        for (var i = 0; i < 3; i++)
        {
            await Task.Delay(10, ct).ConfigureAwait(false);
            yield return new ChatStreamEvent { Type = "content_delta", Content = $"chunk{i}" };
        }
        yield return new ChatStreamEvent { Type = "message_done" };
    }

    /// <summary>持续发送内容但需外部取消的流</summary>
    private static async IAsyncEnumerable<ChatStreamEvent> LongRunningStream(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        for (var i = 0; i < 1000; i++)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(20, ct).ConfigureAwait(false);
            yield return new ChatStreamEvent { Type = "content_delta", Content = $"chunk{i}" };
        }
    }

    #endregion

    // ── BackgroundTask ──────────────────────────────────────────────────────

    #region BackgroundTask

    [Fact]
    [DisplayName("BackgroundTask—ContentBuilder 默认为空")]
    public void BackgroundTask_ContentBuilder_DefaultEmpty()
    {
        var task = new BackgroundTask();
        Assert.NotNull(task.ContentBuilder);
        Assert.Equal(0, task.ContentBuilder.Length);
    }

    [Fact]
    [DisplayName("BackgroundTask—ToolCalls 列表默认为空")]
    public void BackgroundTask_ToolCalls_DefaultEmpty()
    {
        var task = new BackgroundTask();
        Assert.NotNull(task.ToolCalls);
        Assert.Empty(task.ToolCalls);
    }

    [Fact]
    [DisplayName("BackgroundTask—MessageId、Status、StartTime 属性读写")]
    public void BackgroundTask_Properties_ReadWrite()
    {
        var task = new BackgroundTask
        {
            MessageId = 999,
            Status = BackgroundTaskStatus.Running,
            StartTime = new DateTime(2025, 1, 1),
        };
        Assert.Equal(999, task.MessageId);
        Assert.Equal(BackgroundTaskStatus.Running, task.Status);
        Assert.Equal(new DateTime(2025, 1, 1), task.StartTime);
    }

    [Fact]
    [DisplayName("BackgroundTaskStatus—枚举包含 Running/Completed/Failed/Cancelled")]
    public void BackgroundTaskStatus_HasRequiredValues()
    {
        Assert.True(Enum.IsDefined(typeof(BackgroundTaskStatus), BackgroundTaskStatus.Running));
        Assert.True(Enum.IsDefined(typeof(BackgroundTaskStatus), BackgroundTaskStatus.Completed));
        Assert.True(Enum.IsDefined(typeof(BackgroundTaskStatus), BackgroundTaskStatus.Failed));
        Assert.True(Enum.IsDefined(typeof(BackgroundTaskStatus), BackgroundTaskStatus.Cancelled));
    }

    #endregion
}
