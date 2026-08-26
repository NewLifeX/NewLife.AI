namespace NewLife.AI.ModelContextProtocol;

/// <summary>MCP/JSON-RPC 标准错误码。对齐官方 ModelContextProtocol SDK 的 McpErrorCode 枚举值</summary>
/// <remarks>
/// MCP 基于 JSON-RPC 2.0，协议级错误必须使用本类定义的负值错误码，
/// 而非 HTTP 风格状态码（400/404/500）。官方客户端按此识别错误类型：
/// 方法未找到（-32601）、参数无效（-32602）、资源未找到（-32002）等。
/// </remarks>
public static class McpErrorCode
{
    /// <summary>解析错误。收到的 JSON 无法解析或语法错误</summary>
    public const Int32 ParseError = -32700;

    /// <summary>无效请求。请求结构不符合 JSON-RPC（缺字段、协议版本不支持等）</summary>
    public const Int32 InvalidRequest = -32600;

    /// <summary>方法未找到。请求的方法不存在，或请求了未声明能力的方法</summary>
    public const Int32 MethodNotFound = -32601;

    /// <summary>无效参数。协议级参数校验失败：未知工具名/提示词名、无效工具参数、未知资源 URI、参数缺失等</summary>
    public const Int32 InvalidParams = -32602;

    /// <summary>内部错误。处理请求时发生意外异常</summary>
    public const Int32 InternalError = -32603;

    /// <summary>资源未找到。2025-06-18 及更早协议版本下未知资源 URI 使用此码；新协议版本统一用 <see cref="InvalidParams"/></summary>
    public const Int32 ResourceNotFound = -32002;
}
