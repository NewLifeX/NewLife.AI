using Microsoft.AspNetCore.Mvc;
using NewLife.AI.ChatAI.Contracts;
using NewLife.ChatAI.Services;
using NewLife.Cube;

namespace NewLife.ChatAI.Controllers;

/// <summary>用户设置控制器</summary>
[ApiController]
[Route("api/user/settings")]
public class UserSettingsController(ChatApplicationService chatService) : ControllerBase
{
    [HttpGet("profile")]
    public ActionResult<UserProfileDto> GetProfile(CancellationToken cancellationToken)
    {
        var user = ManageProvider2.User;
        if (user == null) return Unauthorized();

        var result = new UserProfileDto(user.DisplayName ?? "用户", user.Name, null);
        return Ok(result);
    }

    [HttpGet("settings")]
    public async Task<ActionResult<UserSettingsDto>> GetSettingsAsync(CancellationToken cancellationToken)
    {
        var result = await chatService.GetUserSettingsAsync(cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    [HttpPut("settings")]
    public async Task<ActionResult<UserSettingsDto>> SaveSettingsAsync([FromBody] UserSettingsDto request, CancellationToken cancellationToken)
    {
        var result = await chatService.UpdateUserSettingsAsync(request, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    [HttpPost("export")]
    public async Task<IActionResult> ExportAsync(CancellationToken cancellationToken)
    {
        var stream = await chatService.ExportUserDataAsync(cancellationToken).ConfigureAwait(false);
        return File(stream, "application/json", "chat-export.json");
    }

    [HttpDelete("conversations")]
    public async Task<IActionResult> ClearAsync(CancellationToken cancellationToken)
    {
        await chatService.ClearUserConversationsAsync(cancellationToken).ConfigureAwait(false);
        return NoContent();
    }
}
