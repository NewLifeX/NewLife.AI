using Microsoft.AspNetCore.Mvc;
using NewLife.ChatAI.Models;
using NewLife.ChatAI.Services;

namespace NewLife.ChatAI.Controllers;

/// <summary>反馈控制器</summary>
[Route("api/messages")]
public class FeedbackController(ChatApplicationService chatService) : ChatApiControllerBase
{
    /// <summary>提交点赞/点踩反馈</summary>
    /// <param name="id">消息编号</param>
    /// <param name="request">反馈请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    [HttpPost("{id:long}/feedback")]
    public async Task<IActionResult> SubmitAsync([FromRoute] Int64 id, [FromBody] FeedbackRequest request, CancellationToken cancellationToken)
    {
        await chatService.SubmitFeedbackAsync(id, request, GetCurrentUserId(), cancellationToken).ConfigureAwait(false);
        return Accepted();
    }

    /// <summary>取消反馈</summary>
    /// <param name="id">消息编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    [HttpDelete("{id:long}/feedback")]
    public async Task<IActionResult> DeleteAsync([FromRoute] Int64 id, CancellationToken cancellationToken)
    {
        await chatService.DeleteFeedbackAsync(id, GetCurrentUserId(), cancellationToken).ConfigureAwait(false);
        return NoContent();
    }
}
