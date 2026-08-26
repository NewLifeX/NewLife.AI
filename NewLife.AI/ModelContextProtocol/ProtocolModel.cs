using System.Runtime.Serialization;

namespace NewLife.AI.ModelContextProtocol;

/// <summary>JSON-RPC请求消息</summary>
/// <remarks>JsonRpc 属性通过 <see cref="DataMemberAttribute"/> 映射为规范字段名 jsonrpc（全小写），保证线上格式与 MCP/JSON-RPC 2.0 规范一致</remarks>
public record JsonRpcRequest([property: DataMember(Name = "jsonrpc")] String JsonRpc, String Method, Object? Params, Int32? Id)
{
    /// <summary>实例化（供反序列化，NewLife JsonReader 需参数less构造器）</summary>
    public JsonRpcRequest() : this("2.0", "", null, null) { }
}

/// <summary>JSON-RPC响应消息</summary>
/// <remarks>JsonRpc 属性通过 <see cref="DataMemberAttribute"/> 映射为规范字段名 jsonrpc（全小写），保证线上格式与 MCP/JSON-RPC 2.0 规范一致</remarks>
public record JsonRpcResponse([property: DataMember(Name = "jsonrpc")] String JsonRpc, Object? Result, Object? Error, Int32? Id)
{
    /// <summary>实例化（供反序列化）</summary>
    public JsonRpcResponse() : this("2.0", null, null, null) { }
}

/// <summary>JSON-RPC错误信息</summary>
public record JsonRpcError(Int32 Code, String Message)
{
    /// <summary>实例化（供反序列化）</summary>
    public JsonRpcError() : this(0, "") { }
}

/// <summary>初始化参数</summary>
public record InitializeParams(String ProtocolVersion, ClientInfo ClientInfo)
{
    /// <summary>实例化（供反序列化）</summary>
    public InitializeParams() : this("", null!) { }
}

/// <summary>客户端信息</summary>
public record ClientInfo(String Name, String Version)
{
    /// <summary>实例化（供反序列化）</summary>
    public ClientInfo() : this("", "") { }
}

/// <summary>初始化结果</summary>
public record InitializeResult(String ProtocolVersion, ServerCapabilities Capabilities, ClientInfo ServerInfo)
{
    /// <summary>实例化（供反序列化）</summary>
    public InitializeResult() : this("", null!, null!) { }
}

/// <summary>服务器能力。tools/resources/prompts 能力声明（对齐 MCP 规范）</summary>
public record ServerCapabilities(Object? Tools = null, Object? Resources = null, Object? Prompts = null)
{
    /// <summary>实例化（供反序列化）</summary>
    public ServerCapabilities() : this(null, null, null) { }
}

/// <summary>工具调用参数</summary>
public record ToolCallParams(String Name, Dictionary<String, Object?> Arguments, ToolCallMeta? Meta)
{
    /// <summary>实例化（供反序列化）</summary>
    public ToolCallParams() : this("", null!, null) { }
}

/// <summary>工具调用元数据</summary>
public record ToolCallMeta(String ProgressToken)
{
    /// <summary>实例化（供反序列化）</summary>
    public ToolCallMeta() : this("") { }
}

/// <summary>工具调用结果</summary>
public record ToolCallResult(IList<ContentItem> Content, Boolean IsError = false)
{
    /// <summary>实例化（供反序列化）</summary>
    public ToolCallResult() : this(null!, false) { }
}

/// <summary>内容项</summary>
public record ContentItem(String Type, String Text)
{
    /// <summary>实例化（供反序列化）</summary>
    public ContentItem() : this("", "") { }
}

/// <summary>工具列表结果</summary>
public record ToolListResult(IList<ToolDefinition> Tools)
{
    /// <summary>实例化（供反序列化）</summary>
    public ToolListResult() : this((IList<ToolDefinition>)null!) { }
}

/// <summary>工具定义</summary>
public record ToolDefinition(String Name, String? Description, Object InputSchema)
{
    /// <summary>实例化（供反序列化）</summary>
    public ToolDefinition() : this("", null, null!) { }
}

/// <summary>资源定义。MCP resources/list 返回项</summary>
public record ResourceDefinition(String Uri, String Name, String? Description = null, String? MimeType = null)
{
    /// <summary>实例化（供反序列化）</summary>
    public ResourceDefinition() : this("", "") { }
}

/// <summary>资源列表结果。MCP resources/list 响应</summary>
public record ResourceListResult(IList<ResourceDefinition> Resources)
{
    /// <summary>实例化（供反序列化）</summary>
    public ResourceListResult() : this((IList<ResourceDefinition>)null!) { }
}

/// <summary>资源读取参数。MCP resources/read 请求</summary>
public record ResourceReadParams(String Uri)
{
    /// <summary>实例化（供反序列化）</summary>
    public ResourceReadParams() : this("") { }
}

/// <summary>资源内容项。MCP resources/read 的 text 内容（type=resource）</summary>
public record ResourceContentItem(String Uri, String Text, String? MimeType = null)
{
    /// <summary>实例化（供反序列化）</summary>
    public ResourceContentItem() : this("", "") { }
}

/// <summary>资源读取结果。MCP resources/read 响应</summary>
public record ReadResourceResult(IList<ResourceContentItem> Contents)
{
    /// <summary>实例化（供反序列化）</summary>
    public ReadResourceResult() : this((IList<ResourceContentItem>)null!) { }
}

/// <summary>提示词参数定义。MCP prompts/list 返回项</summary>
public record PromptArgument(String Name, String? Description = null, Boolean Required = false)
{
    /// <summary>实例化（供反序列化）</summary>
    public PromptArgument() : this("") { }
}

/// <summary>提示词定义。MCP prompts/list 返回项</summary>
public record PromptDefinition(String Name, String? Description = null, IList<PromptArgument>? Arguments = null)
{
    /// <summary>实例化（供反序列化）</summary>
    public PromptDefinition() : this("") { }
}

/// <summary>提示词列表结果。MCP prompts/list 响应</summary>
public record PromptListResult(IList<PromptDefinition> Prompts)
{
    /// <summary>实例化（供反序列化）</summary>
    public PromptListResult() : this((IList<PromptDefinition>)null!) { }
}

/// <summary>提示词获取参数。MCP prompts/get 请求</summary>
public record PromptGetParams(String Name, Dictionary<String, Object?>? Arguments = null)
{
    /// <summary>实例化（供反序列化）</summary>
    public PromptGetParams() : this("") { }
}

/// <summary>提示词消息。role=user/assistant，content 为文本内容</summary>
public record PromptMessage(String Role, ContentItem Content)
{
    /// <summary>实例化（供反序列化）</summary>
    public PromptMessage() : this("", null!) { }
}

/// <summary>提示词获取结果。MCP prompts/get 响应</summary>
public record GetPromptResult(String? Description, IList<PromptMessage> Messages)
{
    /// <summary>实例化（供反序列化）</summary>
    public GetPromptResult() : this(null, null!) { }
}

/// <summary>进度通知。MCP 服务端→客户端 notifications/progress</summary>
public record ProgressNotification(String ProgressToken, Int32 Progress, Int32? Total = null)
{
    /// <summary>实例化（供反序列化）</summary>
    public ProgressNotification() : this("", 0) { }
}
