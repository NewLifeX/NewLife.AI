using Microsoft.AspNetCore.Mvc;
using NewLife.ChatAI.Entity;
using NewLife.ChatAI.Services;
using NewLife.Serialization;
using NewLife.AI.Models;

namespace NewLife.ChatAI.Controllers;

/// <summary>MCP 管理控制器。MCP Server 的配置管理和工具发现</summary>
[Route("api/mcp")]
public class McpApiController(McpClientService mcpClientService) : ChatApiControllerBase
{
    #region MCP Server 管理
    /// <summary>获取已配置的 MCP Server 列表</summary>
    /// <returns></returns>
    [HttpGet("servers")]
    public ActionResult<IList<McpServerResponseDto>> GetServers()
    {
        var list = McpServerConfig.FindAll();
        var items = list.OrderByDescending(e => e.Sort).ThenByDescending(e => e.Id).Select(e => ToDto(e)).ToList();
        return Ok(items);
    }

    /// <summary>添加 MCP Server</summary>
    /// <param name="request">创建请求</param>
    /// <returns></returns>
    [HttpPost("servers")]
    public ActionResult<McpServerResponseDto> AddServer([FromBody] CreateMcpServerRequest request)
    {
        if (String.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { code = "INVALID_REQUEST", message = "名称不能为空" });
        if (String.IsNullOrWhiteSpace(request.Endpoint))
            return BadRequest(new { code = "INVALID_REQUEST", message = "接口地址不能为空" });

        // 检查名称唯一性
        var existing = McpServerConfig.FindByName(request.Name.Trim());
        if (existing != null)
            return BadRequest(new { code = "DUPLICATE_NAME", message = $"名称 '{request.Name}' 已存在" });

        var entity = new McpServerConfig
        {
            Name = request.Name.Trim(),
            Endpoint = request.Endpoint.Trim(),
            TransportType = request.TransportType ?? McpTransportType.Http,
            AuthType = request.AuthType ?? "None",
            AuthToken = request.AuthToken ?? String.Empty,
            Enable = true,
            Sort = request.Sort,
            Remark = request.Remark ?? String.Empty,
        };

        entity.Insert();

        return Ok(ToDto(entity));
    }

    /// <summary>更新 MCP Server 配置</summary>
    /// <param name="id">配置编号</param>
    /// <param name="request">更新请求</param>
    /// <returns></returns>
    [HttpPut("servers/{id:int}")]
    public ActionResult<McpServerResponseDto> UpdateServer([FromRoute] Int32 id, [FromBody] UpdateMcpServerRequest request)
    {
        var entity = McpServerConfig.FindById(id);
        if (entity == null) return NotFound();

        if (!String.IsNullOrWhiteSpace(request.Name))
            entity.Name = request.Name.Trim();
        if (!String.IsNullOrWhiteSpace(request.Endpoint))
            entity.Endpoint = request.Endpoint.Trim();
        if (request.TransportType.HasValue)
            entity.TransportType = request.TransportType.Value;
        if (!String.IsNullOrWhiteSpace(request.AuthType))
            entity.AuthType = request.AuthType;
        if (request.AuthToken != null)
            entity.AuthToken = request.AuthToken;
        if (request.Enable != null)
            entity.Enable = request.Enable.Value;
        if (request.Sort != null)
            entity.Sort = request.Sort.Value;
        if (request.Remark != null)
            entity.Remark = request.Remark;

        entity.Update();

        return Ok(ToDto(entity));
    }

    /// <summary>删除 MCP Server</summary>
    /// <param name="id">配置编号</param>
    /// <returns></returns>
    [HttpDelete("servers/{id:int}")]
    public IActionResult DeleteServer([FromRoute] Int32 id)
    {
        var entity = McpServerConfig.FindById(id);
        if (entity == null) return NotFound();

        entity.Delete();
        return NoContent();
    }
    #endregion

    #region 工具发现与调用
    /// <summary>手动触发工具发现。连接指定 MCP Server 获取可用工具列表</summary>
    /// <param name="id">配置编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    [HttpPost("servers/{id:int}/discover")]
    public async Task<IActionResult> DiscoverTools([FromRoute] Int32 id, CancellationToken cancellationToken)
    {
        try
        {
            var tools = await mcpClientService.DiscoverToolsAsync(id, cancellationToken).ConfigureAwait(false);
            return Ok(new { count = tools.Count, tools });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { code = "MCP_SERVER_NOT_FOUND", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(503, new { code = "MCP_SERVER_UNAVAILABLE", message = ex.Message });
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(503, new { code = "MCP_SERVER_UNAVAILABLE", message = $"无法连接 MCP Server: {ex.Message}" });
        }
    }

    /// <summary>获取所有已发现的 MCP 工具列表</summary>
    /// <returns></returns>
    [HttpGet("tools")]
    public ActionResult<IList<McpToolInfo>> GetTools()
    {
        var tools = mcpClientService.GetAllTools();
        return Ok(tools);
    }
    #endregion

    #region 辅助
    private static McpServerResponseDto ToDto(McpServerConfig entity)
    {
        var toolCount = 0;
        if (!entity.AvailableTools.IsNullOrEmpty())
        {
            try
            {
                var tools = entity.AvailableTools.ToJsonEntity<Object[]>();
                toolCount = tools?.Length ?? 0;
            }
            catch { }
        }

        return new McpServerResponseDto(
            entity.Id,
            entity.Name,
            entity.Endpoint,
            entity.TransportType,
            entity.AuthType,
            entity.Enable,
            entity.Sort,
            toolCount,
            entity.Remark,
            entity.CreateTime,
            entity.UpdateTime);
    }
    #endregion
}

#region DTO 定义
/// <summary>创建 MCP Server 请求</summary>
public record CreateMcpServerRequest(
    String Name,
    String Endpoint,
    McpTransportType? TransportType,
    String? AuthType,
    String? AuthToken,
    Int32 Sort = 0,
    String? Remark = null);

/// <summary>更新 MCP Server 请求</summary>
public record UpdateMcpServerRequest(
    String? Name,
    String? Endpoint,
    McpTransportType? TransportType,
    String? AuthType,
    String? AuthToken,
    Boolean? Enable,
    Int32? Sort,
    String? Remark);

/// <summary>MCP Server 响应</summary>
public record McpServerResponseDto(
    Int32 Id,
    String Name,
    String Endpoint,
    McpTransportType TransportType,
    String AuthType,
    Boolean Enable,
    Int32 Sort,
    Int32 ToolCount,
    String? Remark,
    DateTime CreateTime,
    DateTime UpdateTime);
#endregion
