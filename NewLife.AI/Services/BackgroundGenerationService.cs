using System.Collections.Concurrent;
using System.Text;
using NewLife.AI.Models;
using NewLife.Caching;
using NewLife.Log;

namespace NewLife.AI.Services;

/// <summary>后台继续生成服务。浏览器关闭后模型继续生成，结果持久化到数据库</summary>
/// <remarks>
/// 工作机制：
/// 1. 用户发送消息时，如果开启后台生成，将管道事件流注册到后台
/// 2. 后台通过 MemoryQueue 缓冲事件，前端从队列异步消费
/// 3. 浏览器断开后，后台任务继续执行并收集完整结果
/// 4. 任务完成后触发回调，将完整内容持久化到数据库
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
    /// <summary>注册后台生成任务。启动后台消费并返回事件队列供前端实时读取</summary>
    /// <param name="messageId">AI 回复消息编号</param>
    /// <param name="eventStream">管道事件流异步枚举</param>
    /// <param name="onComplete">任务完成回调（成功/失败/取消均触发）</param>
    /// <returns>生产消费队列，前端通过 TakeOneAsync 消费，null 表示流结束</returns>
    public IProducerConsumer<ChatStreamEvent> Register(Int64 messageId, IAsyncEnumerable<ChatStreamEvent> eventStream, Func<BackgroundTask, Task>? onComplete = null)
    {
        var cts = new CancellationTokenSource();
        var queue = new MemoryQueue<ChatStreamEvent>();
        var task = new BackgroundTask
        {
            MessageId = messageId,
            StartTime = DateTime.Now,
            Status = BackgroundTaskStatus.Running,
        };

        _tasks[messageId] = task;
        _cancellations[messageId] = cts;

        // 启动后台消费任务：写入队列同时收集完整结果
        _ = ConsumeAsync(task, eventStream, queue, onComplete, cts.Token);

        return queue;
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
    /// <summary>后台消费事件流。将事件写入队列供前端实时消费，同时收集完整内容供回调持久化</summary>
    private async Task ConsumeAsync(BackgroundTask task, IAsyncEnumerable<ChatStreamEvent> eventStream, MemoryQueue<ChatStreamEvent> queue, Func<BackgroundTask, Task>? onComplete, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var ev in eventStream.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                // 写入队列供前端实时消费
                queue.Add(ev);

                // 同步收集完整结果（不受前端连接状态影响）
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
            // null 哨兵通知消费端流已结束（避免 default! 直接传入 params 引起歧义）
            ChatStreamEvent? end = null;
            queue.Add(end!);
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
