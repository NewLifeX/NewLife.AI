using Microsoft.AspNetCore.Mvc;
using NewLife.AI.ChatAI.Contracts;

namespace NewLife.ChatAI.Controllers;

/// <summary>反馈控制器</summary>
[ApiController]
[Route("api/messages/{messageId:long}/feedback")]
public class FeedbackController(IChatApplicationService chatService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> SubmitAsync([FromRoute] Int64 messageId, [FromBody] FeedbackRequest request, CancellationToken cancellationToken)
    {
        await chatService.SubmitFeedbackAsync(messageId, request, cancellationToken).ConfigureAwait(false);
        return Accepted();
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteAsync([FromRoute] Int64 messageId, CancellationToken cancellationToken)
    {
        await chatService.DeleteFeedbackAsync(messageId, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }
}
