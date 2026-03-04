using Microsoft.AspNetCore.Mvc;
using NewLife.AI.ChatAI.Contracts;
using NewLife.Cube;

namespace NewLife.ChatAI.Controllers;

/// <summary>模型控制器</summary>
[ApiController]
[Route("api/models")]
public class ModelsController(IChatApplicationService chatService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ModelInfoDto[]>> QueryAsync(CancellationToken cancellationToken)
    {
        var user = ManageProvider2.User;
        if (user == null) return Unauthorized();

        var roleIds = user.Roles?.Select(e => e.ID).ToArray() ?? [];
        var departmentId = user.DepartmentID;

        var result = await chatService.GetModelsAsync(roleIds, departmentId, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }
}
