using Microsoft.AspNetCore.Mvc;
using NewLife.AI.ChatAI;
using NewLife.ChatAI.Services;
using NewLife.Cube;

namespace NewLife.ChatAI.Controllers;

/// <summary>用户设置控制器</summary>
[Route("api/user")]
public class UserSettingsController(ChatApplicationService chatService) : ChatApiControllerBase
{
    /// <summary>获取当前用户资料</summary>
    [HttpGet("profile")]
    public ActionResult<UserProfileDto> GetProfile()
    {
        var user = ManageProvider2.User;
        var nickname = user?.DisplayName ?? user?.Name ?? "用户";
        var account = user?.Name ?? "";
        var avatar = user?.GetAvatarUrl();

        return Ok(new UserProfileDto(nickname, account, avatar));
    }

    /// <summary>获取用户设置</summary>
    [HttpGet("settings")]
    public async Task<ActionResult<UserSettingsDto>> GetSettingsAsync(CancellationToken cancellationToken)
    {
        var result = await chatService.GetUserSettingsAsync(GetCurrentUserId(), cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>保存用户设置</summary>
    [HttpPut("settings")]
    public async Task<ActionResult<UserSettingsDto>> SaveSettingsAsync([FromBody] UserSettingsDto request, CancellationToken cancellationToken)
    {
        var result = await chatService.UpdateUserSettingsAsync(request, GetCurrentUserId(), cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>导出用户数据</summary>
    [HttpGet("data/export")]
    public async Task<IActionResult> ExportAsync(CancellationToken cancellationToken)
    {
        var stream = await chatService.ExportUserDataAsync(GetCurrentUserId(), cancellationToken).ConfigureAwait(false);
        return File(stream, "application/json", "chat-export.json");
    }

    /// <summary>清除用户数据</summary>
    [HttpDelete("data/clear")]
    public async Task<IActionResult> ClearAsync(CancellationToken cancellationToken)
    {
        await chatService.ClearUserConversationsAsync(GetCurrentUserId(), cancellationToken).ConfigureAwait(false);
        return NoContent();
    }
}