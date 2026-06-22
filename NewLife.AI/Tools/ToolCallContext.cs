using NewLife.AI.Clients;
using NewLife.Data;

namespace NewLife.AI.Tools;

/// <summary>工具调用环境上下文。在工具执行期间透传调用来源信息，作为参数注入到需要的工具方法</summary>
/// <remarks>
/// 每次工具调用创建独立实例（通过 <c>new ToolCallContext { ..., ToolCallId = tc.Id }</c>），
/// 由 <see cref="ToolChatClient"/> 在启动并行执行前构建并传入各工具方法。
/// 工具方法声明 <c>ToolCallContext? context = null</c> 参数即可接收注入，框架按类型自动匹配（与 <c>CancellationToken</c> 机制相同），Schema 生成时自动跳过该参数。
/// </remarks>
public class ToolCallContext : IExtend
{
    #region 属性
    /// <summary>触发工具调用的原始请求（整个请求生命周期内不变）</summary>
    public IChatRequest? Request { get; init; }

    /// <summary>当前轮次的 LLM 响应，每次 LLM 返回后由 ToolChatClient 更新。流式模式下始终为 null</summary>
    public IChatResponse? Response { get; init; }

    /// <summary>当前工具调用的唯一编号（由 LLM 分配，构造时固定）。工具方法内读取此值可将调用 ID 透传给 HITL 等回调机制</summary>
    public String? ToolCallId { get; init; }

    /// <summary>工具方法返回的 IToolResult。由 InvokeMethodAsync 写入，CallToolAsync 读取</summary>
    /// <remarks>可视化工具（show_xxx）直接返回 ToolResult 时，框架通过此属性将受众分离结果透传给 ToolChatClient</remarks>
    public IToolResult? ToolResult { get; set; }

    /// <summary>扩展元数据。与 IExtend 接口共享同一字典，handler 间可读写</summary>
    public IDictionary<String, Object?> Items { get; } = new Dictionary<String, Object?>();

    /// <summary>通过键读写扩展元数据</summary>
    /// <param name="key">键</param>
    public Object? this[String key]
    {
        get => Items.TryGetValue(key, out var v) ? v : null;
        set => Items[key] = value;
    }
    #endregion
}

