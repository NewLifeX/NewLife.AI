using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;
using NewLife.AI.Models;
using NewLife.Log;

namespace NewLife.AI.Services;

/// <summary>后台继续生成服务。浏览器关闭后模型继续生成，结果持久化到数据库</summary>
/// <remarks>
/// 工作机制：
/// 1. Register 启动后台消费，事件写入 BackgroundTask.Events 列表
/// 2. Subscribe 返回 IAsyncEnumerable，从 Events[0] 开始读取，任务运行中则等待新事件
/// 3. 首次连接和断线重连均使用同一个 Subscribe，天然支持从头回放
/// 4. 任务完成后触发 onComplete 回调，将完整内容持久化到数据库
/// </remarks>
/// <param name="log">日志</param>
public class BackgroundGenerationService(ILog log)
{
    #region 属性
    private readonly ConcurrentDictionary<Int64, BackgroundTask> _tasks = new();
    private readonly ConcurrentDictionary<Int64, CancellationTokenSource> _cancellations = new();
    #endregion

    #region 任务管理
    /// <summary>注册后台生成任务。启动后台消费，事件存入 BackgroundTask 供 Subscribe 读取</summary>
    /// <param name="messageId">AI 回复消息编号</param>
    /// <param name="eventStream">管道事件流异步枚举</param>
    /// <param name="onComplete">任务完成回调（成功/失败/取消均触发）</param>
    public void Register(Int64 messageId, IAsyncEnumerable<ChatStreamEvent> eventStream, Func<BackgroundTask, Task>? onComplete = null)
    {
        var cts = new CancellationTokenSource();
        var task = new BackgroundTask
        {
            MessageId = messageId,
            StartTime = DateTime.Now,
            Status = BackgroundTaskStatus.Running,
        };

        _tasks[messageId] = task;
        _cancellations[messageId] = cts;

        // 启动后台消费任务
        _ = ConsumeAsync(task, eventStream, onComplete, cts.Token);
    }

    /// <summary>订阅事件流。从头回放全部已有事件，任务运行中则继续等待实时事件</summary>
    /// <param name="messageId">消息编号</param>
    /// <param name="cancellationToken">取消令牌（客户端断连时取消）</param>
    /// <returns>事件流（历史+实时），任务不存在则返回空流</returns>
    public async IAsyncEnumerable<ChatStreamEvent> Subscribe(Int64 messageId, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!_tasks.TryGetValue(messageId, out var task)) yield break;

        var index = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            // 关键：先快照等待句柄，再读取事件。否则若 AddEvent/Notify 发生在“读完事件”与“进入 WaitAsync”之间，
            // _signal 已被 Exchange 为新 tcs，新 tcs 不会被本次 Notify 触发，导致订阅者永久挂起、错过事件
            var waitTask = task.SignalTask;

            // 读取所有已有事件
            while (index < task.EventCount)
            {
                yield return task.GetEvent(index++);
            }

            // 任务已结束则退出
            if (task.Status != BackgroundTaskStatus.Running) yield break;

            // 等待新事件通知（使用先前快照的句柄，避免丢失唤醒）
            try { await task.WaitAsync(waitTask, cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { yield break; }
        }
    }

    /// <summary>停止后台生成任务</summary>
    /// <param name="messageId">消息编号</param>
    public void Stop(Int64 messageId)
    {
        if (_cancellations.TryRemove(messageId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }

        if (_tasks.TryGetValue(messageId, out var task))
        {
            task.Status = BackgroundTaskStatus.Cancelled;
            task.Notify();
        }
    }

    /// <summary>获取后台任务状态</summary>
    /// <param name="messageId">消息编号</param>
    /// <returns>后台任务信息，不存在返回 null</returns>
    public BackgroundTask? GetTask(Int64 messageId)
    {
        _tasks.TryGetValue(messageId, out var task);
        return task;
    }

    /// <summary>是否有正在运行的后台任务</summary>
    /// <param name="messageId">消息编号</param>
    /// <returns></returns>
    public Boolean IsRunning(Int64 messageId)
    {
        return _tasks.TryGetValue(messageId, out var task) && task.Status == BackgroundTaskStatus.Running;
    }
    #endregion

    #region 辅助
    /// <summary>后台消费事件流。将事件写入 BackgroundTask 供订阅者读取，同时收集完整内容供回调持久化</summary>
    private async Task ConsumeAsync(BackgroundTask task, IAsyncEnumerable<ChatStreamEvent> eventStream, Func<BackgroundTask, Task>? onComplete, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var ev in eventStream.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                // 写入事件列表并通知订阅者
                task.AddEvent(ev);

                // 收集完整结果（不受前端连接状态影响）
                switch (ev.Type)
                {
                    case "content_delta" when ev.Content != null:
                        task.ContentBuilder.Append(ev.Content);
                        break;
                    case "thinking_delta" when ev.Content != null:
                        task.ThinkingBuilder.Append(ev.Content);
                        break;
                    case "tool_call_start":
                        task.ToolCalls.Add(new BackgroundToolCall(ev.ToolCallId + "", ev.Name + "", ev.Arguments));
                        break;
                    case "tool_call_done":
                        UpdateToolCall(task.ToolCalls, ev.ToolCallId, true, ev.Result);
                        break;
                    case "tool_call_error":
                        UpdateToolCall(task.ToolCalls, ev.ToolCallId, false, ev.Error);
                        break;
                    case "message_done":
                        task.Usage = ev.Usage;
                        break;
                    case "error":
                        task.Error = ev.Message;
                        break;
                }
            }

            task.Status = BackgroundTaskStatus.Completed;
            log?.Info("后台生成任务完成，消息 {0}，内容长度 {1}", task.MessageId, task.ContentBuilder.Length);
        }
        catch (OperationCanceledException)
        {
            task.Status = BackgroundTaskStatus.Cancelled;
        }
        catch (Exception ex)
        {
            task.Status = BackgroundTaskStatus.Failed;
            task.Error = ex.Message;
            log?.Error("后台生成任务失败，消息 {0}: {1}", task.MessageId, ex.Message);
        }
        finally
        {
            task.EndTime = DateTime.Now;
            // 通知订阅者任务已结束（Subscribe 检查 Status 后退出）
            task.Notify();
            if (_cancellations.TryRemove(task.MessageId, out var removedCts))
                removedCts.Dispose();

            if (onComplete != null)
            {
                try
                {
                    await onComplete(task).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    log?.Error("后台生成回调失败: {0}", ex.Message);
                }
            }
        }
    }

    /// <summary>更新工具调用列表中指定 id 的结果</summary>
    private static void UpdateToolCall(List<BackgroundToolCall> calls, String? id, Boolean success, String? value)
    {
        for (var i = calls.Count - 1; i >= 0; i--)
        {
            if (calls[i].Id == id)
            {
                calls[i].Done = true;
                calls[i].Success = success;
                calls[i].Result = value;
                break;
            }
        }
    }
    #endregion
}

/// <summary>后台生成任务信息</summary>
public class BackgroundTask
{
    /// <summary>消息编号</summary>
    public Int64 MessageId { get; set; }

    /// <summary>任务状态</summary>
    public BackgroundTaskStatus Status { get; set; }

    /// <summary>开始时间</summary>
    public DateTime StartTime { get; set; }

    /// <summary>结束时间</summary>
    public DateTime EndTime { get; set; }

    /// <summary>正文内容</summary>
    public StringBuilder ContentBuilder { get; } = new();

    /// <summary>思考内容</summary>
    public StringBuilder ThinkingBuilder { get; } = new();

    /// <summary>工具调用记录</summary>
    public List<BackgroundToolCall> ToolCalls { get; } = [];

    /// <summary>用量统计</summary>
    public UsageDetails? Usage { get; set; }

    /// <summary>错误信息</summary>
    public String? Error { get; set; }

    #region 事件通知
    private readonly List<ChatStreamEvent> _events = [];
    private volatile TaskCompletionSource<Boolean> _signal = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>当前等待句柄的 Task 快照。订阅者必须在读取事件“之前”取此快照，避免 Notify 丢失</summary>
    internal Task SignalTask => _signal.Task;

    /// <summary>事件总数</summary>
    public Int32 EventCount { get { lock (_events) return _events.Count; } }

    /// <summary>读取指定位置的事件</summary>
    /// <param name="index">事件索引</param>
    /// <returns></returns>
    public ChatStreamEvent GetEvent(Int32 index) { lock (_events) return _events[index]; }

    /// <summary>写入事件并通知所有等待者</summary>
    /// <param name="ev">事件</param>
    internal void AddEvent(ChatStreamEvent ev)
    {
        lock (_events) _events.Add(ev);
        Notify();
    }

    /// <summary>唤醒所有等待中的订阅者</summary>
    internal void Notify()
    {
        var old = Interlocked.Exchange(ref _signal, new TaskCompletionSource<Boolean>(TaskCreationOptions.RunContinuationsAsynchronously));
        old.TrySetResult(true);
    }

    /// <summary>等待事件通知。<paramref name="signalTask"/> 应为读取事件之前快照的 <see cref="SignalTask"/></summary>
    /// <param name="signalTask">外部预先快照的等待句柄，避免与 Notify 产生丢失唤醒竞态</param>
    /// <param name="cancellationToken">取消令牌</param>
    internal async Task WaitAsync(Task signalTask, CancellationToken cancellationToken)
    {
        if (signalTask.IsCompleted) return;

        var tcs = new TaskCompletionSource<Boolean>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var _ = cancellationToken.Register(static s => ((TaskCompletionSource<Boolean>)s!).TrySetResult(default), tcs);
        await Task.WhenAny(signalTask, tcs.Task).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
    }
    #endregion
}

/// <summary>后台任务工具调用信息</summary>
/// <remarks>实例化</remarks>
/// <param name="id">调用编号</param>
/// <param name="name">工具名称</param>
/// <param name="arguments">调用参数</param>
public class BackgroundToolCall(String id, String name, String? arguments)
{
    /// <summary>调用编号</summary>
    public String Id { get; set; } = id;

    /// <summary>工具名称</summary>
    public String Name { get; set; } = name;

    /// <summary>调用参数</summary>
    public String? Arguments { get; set; } = arguments;

    /// <summary>是否已完成</summary>
    public Boolean Done { get; set; }

    /// <summary>是否成功</summary>
    public Boolean Success { get; set; } = true;

    /// <summary>返回结果或错误信息</summary>
    public String? Result { get; set; }
}

/// <summary>后台任务状态</summary>
public enum BackgroundTaskStatus
{
    /// <summary>运行中</summary>
    Running = 0,

    /// <summary>已完成</summary>
    Completed = 1,

    /// <summary>已失败</summary>
    Failed = 2,

    /// <summary>已取消</summary>
    Cancelled = 3,
}
