using NewLife.AI.Clients;

namespace NewLife.AI.Tools;

/// <summary>工具调用环境上下文。在工具执行期间透传调用来源信息，作为参数注入到需要的工具方法</summary>
/// <remarks>
/// <see cref="ToolChatClient"/> 在工具调用循环外构建本对象，每轮工具调用前设置 <see cref="CurrentToolCallId"/>，
/// 随后作为显式参数经 <see cref="IToolProvider.CallToolAsync"/> 传入工具方法。
/// 工具方法声明 <c>ToolCallContext? context = null</c> 参数即可接收注入，框架按类型自动匹配（与 <c>CancellationToken</c> 机制相同），Schema 生成时自动跳过该参数。
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
}

