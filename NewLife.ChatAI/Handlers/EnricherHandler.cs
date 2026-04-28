using System.Runtime.CompilerServices;
using NewLife.AI.Models;
using NewLife.AI.Services;
using NewLife.ChatAI.Services;
using NewLife.Log;

namespace NewLife.ChatAI.Handlers;

/// <summary>上下文增强器适配器。把现有 <see cref="IContextEnricher"/> 集合包装为单个 <see cref="IChatHandler"/>，
/// 在事前阶段按 <see cref="IContextEnricher.Order"/> 升序依次执行，事后透传 next 事件流</summary>
/// <remarks>
/// <para>过渡桥接：让 Step 5 改写 MessageFlow 主流程时，<b>无需逐个重写</b> StarChat 的 4 个 Enricher
/// （EarlyTruncation / CaseInjection / Sandwich / Compaction），直接通过本适配器接入新 Handler 链。</para>
/// <para>异常策略：与 <see cref="IChatHandler"/> 保持一致，<b>不捕获</b>，由消息流入口统一处理。</para>
/// </remarks>
/// <param name="enrichers">DI 注入的全部 Enricher（按 Order 升序排序后使用）</param>
/// <param name="tracer">追踪器</param>
public class EnricherHandler(IEnumerable<IContextEnricher> enrichers, ITracer? tracer = null) : IChatHandler
{
    private readonly IReadOnlyList<IContextEnricher> _enrichers = enrichers.OrderBy(e => e.Order).ToArray();

    /// <inheritdoc/>
    public async IAsyncEnumerable<ChatStreamEvent> InvokeAsync(IChatContext context, ChatHandlerDelegate next, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (context is not IMessageFlowContext flow)
            throw new InvalidOperationException($"{nameof(EnricherHandler)} 当前实现要求上下文同时实现 {nameof(IMessageFlowContext)}");

        using (var span = tracer?.NewSpan("handler:Enrichers", new { count = _enrichers.Count }))
        {
            foreach (var enricher in _enrichers)
            {
                if (!enricher.IsApplicable(flow)) continue;

                using var subSpan = tracer?.NewSpan($"enricher:{enricher.GetType().Name}");
                await enricher.EnrichAsync(flow, cancellationToken).ConfigureAwait(false);
            }
        }

        await foreach (var ev in next(cancellationToken).ConfigureAwait(false))
        {
            yield return ev;
        }
    }
}
