using Microsoft.AspNetCore.Mvc;
using NewLife.AI.ChatAI.Contracts;

namespace NewLife.ChatAI.Controllers;

/// <summary>模型控制器</summary>
[ApiController]
[Route("api/models")]
public class ModelsController(IChatApplicationService chatService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ModelInfoDto[]>> QueryAsync(CancellationToken cancellationToken)
    {
        var result = await chatService.GetModelsAsync(cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }
}
