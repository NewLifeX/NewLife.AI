using NewLife.Log;

namespace NewLife.AI.Models;

/// <summary>SSE 流式事件。用于对话流式输出的事件模型</summary>
public class ChatStreamEvent
{
    #region 属性
    /// <summary>事件类型</summary>
    public String Type { get; set; } = null!;

    /// <summary>消息编号。message_start 时设置</summary>
    public Int64 MessageId { get; set; }

    /// <summary>模型编码</summary>
    public String? Model { get; set; }

    /// <summary>思考模式</summary>
    public ThinkingMode ThinkingMode { get; set; }

    /// <summary>文本内容。content_delta / thinking_delta 时的增量文本</summary>
    public String? Content { get; set; }

    /// <summary>思考耗时。thinking_done 时的耗时毫秒数</summary>
    public Int32 ThinkingTime { get; set; }

    /// <summary>工具调用编号</summary>
    public String? ToolCallId { get; set; }

    /// <summary>工具名称</summary>
    public String? Name { get; set; }

    /// <summary>调用参数。JSON 字符串</summary>
    public String? Arguments { get; set; }

    /// <summary>工具返回结果</summary>
    public String? Result { get; set; }

    /// <summary>是否成功</summary>
    public Boolean Success { get; set; }

    /// <summary>错误信息</summary>
    public String? Error { get; set; }

    /// <summary>错误码</summary>
    public String? Code { get; set; }

    /// <summary>错误描述</summary>
    public String? Message { get; set; }

    /// <summary>完成原因。stop/length/tool_calls/content_filter</summary>
    public String? FinishReason { get; set; }

    /// <summary>令牌用量统计</summary>
    public UsageDetails? Usage { get; set; }

    /// <summary>会话标题。首条消息自动生成标题时返回</summary>
    public String? Title { get; set; }
    #endregion

    #region 工厂方法
    /// <summary>消息开始事件</summary>
    /// <param name="messageId">消息编号</param>
    /// <param name="model">模型编码</param>
    /// <param name="thinkingMode">思考模式</param>
    /// <returns></returns>
    public static ChatStreamEvent MessageStart(Int64 messageId, String model, ThinkingMode thinkingMode) =>
        new() { Type = "message_start", MessageId = messageId, Model = model, ThinkingMode = thinkingMode };

    /// <summary>思考增量事件</summary>
    /// <param name="content">思考内容</param>
    /// <returns></returns>
    public static ChatStreamEvent ThinkingDelta(String content) =>
        new() { Type = "thinking_delta", Content = content };

    /// <summary>思考完成事件</summary>
    /// <param name="thinkingTime">思考耗时毫秒</param>
    /// <returns></returns>
    public static ChatStreamEvent ThinkingDone(Int32 thinkingTime) =>
        new() { Type = "thinking_done", ThinkingTime = thinkingTime };

    /// <summary>内容增量事件</summary>
    /// <param name="content">内容文本</param>
    /// <returns></returns>
    public static ChatStreamEvent ContentDelta(String content) =>
        new() { Type = "content_delta", Content = content };

    /// <summary>消息完成事件</summary>
    /// <param name="usage">用量统计</param>
    /// <param name="title">标题（可选）</param>
    /// <param name="finishReason">完成原因（可选）</param>
    /// <returns></returns>
    public static ChatStreamEvent MessageDone(UsageDetails? usage = null, String? title = null, String? finishReason = null) =>
        new() { Type = "message_done", Usage = usage, Title = title, FinishReason = finishReason };

    /// <summary>错误事件</summary>
    /// <param name="code">错误码</param>
    /// <param name="message">错误描述</param>
    /// <returns></returns>
    public static ChatStreamEvent ErrorEvent(String code, String message)
    {
        using var span = DefaultTracer.Instance?.NewSpan("ai:StreamError", $"[{code}]{message}");

        return new() { Type = "error", Code = code, Message = message };
    }

    /// <summary>工具调用开始事件</summary>
    /// <param name="toolCallId">调用编号</param>
    /// <param name="name">工具名称</param>
    /// <param name="arguments">调用参数</param>
    /// <returns></returns>
    public static ChatStreamEvent ToolCallStart(String toolCallId, String name, String? arguments) =>
        new() { Type = "tool_call_start", ToolCallId = toolCallId, Name = name, Arguments = arguments };

    /// <summary>工具调用完成事件</summary>
    /// <param name="toolCallId">调用编号</param>
    /// <param name="result">返回结果</param>
    /// <param name="success">是否成功</param>
    /// <returns></returns>
    public static ChatStreamEvent ToolCallDone(String toolCallId, String? result, Boolean success) =>
        new() { Type = "tool_call_done", ToolCallId = toolCallId, Result = result, Success = success };

    /// <summary>工具调用失败事件</summary>
    /// <param name="toolCallId">调用编号</param>
    /// <param name="error">错误描述</param>
    /// <returns></returns>
    public static ChatStreamEvent ToolCallError(String toolCallId, String error) =>
        new() { Type = "tool_call_error", ToolCallId = toolCallId, Error = error };
    #endregion
}
