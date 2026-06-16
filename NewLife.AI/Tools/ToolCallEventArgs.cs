namespace NewLife.AI.Tools;

/// <summary>工具调用事件参数。由 <see cref="ToolChatClient"/> 在执行工具完成后通过 <c>OnToolExecuted</c> 回调通知外部</summary>
/// <param name="ToolName">工具名称</param>
/// <param name="Arguments">原始参数 JSON 字符串（模型原文）</param>
/// <param name="ResultSummary">工具结果摘要（截断到 200 字符，超过部分以 ... 表示）</param>
/// <param name="IsError">是否执行出错</param>
/// <param name="ElapsedMs">执行耗时（毫秒）</param>
public record ToolCallEventArgs(String ToolName, String? Arguments, String? ResultSummary, Boolean IsError, Int64 ElapsedMs);
