using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using NewLife.AI.ChatAI.Contracts;
using NewLife.AI.Models;
using NewLife.Serialization;

namespace NewLife.ChatAI.Controllers;

/// <summary>消息控制器</summary>
[ApiController]
[Route("api")]
public class MessagesController(IChatApplicationService chatService) : ControllerBase
{
    /// <summary>发送消息并以 SSE 流式返回。支持 message_start/thinking_delta/thinking_done/content_delta/tool_call_start/tool_call_done/tool_call_error/message_done/error 事件</summary>
    /// <param name="conversationId">会话编号</param>
    /// <param name="request">发送消息请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    [HttpPost("conversations/{conversationId:long}/messages")]
    public async Task StreamSendAsync([FromRoute] Int64 conversationId, [FromBody] SendMessageRequest request, CancellationToken cancellationToken)
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");

        try
        {
            await foreach (var chunk in chatService.StreamMessageAsync(conversationId, request, cancellationToken).ConfigureAwait(false))
            {
                // 服务层返回的是原始文本块，封装为 SSE content_delta 事件
                var ev = ChatStreamEvent.ContentDelta(chunk);
                await WriteSseEventAsync(ev, cancellationToken).ConfigureAwait(false);
            }

            // 流式输出完成
            var done = ChatStreamEvent.MessageDone();
            await WriteSseEventAsync(done, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 用户取消，不需要额外处理
        }
        catch (Exception ex)
        {
            var error = ChatStreamEvent.ErrorEvent("MODEL_UNAVAILABLE", ex.Message);
            await WriteSseEventAsync(error, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>获取会话消息列表</summary>
    /// <param name="conversationId">会话编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    [HttpGet("conversations/{conversationId:long}/messages")]
    public async Task<ActionResult<IReadOnlyList<MessageDto>>> QueryAsync([FromRoute] Int64 conversationId, CancellationToken cancellationToken)
    {
        var result = await chatService.GetMessagesAsync(conversationId, cancellationToken).ConfigureAwait(false);
        return Ok(result);
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

    /// <summary>重新生成回复</summary>
    /// <param name="id">消息编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    [HttpPost("messages/{id:long}/regenerate")]
    public async Task<ActionResult<MessageDto>> RegenerateAsync([FromRoute] Int64 id, CancellationToken cancellationToken)
    {
        var result = await chatService.RegenerateMessageAsync(id, cancellationToken).ConfigureAwait(false);
        if (result == null) return NotFound();
        return Ok(result);
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

    #region 辅助
    /// <summary>写入 SSE 事件</summary>
    /// <param name="ev">事件对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    private async Task WriteSseEventAsync(ChatStreamEvent ev, CancellationToken cancellationToken)
    {
        var json = ev.ToJson();
        await Response.WriteAsync($"data: {json}\n\n", cancellationToken).ConfigureAwait(false);
        await Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
    #endregion
}
