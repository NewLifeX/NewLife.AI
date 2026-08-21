using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using NewLife.Serialization;

namespace NewLife.ChatAI.Controllers;

/// <summary>ChatAI SSE 流式 API 控制器基类。在 <see cref="ChatApiControllerBase"/> 基础上提供 SSE 流式输出能力
/// （心跳保活 / 取消与异常处理 / 超大字段截断），仅需要 SSE 的控制器继承本类</summary>
/// <remarks>
/// StarChat 通过源码链接（<c>&lt;Compile Include&gt;</c>）共用本文件，修改时保持基类方法签名稳定。
/// </remarks>
public abstract class ChatSseControllerBase : ChatApiControllerBase
{
    #region SSE 辅助
    /// <summary>SSE 事件的 JSON 序列化选项。从 Default 派生以携带 TypeInfoResolver，避免节点序列化触发 MakeReadOnly 抛错</summary>
    protected static readonly JsonSerializerOptions SseJsonOptions = new(JsonSerializerOptions.Default)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // 允许中文等非 ASCII 字符直接输出，避免 SSE 数据中出现 \uXXXX 转义
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new SafeInt64Converter() },
    };

    /// <summary>心跳间隔（毫秒）。无新事件时按此间隔推送保活帧，防止反向代理因连接静默而断连</summary>
    private const Int32 _heartbeatIntervalMs = 20_000;

    /// <summary>SSE 事件单字段最大字符数。超过此长度时自动截断以防止 OOM</summary>
    private const Int32 _maxFieldLength = 100_000;

    /// <summary>SSE 心跳事件（单例复用，避免每次分配）</summary>
    private static readonly ChatStreamEvent _heartbeatEvent = ChatStreamEvent.Heartbeat();

    /// <summary>设置 SSE 响应头</summary>
    protected void SetSseHeaders()
    {
        Response.Headers.Append("Content-Type", "text/event-stream; charset=utf-8");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");
        Response.Headers.Append("X-Accel-Buffering", "no");  // 告知 Nginx 等反向代理禁用响应缓冲，保证 SSE 实时推送
    }

    /// <summary>流式写入 SSE 事件序列。心跳循环（20 秒保活 + 取消/异常统一处理），序列化与 HttpResponse 写入由本基类实现</summary>
    /// <param name="events">事件异步序列</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <param name="errorCode">异常时向客户端推送的错误码</param>
    /// <param name="onError">异常回调，可用于埋点等副作用</param>
    protected async Task StreamEventsAsync(IAsyncEnumerable<ChatStreamEvent> events, CancellationToken cancellationToken, String errorCode = "STREAM_ERROR", Action<Exception>? onError = null)
    {
        if (events == null) throw new ArgumentNullException(nameof(events));

        var enumerator = events.GetAsyncEnumerator(cancellationToken);
        try
        {
            var nextTask = enumerator.MoveNextAsync().AsTask();
            var heartbeatDelay = Task.Delay(_heartbeatIntervalMs, cancellationToken);

            while (true)
            {
                // 用户取消时退出：取消后的 Task.Delay 恒为 canceled，若不检查会陷入心跳死循环
                if (cancellationToken.IsCancellationRequested) break;

                var winner = await Task.WhenAny(nextTask, heartbeatDelay).ConfigureAwait(false);

                if (winner == heartbeatDelay)
                {
                    // 超过心跳间隔仍无新事件，推送保活帧并重置计时
                    await WriteSseEventAsync(_heartbeatEvent, cancellationToken).ConfigureAwait(false);
                    heartbeatDelay = Task.Delay(_heartbeatIntervalMs, cancellationToken);
                    continue;
                }

                // 有新事件到达：重置心跳计时
                heartbeatDelay = Task.Delay(_heartbeatIntervalMs, cancellationToken);

                if (!await nextTask.ConfigureAwait(false)) break;
                await WriteSseEventAsync(enumerator.Current, cancellationToken).ConfigureAwait(false);
                nextTask = enumerator.MoveNextAsync().AsTask();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 用户主动取消（关闭 Tab / 导航离开），正常退出，不需要额外处理
        }
        catch (OperationCanceledException ex)
        {
            // 非用户取消的超时或内部取消（如 HttpClient.Timeout / 下游管道超时）。
            // 必须通知前端，否则 isGenerating 永不清零，消息卡在 streaming 状态
            onError?.Invoke(ex);
            await TryWriteErrorAsync(ex, errorCode);
        }
        catch (Exception ex)
        {
            onError?.Invoke(ex);
            await TryWriteErrorAsync(ex, errorCode);
        }
        finally
        {
            try
            {
                await enumerator.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex);
                // DisposeAsync 内部异常（如 NotSupportedException）不影响响应，仅记录
            }
        }
    }

    /// <summary>异常 → 错误事件。超时（OperationCanceledException）推固定文案，其余保持原始错误信息</summary>
    /// <param name="ex">异常</param>
    /// <param name="errorCode">错误码</param>
    /// <returns>错误事件</returns>
    private static ChatStreamEvent? MapError(Exception ex, String errorCode)
    {
        if (ex is OperationCanceledException)
            return ChatStreamEvent.ErrorEvent(errorCode, "生成超时，请重试");

        return ChatStreamEvent.ErrorEvent(errorCode, ex.Message);
    }

    /// <summary>异常时推送错误事件。客户端已断开时静默</summary>
    /// <param name="ex">异常</param>
    /// <param name="errorCode">错误码</param>
    private async Task TryWriteErrorAsync(Exception ex, String errorCode)
    {
        try
        {
            var errorEvent = MapError(ex, errorCode);
            if (errorEvent != null)
                await WriteSseEventAsync(errorEvent, CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // 客户端已断开连接，无法推送错误事件，仅记录日志
        }
    }

    /// <summary>写入单个 SSE 事件（<c>data: {json}\n\n</c>）。序列化前自动截断超大字段，防止 Utf8JsonWriter 分配过量缓冲区导致 OOM</summary>
    /// <param name="ev">事件对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    protected async Task WriteSseEventAsync(ChatStreamEvent ev, CancellationToken cancellationToken)
    {
        // 截断超大字符串字段，防御工具结果 / content_delta / knowledge_refs 等来源的巨量数据
        TruncateLargeFields(ev);

        try
        {
            var json = JsonSerializer.Serialize(ev, SseJsonOptions);
            await Response.WriteAsync($"data: {json}\n\n", cancellationToken).ConfigureAwait(false);
            await Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OutOfMemoryException)
        {
            // 极少数情况下截断后仍可能 OOM（如字符串本身已被截断但嵌套层级复杂），这里兜底发送错误事件
            try
            {
                var fallback = ChatStreamEvent.ErrorEvent("STREAM_ERROR", "响应内容过长，已截断部分内容");
                var fallbackJson = JsonSerializer.Serialize(fallback, SseJsonOptions);
                await Response.WriteAsync($"data: {fallbackJson}\n\n", CancellationToken.None).ConfigureAwait(false);
                await Response.Body.FlushAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // 客户端已断连，静默忽略
            }
        }
    }

    /// <summary>截断字符串到指定长度，追加省略标记</summary>
    /// <param name="value">原值</param>
    /// <param name="maxLength">最大长度</param>
    /// <returns>截断后的字符串；未超长时返回原值</returns>
    private static String? TruncateField(String? value, Int32 maxLength)
    {
        if (value == null || value.Length <= maxLength) return value;
        return value[..maxLength] + $"\n\n[已截断，原始长度 {value.Length} 字符]";
    }

    /// <summary>截断 <see cref="ChatStreamEvent"/> 中超过 <see cref="_maxFieldLength"/> 的字符串字段（原地修改）。
    /// 防御工具结果 / content_delta / knowledge_refs 等来源的巨量数据</summary>
    /// <param name="ev">事件对象</param>
    private static void TruncateLargeFields(ChatStreamEvent ev)
    {
        ev.Content = TruncateField(ev.Content, _maxFieldLength);
        ev.Arguments = TruncateField(ev.Arguments, _maxFieldLength);
        ev.Result = TruncateField(ev.Result, _maxFieldLength);
        ev.Error = TruncateField(ev.Error, _maxFieldLength);
        ev.Message = TruncateField(ev.Message, _maxFieldLength);
        ev.KnowledgeRefs = TruncateField(ev.KnowledgeRefs, _maxFieldLength);
        ev.Title = TruncateField(ev.Title, _maxFieldLength);
        ev.Url = TruncateField(ev.Url, _maxFieldLength);
    }
    #endregion
}
