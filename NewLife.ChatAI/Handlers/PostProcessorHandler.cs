using System.Runtime.CompilerServices;
using NewLife.AI.Models;
using NewLife.AI.Services;
using NewLife.ChatAI.Services;
using NewLife.Log;

namespace NewLife.ChatAI.Handlers;

/// <summary>消息流后处理器适配器。把现有 <see cref="IMessageFlowPostProcessor"/> 集合包装为单个 <see cref="IChatHandler"/>，
/// 在事前透传 next，事后按 <see cref="IMessageFlowPostProcessor.Order"/> 升序依次执行后处理逻辑</summary>
/// <remarks>
/// <para>过渡桥接：让 Step 5 改写 MessageFlow 主流程时，<b>无需立刻重写</b> StarChat 的 QuotaPostProcessor 等实现，
/// 通过本适配器接入新 Handler 链。</para>
/// <para>异常策略：原 <see cref="IMessageFlowPostProcessor"/> 约定"实现者自行吞掉异常"。
/// 本适配器追加一层防御性 try/catch 防止某个 PostProcessor 抛异常中断后续，与 <see cref="IChatHandler"/>
/// "不捕获异常"约定的差异由旧契约决定，待 Step 8 原生化后回归统一规范。</para>
/// </remarks>
/// <param name="postProcessors">DI 注入的全部 PostProcessor（按 Order 升序排序后使用）</param>
/// <param name="tracer">追踪器</param>
/// <param name="log">日志</param>
public class PostProcessorHandler(IEnumerable<IMessageFlowPostProcessor> postProcessors, ITracer? tracer = null, ILog? log = null) : IChatHandler
{
    private readonly IReadOnlyList<IMessageFlowPostProcessor> _postProcessors = postProcessors.OrderBy(p => p.Order).ToArray();

    /// <inheritdoc/>
    public async IAsyncEnumerable<ChatStreamEvent> InvokeAsync(IChatContext context, ChatHandlerDelegate next, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var ev in next(cancellationToken).ConfigureAwait(false))
        {
            yield return ev;
        }

        if (context is not IMessageFlowContext flow) yield break;

        using var span = tracer?.NewSpan("handler:PostProcessors", new { count = _postProcessors.Count });
        foreach (var processor in _postProcessors)
        {
            if (!processor.IsApplicable(flow)) continue;

            using var subSpan = tracer?.NewSpan($"postProcessor:{processor.GetType().Name}");
            try
            {
                await processor.ProcessAsync(flow, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // 兼容旧契约：单个 PostProcessor 异常被吞掉，不影响后续与主流程返回
                log?.Error("后处理器 {0} 执行失败: {1}", processor.GetType().Name, ex.Message);
            }
        }
    }
}
