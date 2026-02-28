using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using NewLife.AI.ChatAI.Contracts;

namespace NewLife.ChatAI.Controllers;

/// <summary>消息控制器</summary>
[ApiController]
[Route("api")]
public class MessagesController(IChatApplicationService chatService) : ControllerBase
{
    [HttpPost("conversations/{conversationId:long}/messages")]
    public async Task StreamSendAsync([FromRoute] Int64 conversationId, [FromBody] SendMessageRequest request, CancellationToken cancellationToken)
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");

        await foreach (var chunk in chatService.StreamMessageAsync(conversationId, request, cancellationToken).ConfigureAwait(false))
        {
            var payload = JsonSerializer.Serialize(new { type = "delta", content = chunk });
            await Response.WriteAsync($"data: {payload}\n\n", cancellationToken).ConfigureAwait(false);
            await Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        await Response.WriteAsync("data: {\"type\":\"done\"}\n\n", cancellationToken).ConfigureAwait(false);
        await Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    [HttpGet("conversations/{conversationId:long}/messages")]
    public async Task<ActionResult<IReadOnlyList<MessageDto>>> QueryAsync([FromRoute] Int64 conversationId, CancellationToken cancellationToken)
    {
        var result = await chatService.GetMessagesAsync(conversationId, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    [HttpPut("messages/{id:long}")]
    public async Task<ActionResult<MessageDto>> EditAsync([FromRoute] Int64 id, [FromBody] EditMessageRequest request, CancellationToken cancellationToken)
    {
        var result = await chatService.EditMessageAsync(id, request, cancellationToken).ConfigureAwait(false);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPost("messages/{id:long}/regenerate")]
    public async Task<ActionResult<MessageDto>> RegenerateAsync([FromRoute] Int64 id, CancellationToken cancellationToken)
    {
        var result = await chatService.RegenerateMessageAsync(id, cancellationToken).ConfigureAwait(false);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPost("messages/{id:long}/stop")]
    public async Task<IActionResult> StopAsync([FromRoute] Int64 id, CancellationToken cancellationToken)
    {
        await chatService.StopGenerateAsync(id, cancellationToken).ConfigureAwait(false);
        return Accepted();
    }
}
