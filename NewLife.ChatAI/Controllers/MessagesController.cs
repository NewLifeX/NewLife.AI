using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using NewLife.AI.Models;
using NewLife.AI.Services;
using NewLife.Log;
using NewLife.ChatAI.Models;
using NewLife.ChatAI.Services;
using NewLife.Serialization;

namespace NewLife.ChatAI.Controllers;

/// <summary>消息控制器</summary>
[Route("api")]
public class MessagesController(ChatApplicationService chatService, MessageRateLimiter rateLimiter, ITracer tracer) : ChatApiControllerBase
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new SafeInt64Converter() },
    };

    /// <summary>获取会话消息列表</summary>
    /// <param name="conversationId">会话编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    [HttpGet("conversations/{conversationId:long}/messages")]
    public async Task<ActionResult<IReadOnlyList<MessageDto>>> QueryAsync([FromRoute] Int64 conversationId, CancellationToken cancellationToken)
    {
        var result = await chatService.GetMessagesAsync(conversationId, GetCurrentUserId(), cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>全文搜索消息内容。在当前用户所有会话中按关键词搜索</summary>
    /// <param name="keyword">搜索关键词</param>
    /// <param name="page">页码</param>
    /// <param name="pageSize">每页数量</param>
    /// <returns></returns>
    [HttpGet("messages/search")]
    public ActionResult<PagedResultDto<MessageSearchResultDto>> SearchAsync([FromQuery] String keyword, [FromQuery] Int32 page = 1, [FromQuery] Int32 pageSize = 20)
    {
        if (String.IsNullOrWhiteSpace(keyword))
            return BadRequest(new { code = "INVALID_REQUEST", message = "keyword 不能为空" });
        if (keyword.Length > 200)
            return BadRequest(new { code = "INVALID_REQUEST", message = "搜索关键词过长" });

        var result = chatService.SearchMessages(GetCurrentUserId(), keyword, page, pageSize);
        return Ok(result);
    }

    /// <summary>发送消息并以 SSE 流式返回。支持 message_start/thinking_delta/thinking_done/content_delta/tool_call_start/tool_call_done/tool_call_error/message_done/error 事件</summary>
    /// <param name="conversationId">会话编号</param>
    /// <param name="request">发送消息请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    [HttpPost("conversations/{conversationId:long}/messages")]
    public async Task StreamSendAsync([FromRoute] Int64 conversationId, [FromBody] SendMessageRequest request, CancellationToken cancellationToken)
    {
        // 速率限制检查：在设置 SSE 头之前返回 429，前端可直接解析 JSON
        var userId = GetCurrentUserId();
        if (!rateLimiter.IsAllowed(userId, ChatSetting.Current.MaxMessagesPerMinute))
        {
            Response.StatusCode = 429;
            Response.ContentType = "application/json";
            await Response.WriteAsync("{\"code\":\"RATE_LIMITED\",\"message\":\"请求过于频繁，请稍后再试\"}", cancellationToken).ConfigureAwait(false);
            return;
        }

        using var span = tracer?.NewSpan("chat:StreamSend", new { request.ModelId, request.ThinkingMode });
        span?.AppendTag(request.Content);
        SetSseHeaders();
        await StreamEventsAsync(chatService.StreamMessageAsync(conversationId, request, userId, cancellationToken), cancellationToken, "MODEL_UNAVAILABLE", ex => span?.SetError(ex)).ConfigureAwait(false);
    }

    /// <summary>编辑消息内容</summary>
    /// <param name="id">消息编号</param>
    /// <param name="request">编辑请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    [HttpPut("messages/{id:long}")]
    public async Task<ActionResult<MessageDto>> EditAsync([FromRoute] Int64 id, [FromBody] EditMessageRequest request, CancellationToken cancellationToken)
    {
        var result = await chatService.EditMessageAsync(id, request, cancellationToken).ConfigureAwait(false);
        if (result == null) return NotFound();
        return Ok(result);
    }

    /// <summary>编辑用户消息并重新发送，以 SSE 事件流返回。删除后续所有消息，流式生成新 AI 回复</summary>
    /// <param name="id">用户消息编号</param>
    /// <param name="request">编辑请求（包含新内容）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    [HttpPost("messages/{id:long}/edit-and-resend")]
    public async Task EditAndResendStreamAsync([FromRoute] Int64 id, [FromBody] EditMessageRequest request, CancellationToken cancellationToken)
    {
        using var span = tracer?.NewSpan("chat:ResendStream", new { id, request.Content });
        SetSseHeaders();
        await StreamEventsAsync(chatService.EditAndResendStreamAsync(id, request.Content, GetCurrentUserId(), cancellationToken), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>重新生成回复（非流式）</summary>
    /// <param name="id">消息编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    [HttpPost("messages/{id:long}/regenerate")]
    public async Task<ActionResult<MessageDto>> RegenerateAsync([FromRoute] Int64 id, CancellationToken cancellationToken)
    {
        var result = await chatService.RegenerateMessageAsync(id, GetCurrentUserId(), cancellationToken).ConfigureAwait(false);
        if (result == null) return NotFound();
        return Ok(result);
    }

    /// <summary>流式重新生成回复，以 SSE 事件流返回</summary>
    /// <param name="id">消息编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    [HttpPost("messages/{id:long}/regenerate/stream")]
    public async Task StreamRegenerateAsync([FromRoute] Int64 id, CancellationToken cancellationToken)
    {
        using var span = tracer?.NewSpan("chat:StreamRegenerate", id);
        SetSseHeaders();
        await StreamEventsAsync(chatService.RegenerateStreamAsync(id, GetCurrentUserId(), cancellationToken), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>停止生成</summary>
    /// <param name="id">消息编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    [HttpPost("messages/{id:long}/stop")]
    public async Task<IActionResult> StopAsync([FromRoute] Int64 id, CancellationToken cancellationToken)
    {
        await chatService.StopGenerateAsync(id, cancellationToken).ConfigureAwait(false);
        return Accepted();
    }

    /// <summary>删除单条消息</summary>
    /// <param name="id">消息编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    [HttpDelete("messages/{id:long}")]
    public async Task<IActionResult> DeleteAsync([FromRoute] Int64 id, CancellationToken cancellationToken)
    {
        var ok = await chatService.DeleteMessageAsync(id, GetCurrentUserId(), cancellationToken).ConfigureAwait(false);
        if (!ok) return NotFound();
        return NoContent();
    }

    #region 辅助
    /// <summary>设置 SSE 响应头</summary>
    private void SetSseHeaders()
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
    private async Task StreamEventsAsync(IAsyncEnumerable<ChatStreamEvent> events, CancellationToken cancellationToken, String errorCode = "STREAM_ERROR", Action<Exception>? onError = null)
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
    private async Task WriteSseEventAsync(ChatStreamEvent ev, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(ev, _jsonOptions);
        await Response.WriteAsync($"data: {json}\n\n", cancellationToken).ConfigureAwait(false);
        await Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
    #endregion
}
