using NewLife.Remoting;

namespace NewLife.AI.Models;

/// <summary>上下文超限异常。模型输入长度超过上下文窗口时由协议层抛出，携带解析出的窗口上限</summary>
/// <remarks>在 <see cref="ChatErrorHelper.Classify"/> 基础上提供类型化信号，非流式/网关等调用方可按类型捕获并转为友好提示</remarks>
public class ContextLengthExceededException : ApiException
{
    /// <summary>模型上下文窗口上限（Token 数）。从错误原文解析，解析不到时为 null</summary>
    public Int64? ContextLength { get; }

    /// <summary>初始化上下文超限异常</summary>
    /// <param name="code">HTTP 状态码</param>
    /// <param name="body">服务商错误响应体或错误文本</param>
    public ContextLengthExceededException(Int32 code, String body) : base(code, body)
    {
        ContextLength = ChatErrorHelper.TryExtractContextLength(body);
    }

    /// <summary>判断错误文本是否属于上下文超限</summary>
    /// <param name="body">服务商错误响应体或错误文本</param>
    /// <returns>命中返回 true</returns>
    public static Boolean IsContextLengthError(String? body) => ChatErrorHelper.IsContextLengthError(body);
}
