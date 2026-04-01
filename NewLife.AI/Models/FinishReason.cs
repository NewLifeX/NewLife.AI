namespace NewLife.AI.Models;

/// <summary>完成原因枚举。对应 OpenAI finish_reason 标准字段值</summary>
public enum FinishReason
{
    /// <summary>模型自然结束生成</summary>
    Stop,

    /// <summary>达到最大输出令牌数被截断</summary>
    Length,

    /// <summary>模型输出了工具调用请求</summary>
    ToolCalls,

    /// <summary>内容被安全策略过滤</summary>
    ContentFilter,
}

/// <summary>FinishReason 扩展方法</summary>
public static class FinishReasonHelper
{
    /// <summary>将枚举转换为 OpenAI finish_reason API 字符串</summary>
    /// <param name="reason">完成原因枚举</param>
    /// <returns>API 字符串，如 "stop"、"tool_calls"</returns>
    public static String ToApiString(this FinishReason reason) => reason switch
    {
        FinishReason.Stop => "stop",
        FinishReason.Length => "length",
        FinishReason.ToolCalls => "tool_calls",
        FinishReason.ContentFilter => "content_filter",
        _ => "stop",
    };

    /// <summary>将 OpenAI finish_reason 字符串解析为枚举；空或未知值返回 null</summary>
    /// <param name="value">API 完成原因字符串</param>
    /// <returns>对应枚举值，null 表示空或不可识别</returns>
    public static FinishReason? Parse(String? value) => value switch
    {
        "stop" => FinishReason.Stop,
        "length" => FinishReason.Length,
        "tool_calls" => FinishReason.ToolCalls,
        "content_filter" => FinishReason.ContentFilter,
        _ => null,
    };
}
