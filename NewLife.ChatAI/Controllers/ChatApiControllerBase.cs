using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NewLife.AI.Models;
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
        Converters = { new SafeInt64Converter() },
    };

    /// <summary>获取当前登录用户编号</summary>
    /// <returns></returns>
    protected static Int32 GetCurrentUserId() => XCode.Membership.ManageProvider.User?.ID ?? 0;

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
    /// <summary>设置 SSE 响应头</summary>
    protected void SetSseHeaders()
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");
        Response.Headers.Append("X-Accel-Buffering", "no");  // 告知 Nginx 等反向代理禁用响应缓冲，保证 SSE 实时推送
    }

    /// <summary>流式写入 SSE 事件序列，统一处理取消与异常</summary>
    /// <param name="events">事件异步序列</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <param name="errorCode">异常时向客户端推送的错误码</param>
    /// <param name="onError">异常回调，可用于埋点等副作用</param>
    protected async Task StreamEventsAsync(IAsyncEnumerable<ChatStreamEvent> events, CancellationToken cancellationToken, String errorCode = "STREAM_ERROR", Action<Exception>? onError = null)
    {
        try
        {
            await foreach (var ev in events.ConfigureAwait(false))
            {
                await WriteSseEventAsync(ev, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // 用户取消，不需要额外处理
        }
        catch (Exception ex)
        {
            DefaultSpan.Current?.SetError(ex);
            onError?.Invoke(ex);
            await WriteSseEventAsync(ChatStreamEvent.ErrorEvent(errorCode, ex.Message), CancellationToken.None).ConfigureAwait(false);
        }
    }

    /// <summary>写入 SSE 事件</summary>
    /// <param name="ev">事件对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    protected async Task WriteSseEventAsync(ChatStreamEvent ev, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(ev, SseJsonOptions);
        await Response.WriteAsync($"data: {json}\n\n", cancellationToken).ConfigureAwait(false);
        await Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
    #endregion
}
