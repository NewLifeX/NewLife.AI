namespace NewLife.AI.Services;

/// <summary>消息流后处理器。在主流程 PostProcess 阶段被调用（消息已落库），用于用量记录、
/// 自学习触发、Agent 回调、知识沉淀、指标统计等非阻塞收尾工作</summary>
/// <remarks>
/// <para>
/// 多个 PostProcessor 按 <see cref="Order"/> 升序依次执行。建议将无关后处理任务拆分为独立实现，
/// 以便按需增删；失败应被吞掉（记录日志），不影响主流程返回。
/// </para>
/// <para>
/// 对比 <see cref="IContextEnricher"/>：Enricher 发生在生成<b>前</b>、PostProcessor 发生在生成<b>后</b>。
/// 需要同时介入前后两端的业务应实现<b>两个</b>接口。
/// </para>
/// </remarks>
public interface IMessageFlowPostProcessor
{
    /// <summary>执行顺序。数值越小越先执行</summary>
    Int32 Order { get; }

    /// <summary>是否适用于当前请求。返回 false 时跳过 <see cref="ProcessAsync"/></summary>
    /// <param name="context">消息流上下文</param>
    /// <returns>true 表示需要执行</returns>
    Boolean IsApplicable(IMessageFlowContext context);

    /// <summary>执行后处理逻辑。实现者应自行捕获并吞掉异常，确保后续 PostProcessor 能继续执行</summary>
    /// <param name="context">消息流上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步任务</returns>
    Task ProcessAsync(IMessageFlowContext context, CancellationToken cancellationToken);
}
