using System.Runtime.CompilerServices;
using NewLife.AI.Models;
using NewLife.AI.Services;
using NewLife.ChatAI.Services;
using NewLife.Log;

namespace NewLife.ChatAI.Handlers;

/// <summary>核心 LLM 调用处理器。链路终点，负责实际调用模型并发出流式事件，<b>不调用 next</b></summary>
/// <remarks>
/// <para>当前作为薄包装委托既有 <see cref="IChatPipeline.StreamAsync"/>；
/// Step 5 时将其内部 450 行实现内联到本类，并删除 <c>ChatPipeline</c>。</para>
/// <para>由于是终点 Handler，链中任何 Handler 想短路（如缓存命中）只需不调用 <c>next</c> 即可绕过本调用。</para>
/// </remarks>
/// <param name="pipeline">既有对话管道（提供 StreamAsync 实现）</param>
/// <param name="tracer">追踪器</param>
public class LlmCoreHandler(IChatPipeline pipeline, ITracer tracer) : IChatHandler
{
    /// <inheritdoc/>
    public async IAsyncEnumerable<ChatStreamEvent> InvokeAsync(IChatContext context, ChatHandlerDelegate next, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var span = tracer?.NewSpan("handler:LlmCore", new { messages = context.ContextMessages.Count });

        // next 不调用：本 Handler 是链路终点
        _ = next;

        // 桥接 IChatContext → ChatPipelineContext（Step 5 后统一为 IChatContext，无需桥接）
        if (context is not MessageFlowContext flow)
            throw new InvalidOperationException($"{nameof(LlmCoreHandler)} 当前实现要求上下文为 {nameof(MessageFlowContext)}");

        await foreach (var ev in pipeline.StreamAsync(context.ContextMessages, context.ModelConfig, context.ThinkingMode, flow.PipelineContext, cancellationToken).ConfigureAwait(false))
        {
            yield return ev;
        }

        // 同步管道执行结果到 IChatContext（Step 5 后无需）
        context.SystemPrompt = flow.PipelineContext.SystemPrompt;
        foreach (var t in flow.PipelineContext.AvailableToolNames) context.AvailableToolNames.Add(t);
        context.MaxTokens = flow.PipelineContext.MaxTokens;
        context.Temperature = flow.PipelineContext.Temperature;
        context.FinishReason = flow.PipelineContext.FinishReason;
    }
}
