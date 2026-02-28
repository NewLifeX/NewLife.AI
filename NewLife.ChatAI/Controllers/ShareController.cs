using Microsoft.AspNetCore.Mvc;
using NewLife.AI.ChatAI.Contracts;

namespace NewLife.ChatAI.Controllers;

/// <summary>分享控制器</summary>
[ApiController]
[Route("api")]
public class ShareController(IChatApplicationService chatService) : ControllerBase
{
    [HttpPost("conversations/{conversationId:long}/share")]
    public async Task<ActionResult<ShareLinkDto>> CreateAsync([FromRoute] Int64 conversationId, [FromBody] CreateShareRequest request, CancellationToken cancellationToken)
    {
        var result = await chatService.CreateShareLinkAsync(conversationId, request, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    [HttpGet("share/{token}")]
    public async Task<IActionResult> GetAsync([FromRoute] String token, CancellationToken cancellationToken)
    {
        var result = await chatService.GetShareContentAsync(token, cancellationToken).ConfigureAwait(false);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpDelete("share/{token}")]
    public async Task<IActionResult> DeleteAsync([FromRoute] String token, CancellationToken cancellationToken)
    {
        var result = await chatService.RevokeShareLinkAsync(token, cancellationToken).ConfigureAwait(false);
        if (!result) return NotFound();
        return NoContent();
    }
}
