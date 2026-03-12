using Microsoft.AspNetCore.Mvc;
using NewLife.AI.ChatAI;
using NewLife.ChatAI.Services;

namespace NewLife.ChatAI.Controllers;

/// <summary>会话控制器</summary>
[Route("api/conversations")]
public class ConversationsController(ChatApplicationService chatService) : ChatApiControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ConversationSummaryDto>> CreateAsync([FromBody] CreateConversationRequest request, CancellationToken cancellationToken)
    {
        var result = await chatService.CreateConversationAsync(request, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    [HttpGet]
    public async Task<ActionResult<PagedResultDto<ConversationSummaryDto>>> QueryAsync([FromQuery] Int32 page = 1, [FromQuery] Int32 pageSize = 20, CancellationToken cancellationToken = default)
    {
        var result = await chatService.GetConversationsAsync(GetCurrentUserId(), page, pageSize, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<ConversationSummaryDto>> UpdateAsync([FromRoute] Int64 id, [FromBody] UpdateConversationRequest request, CancellationToken cancellationToken)
    {
        var result = await chatService.UpdateConversationAsync(id, request, cancellationToken).ConfigureAwait(false);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> DeleteAsync([FromRoute] Int64 id, CancellationToken cancellationToken)
    {
        var result = await chatService.DeleteConversationAsync(id, cancellationToken).ConfigureAwait(false);
        if (!result) return NotFound();
        return NoContent();
    }

    [HttpPatch("{id:long}/pin")]
    public async Task<IActionResult> SetPinAsync([FromRoute] Int64 id, [FromQuery] Boolean isPinned, CancellationToken cancellationToken)
    {
        var result = await chatService.SetPinAsync(id, isPinned, cancellationToken).ConfigureAwait(false);
        if (!result) return NotFound();
        return NoContent();
    }
}
