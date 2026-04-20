using Microsoft.AspNetCore.Mvc;
using NewLife.ChatAI.Models;
using NewLife.ChatAI.Services;
using NewLife.Log;

namespace NewLife.ChatAI.Controllers;

/// <summary>消息控制器</summary>
[Route("api")]
public class MessagesController(ChatApplicationService chatService, MessageService messageService, MessageRateLimiter rateLimiter, ITracer tracer, ChatSetting chatSetting) : ChatApiControllerBase
{
    /// <summary>全文搜索消息内容。在当前用户的所有会话中按关键词检索</summary>
    /// <param name="keyword">搜索关键词</param>
    /// <param name="page">页码</param>
    /// <param name="pageSize">每页数量</param>
    /// <returns></returns>
    [HttpGet("messages/search")]
    public ActionResult<PagedResultDto<MessageSearchResultDto>> SearchAsync([FromQuery] String keyword, [FromQuery] Int32 page = 1, [FromQuery] Int32 pageSize = 20)
    {
        if (keyword.IsNullOrWhiteSpace())
            return Ok(new PagedResultDto<MessageSearchResultDto>([], 0, page, pageSize));
        var result = chatService.SearchMessages(GetCurrentUserId(), keyword, page, pageSize);
        return Ok(result);
    }

    /// <summary>获取会话消息列表</summary>
    /// <param name="conversationId">会话编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    [HttpGet("conversations/{conversationId:long}/messages")]
    public async Task<ActionResult<IReadOnlyList<MessageDto>>> QueryAsync([FromRoute] Int64 conversationId, CancellationToken cancellationToken)
    {
        var result = await chatService.GetMessagesAsync(conversationId, GetCurrentUserId(), cancellationToken).ConfigureAwait(false);
        if (result == null) return NotFound();
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
        if (!rateLimiter.IsAllowed(userId, chatSetting.MaxMessagesPerMinute))
        {
            Response.StatusCode = 429;
            Response.ContentType = "application/json";
            await Response.WriteAsync("{\"code\":\"RATE_LIMITED\",\"message\":\"请求过于频繁，请稍后再试\"}", cancellationToken).ConfigureAwait(false);
            return;
        }

        // 会话归属校验：必须在 SetSseHeaders() 之前，否则无法返回正确状态码
        if (!chatService.CanAccessConversation(conversationId, userId))
        {
            Response.StatusCode = 404;
            return;
        }

        using var span = tracer?.NewSpan("ai:StreamSend", new { request.ModelId, request.SkillCode, request.ThinkingMode });
        span?.AppendTag(request.Content);
        SetSseHeaders();
        await StreamEventsAsync(messageService.StreamMessageAsync(conversationId, request, userId, cancellationToken), cancellationToken, "MODEL_UNAVAILABLE", ex => span?.SetError(ex)).ConfigureAwait(false);
    }

    /// <summary>编辑消息内容</summary>
    /// <param name="id">消息编号</param>
    /// <param name="request">编辑请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    [HttpPut("messages/{id:long}")]
    public async Task<ActionResult<MessageDto>> EditAsync([FromRoute] Int64 id, [FromBody] EditMessageRequest request, CancellationToken cancellationToken)
    {
        var result = await chatService.EditMessageAsync(id, request, GetCurrentUserId(), cancellationToken).ConfigureAwait(false);
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
        var userId = GetCurrentUserId();
        // 消息归属校验：必须在 SetSseHeaders() 之前，否则无法返回正确状态码
        if (!chatService.CanAccessMessage(id, userId))
        {
            Response.StatusCode = 404;
            return;
        }

        using var span = tracer?.NewSpan("ai:ResendStream", new { id, request.Content });
        SetSseHeaders();
        await StreamEventsAsync(messageService.EditAndResendStreamAsync(id, request.Content, userId, cancellationToken), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>重新生成回复（非流式）</summary>
    /// <param name="id">消息编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    [HttpPost("messages/{id:long}/regenerate")]
    public async Task<ActionResult<MessageDto>> RegenerateAsync([FromRoute] Int64 id, CancellationToken cancellationToken)
    {
        using var span = tracer?.NewSpan("ai:Regenerate", id);
        var result = await messageService.RegenerateMessageAsync(id, GetCurrentUserId(), cancellationToken).ConfigureAwait(false);
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
        var userId = GetCurrentUserId();
        // 消息归属校验：必须在 SetSseHeaders() 之前，否则无法返回正确状态码
        if (!chatService.CanAccessMessage(id, userId))
        {
            Response.StatusCode = 404;
            return;
        }

        using var span = tracer?.NewSpan("ai:StreamRegenerate", id);
        SetSseHeaders();
        await StreamEventsAsync(messageService.RegenerateStreamAsync(id, userId, cancellationToken), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>停止生成</summary>
    /// <param name="id">消息编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    [HttpPost("messages/{id:long}/stop")]
    public async Task<IActionResult> StopAsync([FromRoute] Int64 id, CancellationToken cancellationToken)
    {
        if (!chatService.CanAccessMessage(id, GetCurrentUserId())) return NotFound();
        await messageService.StopGenerateAsync(id, cancellationToken).ConfigureAwait(false);
        return Accepted();
    }

}
