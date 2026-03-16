namespace NewLife.AI.Agents;

/// <summary>代理上下文。在拦截链中传递的执行上下文信息</summary>
public class AgentContext
{
    /// <summary>当前消息历史</summary>
    public IList<AgentMessage> History { get; set; } = [];

    /// <summary>取消令牌</summary>
    public CancellationToken CancellationToken { get; set; }

    /// <summary>扩展属性。用于在拦截链中传递自定义数据</summary>
    public IDictionary<String, Object?> Properties { get; } = new Dictionary<String, Object?>();
}

/// <summary>委托代理。装饰器模式实现，通过 OnBefore/OnAfter 虚方法提供拦截扩展点</summary>
/// <remarks>
/// 用法示例：
/// <code>
/// class LoggingAgent : DelegatingAgent
/// {
///     public LoggingAgent(IAgent inner) : base(inner) { }
///     protected override Task OnBeforeAsync(AgentContext ctx) { Log("before"); return Task.CompletedTask; }
///     protected override Task&lt;IList&lt;AgentMessage&gt;&gt; OnAfterAsync(AgentContext ctx, IList&lt;AgentMessage&gt; results) { Log("after"); return Task.FromResult(results); }
/// }
/// </code>
/// 支持多层嵌套：new LoggingAgent(new MetricsAgent(innerAgent))
/// </remarks>
public class DelegatingAgent : IAgent
{
    #region 属性

    /// <summary>被装饰的内部代理</summary>
    public IAgent InnerAgent { get; }

    /// <inheritdoc/>
    public String Name => InnerAgent.Name;

    /// <inheritdoc/>
    public String? Description => InnerAgent.Description;

    #endregion

    #region 构造

    /// <summary>初始化委托代理</summary>
    /// <param name="innerAgent">被装饰的内部代理</param>
    public DelegatingAgent(IAgent innerAgent)
    {
        if (innerAgent == null) throw new ArgumentNullException(nameof(innerAgent));
        InnerAgent = innerAgent;
    }

    #endregion

    #region 方法

    /// <summary>处理历史消息，通过 OnBefore → InnerAgent → OnAfter 管线返回消息流</summary>
    /// <param name="history">完整消息历史</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async IAsyncEnumerable<AgentMessage> HandleAsync(
        IList<AgentMessage> history,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (history == null) throw new ArgumentNullException(nameof(history));

        var ctx = new AgentContext
        {
            History = history,
            CancellationToken = cancellationToken,
        };

        // 前置拦截
        await OnBeforeAsync(ctx).ConfigureAwait(false);

        // 收集内部代理产出的所有消息
        var results = new List<AgentMessage>();
        await foreach (var msg in InnerAgent.HandleAsync(ctx.History, cancellationToken).ConfigureAwait(false))
        {
            results.Add(msg);
        }

        // 后置拦截（可修改/过滤/追加消息）
        results = (List<AgentMessage>)await OnAfterAsync(ctx, results).ConfigureAwait(false);

        // 逐条产出
        foreach (var msg in results)
        {
            yield return msg;
        }
    }

    /// <summary>前置拦截。在 InnerAgent.HandleAsync 之前调用，可修改 context.History 或注入额外逻辑</summary>
    /// <param name="context">代理执行上下文</param>
    protected virtual Task OnBeforeAsync(AgentContext context) => Task.CompletedTask;

    /// <summary>后置拦截。在 InnerAgent.HandleAsync 之后调用，可修改/过滤/追加消息结果</summary>
    /// <param name="context">代理执行上下文</param>
    /// <param name="results">内部代理产出的消息列表</param>
    /// <returns>最终消息列表（可与输入相同或不同）</returns>
    protected virtual Task<IList<AgentMessage>> OnAfterAsync(AgentContext context, IList<AgentMessage> results) => Task.FromResult(results);

    #endregion
}
