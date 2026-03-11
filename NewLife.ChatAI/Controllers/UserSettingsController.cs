using Microsoft.AspNetCore.Mvc;
using NewLife.AI.ChatAI.Contracts;
using NewLife.ChatAI.Services;
using NewLife.Cube;

namespace NewLife.ChatAI.Controllers;

/// <summary>用户设置控制器</summary>
[ApiController]
[Route("api/user")]
public class UserSettingsController(ChatApplicationService chatService) : ControllerBase
{
    /// <summary>获取当前用户资料</summary>
    [HttpGet("profile")]
    public ActionResult<UserProfileDto> GetProfile()
    {
        // 本期暂不启用登录鉴权，未登录时返回默认资料
        var user = ManageProvider2.User;
        var nickname = user?.DisplayName ?? "用户";
        var account = user?.Name ?? "anonymous";

        return Ok(new UserProfileDto(nickname, account, null));
    }

    /// <summary>获取用户设置</summary>
    [HttpGet("settings")]
    public async Task<ActionResult<UserSettingsDto>> GetSettingsAsync(CancellationToken cancellationToken)
    {
        var result = await chatService.GetUserSettingsAsync(cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>保存用户设置</summary>
    [HttpPut("settings")]
    public async Task<ActionResult<UserSettingsDto>> SaveSettingsAsync([FromBody] UserSettingsDto request, CancellationToken cancellationToken)
    {
        var result = await chatService.UpdateUserSettingsAsync(request, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>导出用户数据</summary>
    [HttpGet("data/export")]
    public async Task<IActionResult> ExportAsync(CancellationToken cancellationToken)
    {
        var stream = await chatService.ExportUserDataAsync(cancellationToken).ConfigureAwait(false);
        return File(stream, "application/json", "chat-export.json");
    }

    /// <summary>清除用户数据</summary>
    [HttpDelete("data/clear")]
    public async Task<IActionResult> ClearAsync(CancellationToken cancellationToken)
    {
        await chatService.ClearUserConversationsAsync(cancellationToken).ConfigureAwait(false);
        return NoContent();
    }
}