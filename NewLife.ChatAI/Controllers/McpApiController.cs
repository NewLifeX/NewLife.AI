using Microsoft.AspNetCore.Mvc;
using NewLife.ChatAI.Entity;
using NewLife.ChatAI.Models;

namespace NewLife.ChatAI.Controllers;

/// <summary>MCP 服务器管理控制器。获取已配置的 MCP Server 列表及启停控制</summary>
/// <remarks>
/// 当前基于 ProviderConfig（ApiProtocol="Mcp"）存储 MCP 服务器配置。
/// 后续可迁移到独立的 McpServerConfig 实体表。
/// </remarks>
[Route("api/mcp/servers")]
public class McpApiController : ChatApiControllerBase
{
    /// <summary>MCP 提供商标识。ProviderConfig 中 ApiProtocol 为此值的记录视为 MCP Server</summary>
    private const String McpProtocol = "Mcp";

    /// <summary>获取已配置的 MCP Server 列表</summary>
    [HttpGet]
    public ActionResult<IList<McpServerDto>> GetList()
    {
        var list = ProviderConfig.FindAll(ProviderConfig._.ApiProtocol == McpProtocol, ProviderConfig._.Sort, null, 0, 0);

        var result = list.Select(e => new McpServerDto
        {
            Id = e.Id,
            Name = e.Name ?? String.Empty,
            Endpoint = e.Endpoint ?? String.Empty,
            TransportType = e.Provider ?? "sse",
            AuthType = e.ApiKey.IsNullOrEmpty() ? "none" : "bearer",
            Enable = e.Enable,
            Sort = e.Sort,
            Remark = e.Remark,
        }).ToList();

        return Ok(result);
    }

    /// <summary>更新 MCP Server 配置（启停控制）</summary>
    /// <param name="id">MCP Server 编号</param>
    /// <param name="request">更新请求</param>
    [HttpPut("{id:int}")]
    public IActionResult Update([FromRoute] Int32 id, [FromBody] UpdateMcpServerRequest request)
    {
        var entity = ProviderConfig.FindById(id);
        if (entity == null || entity.ApiProtocol != McpProtocol) return NotFound();

        if (request.Enable != null) entity.Enable = request.Enable.Value;
        entity.Save();

        return NoContent();
    }
}

/// <summary>更新 MCP Server 请求</summary>
public class UpdateMcpServerRequest
{
    /// <summary>是否启用</summary>
    public Boolean? Enable { get; set; }
}
