namespace NewLife.AI.Models;

/// <summary>消息流程初始化异常。在 CreateFlowByMessage 验证失败时抛出，携带结构化错误码与描述</summary>
public class ChatException : Exception
{
    /// <summary>错误码（如 MESSAGE_NOT_FOUND、CONVERSATION_NOT_FOUND、MODEL_UNAVAILABLE）</summary>
    public String Code { get; }

    /// <summary>初始化流程异常</summary>
    /// <param name="code">错误码</param>
    /// <param name="message">错误描述</param>
    public ChatException(String code, String message) : base(message) => Code = code;
}
