namespace NewLife.AI.Tools;

/// <summary>工具参数校验异常。携带分别面向用户和 LLM 的错误信息，由 <see cref="ToolChatClient"/> 捕获后包装为结构化的 <see cref="ToolResult"/></summary>
/// <remarks>
/// 与普通 <see cref="ArgumentException"/> 的区别：
/// <list type="bullet">
/// <item><see cref="ForUser"/>：面向终端用户的简短错误描述，通过 SSE 发送到前端展示</item>
/// <item><see cref="ForLlm"/>：面向 LLM 的恢复指引，告知模型发生了什么以及如何修正或降级</item>
/// </list>
/// 工具方法在参数校验失败时应抛出本异常，而非 <see cref="ArgumentException"/>。<br/>
/// <see cref="ToolChatClient.ExecuteToolAsync"/> 捕获本异常后生成带受众分离的 <see cref="ToolResult"/>（IsError=true），
/// 使 LLM 获得明确的恢复指引，避免在无提示下盲目重试导致连环失败。
/// </remarks>
public class ToolException : Exception
{
    /// <summary>面向终端用户的错误描述（简短，通过 SSE 发送到前端）</summary>
    public String ForUser { get; }

    /// <summary>面向 LLM 的恢复指引（告知模型错误原因及修正或降级方式）</summary>
    public String ForLlm { get; }

    /// <summary>创建工具参数校验异常</summary>
    /// <param name="forUser">面向用户的错误描述</param>
    /// <param name="forLlm">面向 LLM 的恢复指引</param>
    /// <param name="innerException">内部异常（可选）</param>
    public ToolException(String forUser, String forLlm, Exception? innerException = null)
        : base(forLlm, innerException)
    {
        ForUser = forUser;
        ForLlm = forLlm;
    }
}
