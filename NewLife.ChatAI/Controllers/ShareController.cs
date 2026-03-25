using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewLife.Cube;
using NewLife.ChatAI.Models;
using NewLife.ChatAI.Services;

namespace NewLife.ChatAI.Controllers;

/// <summary>分享控制器</summary>
public class ShareController(ChatApplicationService chatService) : ChatApiControllerBase
{
    /// <summary>创建共享链接</summary>
    /// <param name="conversationId">会话编号</param>
    /// <param name="request">创建分享请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    [HttpPost("api/conversations/{conversationId:long}/share")]
    public async Task<ActionResult<ShareLinkDto>> CreateAsync([FromRoute] Int64 conversationId, [FromBody] CreateShareRequest request, CancellationToken cancellationToken)
    {
        var user = ManageProvider2.User;
        var result = await chatService.CreateShareLinkAsync(conversationId, request, user, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>获取共享对话内容（公开接口，无需登录）</summary>
    /// <param name="token">分享令牌</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    [AllowAnonymous]
    [HttpGet("api/share/{token}")]
    public async Task<IActionResult> GetAsync([FromRoute] String token, CancellationToken cancellationToken)
    {
        var result = await chatService.GetShareContentAsync(token, cancellationToken).ConfigureAwait(false);
        if (result == null) return NotFound(new { code = "SHARE_NOT_FOUND", message = "共享链接不存在或已过期" });
        return Ok(result);
    }

    /// <summary>撤销共享链接</summary>
    /// <param name="token">分享令牌</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    [HttpDelete("api/share/{token}")]
    public async Task<IActionResult> DeleteAsync([FromRoute] String token, CancellationToken cancellationToken)
    {
        var result = await chatService.RevokeShareLinkAsync(token, cancellationToken).ConfigureAwait(false);
        if (!result) return NotFound();
        return NoContent();
    }
}
