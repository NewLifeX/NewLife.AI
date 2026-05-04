using NewLife.Log;
using NewLife.Serialization;

namespace NewLife.ChatAI.Handlers;

/// <summary>消息持久化处理器。事后将 <see cref="IChatContext"/> 收集器中的内容写入 DbChatMessage / Conversation</summary>
/// <remarks>
/// <para>事后职责：从 <see cref="IChatContext.ContentBuilder"/>、<see cref="IChatContext.ThinkingBuilder"/>、
/// <see cref="IChatContext.ToolCalls"/>、<see cref="IChatContext.Usage"/> 等收集字段读取最终结果，
/// 写入 <c>flow.AssistantMessage</c> / <c>flow.UserMessage</c> / <c>flow.Conversation</c> 实体。</para>
/// <para>注意：会话级 Token 累加由 <c>ConversationStatsHandler</c> 负责，UsageService.Record 由 <c>UsageRecordHandler</c> 负责。</para>
/// </remarks>
/// <param name="tracer">追踪器</param>
public class PersistMessageHandler(ITracer? tracer) : IChatHandler
{
    /// <inheritdoc/>
    public Task OnBefore(IChatContext context, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task OnAfter(IChatContext context, CancellationToken cancellationToken)
    {
        if (context is not MessageFlowContext flow) return Task.CompletedTask;
        using var span = tracer?.NewSpan("handler:PersistMessage");

        var assistantMsg = flow.AssistantMessage;
        if (assistantMsg == null) return Task.CompletedTask;

        // 写入消息内容
        assistantMsg.Content = flow.ContentBuilder.Length > 0 ? flow.ContentBuilder.ToString() : null;
        if (flow.ThinkingBuilder.Length > 0)
            assistantMsg.ThinkingContent = flow.ThinkingBuilder.ToString();
        if (flow.ToolCalls.Count > 0)
        {
            assistantMsg.ToolCalls = flow.ToolCalls.ToJson();
            assistantMsg.ToolNames = String.Join(",", flow.ToolCalls.Select(t => t.Name).Distinct(StringComparer.OrdinalIgnoreCase));
        }

        // 用量与请求参数（不记录到 UsageService，仅写入消息字段）
        ApplyUsageToMessage(assistantMsg, flow.Usage, flow.HasError, flow.DeferredError?.Error);
        ApplyRequestParams(assistantMsg, flow.ModelConfig, flow);
        assistantMsg.Update();

        // 用户消息追加技能名称与可用工具
        var userMessage = flow.UserMessage;
        if (userMessage != null)
        {
            var skillNames = new HashSet<String>(flow.ResolvedSkillNames, StringComparer.OrdinalIgnoreCase);
            var skillName = flow["SkillName"] as String;
            if (flow.SkillId > 0 && !skillName.IsNullOrEmpty())
                skillNames.Add(skillName);

            if (skillNames.Count > 0)
                userMessage.SkillNames = String.Join(",", skillNames);
            if (flow.AvailableToolNames.Count > 0)
                userMessage.ToolNames = String.Join(",", flow.AvailableToolNames);
            userMessage.Update();
        }

        return Task.CompletedTask;
    }

    /// <summary>将用量统计写入 AI 回复消息实体（不保存）</summary>
    /// <param name="msg">消息实体</param>
    /// <param name="usage">用量统计</param>
    /// <param name="hasError">是否有错误</param>
    /// <param name="errorDetail">错误详情</param>
    private static void ApplyUsageToMessage(DbChatMessage msg, UsageDetails? usage, Boolean hasError, String? errorDetail = null)
    {
        if (msg.Content.IsNullOrEmpty())
        {
            if (hasError)
                msg.Content = errorDetail.IsNullOrEmpty() ? "[生成失败]" : $"[生成失败] {errorDetail}";
            else if (msg.ThinkingContent.IsNullOrEmpty())
                msg.Content = "[已中断]";
        }
        else if (hasError && !errorDetail.IsNullOrEmpty())
        {
            msg.Content += $"\n\n[错误] {errorDetail}";
        }
        if (usage != null)
        {
            msg.InputTokens = usage.InputTokens;
            msg.OutputTokens = usage.OutputTokens;
            msg.TotalTokens = usage.TotalTokens;
            msg.ElapsedMs = usage.ElapsedMs;
        }
    }

    /// <summary>将请求参数写入消息实体（不保存）</summary>
    /// <param name="msg">消息实体</param>
    /// <param name="modelConfig">模型配置</param>
    /// <param name="context">对话上下文</param>
    private static void ApplyRequestParams(DbChatMessage msg, ModelConfig modelConfig, IChatContext context)
    {
        msg.ModelName = modelConfig.Code;
        if (context.MaxTokens > 0) msg.MaxTokens = context.MaxTokens;
        if (context.Temperature != null) msg.Temperature = context.Temperature.Value;
        if (!context.FinishReason.IsNullOrEmpty()) msg.FinishReason = context.FinishReason;
    }
}
