using System.Runtime.CompilerServices;
using NewLife.AI.Models;
using NewLife.AI.Services;
using NewLife.AI.Tools;
using NewLife.ChatAI.Tools;
using NewLife.Log;

namespace NewLife.ChatAI.Handlers;

/// <summary>系统提示词处理器。在调用模型前，将技能提示词注入 system 消息、解析 @工具引用</summary>
/// <remarks>
/// <para>事前职责：调用 <see cref="InjectSkillPrompt"/> 完成技能注入与工具集合解析；
/// 把构建完成的 system 文本写入 <see cref="IChatContext.SystemPrompt"/>，供持久化层使用。</para>
/// <para>事后无操作。短路场景：技能服务未注册时直接 next。</para>
/// </remarks>
/// <param name="toolProviders">已注册的工具提供者（用于解析 MCP 触发词）</param>
/// <param name="skillService">技能服务（可选，未注册时仅做 system 消息记录）</param>
/// <param name="tracer">追踪器</param>
public class SystemPromptHandler(
    IEnumerable<IToolProvider> toolProviders,
    SkillService? skillService,
    ITracer? tracer) : IChatHandler
{
    /// <summary>工具提供者集合（供派生类访问）</summary>
    protected readonly IEnumerable<IToolProvider> ToolProviders = toolProviders;

    /// <summary>技能服务（供派生类访问）</summary>
    protected readonly SkillService? SkillServiceInstance = skillService;

    /// <summary>追踪器（供派生类访问）</summary>
    protected readonly ITracer? Tracer = tracer;

    /// <inheritdoc/>
    public virtual async IAsyncEnumerable<ChatStreamEvent> InvokeAsync(IChatContext context, ChatHandlerDelegate next, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using (var span = Tracer?.NewSpan("handler:SystemPrompt"))
        {
            // 1. 技能注入 + @ 工具引用解析（派生类可覆盖以扩展自动匹配等行为）
            InjectSkillPrompt(context.ContextMessages, context);

            // 2. 记录构建完成的 system 提示词，供持久化使用
            var systemMsg = context.ContextMessages.FirstOrDefault(m => m.Role == "system");
            context.SystemPrompt = systemMsg?.Content as String;

            // 3. 记录技能使用次数
            if (context.SkillId > 0 && SkillServiceInstance != null && context.UserId > 0)
                SkillServiceInstance.RecordUsage(context.UserId, context.SkillId);
        }

        await foreach (var ev in next(cancellationToken).ConfigureAwait(false))
        {
            yield return ev;
        }
    }

    /// <summary>注入技能系统提示词。取消息列表中的系统消息，将技能提示词前置拼接；
    /// 同时解析消息中的 @ToolName 引用并填充 <see cref="IChatContext.SelectedTools"/></summary>
    /// <param name="contextMessages">上下文消息（会被修改）</param>
    /// <param name="context">对话上下文</param>
    protected virtual void InjectSkillPrompt(IList<AiChatMessage> contextMessages, IChatContext context)
    {
        if (SkillServiceInstance == null) return;

        using var span = Tracer?.NewSpan("ai:InjectSkillPrompt");

        // 取最后一条用户消息的内容，用于解析 @引用 等技能占位符
        var lastUserContent = contextMessages.LastOrDefault(m => m.Role == "user")?.Content as String;

        // 追加 NativeTool 触发词命中（低于系统工具，高于普通自动发现）
        var matchedNativeTools = SkillServiceInstance.MatchNativeToolNamesByContent(lastUserContent);
        foreach (var toolName in matchedNativeTools)
            context.SelectedTools.Add(toolName);

        // 追加 MCP 触发词命中（含默认每轮可用的服务）
        foreach (var mcp in ToolProviders.OfType<McpClientService>())
        {
            var matchedMcpTools = mcp.MatchToolNamesByContent(lastUserContent);
            foreach (var toolName in matchedMcpTools)
                context.SelectedTools.Add(toolName);
        }

        var skillPrompt = SkillServiceInstance.BuildSkillPrompt(context.SkillId, lastUserContent, context.SelectedTools, context.ResolvedSkillNames);
        if (skillPrompt.IsNullOrWhiteSpace()) return;

        span?.AppendTag(skillPrompt);

        var systemMsg = contextMessages.FirstOrDefault(m => m.Role == "system");
        if (systemMsg != null)
        {
            var existing = systemMsg.Content as String ?? String.Empty;
            systemMsg.Content = skillPrompt.Trim() + (existing.Length > 0 ? "\n\n" + existing : String.Empty);
        }
        else
        {
            contextMessages.Insert(0, new AiChatMessage { Role = "system", Content = skillPrompt.Trim() });
        }
    }
}
