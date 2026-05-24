using NewLife.Collections;

namespace NewLife.AI.Tools;

/// <summary>工具执行结构化错误。工具执行失败时返回此对象的 JSON 而非裸字符串，使模型能利用结构化信息自我纠错</summary>
/// <remarks>
/// 相比裸字符串错误，结构化错误使 Agent 能根据错误码选择不同的纠错策略。
/// 工具执行失败时，<see cref="ToolChatClient"/> 会自动将异常封装为此格式并返回给模型。
/// 常用错误码：PERMISSION_DENIED（代码强制阻断）/ USER_DENIED（用户拒绝）/ EXECUTION_ERROR（执行异常）
/// </remarks>
public class ToolError
{
    /// <summary>机器可读的错误类型码（如 SYNTAX_ERROR / PERMISSION_DENIED / NOT_FOUND）</summary>
    public String ErrorCode { get; set; } = String.Empty;

    /// <summary>人类可读的错误描述，告知模型发生了什么</summary>
    public String Hint { get; set; } = String.Empty;

    /// <summary>可操作的修复建议（可为空），提示模型下一步应如何修正</summary>
    public String? SuggestedFix { get; set; }

    /// <summary>创建结构化工具错误</summary>
    /// <param name="errorCode">错误类型码</param>
    /// <param name="hint">错误描述</param>
    /// <param name="suggestedFix">修复建议（可为空）</param>
    public static ToolError Create(String errorCode, String hint, String? suggestedFix = null)
        => new() { ErrorCode = errorCode, Hint = hint, SuggestedFix = suggestedFix };

    /// <summary>将错误序列化为 JSON 字符串，用作工具执行结果回传给模型</summary>
    public String ToJson()
    {
        var sb = Pool.StringBuilder.Get();
        sb.Append("{\"error_code\":\"");
        AppendJsonEscaped(sb, ErrorCode);
        sb.Append("\",\"hint\":\"");
        AppendJsonEscaped(sb, Hint);
        sb.Append('"');
        if (!SuggestedFix.IsNullOrEmpty())
        {
            sb.Append(",\"suggested_fix\":\"");
            AppendJsonEscaped(sb, SuggestedFix!);
            sb.Append('"');
        }
        sb.Append('}');
        return sb.Return(true);
    }

    /// <summary>检查字符串是否为工具错误 JSON（由 <see cref="ToJson"/> 序列化生成）</summary>
    /// <param name="result">工具返回的结果字符串</param>
    public static Boolean IsToolError(String? result)
        => !result.IsNullOrEmpty() && result!.Contains("\"error_code\"");

    private static void AppendJsonEscaped(System.Text.StringBuilder sb, String value)
    {
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default: sb.Append(ch); break;
            }
        }
    }
}
