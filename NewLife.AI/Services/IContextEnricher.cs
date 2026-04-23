namespace NewLife.AI.Services;

/// <summary>上下文增强器。在主流程 Prepare 阶段被调用，用于丰富 <see cref="IMessageFlowContext"/>，
/// 典型职责：注入记忆片段、附加文档解析结果、插入三明治引导提示、应用上下文压缩等</summary>
/// <remarks>
/// <para>
/// 多个 Enricher 按 <see cref="Order"/> 升序依次执行，形成<b>责任链</b>。
/// 各 Enricher 无需感知彼此，只读/写需要的 <see cref="IMessageFlowContext"/> 字段或 <see cref="IMessageFlowContext.Items"/>。
/// </para>
/// <para>
/// StarChat 专属能力（CompactionService、三明治、追问检测、NociceptionService 等）应以独立 Enricher 形式注册到 StarChat DI，
/// ChatAI 社区版 Enricher 放在 ChatAI 自身 DI 装配。
/// </para>
/// </remarks>
public interface IContextEnricher
{
    /// <summary>执行顺序。数值越小越先执行；建议按 100 / 200 / 300 ... 的步长留出插入空间</summary>
    Int32 Order { get; }

    /// <summary>是否适用于当前请求。返回 false 时跳过 <see cref="EnrichAsync"/>，避免不必要的构建开销</summary>
    /// <param name="context">消息流上下文</param>
    /// <returns>true 表示需要执行</returns>
    Boolean IsApplicable(IMessageFlowContext context);

    /// <summary>执行增强逻辑。可修改 <see cref="IMessageFlowContext.PipelineContext"/> / <see cref="IMessageFlowContext.Items"/>，
    /// 或追加上下文消息、调整系统提示词</summary>
    /// <param name="context">消息流上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步任务</returns>
    Task EnrichAsync(IMessageFlowContext context, CancellationToken cancellationToken);
}
