using NewLife.AI.Models;
using NewLife.Data;

namespace NewLife.AI.Filters;

/// <summary>对话过滤器上下文。在过滤器链中传递请求与响应信息</summary>
public class ChatFilterContext : IExtend
{
    /// <summary>对话完成请求。过滤器可修改此对象以影响后续处理</summary>
    public ChatCompletionRequest Request { get; set; } = null!;

    /// <summary>对话完成响应。在 After 阶段由过滤器读取或修改</summary>
    public ChatResponse? Response { get; set; }

    /// <summary>是否流式处理</summary>
    public Boolean IsStreaming { get; set; }

    /// <summary>当前请求的用户编号（0 表示未设置）。由管道调用方通过 ChatOptions.UserId 传入</summary>
    public String? UserId { get; set; }

    /// <summary>当前请求的会话编号（0 表示未设置）。由管道调用方通过 ChatOptions.ConversationId 传入</summary>
    public String? ConversationId { get; set; }

    /// <summary>附加数据。用于在过滤器链之间传递自定义状态</summary>
    public IDictionary<String, Object?> Items { get; set; } = new Dictionary<String, Object?>();

    /// <summary>索引器，方便访问附加数据</summary>
    public Object? this[String key] { get => Items.TryGetValue(key, out var value) ? value : null; set => Items[key] = value; }
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
