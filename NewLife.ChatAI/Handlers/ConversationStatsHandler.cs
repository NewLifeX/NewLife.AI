using NewLife.Log;

namespace NewLife.ChatAI.Handlers;

/// <summary>会话统计处理器。事后累加 Token、消息数、最后消息时间到 Conversation 实体并保存</summary>
/// <param name="tracer">追踪器</param>
public class ConversationStatsHandler(ITracer? tracer) : IChatHandler
{
    /// <inheritdoc/>
    public Task OnBefore(IChatContext context, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task OnAfter(IChatContext context, CancellationToken cancellationToken)
    {
        if (context is not MessageFlowContext flow) return Task.CompletedTask;
        using var span = tracer?.NewSpan("handler:ConversationStats");

        var conversation = flow.Conversation;
        if (conversation == null) return Task.CompletedTask;

        conversation.LastMessageTime = DateTime.Now;
        conversation.MessageCount = DbChatMessage.CountByConversationId(conversation.Id);
        if (flow.Usage != null)
        {
            conversation.InputTokens += flow.Usage.InputTokens;
            conversation.OutputTokens += flow.Usage.OutputTokens;
            conversation.TotalTokens += flow.Usage.TotalTokens;
            conversation.ElapsedMs += flow.Usage.ElapsedMs;
        }
        if (flow.ModelConfig != null) conversation.ModelName = flow.ModelConfig.Name;
        conversation.Update();

        return Task.CompletedTask;
    }
}
