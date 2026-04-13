using Microsoft.AspNetCore.Mvc;
using NewLife.ChatAI.Models;
using NewLife.ChatAI.Services;
using XCode.Membership;

namespace NewLife.ChatAI.Controllers;

/// <summary>模型控制器</summary>
[Route("api/models")]
public class ModelsController(ChatApplicationService chatService) : ChatApiControllerBase
{
    /// <summary>查询可用模型列表</summary>
    [HttpGet]
    public async Task<ActionResult<ModelInfoDto[]>> QueryAsync(CancellationToken cancellationToken)
    {
        var user = ManageProvider.User;
        var roleIds = user?.Roles?.Select(e => e.ID).ToArray() ?? [];
        var departmentId = user?.DepartmentID ?? 0;

        var result = await chatService.GetModelsAsync(roleIds, departmentId, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }
}