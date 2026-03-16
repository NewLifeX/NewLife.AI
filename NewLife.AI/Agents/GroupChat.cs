namespace NewLife.AI.Agents;

/// <summary>GroupChat 发言顺序选择器接口。决定每轮由哪个代理发言</summary>
public interface IGroupChatSelector
{
    /// <summary>选择下一个发言的代理</summary>
    /// <param name="agents">参与群聊的代理列表</param>
    /// <param name="history">当前消息历史</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>下一个应发言的代理</returns>
    Task<IAgent> SelectNextAsync(IList<IAgent> agents, IList<AgentMessage> history, CancellationToken cancellationToken = default);
}

/// <summary>轮询选择器。按注册顺序循环调用每个代理</summary>
public sealed class RoundRobinSelector : IGroupChatSelector
{
    private Int32 _index = -1;

    /// <summary>按轮询顺序选择下一个代理</summary>
    /// <param name="agents">代理列表</param>
    /// <param name="history">消息历史（轮询选择器不使用）</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task<IAgent> SelectNextAsync(IList<IAgent> agents, IList<AgentMessage> history, CancellationToken cancellationToken = default)
    {
        if (agents == null || agents.Count == 0) throw new ArgumentException("代理列表不能为空", nameof(agents));

        // 原子操作确保多线程安全
        var next = Interlocked.Increment(ref _index) % agents.Count;
        return Task.FromResult(agents[next]);
    }

    /// <summary>重置轮询计数器</summary>
    public void Reset() => Interlocked.Exchange(ref _index, -1);
}

/// <summary>群聊控制器。协调多个 IAgent 按照选择器策略轮流发言，直到收到 StopMessage 或达到最大轮次</summary>
/// <remarks>
/// 基本用法：
/// <code>
/// var chat = new GroupChat(agents, new RoundRobinSelector(), maxRounds: 10);
/// await foreach (var msg in chat.RunAsync(new TextMessage { Source = "user", Content = "你好" }))
///     Console.WriteLine($"[{msg.Source}] {(msg as TextMessage)?.Content}");
/// </code>
/// </remarks>
public sealed class GroupChat
{
    #region 属性

    /// <summary>参与群聊的代理列表</summary>
    public IList<IAgent> Agents { get; }

    /// <summary>发言顺序选择器</summary>
    public IGroupChatSelector Selector { get; }

    /// <summary>最大轮次。达到后强制停止（防止死循环），默认 20</summary>
    public Int32 MaxRounds { get; set; } = 20;

    #endregion

    #region 构造

    /// <summary>初始化群聊</summary>
    /// <param name="agents">参与代理列表</param>
    /// <param name="selector">发言选择器（默认轮询）</param>
    /// <param name="maxRounds">最大轮次</param>
    public GroupChat(IList<IAgent> agents, IGroupChatSelector? selector = null, Int32 maxRounds = 20)
    {
        if (agents == null || agents.Count == 0) throw new ArgumentException("代理列表不能为空", nameof(agents));

        Agents = agents;
        Selector = selector ?? new RoundRobinSelector();
        MaxRounds = maxRounds;
    }

    #endregion

    #region 方法

    /// <summary>运行群聊对话流。异步产出每条消息，调用方可实时消费</summary>
    /// <param name="initial">初始触发消息（用户输入）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>群聊产生的所有消息流（含每个代理的响应）</returns>
    public async IAsyncEnumerable<AgentMessage> RunAsync(
        AgentMessage initial,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (initial == null) throw new ArgumentNullException(nameof(initial));

        var history = new List<AgentMessage> { initial };
        yield return initial;

        for (var round = 0; round < MaxRounds; round++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var agent = await Selector.SelectNextAsync(Agents, history, cancellationToken).ConfigureAwait(false);
            var stop = false;

            await foreach (var msg in agent.HandleAsync(history, cancellationToken).ConfigureAwait(false))
            {
                history.Add(msg);
                yield return msg;

                if (msg.Type == AgentMessageType.Stop)
                {
                    stop = true;
                    break;
                }
            }

            if (stop) break;
        }
    }

    #endregion
}
