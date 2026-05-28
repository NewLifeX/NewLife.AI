using NewLife.Log;

namespace NewLife.ChatAI.Handlers;

/// <summary>自学习处理器。属于知识进化层，事前注入记忆上下文，事后异步触发自学习分析</summary>
/// <remarks>
/// 已从 <c>LearningFilter</c> 迁移而来，统一纳入 IChatHandler 三段式调用链。
/// <list type="bullet">
/// <item><description>OnBefore：从 MemoryService 注入用户记忆到系统提示词</description></item>
/// <item><description>OnAfter：触发 ConversationAnalysisService 自学习分析（火焰即忘）</description></item>
/// </list>
/// 注册顺序：应位于 SkillActivation / TitleGeneration 之后，位于 UsageRecord / PersistMessage 之前，
/// 确保记忆上下文覆盖在技能提示词之后，同时分析时已有完整响应。
/// </remarks>
/// <param name="analysisService">对话分析与记忆服务</param>
/// <param name="chatSetting">配置</param>
/// <param name="log">日志</param>
[ChatHandlerOrder(110)]
public class LearningHandler(ConversationAnalysisService analysisService, ChatSetting chatSetting, ILog log) : ChatHandlerBase
{
    ///// <inheritdoc/>
    //public ChatHandlerCapabilities Capabilities => ChatHandlerCapabilities.Before | ChatHandlerCapabilities.After;

    /// <inheritdoc/>
    public override Task OnBefore(IChatContext context, CancellationToken cancellationToken)
    {
        if (!chatSetting.EnableAutoLearning) return Task.CompletedTask;

        var userId = context.UserId;
        if (userId > 0)
            InjectMemoryContext(context, userId);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task OnAfter(IChatContext context, CancellationToken cancellationToken)
    {
        if (!chatSetting.EnableAutoLearning) return Task.CompletedTask;
        if (context.HasError) return Task.CompletedTask;

        TriggerAnalysisFireAndForget(context, "Chat");
        return Task.CompletedTask;
    }

    #region 辅助

    /// <summary>将用户记忆注入到上下文消息的系统提示词中</summary>
    /// <param name="context">对话上下文</param>
    /// <param name="userId">用户编号</param>
    private void InjectMemoryContext(IChatContext context, Int32 userId)
    {
        try
        {
            var memoryContext = analysisService.MemoryService.BuildContextForUser(userId);
            if (memoryContext.IsNullOrEmpty()) return;

            context.SystemSegments.Add(memoryContext);
        }
        catch (Exception ex)
        {
            log?.Error("[Learning] 注入记忆上下文失败: {0}", ex.Message);
        }
    }

    /// <summary>触发自学习分析（火焰即忘）</summary>
    /// <param name="context">对话上下文</param>
    /// <param name="triggerReason">触发来源标识</param>
    private void TriggerAnalysisFireAndForget(IChatContext context, String triggerReason)
    {
        var userId = context.UserId;
        if (userId <= 0) return;

        var conversationId = context.Conversation?.Id ?? 0L;
        var requestMessages = context.ContextMessages.ToList();  // 捕获快照，避免外部修改

        var assistantText = context.ContentBuilder.ToString();
        if (assistantText.IsNullOrEmpty()) return;

        var response = new ChatResponse();
        response.Add(assistantText);
        response.Usage = context.Usage;

        _ = Task.Run(async () =>
        {
            try
            {
                await analysisService.AnalyzeAsync(userId, conversationId, requestMessages, response, triggerReason).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                log?.Error("[Learning] 自学习分析异步任务异常: {0}", ex.Message);
            }
        });
    }

    #endregion

    #region 日志
    #endregion
}
