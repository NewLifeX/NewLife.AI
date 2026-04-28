using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using NewLife.AI.Clients;
using NewLife.AI.Filters;
using NewLife.AI.Interfaces;
using NewLife.AI.Models;
using NewLife.AI.Services;
using NewLife.AI.Tools;
using NewLife.ChatAI.Services;
using NewLife.ChatAI.Tools;
using NewLife.Log;
using NewLife.Serialization;
using ChatResponse = NewLife.AI.Models.ChatResponse;
using UsageDetails = NewLife.AI.Models.UsageDetails;

namespace NewLife.ChatAI.Handlers;

/// <summary>核心 LLM 调用处理器。链路终点，负责实际调用模型并发出流式事件，<b>不调用 next</b></summary>
/// <remarks>
/// <para>包含原 <c>ChatPipeline.StreamAsync</c> 的完整实现：组装过滤器链 + 工具循环 + 流式 SSE 转换。
/// 派生类可通过覆盖 <see cref="ApplyTools"/> / <see cref="ApplyResponseStyle"/> / <see cref="BuildScopedProviders"/> 扩展能力。</para>
/// <para>由于是终点 Handler，链中任何 Handler 想短路（如缓存命中）只需不调用 <c>next</c> 即可绕过本调用。</para>
/// </remarks>
/// <param name="modelService">模型服务</param>
/// <param name="toolProviders">已注册的工具提供者（DbToolProvider、McpClientService 等）</param>
/// <param name="chatFilters">聊天过滤器链（日志、学习、Agent 触发等）</param>
/// <param name="tracer">追踪器</param>
public class LlmCoreHandler(
    ModelService modelService,
    IEnumerable<IToolProvider> toolProviders,
    IEnumerable<IChatFilter> chatFilters,
    ITracer? tracer) : IChatHandler
{
    /// <summary>工具提供者（供派生类访问）</summary>
    protected readonly IEnumerable<IToolProvider> ToolProviders = toolProviders;

    /// <summary>追踪器（供派生类访问）</summary>
    protected readonly ITracer? Tracer = tracer;

    /// <inheritdoc/>
    public virtual async IAsyncEnumerable<ChatStreamEvent> InvokeAsync(IChatContext context, ChatHandlerDelegate next, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var span = Tracer?.NewSpan("handler:LlmCore", new { messages = context.ContextMessages.Count });

        // next 不调用：本 Handler 是链路终点
        _ = next;

        var contextMessages = context.ContextMessages;
        var modelConfig = context.ModelConfig;

        // 1. 获取服务商客户端（ModelService.CreateClient 依赖具体实体导航属性，安全向下转型）
        using var rawClient = modelService.CreateClient((ModelConfig)modelConfig);
        if (rawClient == null)
        {
            yield return ChatStreamEvent.ErrorEvent("MODEL_UNAVAILABLE", $"未找到服务商 '{modelConfig.GetEffectiveProvider()}'");
            yield break;
        }

        // 2. 组装中间件管道：过滤器在外层（仅触发一次回调），工具循环在内层靠近 RawClient
        var clientBuilder = rawClient.AsBuilder();
        foreach (var filter in chatFilters)
            clientBuilder = clientBuilder.UseFilters(filter);
        var providers = BuildScopedProviders(context.SelectedTools).ToArray();
        ApplyTools(ref clientBuilder, contextMessages, providers);

        // 记录本轮可用工具名称（供持久化层写入 userMsg.ToolNames）
        foreach (var p in providers)
        {
            foreach (var t in p.GetTools())
            {
                if (t.Function?.Name != null)
                    context.AvailableToolNames.Add(t.Function.Name);
            }
        }

        // 将本轮工具函数定义（含参数 Schema）记录到埋点
        if (context.AvailableToolNames.Count > 0)
        {
            using var toolSchemaSpan = Tracer?.NewSpan("ai:ToolSchema");
            toolSchemaSpan?.AppendTag(providers.SelectMany(p => p.GetTools())
                .Where(t => t.Function != null)
                .Select(t => t.Function)
                .ToJson());
        }

        // 3. 构建 ChatOptions
        var userId = context.UserId > 0 ? context.UserId.ToString() : null;
        var conversationId = context.Conversation?.Id > 0 ? context.Conversation.Id.ToString() : null;
        var chatOptions = new ChatOptions
        {
            Model = modelConfig.Code,
            EnableThinking = context.ThinkingMode switch
            {
                ThinkingMode.Think => true,
                ThinkingMode.Fast => false,
                _ => modelConfig.SupportThinking ? true : null,
            },
            UserId = userId,
            ConversationId = conversationId,
        };
        if (context.Items.Count > 0) chatOptions.Items = context.Items;
        ApplyResponseStyle(chatOptions, userId);

        // 记录实际请求参数到上下文，供持久化层写入消息记录
        context.MaxTokens = chatOptions.MaxTokens ?? 0;
        context.Temperature = chatOptions.Temperature;

        // 4. 流式调用并转换为 SSE 事件
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

    /// <summary>将 DbToolProvider/McpClientService 包装为携带 selectedTools 的作用域版本</summary>
    /// <param name="selectedTools">本轮选中的工具名称集合</param>
    /// <returns>作用域化的工具提供者序列</returns>
    protected virtual IEnumerable<IToolProvider> BuildScopedProviders(ISet<String> selectedTools)
    {
        foreach (var p in ToolProviders)
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
