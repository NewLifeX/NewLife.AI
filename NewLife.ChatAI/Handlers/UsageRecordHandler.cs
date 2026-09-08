namespace NewLife.ChatAI.Handlers;

/// <summary>用量入库处理器。事后通过 <see cref="UsageService"/> 记录 Token 使用、调用次数等度量</summary>
/// <param name="usageService">用量服务（可为 null）</param>
[ChatHandlerOrder(After = 9000)]
public class UsageRecordHandler(UsageService? usageService) : ChatHandlerBase, IChatHandlerScope
{
    /// <inheritdoc/>
    /// <remarks>用量记录适用于所有渠道</remarks>
    public ChatFlowSource SupportedSources => ChatFlowSource.All;

    /// <inheritdoc/>
    /// <remarks>用量属于核心运营能力，精简链模式下仍需执行</remarks>
    public ChatHandlerTier Tier => ChatHandlerTier.Core;

    /// <inheritdoc/>
    public override ChatHandlerCapabilities Capabilities => ChatHandlerCapabilities.After;

    ///// <inheritdoc/>
    //public Task OnBefore(IChatContext context, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc/>
    public override Task OnAfter(IChatContext context, CancellationToken cancellationToken)
    {
        if (usageService == null) return Task.CompletedTask;
        //if (context is not MessageFlowContext flow) return Task.CompletedTask;

        // 网关来源的用量由网关出口适配器（GatewayService2.RecordUsage，Source=Gateway）统一落库并扣减配额；
        // 网关流无持久化消息实体（messageId=0，无幂等保护），此处再以 Source=Chat 记录会造成 UsageRecord 双写
        if (context.Source == ChatFlowSource.Gateway) return Task.CompletedTask;

        // 主流程用量（Source=Chat）。渠道来源暂由本 Handler 记录（渠道适配器侧暂无独立用量记录点）
        if (context.Usage != null)
            usageService.Record(context.Conversation, context.AssistantMessage, null, context.ModelConfig, context.Usage, "Chat");

        //// 子流程用量（Sandwich/Title 等）——Before 阶段同步子流程积累的用量，独立落库
        //foreach (var (source, subUsage) in context.SubFlowUsages)
        //{
        //    usageService.Record(context.Conversation, context.AssistantMessage, context.ModelConfig, subUsage, source);
        //}

        return Task.CompletedTask;
    }
}
