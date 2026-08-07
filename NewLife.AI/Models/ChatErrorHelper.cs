using System.Text.RegularExpressions;
using NewLife.Remoting;

namespace NewLife.AI.Models;

/// <summary>聊天错误信息。由 <see cref="ChatErrorHelper.Classify"/> 生成，包含错误码与面向用户的友好文案</summary>
public class ChatErrorInfo
{
    /// <summary>错误码（如 CONTEXT_TOO_LONG、STREAM_ERROR）</summary>
    public String Code { get; set; } = "STREAM_ERROR";

    /// <summary>面向用户的错误描述。上下文超限时替换为友好文案，其余错误保持原始信息</summary>
    public String Message { get; set; } = String.Empty;

    /// <summary>模型上下文窗口上限（Token 数）。从错误原文解析，解析不到时为 null</summary>
    public Int64? ContextLength { get; set; }
}

/// <summary>聊天错误识别与友好化帮助类。识别服务商返回的上下文超限错误，转换为用户可理解的提示</summary>
/// <remarks>
/// 主流网关/服务商上下文超限错误文案差异较大，采用多模式匹配：
/// OpenAI/LiteLLM（context_length_exceeded、maximum context length）、Anthropic（prompt is too long）、
/// Ollama（context window）、阿里云 qwen（Range of input length should be [1, N]）以及中文文案。
/// 命中时返回 <c>CONTEXT_TOO_LONG</c> 错误码与操作建议；未命中保持原始信息，不影响既有错误链路。
/// </remarks>
public static class ChatErrorHelper
{
    /// <summary>上下文超限错误码。前端/调用方据此展示友好提示</summary>
    public const String ContextTooLongCode = "CONTEXT_TOO_LONG";

    /// <summary>上下文超限识别模式（不区分大小写）。命中任一即判定为上下文超限</summary>
    private static readonly String[] _patterns =
    [
        // OpenAI / LiteLLM
        "context_length_exceeded",
        "maximum context length",
        "max context length",
        "context window",
        // Anthropic
        "prompt is too long",
        "request too large",
        // 通用
        "too many tokens",
        "token limit",
        "token limit exceeded",
        "exceeds the maximum",
        // 阿里云 qwen / DashScope
        "input length",
        "range of input length",
        // 中文
        "上下文超",
        "超出上下文",
        "上下文窗口",
        "输入长度",
        "内容过长",
        "token数超限",
    ];

    /// <summary>判断错误文本是否属于上下文超限</summary>
    /// <param name="message">错误文本（异常 Message 或服务商返回体）</param>
    /// <returns>命中返回 true</returns>
    public static Boolean IsContextLengthError(String? message)
    {
        if (String.IsNullOrEmpty(message)) return false;

        foreach (var p in _patterns)
        {
            if (message.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }
        return false;
    }

    /// <summary>从错误文本解析模型上下文窗口上限。支持 [1, N]、maximum/max context length is N、context window is N 等格式</summary>
    /// <param name="message">错误文本</param>
    /// <returns>窗口上限（Token 数），解析失败返回 null</returns>
    public static Int64? TryExtractContextLength(String? message)
    {
        if (String.IsNullOrEmpty(message)) return null;

        var m = Regex.Match(message,
            @"\[1,\s*(\d+)\]|maximum context length (?:is|of)\s*(\d+)|max context length (?:is|of)\s*(\d+)|context window (?:is|of)\s*(\d+)|window (?:is|of)\s*(\d+)",
            RegexOptions.IgnoreCase);
        if (m.Success)
        {
            for (var i = 1; i < m.Groups.Count; i++)
            {
                if (m.Groups[i].Success && Int64.TryParse(m.Groups[i].Value, out var v) && v > 0)
                    return v;
            }
        }
        return null;
    }

    /// <summary>生成上下文超限的友好提示文案</summary>
    /// <param name="contextLength">模型上下文窗口上限（Token 数），可解析时带上数值</param>
    /// <returns>面向用户的友好文案</returns>
    public static String BuildContextLimitMessage(Int64? contextLength)
    {
        if (contextLength is > 0)
            return $"对话内容已超出当前模型的上下文窗口限制（{contextLength:N0} tokens）。请尝试：1）开启新会话并重新提问；2）精简输入内容（如删除过长的历史消息或缩减粘贴的文档）；3）若持续出现请联系管理员调整模型上下文配置。";
        return "对话内容已超出当前模型的上下文窗口限制。请尝试：1）开启新会话并重新提问；2）精简输入内容（如删除过长的历史消息或缩减粘贴的文档）；3）若持续出现请联系管理员调整模型上下文配置。";
    }

    /// <summary>识别并友好化错误文本。上下文超限时替换为友好文案，其余错误保持原始信息</summary>
    /// <param name="message">错误文本（异常 Message 或服务商返回体）</param>
    /// <param name="defaultCode">未命中时的错误码，默认 STREAM_ERROR</param>
    /// <returns>错误信息（码 + 文案）</returns>
    public static ChatErrorInfo Classify(String? message, String defaultCode = "STREAM_ERROR")
    {
        if (IsContextLengthError(message))
        {
            var limit = TryExtractContextLength(message);
            return new ChatErrorInfo
            {
                Code = ContextTooLongCode,
                Message = BuildContextLimitMessage(limit),
                ContextLength = limit,
            };
        }

        return new ChatErrorInfo { Code = defaultCode, Message = message ?? String.Empty };
    }
}
