using Microsoft.AspNetCore.Mvc;
using NewLife.AI.ChatAI.Contracts;

namespace NewLife.ChatAI.Controllers;

/// <summary>数据管理控制器</summary>
[ApiController]
[Route("api/user/data")]
public class DataController(IChatApplicationService chatService) : ControllerBase
{
    /// <summary>导出用户全部对话数据（JSON格式）</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>JSON 文件流</returns>
    [HttpGet("export")]
    public async Task<IActionResult> ExportAsync(CancellationToken cancellationToken)
    {
        var stream = await chatService.ExportUserDataAsync(cancellationToken).ConfigureAwait(false);
        return File(stream, "application/json", $"newlife-chat-export-{DateTime.Now:yyyyMMddHHmmss}.json");
    }

    /// <summary>清除用户全部对话数据</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    [HttpDelete("clear")]
    public async Task<IActionResult> ClearAsync(CancellationToken cancellationToken)
    {
        await chatService.ClearUserConversationsAsync(cancellationToken).ConfigureAwait(false);
        return NoContent();
    }
}
