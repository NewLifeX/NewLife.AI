using Microsoft.AspNetCore.Mvc;
using NewLife.ChatAI.Entity;
using XCode;

namespace NewLife.ChatAI.Controllers;

/// <summary>MCP 服务配置控制器</summary>
[ApiController]
[Route("api/mcp/servers")]
public class McpController : ControllerBase
{
    /// <summary>获取 MCP 服务列表</summary>
    /// <returns></returns>
    [HttpGet]
    public ActionResult<IReadOnlyList<McpServerConfigModel>> QueryAsync()
    {
        var list = McpServerConfig.FindAll(null, McpServerConfig._.Sort.Asc(), null, 0, 0);
        var items = list.Select(e => e.ToModel()).ToList();
        return Ok(items);
    }

    /// <summary>获取单个 MCP 服务配置</summary>
    /// <param name="id">编号</param>
    /// <returns></returns>
    [HttpGet("{id:int}")]
    public ActionResult<McpServerConfigModel> GetAsync([FromRoute] Int32 id)
    {
        var entity = McpServerConfig.FindById(id);
        if (entity == null) return NotFound();
        return Ok(entity.ToModel());
    }

    /// <summary>新增 MCP 服务配置</summary>
    /// <param name="model">配置信息</param>
    /// <returns></returns>
    [HttpPost]
    public ActionResult<McpServerConfigModel> CreateAsync([FromBody] McpServerConfigModel model)
    {
        var entity = new McpServerConfig();
        entity.Copy(model);
        entity.Insert();
        return Ok(entity.ToModel());
    }

    /// <summary>更新 MCP 服务配置</summary>
    /// <param name="id">编号</param>
    /// <param name="model">配置信息</param>
    /// <returns></returns>
    [HttpPut("{id:int}")]
    public ActionResult<McpServerConfigModel> UpdateAsync([FromRoute] Int32 id, [FromBody] McpServerConfigModel model)
    {
        var entity = McpServerConfig.FindById(id);
        if (entity == null) return NotFound();

        entity.Copy(model);
        entity.Id = id; // Copy 会覆盖 Id，需要恢复
        entity.Update();
        return Ok(entity.ToModel());
    }

    /// <summary>删除 MCP 服务配置</summary>
    /// <param name="id">编号</param>
    /// <returns></returns>
    [HttpDelete("{id:int}")]
    public IActionResult DeleteAsync([FromRoute] Int32 id)
    {
        var entity = McpServerConfig.FindById(id);
        if (entity == null) return NotFound();

        entity.Delete();
        return NoContent();
    }

    /// <summary>切换 MCP 服务启用状态</summary>
    /// <param name="id">编号</param>
    /// <param name="enabled">是否启用</param>
    /// <returns></returns>
    [HttpPatch("{id:int}/enable")]
    public IActionResult SetEnableAsync([FromRoute] Int32 id, [FromQuery] Boolean enabled)
    {
        var entity = McpServerConfig.FindById(id);
        if (entity == null) return NotFound();

        entity.Enable = enabled;
        entity.Update();
        return NoContent();
    }
}
