using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using NewLife.AI.Clients;
using NewLife.AI.Filters;
using NewLife.AI.Models;
using NewLife.AI.Interfaces;
using NewLife.AI.Services;
using NewLife.AI.Tools;
using NewLife.ChatAI.Entity;
using NewLife.Log;
using NewLife.Serialization;
using AiChatMessage = NewLife.AI.Models.ChatMessage;
using ChatResponse = NewLife.AI.Models.ChatResponse;
using ChatStreamEvent = NewLife.AI.Models.ChatStreamEvent;
using UsageDetails = NewLife.AI.Models.UsageDetails;
using NewLife.ChatAI.Tools;

namespace NewLife.ChatAI.Services;

/// <summary>默认对话执行管道。将工具调用、技能注入、过滤器链等中间件组装为统一的执行入口</summary>
/// <remarks>
/// 流式管道为单路径：
/// <code>
/// FilteredChatClient(LearningFilter + AgentTriggerFilter)
///   → ToolChatClient（工具调用循环）
///     → 服务商 RawClient（HTTP 调用）
/// </code>
/// 过滤器包在最外层，仅在全部工具调用完成后触发一次 OnStreamCompletedAsync。
/// 商用版可通过派生类覆盖 <see cref="BuildScopedProviders"/> / 注册自家 <see cref="IChatFilter"/> 等手段扩展。
/// </remarks>
/// <param name="modelService">模型服务</param>
/// <param name="toolProviders">已注册的工具提供者（DbToolProvider、McpClientService 等）</param>
/// <param name="chatFilters">聊天过滤器链（日志、学习、Agent 触发等）</param>
/// <param name="skillService">技能服务（可选）</param>
/// <param name="tracer">追踪器</param>
public class ChatPipeline(
    ModelService modelService,
    IEnumerable<IToolProvider> toolProviders,
    IEnumerable<IChatFilter> chatFilters,
    SkillService? skillService,
    ITracer tracer) : IChatPipeline
{
    #region IChatPipeline

    /// <inheritdoc/>
    public virtual void PrepareContext(IList<AiChatMessage> contextMessages, ChatPipelineContext context)
    {
        InjectSkillPrompt(contextMessages, context);

        // 将构建完成的 system 提示词记录到上下文，供外部持久化
        var systemMsg = contextMessages.FirstOrDefault(m => m.Role == "system");
        context.SystemPrompt = systemMsg?.Content as String;

        var userId = context.UserId.ToInt();
        if (context.SkillId > 0 && skillService != null && userId > 0)
            skillService.RecordUsage(userId, context.SkillId);
    }

    /// <inheritdoc/>
    public virtual async IAsyncEnumerable<ChatStreamEvent> StreamAsync(
        IList<AiChatMessage> contextMessages,
        IModelConfig modelConfig,
        ThinkingMode thinkingMode,
        ChatPipelineContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var span = tracer?.NewSpan("ai:StreamAsync", new { messages = contextMessages.Count });

        // 1. 技能注入 + 使用记录（若外部未调用 PrepareContext，此处兜底）
        if (context.SystemPrompt == null)
            PrepareContext(contextMessages, context);

        // 2. 获取服务商客户端（ModelService.CreateClient 依赖具体实体导航属性，此处安全向下转型）
        using var rawClient = modelService.CreateClient((ModelConfig)modelConfig);
        if (rawClient == null)
        {
            yield return ChatStreamEvent.ErrorEvent("MODEL_UNAVAILABLE", $"未找到服务商 '{modelConfig.GetEffectiveProvider()}'");
            yield break;
        }

        // 3. 组装中间件管道：过滤器在外层（仅触发一次回调），工具循环在内层靠近 RawClient
        var clientBuilder = rawClient.AsBuilder();
        foreach (var filter in chatFilters)
            clientBuilder = clientBuilder.UseFilters(filter);
        var providers = BuildScopedProviders(context.SelectedTools).ToArray();
        ApplyTools(ref clientBuilder, contextMessages, providers);

        // 记录本轮可用工具名称（供外部写入 userMsg.ToolNames）
        foreach (var p in providers)
        {
            foreach (var t in p.GetTools())
            {
                if (t.Function?.Name != null)
                    context.AvailableToolNames.Add(t.Function.Name);
            }
        }

        // 将本轮工具函数定义（含参数 Schema）记录到埋点，方便测试分析；不落消息库，避免鉴权信息泄漏
        if (context.AvailableToolNames.Count > 0)
        {
            using var toolSchemaSpan = tracer?.NewSpan("ai:ToolSchema");
            toolSchemaSpan?.AppendTag(providers.SelectMany(p => p.GetTools())
                .Where(t => t.Function != null)
                .Select(t => t.Function)
                .ToJson());
        }

        // 4. 构建 ChatOptions
        var chatOptions = new ChatOptions
        {
            Model = modelConfig.Code,
            EnableThinking = thinkingMode switch
            {
                ThinkingMode.Think => true,
                ThinkingMode.Fast => false,
                _ => modelConfig.SupportThinking ? true : null,
            },
            UserId = context.UserId,
            ConversationId = context.ConversationId,
        };
        if (context.Items.Count > 0) chatOptions.Items = context.Items;
        ApplyResponseStyle(chatOptions, context.UserId);

        // 记录实际请求参数到上下文，供外部写入消息记录
        context.MaxTokens = chatOptions.MaxTokens ?? 0;
        context.Temperature = chatOptions.Temperature;

        // 5. 流式调用并转换为 SSE 事件
        using var streamClient = clientBuilder.Build();

        var thinkingBuilder = new StringBuilder();
        UsageDetails? lastUsage = null;
        Int64 thinkingStart = 0;
        String? lastFinishReason = null;
        var streamSw = Stopwatch.StartNew();

        var sysFired = false;

        await foreach (var chunk in streamClient.GetStreamingResponseAsync(contextMessages, chatOptions, cancellationToken).ConfigureAwait(false))
        {
            // 第一个 chunk 到来时 before filter（含记忆注入）已完成，立即触发一次
            if (!sysFired)
            {
                sysFired = true;
                context.SystemPrompt = contextMessages.FirstOrDefault(m => m.Role == "system")?.Content as String;
                context.OnSystemReady?.Invoke(context.SystemPrompt!);
            }

            if (chunk.Usage != null) lastUsage = chunk.Usage;

            // 处理 ToolChatClient 注入的工具调用事件
            if (chunk is ChatResponse cr && cr.ToolCallEvents is { Count: > 0 } events)
            {
                foreach (var evt in events)
                {
                    switch (evt.Type)
                    {
                        case "start":
                            yield return ChatStreamEvent.ToolCallStart(evt.ToolCallId, evt.Name, evt.Value);
                            break;
                        case "done":
                            yield return ChatStreamEvent.ToolCallDone(evt.ToolCallId, evt.Value, true);
                            break;
                        case "error":
                            yield return ChatStreamEvent.ToolCallError(evt.ToolCallId, evt.Value ?? String.Empty);
                            break;
                    }
                }
                continue;
            }

            var choice = chunk.Messages?.FirstOrDefault();
            if (choice == null) continue;

            // 追踪最后一个 FinishReason
            if (choice.FinishReason != null)
                lastFinishReason = choice.FinishReason.Value.ToApiString();

            var delta = choice.Delta;
            if (delta == null) continue;

            if (!String.IsNullOrEmpty(delta.ReasoningContent))
            {
                if (thinkingStart == 0) thinkingStart = Runtime.TickCount64;
                thinkingBuilder.Append(delta.ReasoningContent);
                yield return ChatStreamEvent.ThinkingDelta(delta.ReasoningContent);
            }

            var text = delta.Content as String;
            if (!String.IsNullOrEmpty(text))
                yield return ChatStreamEvent.ContentDelta(text);
        }

        // 兜底：无 chunk 时（空响应/异常）亦更新 SystemPrompt
        if (!sysFired) context.SystemPrompt = contextMessages.FirstOrDefault(m => m.Role == "system")?.Content as String;

        if (thinkingBuilder.Length > 0)
            yield return ChatStreamEvent.ThinkingDone((Int32)(Runtime.TickCount64 - thinkingStart));

        streamSw.Stop();
        lastUsage ??= new UsageDetails();
        lastUsage.ElapsedMs = (Int32)streamSw.ElapsedMilliseconds;

        context.FinishReason = lastFinishReason;

        yield return ChatStreamEvent.MessageDone(lastUsage, finishReason: lastFinishReason);
    }

    /// <inheritdoc/>
    public virtual async Task<ChatResponse> CompleteAsync(
        IList<AiChatMessage> contextMessages,
        IModelConfig modelConfig,
        ChatPipelineContext context,
        CancellationToken cancellationToken)
    {
        using var span = tracer?.NewSpan("ai:CompleteAsync", new { messages = contextMessages.Count });

        if (context.SystemPrompt == null)
            PrepareContext(contextMessages, context);

        using var rawClient = modelService.CreateClient((ModelConfig)modelConfig);
        if (rawClient == null)
            return new ChatResponse { Messages = [new ChatChoice { Message = new AiChatMessage { Role = "assistant", Content = "未找到服务商" } }] };

        var clientBuilder = rawClient.AsBuilder();
        foreach (var filter in chatFilters)
            clientBuilder = clientBuilder.UseFilters(filter);
        var providers2 = BuildScopedProviders(context.SelectedTools).ToArray();
        ApplyTools(ref clientBuilder, contextMessages, providers2);

        var chatOptions = new ChatOptions
        {
            Model = modelConfig.Code,
            UserId = context.UserId,
            ConversationId = context.ConversationId,
        };
        ApplyResponseStyle(chatOptions, context.UserId);

        // 记录实际请求参数到上下文
        context.MaxTokens = chatOptions.MaxTokens ?? 0;
        context.Temperature = chatOptions.Temperature;

        using var chatClient = clientBuilder.Build();
        var response = ChatResponse.From(await chatClient.GetResponseAsync(contextMessages, chatOptions, cancellationToken).ConfigureAwait(false));

        // 记录完成原因到上下文
        var firstChoice = response.Messages?.FirstOrDefault();
        if (firstChoice?.FinishReason != null)
            context.FinishReason = firstChoice.FinishReason.Value.ToApiString();

        return response;
    }

    #endregion

    #region 可覆盖钩子

    /// <summary>将工具提供者应用到客户端构建器。派生类可覆盖以实现渐进式工具发现、结果截断等策略</summary>
    /// <param name="clientBuilder">客户端构建器（ref 传入，便于派生类链式替换）</param>
    /// <param name="contextMessages">上下文消息列表</param>
    /// <param name="providers">已解析的工具提供者集合</param>
    protected virtual void ApplyTools(ref ChatClientBuilder clientBuilder, IList<AiChatMessage> contextMessages, IToolProvider[] providers)
    {
        if (providers.Length > 0) clientBuilder = clientBuilder.UseTools(providers);
    }

    /// <summary>根据用户回应风格设置采样参数。仅在请求未显式指定时设置，不强制覆盖</summary>
    /// <param name="chatOptions">聊天选项</param>
    /// <param name="userId">用户编号</param>
    protected virtual void ApplyResponseStyle(ChatOptions chatOptions, String? userId)
    {
        var uid = userId.ToInt();
        if (uid <= 0) return;

        var userSetting = UserSetting.FindByUserId(uid);
        if (userSetting == null || userSetting.ResponseStyle == ResponseStyle.Balanced) return;

        var (temp, topP) = userSetting.ResponseStyle switch
        {
            ResponseStyle.Precise => (0.3, 0.7),
            ResponseStyle.Vivid => (1.0, 0.9),
            ResponseStyle.Creative => (1.4, 0.95),
            _ => ((Double?)null, (Double?)null)
        };
        chatOptions.Temperature ??= temp;
        chatOptions.TopP ??= topP;
    }

    /// <summary>注入技能系统提示词。取消息列表中的系统消息，将技能提示词前置拼接；同时解析消息中的 @ToolName 引用并填充 context.SelectedTools</summary>
    /// <param name="contextMessages">上下文消息（会被修改）</param>
    /// <param name="context">管道执行上下文</param>
    protected virtual void InjectSkillPrompt(IList<AiChatMessage> contextMessages, ChatPipelineContext context)
    {
        if (skillService == null) return;

        using var span = tracer?.NewSpan("ai:InjectSkillPrompt");

        // 取最后一条用户消息的内容，用于解析 @引用 等技能占位符
        var lastUserContent = contextMessages.LastOrDefault(m => m.Role == "user")?.Content as String;

        // 追加 NativeTool 触发词命中（低于系统工具，高于普通自动发现）
        var matchedNativeTools = skillService.MatchNativeToolNamesByContent(lastUserContent);
        foreach (var toolName in matchedNativeTools)
            context.SelectedTools.Add(toolName);

        // 追加 MCP 触发词命中（含默认每轮可用的服务）
        foreach (var mcp in toolProviders.OfType<McpClientService>())
        {
            var matchedMcpTools = mcp.MatchToolNamesByContent(lastUserContent);
            foreach (var toolName in matchedMcpTools)
                context.SelectedTools.Add(toolName);
        }

        var skillPrompt = skillService.BuildSkillPrompt(context.SkillId, lastUserContent, context.SelectedTools, context.ResolvedSkillNames);
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

    /// <summary>将 DbToolProvider/McpClientService 包装为携带 selectedTools 的作用域版本</summary>
    /// <param name="selectedTools">本轮选中的工具名称集合</param>
    /// <returns>作用域化的工具提供者序列</returns>
    protected virtual IEnumerable<IToolProvider> BuildScopedProviders(ISet<String> selectedTools)
    {
        foreach (var p in toolProviders)
        {
            if (p is DbToolProvider dbTool)
                yield return new ScopedDbToolProvider(dbTool, selectedTools);
            else if (p is McpClientService mcp)
                yield return new ScopedMcpToolProvider(mcp, selectedTools);
            else
                yield return p;
        }
    }

    #endregion

    #region 辅助类型

    /// <summary>携带 SelectedTools 的轻量 DbToolProvider 包装器</summary>
    private sealed class ScopedDbToolProvider(DbToolProvider inner, ISet<String> selectedTools) : IToolProvider
    {
        /// <inheritdoc/>
        public IList<ChatTool> GetTools() => inner.GetFilteredTools(selectedTools);

        /// <inheritdoc/>
        public Task<String> CallToolAsync(String toolName, String? argumentsJson, CancellationToken cancellationToken = default)
            => inner.CallToolAsync(toolName, argumentsJson, cancellationToken);
    }

    /// <summary>携带 SelectedTools 的轻量 McpClientService 包装器</summary>
    private sealed class ScopedMcpToolProvider(McpClientService inner, ISet<String> selectedTools) : IToolProvider
    {
        /// <inheritdoc/>
        public IList<ChatTool> GetTools() => inner.GetFilteredTools(selectedTools);

        /// <inheritdoc/>
        public Task<String> CallToolAsync(String toolName, String? argumentsJson, CancellationToken cancellationToken = default)
            => ((IToolProvider)inner).CallToolAsync(toolName, argumentsJson, cancellationToken);
    }

    #endregion
}
