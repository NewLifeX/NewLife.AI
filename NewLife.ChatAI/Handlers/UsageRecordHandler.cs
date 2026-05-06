namespace NewLife.ChatAI.Handlers;

/// <summary>用量入库处理器。事后通过 <see cref="UsageService"/> 记录 Token 使用、调用次数等度量</summary>
/// <param name="usageService">用量服务（可为 null）</param>
[ChatHandlerOrder(After = 9000)]
public class UsageRecordHandler(UsageService? usageService) : IChatHandler
{
    /// <inheritdoc/>
    public ChatHandlerCapabilities Capabilities => ChatHandlerCapabilities.After;

    /// <inheritdoc/>
    public Task OnBefore(IChatContext context, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task OnAfter(IChatContext context, CancellationToken cancellationToken)
    {
        if (usageService == null) return Task.CompletedTask;
        //if (context is not MessageFlowContext flow) return Task.CompletedTask;

        // 主流程用量（Source=Chat）
        if (context.Usage != null)
            usageService.Record(context.Conversation, context.AssistantMessage, context.ModelConfig, context.Usage, "Chat");

        // 子流程用量（Sandwich/Title 等）——Before 阶段同步子流程积累的用量，独立落库
        foreach (var (source, subUsage) in context.SubFlowUsages)
        {
            usageService.Record(context.Conversation, context.AssistantMessage, context.ModelConfig, subUsage, source);
        }

        return Task.CompletedTask;
    }
}
