using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NewLife.Log;
using NewLife.Serialization;
using XCode.Membership;

namespace NewLife.ChatAI.Controllers;

/// <summary>ChatAI API 控制器基类。统一校验登录状态，提供当前用户信息和 SSE 流式输出能力</summary>
[ApiController]
public abstract class ChatApiControllerBase : ControllerBase, IActionFilter
{
    /// <summary>SSE 事件的 JSON 序列化选项</summary>
    protected static readonly JsonSerializerOptions SseJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // 允许中文等非 ASCII 字符直接输出，避免 SSE 数据中出现 \uXXXX 转义
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new SafeInt64Converter() },
    };

    /// <summary>获取当前登录用户编号</summary>
    /// <returns></returns>
    protected static Int32 GetCurrentUserId() => ManageProvider.User?.ID ?? 0;

    /// <summary>判断当前用户是否拥有系统角色（IsSystem=true）。用于系统管理接口的权限校验</summary>
    /// <returns>拥有任意 IsSystem 角色则返回 true</returns>
    protected static Boolean IsCurrentUserSystem()
    {
        var user = ManageProvider.User;
        return user != null && user.Roles.Any(e => e.IsSystem);
    }

    /// <summary>Action 执行前校验登录状态。未标记 AllowAnonymous 的接口要求已登录</summary>
    /// <param name="context">上下文</param>
    [NonAction]
    public void OnActionExecuting(ActionExecutingContext context)
    {
        // 标记了 AllowAnonymous 的接口跳过校验
        if (context.ActionDescriptor.EndpointMetadata.OfType<AllowAnonymousAttribute>().Any()) return;

        if (ManageProvider.User == null)
        {
            context.Result = new ObjectResult(new { code = "UNAUTHORIZED", message = "未登录，请先登录" })
            {
                StatusCode = 401
            };
        }
    }

    /// <summary>Action 执行后处理</summary>
    /// <param name="context">上下文</param>
    [NonAction]
    public void OnActionExecuted(ActionExecutedContext context) { }

    #region SSE 辅助
    /// <summary>SSE 心跳间隔（毫秒）。无新事件时按此间隔推送保活帧，防止反向代理因连接静默而断连</summary>
    private const Int32 SseHeartbeatIntervalMs = 20_000;

    /// <summary>SSE 心跳事件（单例复用，避免每次分配）</summary>
    private static readonly ChatStreamEvent _heartbeatEvent = ChatStreamEvent.Heartbeat();

    /// <summary>设置 SSE 响应头</summary>
    protected void SetSseHeaders()
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");
        Response.Headers.Append("X-Accel-Buffering", "no");  // 告知 Nginx 等反向代理禁用响应缓冲，保证 SSE 实时推送
    }

    /// <summary>流式写入 SSE 事件序列，统一处理取消与异常。每隔 20 秒无新事件时自动推送心跳保活帧</summary>
    /// <param name="events">事件异步序列</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <param name="errorCode">异常时向客户端推送的错误码</param>
    /// <param name="onError">异常回调，可用于埋点等副作用</param>
    protected async Task StreamEventsAsync(IAsyncEnumerable<ChatStreamEvent> events, CancellationToken cancellationToken, String errorCode = "STREAM_ERROR", Action<Exception>? onError = null)
    {
        var enumerator = events.GetAsyncEnumerator(cancellationToken);
        try
        {
            var nextTask = enumerator.MoveNextAsync().AsTask();
            var heartbeatDelay = Task.Delay(SseHeartbeatIntervalMs, cancellationToken);

            while (true)
            {
                var winner = await Task.WhenAny(nextTask, heartbeatDelay).ConfigureAwait(false);

                if (winner == heartbeatDelay)
                {
                    // 超过心跳间隔仍无新事件，推送保活帧并重置计时
                    await WriteSseEventAsync(_heartbeatEvent, cancellationToken).ConfigureAwait(false);
                    heartbeatDelay = Task.Delay(SseHeartbeatIntervalMs, cancellationToken);
                    continue;
                }

                // 有新事件到达：重置心跳计时
                heartbeatDelay = Task.Delay(SseHeartbeatIntervalMs, cancellationToken);

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
            // 非用户取消的超时或内部取消（如 HttpClient.Timeout / 下游管道超时）
            DefaultSpan.Current?.SetError(ex);
            onError?.Invoke(ex);
            try
            {
                await WriteSseEventAsync(ChatStreamEvent.ErrorEvent(errorCode, "生成超时，请重试"), CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // 客户端已断开连接，无法推送错误事件，仅记录日志
            }
        }
        catch (Exception ex)
        {
            DefaultSpan.Current?.SetError(ex);
            onError?.Invoke(ex);
            try
            {
                // 上下文超限等已知错误映射为友好文案，其余保持原始信息
                var info = ChatErrorHelper.Classify(ex.Message, errorCode);
                await WriteSseEventAsync(ChatStreamEvent.ErrorEvent(info.Code, info.Message), CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // 客户端已断开连接，无法推送错误事件，仅记录日志
            }
        }
        finally
        {
            try
            {
                await enumerator.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                DefaultSpan.Current?.SetError(ex);
                // DisposeAsync 内部异常（如 NotSupportedException）不影响响应，仅记录
            }
        }
    }

    /// <summary>SSE 事件单字段最大字符数。超过此长度时自动截断以防止 OOM</summary>
    private const Int32 MaxSseFieldLength = 100_000;

    /// <summary>写入 SSE 事件。序列化前自动截断超大字段，防止 Utf8JsonWriter 分配过量缓冲区导致 OOM</summary>
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
            // 极少数情况下截断后仍可能 OOM，兜底发送错误事件
            try
            {
                var fallback = ChatStreamEvent.ErrorEvent("STREAM_ERROR", "响应内容过长，已截断部分内容");
                var json = JsonSerializer.Serialize(fallback, SseJsonOptions);
                await Response.WriteAsync($"data: {json}\n\n", CancellationToken.None).ConfigureAwait(false);
                await Response.Body.FlushAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // 客户端已断连，静默忽略
            }
        }
    }

    /// <summary>截断 ChatStreamEvent 中超过 <see cref="MaxSseFieldLength"/> 的字符串字段</summary>
    private static void TruncateLargeFields(ChatStreamEvent ev)
    {
        ev.Content = TruncateField(ev.Content, MaxSseFieldLength);
        ev.Arguments = TruncateField(ev.Arguments, MaxSseFieldLength);
        ev.Result = TruncateField(ev.Result, MaxSseFieldLength);
        ev.Error = TruncateField(ev.Error, MaxSseFieldLength);
        ev.Message = TruncateField(ev.Message, MaxSseFieldLength);
        ev.KnowledgeRefs = TruncateField(ev.KnowledgeRefs, MaxSseFieldLength);
        ev.Title = TruncateField(ev.Title, MaxSseFieldLength);
        ev.Url = TruncateField(ev.Url, MaxSseFieldLength);
    }

    /// <summary>截断字符串到指定长度，追加省略标记</summary>
    private static String? TruncateField(String? value, Int32 maxLength)
    {
        if (value == null || value.Length <= maxLength) return value;
        return value[..maxLength] + $"\n\n[已截断，原始长度 {value.Length} 字符]";
    }
    #endregion
}
