namespace NewLife.AI.Agents;

/// <summary>智能代理接口。Agent 是 AgentChat 协议的基本单元</summary>
/// <remarks>
/// 每个 Agent 拥有名称与描述（供 GroupChat 调度器使用），
/// 并通过 HandleAsync 处理历史消息列表并返回响应消息流。
/// </remarks>
public interface IAgent
{
    /// <summary>代理名称。在 GroupChat 中唯一标识一个代理</summary>
    String Name { get; }

    /// <summary>代理描述。供调度器（IGroupChatSelector）参考，可为 null</summary>
    String? Description { get; }

    /// <summary>处理历史消息，返回新消息流</summary>
    /// <param name="history">完整的消息历史（含来自其他代理的消息）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>代理产出的消息序列（可能包含 TextMessage、ToolCallMessage 等）</returns>
    IAsyncEnumerable<AgentMessage> HandleAsync(IList<AgentMessage> history, CancellationToken cancellationToken = default);
}
