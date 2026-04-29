using NewLife.ChatAI.Services;
using NewLife.Log;

namespace NewLife.ChatAI.Handlers;

/// <summary>用量入库处理器。事后通过 <see cref="UsageService"/> 记录 Token 使用、调用次数等度量</summary>
/// <param name="usageService">用量服务（可为 null）</param>
/// <param name="tracer">追踪器</param>
public class UsageRecordHandler(UsageService? usageService, ITracer? tracer) : IChatHandler
{
    /// <inheritdoc/>
    public Task OnBefore(IChatContext context, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task OnAfter(IChatContext context, CancellationToken cancellationToken)
    {
        if (usageService == null) return Task.CompletedTask;
        if (context is not MessageFlowContext flow) return Task.CompletedTask;
        if (flow.Usage == null) return Task.CompletedTask;

        using var span = tracer?.NewSpan("handler:UsageRecord");
        usageService.Record(flow.Conversation, flow.AssistantMessage.Id, flow.ModelConfig.Id, flow.Usage, "Chat");
        return Task.CompletedTask;
    }
}
