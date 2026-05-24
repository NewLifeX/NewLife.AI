#if NETFRAMEWORK
using System.Runtime.Remoting.Messaging;
#endif

using NewLife.AI.Clients;

namespace NewLife.AI.Tools;

/// <summary>工具调用环境上下文。通过 AsyncLocal 在工具执行期间透传调用来源信息，无需修改工具方法签名</summary>
/// <remarks>
/// <see cref="ToolChatClient"/> 在工具调用循环外构建本对象并赋给 <see cref="ToolCallContext.Current"/>，
/// 每轮 LLM 返回后更新 <see cref="Response"/>；工具方法从 <see cref="ToolCallContext.Current"/> 读取，无需修改签名。
/// <para>
/// .NET 4.6+/netstandard：使用 AsyncLocal（按异步执行上下文隔离，并发安全）；
/// .NET 4.5：降级为 CallContext.LogicalGetData/SetData（语义相同，支持跨异步延续传播）。
/// </para>
/// </remarks>
public class ToolCallContext
{
    #region 属性
    /// <summary>触发工具调用的原始请求（整个请求生命周期内不变）</summary>
    public IChatRequest? Request { get; init; }

    /// <summary>当前轮次的 LLM 响应，每次 LLM 返回后由 ToolChatClient 更新。流式模式下始终为 null</summary>
    public IChatResponse? Response { get; set; }

    /// <summary>当前正在执行的工具调用编号（由 LLM 分配）。在工具方法内读取，用于将工具调用 ID 透传给 HITL 等需要回调的机制</summary>
    public String? CurrentToolCallId { get; set; }
    #endregion

#if NETFRAMEWORK
    private const String CurrentKey = "ToolCallContext.Current";

    /// <summary>当前工具调用上下文数据</summary>
    public static ToolCallContext? Current
    {
        get => CallContext.LogicalGetData(CurrentKey) as ToolCallContext;
        set => CallContext.LogicalSetData(CurrentKey, value);
    }
#else
    private static readonly AsyncLocal<ToolCallContext?> _current = new();

    /// <summary>当前工具调用上下文数据</summary>
    public static ToolCallContext? Current
    {
        get => _current.Value;
        set => _current.Value = value;
    }
#endif
}

