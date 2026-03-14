using NewLife.AI.Models;

namespace NewLife.AI.Filters;

/// <summary>对话过滤器上下文。在过滤器链中传递请求与响应信息</summary>
public class ChatFilterContext
{
    /// <summary>对话完成请求。过滤器可修改此对象以影响后续处理</summary>
    public ChatCompletionRequest Request { get; set; } = null!;

    /// <summary>对话完成响应。在 After 阶段由过滤器读取或修改</summary>
    public ChatCompletionResponse? Response { get; set; }

    /// <summary>是否流式处理</summary>
    public Boolean IsStreaming { get; set; }

    /// <summary>附加数据。用于在过滤器链之间传递自定义状态</summary>
    public Dictionary<String, Object?> ExtraData { get; set; } = [];
}

/// <summary>函数调用过滤器上下文。在工具调用前后传递上下文信息</summary>
public class FunctionInvocationContext
{
    /// <summary>被调用的函数名称</summary>
    public String FunctionName { get; set; } = String.Empty;

    /// <summary>调用参数（JSON 字符串）。过滤器可改写</summary>
    public String? Arguments { get; set; }

    /// <summary>函数执行结果（JSON 字符串）。After 阶段由过滤器读取或覆写</summary>
    public String? Result { get; set; }

    /// <summary>附加数据</summary>
    public Dictionary<String, Object?> ExtraData { get; set; } = [];
}
