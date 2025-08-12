namespace NewLife.AI.ModelContextProtocol;

/// <summary>JSON-RPC请求消息</summary>
public record JsonRpcRequest(String JsonRpc, String Method, Object? Params, Int32? Id);

/// <summary>JSON-RPC响应消息</summary>
public record JsonRpcResponse(String JsonRpc, Object? Result, Object? Error, Int32? Id);

/// <summary>JSON-RPC错误信息</summary>
public record JsonRpcError(Int32 Code, String Message);

/// <summary>初始化参数</summary>
public record InitializeParams(String ProtocolVersion, ClientInfo ClientInfo);

/// <summary>客户端信息</summary>
public record ClientInfo(String Name, String Version);

/// <summary>初始化结果</summary>
public record InitializeResult(String ProtocolVersion, ServerCapabilities Capabilities, ClientInfo ServerInfo);

/// <summary>服务器能力</summary>
public record ServerCapabilities(Object Tools);

/// <summary>工具调用参数</summary>
public record ToolCallParams(String Name, Dictionary<String, Object?> Arguments, ToolCallMeta? Meta);

/// <summary>工具调用元数据</summary>
public record ToolCallMeta(String ProgressToken);

/// <summary>工具调用结果</summary>
public record ToolCallResult(List<ContentItem> Content, Boolean IsError = false);

/// <summary>内容项</summary>
public record ContentItem(String Type, String Text);

/// <summary>工具列表结果</summary>
public record ToolListResult(List<ToolDefinition> Tools);

/// <summary>工具定义</summary>
public record ToolDefinition(String Name, String Description, Object InputSchema);

/// <summary>进度通知</summary>
public record ProgressNotification(String JsonRpc, String Method, ProgressParams Params);

/// <summary>进度参数</summary>
public record ProgressParams(String ProgressToken, Int32 Progress, Int32 Total, String Message);
