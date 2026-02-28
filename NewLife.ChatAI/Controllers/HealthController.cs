using Microsoft.AspNetCore.Mvc;

namespace NewLife.ChatAI.Controllers;

/// <summary>健康检查</summary>
[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { name = "NewLife.ChatAI", status = "ok", time = DateTime.Now });
}
