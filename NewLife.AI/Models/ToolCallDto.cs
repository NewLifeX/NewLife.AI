namespace NewLife.AI.Models;

/// <summary>工具调用信息。持久化到 ChatMessage.ToolCalls 字段，用于前端展示调用入参与结果</summary>
/// <param name="Id">工具调用编号（来自服务商）</param>
/// <param name="Name">工具名称</param>
/// <param name="Status">调用状态</param>
/// <param name="Arguments">入参（JSON 字符串）</param>
/// <param name="Result">完整结果（前端用户内容，JSON/SVG/文本）</param>
/// <param name="LlmResult">LLM 摘要（历史回读时优先用作 role=tool 内容；null 时回退到 Result）</param>
/// <param name="IsError">工具是否失败</param>
/// <param name="ContentOffset">工具调用触发时消息正文的字符偏移量（UTF-16 单元数），用于前端将卡片嵌入内容正确位置。null 表示旧消息不含位置信息</param>
public record ToolCallDto(String Id, String Name, ToolCallStatus Status, String? Arguments = null, String? Result = null, String? LlmResult = null, Boolean? IsError = null, Int32? ContentOffset = null);
