using NewLife.AI.Filters;
using NewLife.AI.Models;
using NewLife.ChatAI.Services;
using NewLife.Log;

namespace NewLife.ChatAI.Filters;

/// <summary>自学习过滤器。属于知识进化层，负责在对话前注入记忆上下文、对话后异步触发自学习分析</summary>
/// <remarks>
/// 在 DI 中注册为单例后，通过 ChatClientBuilder.UseFilters(learningFilter) 接入管道：
/// <code>
/// var pipelineClient = new ChatClientBuilder(rawClient)
///     .UseFilters(learningFilter)
///     .Build();
/// </code>
/// 生命周期：
/// <list type="bullet">
/// <item><description>OnChatAsync before：从 MemoryService 注入用户记忆到系统提示词</description></item>
/// <item><description>OnChatAsync after / OnStreamCompletedAsync：触发 ConversationAnalysisService 自学习分析（火焰即忘）</description></item>
/// </list>
/// 通过 ChatOptions.UserId / ChatOptions.ConversationId 传入用户上下文，FilteredChatClient 会自动复制到 context.UserId / context.ConversationId。
/// </remarks>
/// <remarks>实例化自学习过滤器</remarks>
/// <param name="analysisService">对话分析与记忆服务</param>
/// <param name="chatSetting">配置</param>
/// <param name="log">日志</param>
public class LearningFilter(ConversationAnalysisService analysisService, ChatSetting chatSetting, ILog log) : IChatFilter
{
    #region 方法

    /// <summary>执行对话过滤逻辑。before 阶段注入记忆；after 阶段（非流式）触发自学习分析</summary>
    /// <param name="context">过滤器上下文（context.UserId / context.ConversationId 由调用方通过 ExtraData 传入）</param>
    /// <param name="next">下一处理器</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task OnChatAsync(ChatFilterContext context, Func<ChatFilterContext, CancellationToken, Task> next, CancellationToken cancellationToken = default)
    {
        // 全局自学习开关关闭时跳过记忆注入和分析
        if (!chatSetting.EnableAutoLearning)
        {
            await next(context, cancellationToken).ConfigureAwait(false);
            return;
        }

        // before 阶段：向系统提示词注入用户记忆上下文（知识进化层能力）
        var userId = context.UserId.ToInt();
        if (userId > 0)
            InjectMemoryContext(context, userId);

        // 调用后续过滤器及内层客户端
        await next(context, cancellationToken).ConfigureAwait(false);

        // after 阶段（非流式）：响应返回后异步触发自学习分析
        // 流式路径由 OnStreamCompletedAsync 处理，此处 Response 为 null 时直接跳过
        if (context.Response == null || context.IsStreaming) return;
        TriggerAnalysisAsync(context, "Chat");
    }

    /// <summary>流式对话完成后的回调。由 FilteredChatClient 在流结束后以"火焰即忘"方式触发</summary>
    /// <param name="context">过滤器上下文（Response 包含聚合的 Model/Usage 摘要）</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task OnStreamCompletedAsync(ChatFilterContext context, CancellationToken cancellationToken = default)
    {
        if (!chatSetting.EnableAutoLearning) return Task.CompletedTask;

        TriggerAnalysisAsync(context, "Chat");
        return Task.CompletedTask;
    }

    #endregion

    #region 辅助

    /// <summary>将用户记忆注入到请求的系统提示词中</summary>
    /// <param name="context">过滤器上下文</param>
    /// <param name="userId">用户编号</param>
    private void InjectMemoryContext(ChatFilterContext context, Int32 userId)
    {
        try
        {
            var memoryContext = analysisService.MemoryService.BuildContextForUser(userId);
            if (memoryContext.IsNullOrEmpty()) return;

            var messages = context.Request.Messages;
            var systemMsg = messages.FirstOrDefault(m => m.Role == "system");
            if (systemMsg != null)
            {
                var existingContent = systemMsg.Content as String ?? String.Empty;
                systemMsg.Content = existingContent + "\n\n" + memoryContext;
            }
            else
            {
                messages.Insert(0, new ChatMessage { Role = "system", Content = memoryContext });
            }
        }
        catch (Exception ex)
        {
            log?.Error("注入记忆上下文失败: {0}", ex.Message);
        }
    }

    /// <summary>触发自学习分析（火焰即忘）</summary>
    /// <param name="context">过滤器上下文</param>
    /// <param name="triggerReason">触发来源标识</param>
    private void TriggerAnalysisAsync(ChatFilterContext context, String triggerReason)
    {
        var uid = context.UserId.ToInt();
        if (uid <= 0) return;

        var conversationId = context.ConversationId.ToLong();
        var requestMessages = context.Request.Messages.ToList();  // 捕获快照，避免外部修改
        var response = context.Response;
        if (response == null) return;

        _ = Task.Run(async () =>
        {
            try
            {
                await analysisService.AnalyzeAsync(uid, conversationId, requestMessages, response, triggerReason).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                log?.Error("自学习分析异步任务异常: {0}", ex.Message);
            }
        });
    }

    #endregion
}
