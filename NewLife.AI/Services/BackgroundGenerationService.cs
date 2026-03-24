using System.Collections.Concurrent;
using System.Text;
using NewLife.AI.Models;
using NewLife.Log;

namespace NewLife.AI.Services;

/// <summary>后台继续生成服务。浏览器关闭后模型继续生成，结果持久化到数据库</summary>
/// <remarks>
/// 工作机制：
/// 1. 用户发送消息时，如果开启后台生成，将任务注册到后台
/// 2. 浏览器断开（SSE 连接中断）时，后台任务继续执行
/// 3. 用户重新打开页面，可获取完整的 AI 回复
/// </remarks>
/// <remarks>实例化后台生成服务</remarks>
/// <param name="log">日志</param>
public class BackgroundGenerationService(ILog log)
{
    #region 属性
    private readonly ConcurrentDictionary<Int64, BackgroundTask> _tasks = new();
    private readonly ConcurrentDictionary<Int64, CancellationTokenSource> _cancellations = new();
    #endregion

    #region 任务管理
    /// <summary>注册后台生成任务</summary>
    /// <param name="messageId">AI 回复消息编号</param>
    /// <param name="eventStream">事件流异步枚举</param>
    /// <param name="onComplete">任务完成回调</param>
    /// <returns>取消令牌源，用于外部取消</returns>
    public CancellationTokenSource Register(Int64 messageId, IAsyncEnumerable<ChatStreamEvent> eventStream, Func<BackgroundTask, Task>? onComplete = null)
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

        return cts;
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
            task.Status = BackgroundTaskStatus.Cancelled;
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
    /// <summary>后台消费事件流，收集内容</summary>
    private async Task ConsumeAsync(BackgroundTask task, IAsyncEnumerable<ChatStreamEvent> eventStream, Func<BackgroundTask, Task>? onComplete, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var ev in eventStream.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                if (ev.Type == "content_delta" && ev.Content != null)
                    task.ContentBuilder.Append(ev.Content);
                else if (ev.Type == "thinking_delta" && ev.Content != null)
                    task.ThinkingBuilder.Append(ev.Content);
                else if (ev.Type == "message_done")
                    task.Usage = ev.Usage;
                else if (ev.Type == "error")
                    task.Error = ev.Message;

                task.Events.Add(ev);
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
            _cancellations.TryRemove(task.MessageId, out _);

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

    /// <summary>收集到的所有事件</summary>
    public List<ChatStreamEvent> Events { get; } = [];

    /// <summary>用量统计</summary>
    public UsageDetails? Usage { get; set; }

    /// <summary>错误信息</summary>
    public String? Error { get; set; }
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
