using Microsoft.AspNetCore.Mvc;
using NewLife.Data;
using NewLife.ChatAI.Models;
using NewLife.ChatAI.Services;
using XCode.Membership;

namespace NewLife.ChatAI.Controllers;

/// <summary>会话控制器</summary>
[Route("api/conversations")]
public class ConversationsController(ChatApplicationService chatService) : ChatApiControllerBase
{
    /// <summary>创建新会话</summary>
    [HttpPost]
    public async Task<ActionResult<ConversationSummaryDto>> CreateAsync([FromBody] CreateConversationRequest request, CancellationToken cancellationToken)
    {
        var user = ManageProvider.User;
        var result = await chatService.CreateConversationAsync(request, user!, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>分页查询当前用户会话列表</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResultDto<ConversationSummaryDto>>> QueryAsync([FromQuery] String? keyword = null, [FromQuery] PageParameter? page = null, CancellationToken cancellationToken = default)
    {
        if (keyword?.Length > 200) return BadRequest("keyword 过长");
        var result = await chatService.GetConversationsAsync(GetCurrentUserId(), keyword, page, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>更新会话信息</summary>
    [HttpPut("{id:long}")]
    public async Task<ActionResult<ConversationSummaryDto>> UpdateAsync([FromRoute] Int64 id, [FromBody] UpdateConversationRequest request, CancellationToken cancellationToken)
    {
        var result = await chatService.UpdateConversationAsync(id, request, GetCurrentUserId(), cancellationToken).ConfigureAwait(false);
        if (result == null) return NotFound();
        return Ok(result);
    }

    /// <summary>删除会话</summary>
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> DeleteAsync([FromRoute] Int64 id, CancellationToken cancellationToken)
    {
        var result = await chatService.DeleteConversationAsync(id, GetCurrentUserId(), cancellationToken).ConfigureAwait(false);
        if (!result) return NotFound();
        return NoContent();
    }

    /// <summary>设置会话置顶状态</summary>
    [HttpPatch("{id:long}/pin")]
    public async Task<IActionResult> SetPinAsync([FromRoute] Int64 id, [FromQuery] Boolean isPinned, CancellationToken cancellationToken)
    {
        var result = await chatService.SetPinAsync(id, isPinned, GetCurrentUserId(), cancellationToken).ConfigureAwait(false);
        if (!result) return NotFound();
        return NoContent();
    }
}
