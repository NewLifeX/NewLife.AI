using NewLife.AI.Filters;
using NewLife.AI.Models;
using NewLife.ChatAI.Services;
using NewLife.Log;

namespace NewLife.ChatAI.Filters;

/// <summary>自学习过滤器。在对话完成后异步触发记忆提取，将对话内容输入自学习分析服务</summary>
/// <remarks>
/// 接入方式：在构建 FilteredChatClient 时注册本过滤器：
/// <code>
/// client.AsBuilder().UseFilters(new LearningFilter(analysisService)).Build();
/// </code>
/// 触发条件：ExtraData 中须包含键 "userId"(Int32) 和 "conversationId"(Int64)。
/// 自学习为完全异步的火焰即忘模式，不阻塞主对话响应。
/// </remarks>
public class LearningFilter : IChatFilter
{
    #region 属性
    private readonly ConversationAnalysisService _analysisService;
    private readonly ILog _log;

    /// <summary>ExtraData 中的用户 ID 键名</summary>
    public const String UserIdKey = "userId";

    /// <summary>ExtraData 中的会话 ID 键名</summary>
    public const String ConversationIdKey = "conversationId";
    #endregion

    #region 构造
    /// <summary>实例化自学习过滤器</summary>
    /// <param name="analysisService">对话分析服务</param>
    /// <param name="log">日志</param>
    public LearningFilter(ConversationAnalysisService analysisService, ILog log)
    {
        _analysisService = analysisService;
        _log = log;
    }
    #endregion

    #region 方法
    /// <summary>执行对话过滤逻辑。在响应返回后异步触发自学习分析</summary>
    /// <param name="context">过滤器上下文</param>
    /// <param name="next">下一处理器</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task OnChatAsync(ChatFilterContext context, Func<ChatFilterContext, CancellationToken, Task> next, CancellationToken cancellationToken = default)
    {
        // before 阶段：注入记忆上下文到系统提示词
        if (context.ExtraData.TryGetValue(UserIdKey, out var userIdObj) && userIdObj is Int32 userId && userId > 0)
        {
            try
            {
                var memoryContext = _analysisService.MemoryService.BuildContextForUser(userId);
                if (!memoryContext.IsNullOrEmpty())
                {
                    // 找到第一条 system 消息，追加记忆上下文；否则插入新的 system 消息
                    var messages = context.Request.Messages;
                    var systemMsg = messages.FirstOrDefault(m => m.Role == "system");
                    if (systemMsg != null)
                    {
                        var existingContent = systemMsg.Content as String ?? String.Empty;
                        systemMsg.Content = existingContent + "\n\n" + memoryContext;
                    }
                    else
                    {
                        messages.Insert(0, new NewLife.AI.Models.ChatMessage { Role = "system", Content = memoryContext });
                    }
                }
            }
            catch (Exception ex)
            {
                _log?.Error("注入记忆上下文失败: {0}", ex.Message);
            }
        }

        // 执行后续过滤器及内层客户端
        await next(context, cancellationToken).ConfigureAwait(false);

        // after 阶段：响应完成后，异步触发记忆提取（火焰即忘，不阻塞当前请求）
        if (context.Response == null) return;
        if (!context.ExtraData.TryGetValue(UserIdKey, out var uidObj)) return;
        if (uidObj is not Int32 uid || uid <= 0) return;

        context.ExtraData.TryGetValue(ConversationIdKey, out var cidObj);
        var conversationId = cidObj is Int64 cid ? cid : 0L;

        var requestMessages = context.Request.Messages;
        var response = context.Response;

        _ = Task.Run(async () =>
        {
            try
            {
                await _analysisService.AnalyzeAsync(uid, conversationId, requestMessages, response, "Chat").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log?.Error("自学习分析异步任务异常: {0}", ex.Message);
            }
        });
    }
    #endregion
}
