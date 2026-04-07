using System.Runtime.Serialization;

namespace NewLife.AI.Models;

/// <summary>聊天工具定义</summary>
public class ChatTool
{
    /// <summary>类型。function / mcp / web_search / code_interpreter</summary>
    [DataMember(Name = "type")]
    public String Type { get; set; } = "function";

    /// <summary>函数定义。Type=function 时填写</summary>
    [DataMember(Name = "function")]
    public FunctionDefinition? Function { get; set; }

    /// <summary>MCP 工具配置。Type=mcp 时填写</summary>
    [DataMember(Name = "mcp")]
    public McpToolConfig? Mcp { get; set; }

    /// <summary>通用扩展配置。web_search 等内置工具类型的具体参数字典。DashScope 专用</summary>
    [DataMember(Name = "config")]
    public Object? Config { get; set; }
}

/// <summary>函数定义</summary>
public class FunctionDefinition
{
    /// <summary>名称</summary>
    [DataMember(Name = "name")]
    public String Name { get; set; } = null!;

    /// <summary>描述</summary>
    [DataMember(Name = "description")]
    public String? Description { get; set; }

    /// <summary>参数。JSON Schema 格式</summary>
    [DataMember(Name = "parameters")]
    public Object? Parameters { get; set; }
}

/// <summary>MCP（Model Context Protocol）工具配置</summary>
public class McpToolConfig
{
    /// <summary>MCP Server 地址</summary>
    [DataMember(Name = "server_url")]
    public String? ServerUrl { get; set; }

    /// <summary>MCP Server 唯一标识</summary>
    [DataMember(Name = "server_id")]
    public String? ServerId { get; set; }

    /// <summary>服务器配置参数</summary>
    public IDictionary<String, Object?>? Configs { get; set; }

    /// <summary>允许调用的工具子集。为空则允许全部工具</summary>
    [DataMember(Name = "allowed_tools")]
    public IList<String>? AllowedTools { get; set; }

    /// <summary>鉴权配置</summary>
    public McpAuthConfig? Authorization { get; set; }
}

/// <summary>MCP 工具鉴权配置</summary>
public class McpAuthConfig
{
    /// <summary>鉴权类型。如 bearer</summary>
    public String? Type { get; set; }

    /// <summary>鉴权令牌</summary>
    public String? Token { get; set; }
}
