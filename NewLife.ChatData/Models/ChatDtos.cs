using NewLife.AI.Models;

namespace NewLife.ChatData.Models;

/// <summary>工具调用信息。持久化到 ChatMessage.ToolCalls 字段，用于前端展示调用入参与结果</summary>
/// <param name="Id">工具调用编号（来自服务商）</param>
/// <param name="Name">工具名称</param>
/// <param name="Status">调用状态</param>
/// <param name="Arguments">入参（JSON 字符串）</param>
/// <param name="Result">结果（截断后的文本）</param>
public record ToolCallDto(String Id, String Name, ToolCallStatus Status, String? Arguments = null, String? Result = null);
