using NewLife.AI.Models;
using NewLife.AI.Services;
using NewLife.Log;

namespace NewLife.ChatAI.Handlers;

/// <summary>系统提示词处理器。事前向 system 消息注入会话级提示词；事后无操作</summary>
/// <remarks>
/// <para>基类实现简化版：当上下文中尚无 system 消息时不主动构造（由 <see cref="MessageFlow.BuildSystemMessage"/> 在 BuildContext 阶段已完成）。
/// 派生类（StarChatSystemPromptHandler）可覆盖 <see cref="BuildSystemPromptAsync"/> 注入商用增强（部门信息、三明治等）。</para>
/// <para>不再承担技能注入与 @ 工具引用解析（已迁出至 <c>SkillActivationHandler</c>）。</para>
/// </remarks>
/// <param name="tracer">追踪器</param>
public class SystemPromptHandler(ITracer? tracer) : IChatHandler
{
    /// <summary>追踪器（供派生类访问）</summary>
    protected readonly ITracer? Tracer = tracer;

    /// <inheritdoc/>
    public virtual async Task OnBefore(IChatContext context, CancellationToken cancellationToken)
    {
        using var span = Tracer?.NewSpan("handler:SystemPrompt");

        var addition = await BuildSystemPromptAsync(context, cancellationToken).ConfigureAwait(false);
        if (!addition.IsNullOrWhiteSpace())
        {
            var systemMsg = context.ContextMessages.FirstOrDefault(m => m.Role == "system");
            if (systemMsg != null)
            {
                var existing = systemMsg.Content as String ?? String.Empty;
                systemMsg.Content = existing.Length > 0 ? existing + "\n\n" + addition : addition;
            }
            else
            {
                context.ContextMessages.Insert(0, new AiChatMessage { Role = "system", Content = addition });
            }
        }

        // 记录最终 system 文本，供持久化使用
        var finalSystemMsg = context.ContextMessages.FirstOrDefault(m => m.Role == "system");
        context.SystemPrompt = finalSystemMsg?.Content as String;
    }

    /// <inheritdoc/>
    public virtual Task OnAfter(IChatContext context, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>构建本 Handler 要追加到 system 消息的提示词增强部分。基类返回 null（无增强）。
    /// 派生类可覆盖以注入商用专属内容</summary>
    /// <param name="context">对话上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>要追加的文本，null/空表示不追加</returns>
    protected virtual Task<String?> BuildSystemPromptAsync(IChatContext context, CancellationToken cancellationToken) => Task.FromResult<String?>(null);
}
