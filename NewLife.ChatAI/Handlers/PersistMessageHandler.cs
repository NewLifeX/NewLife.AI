using NewLife.AI.Interfaces;
using NewLife.Serialization;

namespace NewLife.ChatAI.Handlers;

/// <summary>消息持久化处理器。事后将 <see cref="IChatContext"/> 收集器中的内容写入 DbChatMessage / Conversation</summary>
/// <remarks>
/// <para>事后职责：从 <see cref="IChatContext.ContentBuilder"/>、<see cref="IChatContext.ThinkingBuilder"/>、
/// <see cref="IChatContext.ToolCalls"/>、<see cref="IChatContext.Usage"/> 等收集字段读取最终结果，
/// 写入 <c>flow.AssistantMessage</c> / <c>flow.UserMessage</c> / <c>flow.Conversation</c> 实体。</para>
/// <para>注意：UsageService.Record 由 <c>UsageRecordHandler</c> 负责，本处理器仅写入消息/会话字段。</para>
/// </remarks>
[ChatHandlerOrder(9999)]
public class PersistMessageHandler(ChatSetting setting) : ChatHandlerBase, IChatHandlerScope
{
    /// <inheritdoc/>
    /// <remarks>持久化处理器适用于所有来源</remarks>
    public ChatFlowSource SupportedSources => ChatFlowSource.All;

    /// <inheritdoc/>
    /// <remarks>持久化是核心能力，始终保留在链中</remarks>
    public ChatHandlerTier Tier => ChatHandlerTier.Core;

    ///// <inheritdoc/>
    //public ChatHandlerCapabilities Capabilities => ChatHandlerCapabilities.Before | ChatHandlerCapabilities.After;

    /// <inheritdoc/>
    public override Task OnBefore(IChatContext context, CancellationToken cancellationToken)
    {
        // 未开启持久化时跳过数据库写入
        if (context.Source.HasFlag(ChatFlowSource.Web) ||
            context.Source.HasFlag(ChatFlowSource.Gateway) && setting.EnableGatewayRecording
#if STARCHAT
            || context.Source.HasFlag(ChatFlowSource.Channel) && setting.EnableChannelPersist
#endif
            )
        {
            // 用户消息
            if (context.UserMessage is DbChatMessage userMessage)
            {
                // 提取系统提示词：首个 system 消息内容 + 尚未 flush 的 SystemSegments 片段
                // （FlushSystemSegments 在所有 OnBefore 完成后才执行，此时 SystemSegments 已由各处理器注入内容）
                var systemContent = context.ContextMessages.FirstOrDefault(m => m.Role == "system")?.Content as String;
                if (context.SystemSegments.Count > 0)
                {
                    var extra = String.Join("\n\n", context.SystemSegments);
                    systemContent = systemContent.IsNullOrEmpty() ? extra : systemContent + "\n\n" + extra;
                }
                userMessage.ThinkingContent = systemContent;
                userMessage.ToolNames = context.AvailableToolNames?.Join();
                userMessage.Update();
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task OnAfter(IChatContext context, CancellationToken cancellationToken)
    {
        // 未开启持久化时跳过数据库写入
        if (context.Source.HasFlag(ChatFlowSource.Web) ||
            context.Source.HasFlag(ChatFlowSource.Gateway) && setting.EnableGatewayRecording
#if STARCHAT
            || context.Source.HasFlag(ChatFlowSource.Channel) && setting.EnableChannelPersist
#endif
            )
        {

            // 助手消息
            if (context.AssistantMessage is DbChatMessage assistantMsg)
            {
                // 写入消息内容
                assistantMsg.Content = context.ContentBuilder.ToString();
                assistantMsg.ThinkingContent = context.ThinkingBuilder.ToString();
                var toolCalls = context.ToolCalls;
                if (toolCalls.Count > 0)
                {
                    assistantMsg.ToolCalls = toolCalls.ToJson();
                    assistantMsg.ToolNames = toolCalls.Select(t => t.Name).Distinct().Join();
                }

                // 用量与请求参数（不记录到 UsageService，仅写入消息字段）
                ApplyUsageToMessage(assistantMsg, context.Usage, context.HasError, (context as MessageFlowContext)?.DeferredError?.Error);
                ApplyRequestParams(assistantMsg, context.ModelConfig, context);
                assistantMsg.Update();
            }

            // 用户消息
            if (context.UserMessage is DbChatMessage userMessage)
            {
                userMessage.ThinkingContent = context.ContextMessages?.FirstOrDefault(m => m.Role == "system")?.Content as String;
                userMessage.ToolNames = context.AvailableToolNames?.Join();
                userMessage.Update();
            }

            // 会话
            if (context.Conversation is Conversation conversation)
            {
                conversation.LastMessageTime = DateTime.Now;
                conversation.MessageCount = DbChatMessage.CountByConversationId(conversation.Id);
                if (context.Usage != null)
                {
                    conversation.InputTokens += context.Usage.InputTokens;
                    conversation.OutputTokens += context.Usage.OutputTokens;
                    conversation.TotalTokens += context.Usage.TotalTokens;
                    conversation.ElapsedMs += context.Usage.ElapsedMs;
                }
                if (context.ModelConfig != null) conversation.ModelName = context.ModelConfig.Name;

                // 聚合会话级技能列表：合并本轮用户消息 SkillNames 与会话已有 SkillNames，去重
                MergeConversationSkillNames(context, conversation);

                conversation.Update();
            }
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
            {
                // 错误时优先保留已有部分推理内容（ThinkingContent 已在 OnAfter 中从 ThinkingBuilder 写入），
                // 避免只用 "[生成失败]" 覆盖掉部分数据；Content 为空但 ThinkingContent 有值时保留推理记录
                var fallback = errorDetail.IsNullOrEmpty() ? "[生成失败]" : $"[生成失败] {errorDetail}";
                msg.Content = msg.ThinkingContent.IsNullOrEmpty() ? fallback : fallback;
            }
            else if (msg.ThinkingContent.IsNullOrEmpty())
                msg.Content = "[已中断]";
        }
        else if (hasError && !errorDetail.IsNullOrEmpty())
        {
            msg.Content += $"\n\n[错误] {errorDetail}";
        }

        // 仅当 LLM 返回有效 Token 数据时才写入，避免用全零 UsageDetails 覆盖消息已有数据
        // （全零 UsageDetails 通常来自异常路径中 lastUsage ??= new UsageDetails() 创建的虚假对象）
        if (usage is { TotalTokens: > 0 })
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
    private static void ApplyRequestParams(IChatMessage msg, IModelConfig modelConfig, IChatContext context)
    {
        msg.ModelName = modelConfig.Code;
        if (context.Options.MaxTokens > 0) msg.MaxTokens = context.Options.MaxTokens.Value;
        if (context.Options.Temperature != null) msg.Temperature = context.Options.Temperature.Value;
        if (!context.FinishReason.IsNullOrEmpty()) msg.FinishReason = context.FinishReason;
    }

    /// <summary>合并本轮消息的技能到会话级技能列表，去重</summary>
    private static void MergeConversationSkillNames(IChatContext context, Conversation conversation)
    {
        //// 收集本轮用户消息的 SkillNames
        //var newSkills = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
        //if (context.UserMessage is DbChatMessage userMsg && !userMsg.SkillNames.IsNullOrEmpty())
        //{
        //    foreach (var s in userMsg.SkillNames!.Split(','))
        //    {
        //        var trimmed = s.Trim();
        //        if (!trimmed.IsNullOrEmpty()) newSkills.Add(trimmed);
        //    }
        //}

        //if (newSkills.Count == 0) return;

        //// 合并会话已有 SkillNames
        //if (!conversation.SkillNames.IsNullOrEmpty())
        //{
        //    foreach (var s in conversation.SkillNames!.Split(','))
        //    {
        //        var trimmed = s.Trim();
        //        if (!trimmed.IsNullOrEmpty()) newSkills.Add(trimmed);
        //    }
        //}

        //conversation.SkillNames = String.Join(",", newSkills);
        if (conversation.SkillName.IsNullOrEmpty() && context.UserMessage is DbChatMessage userMsg)
        {
            conversation.SkillName = userMsg.SkillNames!.Split(',').FirstOrDefault();
        }
    }
}
