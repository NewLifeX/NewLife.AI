using NewLife.Collections;

namespace NewLife.AI.Agents;

/// <summary>并行群聊控制器。将初始消息同时分发给多个工作代理并行处理，汇总后交给聚合代理整合</summary>
/// <remarks>
/// 工作流程：
/// <list type="number">
/// <item>将初始消息同时分发给所有工作代理（并行执行）</item>
/// <item>按配置的超时和最大并行度控制并发</item>
/// <item>失败的工作代理被跳过（降级策略）</item>
/// <item>收集所有成功结果，构造汇总消息交给聚合代理</item>
/// <item>聚合代理产出最终结果</item>
/// </list>
/// </remarks>
public sealed class ParallelGroupChat
{
    #region 属性

    /// <summary>工作代理列表。并行接收初始消息并各自产出结果</summary>
    public IList<IAgent> Workers { get; }

    /// <summary>聚合代理。接收所有工作代理的结果并整合为最终输出</summary>
    public IAgent Aggregator { get; }

    /// <summary>最大并行度。同时执行的工作代理数量上限，默认 5</summary>
    public Int32 MaxParallelism { get; set; } = 5;

    /// <summary>单个工作代理的超时时间（秒），默认 60</summary>
    public Int32 WorkerTimeoutSeconds { get; set; } = 60;

    #endregion

    #region 构造

    /// <summary>初始化并行群聊控制器</summary>
    /// <param name="workers">工作代理列表</param>
    /// <param name="aggregator">聚合代理</param>
    public ParallelGroupChat(IList<IAgent> workers, IAgent aggregator)
    {
        if (workers == null || workers.Count == 0) throw new ArgumentException("工作代理列表不能为空", nameof(workers));
        if (aggregator == null) throw new ArgumentNullException(nameof(aggregator));

        Workers = workers;
        Aggregator = aggregator;
    }

    #endregion

    #region 方法

    /// <summary>并行执行所有工作代理并聚合结果。异步产出消息流（含工作代理响应和聚合结果）</summary>
    /// <param name="initial">初始触发消息（用户输入）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>包含工作代理响应和聚合结果的消息流</returns>
    public async IAsyncEnumerable<AgentMessage> RunAsync(
        AgentMessage initial,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (initial == null) throw new ArgumentNullException(nameof(initial));

        yield return initial;

        // 并行执行所有工作代理
        var workerResults = await ExecuteWorkersAsync(initial, cancellationToken).ConfigureAwait(false);

        // 产出各工作代理的结果
        foreach (var (agent, messages) in workerResults)
        {
            foreach (var msg in messages)
            {
                yield return msg;
            }
        }

        // 构造聚合输入：初始消息 + 各工作代理的文本结果
        var aggregatorHistory = BuildAggregatorHistory(initial, workerResults);

        // 调用聚合代理
        await foreach (var msg in Aggregator.HandleAsync(aggregatorHistory, cancellationToken).ConfigureAwait(false))
        {
            yield return msg;

            if (msg.Type == AgentMessageType.Stop) yield break;
        }
    }

    /// <summary>并行执行所有工作代理，收集每个代理的响应消息</summary>
    private async Task<IList<(IAgent Agent, IList<AgentMessage> Messages)>> ExecuteWorkersAsync(
        AgentMessage initial, CancellationToken cancellationToken)
    {
        var results = new List<(IAgent Agent, IList<AgentMessage> Messages)>();
        var semaphore = new SemaphoreSlim(MaxParallelism, MaxParallelism);
        var history = new List<AgentMessage> { initial };

        var tasks = new List<Task<(IAgent Agent, IList<AgentMessage> Messages)>>();
        foreach (var worker in Workers)
        {
            tasks.Add(ExecuteWorkerAsync(worker, history, semaphore, cancellationToken));
        }

        // 等待所有工作代理完成
        var completed = await Task.WhenAll(tasks).ConfigureAwait(false);
        foreach (var item in completed)
        {
            // 跳过失败（空结果）的工作代理
            if (item.Messages.Count > 0)
                results.Add(item);
        }

        return results;
    }

    /// <summary>执行单个工作代理（含超时和异常捕获）</summary>
    private async Task<(IAgent Agent, IList<AgentMessage> Messages)> ExecuteWorkerAsync(
        IAgent worker, IList<AgentMessage> history, SemaphoreSlim semaphore, CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(WorkerTimeoutSeconds));

            var messages = new List<AgentMessage>();
            await foreach (var msg in worker.HandleAsync(history, cts.Token).ConfigureAwait(false))
            {
                messages.Add(msg);
            }
            return (worker, messages);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // 工作代理超时，降级跳过
            return (worker, (IList<AgentMessage>)new List<AgentMessage>());
        }
        catch
        {
            // 工作代理执行失败，降级跳过
            return (worker, (IList<AgentMessage>)new List<AgentMessage>());
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>构建聚合代理的消息历史。将各工作代理的文本结果汇总为格式化文本</summary>
    private static IList<AgentMessage> BuildAggregatorHistory(
        AgentMessage initial, IList<(IAgent Agent, IList<AgentMessage> Messages)> workerResults)
    {
        var history = new List<AgentMessage> { initial };

        // 将每个工作代理的结果以文本形式汇总
        var sb = Pool.StringBuilder.Get();
        sb.AppendLine("以下是各工作代理的分析结果，请综合整理：");
        sb.AppendLine();

        foreach (var (agent, messages) in workerResults)
        {
            sb.AppendLine($"### {agent.Name}");
            foreach (var msg in messages)
            {
                if (msg is TextMessage textMsg && !String.IsNullOrEmpty(textMsg.Content))
                    sb.AppendLine(textMsg.Content);
            }
            sb.AppendLine();
        }

        history.Add(new TextMessage
        {
            Source = "system",
            Role = "user",
            Content = sb.Return(true),
        });

        return history;
    }

    #endregion
}
