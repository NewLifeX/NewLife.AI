using NewLife.AI.Services;
using NewLife.AI.Tools;
using NewLife.ChatAI.Services;
using NewLife.ChatAI.Tools;
using NewLife.Log;

namespace NewLife.ChatAI.Handlers;

/// <summary>技能激活处理器。事前解析消息中的技能引用与 @ 工具引用，事后记录技能使用次数</summary>
/// <remarks>
/// <para>事前：从 <c>flow["NewSkillCode"]</c>（请求显式指定）或 <c>flow.SkillId</c>（会话已绑定）解析当前激活技能，
/// 注入技能 Prompt 到 system 消息，匹配 NativeTool/MCP 触发词填充 <see cref="IChatContext.SelectedTools"/>。</para>
/// <para>事后：当 <c>SkillId &gt; 0</c> 且未短路时调用 <see cref="SkillService.RecordUsage"/> 累加技能使用计数。</para>
/// </remarks>
/// <param name="toolProviders">工具提供者集合（用于 MCP 触发词匹配）</param>
/// <param name="skillService">技能服务（可为 null）</param>
/// <param name="tracer">追踪器</param>
public class SkillActivationHandler(
    IEnumerable<IToolProvider> toolProviders,
    SkillService? skillService,
    ITracer? tracer) : IChatHandler
{
    /// <inheritdoc/>
    public Task OnBefore(IChatContext context, CancellationToken cancellationToken)
    {
        if (skillService == null) return Task.CompletedTask;
        using var span = tracer?.NewSpan("handler:SkillActivation");

        // 1. 处理请求中显式指定的技能切换（"none" 清除；其它编码切换；未指定则不变）
        if (context is MessageFlowContext flow && flow["RequestSkillCode"] is String skillCode && !skillCode.IsNullOrEmpty())
        {
            var conversation = flow.Conversation;
            if (skillCode.EqualIgnoreCase("none"))
            {
                if (conversation.SkillId != 0)
                {
                    conversation.SkillId = 0;
                    conversation.SkillName = null;
                    conversation.Update();
                }
                flow.SkillId = 0;
                flow["SkillName"] = null;
            }
            else
            {
                var skill = Skill.FindByCode(skillCode);
                if (skill != null && skill.Enable)
                {
                    if (conversation.SkillId != skill.Id)
                    {
                        conversation.SkillId = skill.Id;
                        conversation.SkillName = skill.Name;
                        conversation.Update();
                    }
                    flow.SkillId = skill.Id;
                    flow["SkillName"] = skill.Name;
                }
            }
        }

        var lastUserContent = context.ContextMessages.LastOrDefault(m => m.Role == "user")?.Content as String;

        // 派生类钩子：商用版可基于内容自动匹配技能
        ResolveSkillByContent(context, lastUserContent);

        // 触发词命中（NativeTool）
        var matchedNative = skillService.MatchNativeToolNamesByContent(lastUserContent);
        foreach (var n in matchedNative)
            context.SelectedTools.Add(n);

        // 触发词命中（MCP）
        foreach (var mcp in toolProviders.OfType<McpClientService>())
        {
            var matchedMcp = mcp.MatchToolNamesByContent(lastUserContent);
            foreach (var n in matchedMcp)
                context.SelectedTools.Add(n);
        }

        // 注入技能 Prompt
        var skillPrompt = skillService.BuildSkillPrompt(context.SkillId, lastUserContent, context.SelectedTools, context.ResolvedSkillNames);
        if (skillPrompt.IsNullOrWhiteSpace()) return Task.CompletedTask;

        var systemMsg = context.ContextMessages.FirstOrDefault(m => m.Role == "system");
        if (systemMsg != null)
        {
            var existing = systemMsg.Content as String ?? String.Empty;
            systemMsg.Content = skillPrompt.Trim() + (existing.Length > 0 ? "\n\n" + existing : String.Empty);
        }
        else
        {
            context.ContextMessages.Insert(0, new AiChatMessage { Role = "system", Content = skillPrompt.Trim() });
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task OnAfter(IChatContext context, CancellationToken cancellationToken)
    {
        if (skillService == null || context.SkillId <= 0 || context.UserId <= 0) return Task.CompletedTask;
        if (context.HasError) return Task.CompletedTask;
        skillService.RecordUsage(context.UserId, context.SkillId);
        return Task.CompletedTask;
    }

    /// <summary>派生类钩子：基于用户消息内容自动匹配技能。基类无操作</summary>
    /// <param name="context">对话上下文</param>
    /// <param name="lastUserContent">最后一条用户消息文本</param>
    protected virtual void ResolveSkillByContent(IChatContext context, String? lastUserContent) { }

    /// <summary>派生类访问 <see cref="SkillService"/> 实例</summary>
    protected SkillService? SkillServiceInstance => skillService;
}
