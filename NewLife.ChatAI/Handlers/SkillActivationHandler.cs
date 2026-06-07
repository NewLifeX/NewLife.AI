using NewLife.Collections;
using NewLife.Log;

namespace NewLife.ChatAI.Handlers;

/// <summary>技能激活处理器。事前解析消息中的技能引用，注入技能 Prompt 到 system 消息；事后记录技能使用次数</summary>
/// <remarks>
/// <para>事前：从 <c>flow["NewSkillCode"]</c>（请求显式指定）或 <c>flow.SkillId</c>（会话已绑定）解析当前激活技能，
/// 将技能 Prompt 加入 <see cref="IChatContext.SystemSegments"/> 待写入 system 消息，并列出技能目录。
/// 触发词命中选择工具由 <c>ToolContextHandler</c>（StarChat）处理并填充 <see cref="IChatContext.SelectedTools"/>。</para>
/// <para>事后：当 <c>SkillId &gt; 0</c> 且未短路时调用 <see cref="SkillService.RecordUsage"/> 累加技能使用计数。</para>
/// </remarks>
/// <param name="skillService">技能服务（可为 null）</param>
[ChatHandlerOrder(20)]
public class SkillActivationHandler(SkillService? skillService) : ChatHandlerBase
{
    ///// <inheritdoc/>
    //public override ChatHandlerCapabilities Capabilities => ChatHandlerCapabilities.Before | ChatHandlerCapabilities.After;

    /// <summary>派生类访问 <see cref="SkillService"/> 实例</summary>
    protected SkillService? SkillServiceInstance => skillService;

    /// <inheritdoc/>
    public override Task OnBefore(IChatContext context, CancellationToken cancellationToken)
    {
        if (skillService == null) return Task.CompletedTask;
        //using var span = tracer?.NewSpan("handler:SkillActivation");

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
                }
            }
        }

        var lastUserContent = context.ContextMessages.LastOrDefault(m => m.Role == "user")?.Content as String;

        // 基于用户消息内容自动匹配技能（StarChat 与 ChatAI 均支持）
        ResolveSkillByContent(context, lastUserContent);

        // 注入技能 Prompt（已选中工具由 ToolContextHandler 在上游填充）
        var skillNames = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
        var skillPrompt = skillService.BuildSkillPrompt(context.SkillId, lastUserContent, context.SelectedTools, skillNames);
        if (!skillPrompt.IsNullOrWhiteSpace())
        {
            context.SystemSegments.Add(skillPrompt.Trim());

            // 用户消息追加技能名称与可用工具
            if (context.UserMessage is DbChatMessage userMessage)
            {
                // 技能名可能是编码或者中文名，而列表里面基本都是 "code/name" 格式。
                var skillName = context.Conversation.SkillName;
                if (!skillName.IsNullOrEmpty() && !skillNames.Any(e => e == skillName || e.StartsWith(skillName + "/") || e.EndsWith("/" + skillName)))
                    skillNames.Add(skillName);

                if (skillNames.Count > 0)
                    userMessage.SkillNames = String.Join(",", skillNames);
                userMessage.Update();

                DefaultSpan.Current?.AppendTag(userMessage.SkillNames!);
            }
        }

        // 技能目录：将所有技能以 code/name + 描述形式注入，供模型参考
        var catalog = BuildSkillCatalog(skillService);
        if (!catalog.IsNullOrWhiteSpace())
            context.SystemSegments.Add(catalog);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task OnAfter(IChatContext context, CancellationToken cancellationToken)
    {
        if (skillService == null || context.SkillId <= 0 || context.UserId <= 0) return Task.CompletedTask;
        if (context.HasError) return Task.CompletedTask;

        skillService.RecordUsage(context.UserId, context.SkillId);

        return Task.CompletedTask;
    }

    /// <summary>基于用户消息内容自动匹配技能。默认实现调用 <see cref="SkillService.MatchSkillByContent"/>；派生类可覆盖</summary>
    /// <param name="context">对话上下文</param>
    /// <param name="lastUserContent">最后一条用户消息文本</param>
    protected virtual void ResolveSkillByContent(IChatContext context, String? lastUserContent)
    {
        if (skillService == null || context.SkillId > 0) return;

        var matched = skillService.MatchSkillByContent(lastUserContent);
        if (matched != null)
            context.SkillId = matched.Id;
    }

    /// <summary>构建技能目录。列出所有启用技能的编码、名称和描述</summary>
    /// <param name="svc">技能服务</param>
    /// <returns>技能目录文本；无可列技能时返回空字符串</returns>
    protected virtual String BuildSkillCatalog(SkillService svc)
    {
        var allSkills = svc.GetAllSkills().ToList();
        if (allSkills.Count == 0) return String.Empty;

        var sb = Pool.StringBuilder.Get();
        sb.AppendLine("## 可用技能目录");
        sb.AppendLine("以下技能可通过 @技能编码 或在会话设置中激活：");
        foreach (var skill in allSkills)
        {
            var entry = skill.Code.IsNullOrEmpty() ? skill.Name : $"{skill.Code}/{skill.Name}";
            var desc = skill.Description;
            if (desc.IsNullOrEmpty()) desc = "无描述";
            if (desc.Length > 60) desc = desc.Substring(0, 60) + "...";
            sb.AppendLine($"- {entry}：{desc}");
        }
        return sb.Return(true);
    }
}