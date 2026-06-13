namespace NewLife.AI.Tools;

/// <summary>工具调用结果。对齐 MCP CallToolResult + Anthropic tool_result</summary>
/// <remarks>
/// 内容块列表中的每块有独立受众（<see cref="ToolAudience"/>）和类型（<see cref="ToolContentType"/>），
/// ToolChatClient 据此分流：Llm 受众 → role=tool 消息；User 受众 → SSE 事件 + DB 持久化。
/// </remarks>
public interface IToolResult
{
    /// <summary>内容块列表。每块有独立的受众和类型</summary>
    IList<ToolContent> Contents { get; }

    /// <summary>是否失败。true 时 ToolChatClient 以 error 事件发出</summary>
    Boolean IsError { get; }
}
