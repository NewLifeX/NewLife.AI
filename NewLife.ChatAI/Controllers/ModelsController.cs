using Microsoft.AspNetCore.Mvc;
using NewLife.AI.ChatAI.Contracts;
using NewLife.ChatAI.Services;
using NewLife.Cube;

namespace NewLife.ChatAI.Controllers;

/// <summary>模型控制器</summary>
[ApiController]
[Route("api/models")]
public class ModelsController(ChatApplicationService chatService) : ControllerBase
{
    /// <summary>查询可用模型列表</summary>
    [HttpGet]
    public async Task<ActionResult<ModelInfoDto[]>> QueryAsync(CancellationToken cancellationToken)
    {
        // 本期暂不启用登录鉴权，未登录时使用默认角色和部门
        var user = ManageProvider2.User;
        var roleIds = user?.Roles?.Select(e => e.ID).ToArray() ?? [];
        var departmentId = user?.DepartmentID ?? 0;

        var result = await chatService.GetModelsAsync(roleIds, departmentId, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }
}