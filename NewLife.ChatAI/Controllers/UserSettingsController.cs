using Microsoft.AspNetCore.Mvc;
using NewLife.AI.ChatAI.Contracts;

namespace NewLife.ChatAI.Controllers;

/// <summary>用户设置控制器</summary>
[ApiController]
[Route("api/user")]
public class UserSettingsController(IChatApplicationService chatService) : ControllerBase
{
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
