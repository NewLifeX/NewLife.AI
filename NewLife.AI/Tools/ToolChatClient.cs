using System.Runtime.CompilerServices;
using NewLife.AI.Clients;
using NewLife.AI.Models;
using NewLife.Log;
using NewLife.Serialization;

namespace NewLife.AI.Tools;

/// <summary>工具对话客户端中间件。注入多个 <see cref="IToolProvider"/> 的工具定义，并自动处理多轮工具调用回路</summary>
/// <remarks>
/// 工作流（非流式 / 流式统一）：
/// <list type="number">
/// <item>请求前，聚合所有 <see cref="Providers"/> 的工具定义与 <c>ChatOptions.Tools</c></item>
/// <item>调用内层客户端获取响应</item>
/// <item>若响应含 <c>tool_calls</c>，按工具名路由到对应 Provider 执行 <see cref="ExecuteToolAsync"/></item>
/// <item>循环重新调用模型，直到无更多工具调用（最多 <see cref="MaxIterations"/> 轮）</item>
/// </list>
/// 使用方式：
/// <code>
/// var client = provider.CreateClient(providerOptions)
///     .AsBuilder()
///     .UseTools(registry, mcpProvider)  // 多个 IToolProvider 按工具名路由
///     .Build();
/// </code>
/// </remarks>
/// <remarks>初始化工具对话客户端中间件</remarks>
/// <param name="innerClient">内层客户端</param>
/// <param name="providers">工具提供者列表（按工具名路由；未找到则抛 <see cref="InvalidOperationException"/>）</param>
public class ToolChatClient(IChatClient innerClient, params IToolProvider[] providers) : DelegatingChatClient(innerClient), ILogFeature, ITracerFeature
{
    #region 属性
    /// <summary>工具提供者列表（按工具名直接路由执行工具调用）</summary>
    public IReadOnlyList<IToolProvider> Providers { get; } = (providers ?? []).ToList().AsReadOnly();

    private Int32 _maxIterations = 10;
    /// <summary>最大工具调用循环次数，防止无限递归。默认 10；设为 0 或负数时自动回退为 10</summary>
    public Int32 MaxIterations { get => _maxIterations; set => _maxIterations = value > 0 ? value : 10; }

    /// <summary>工具结果最大字符数。超过此长度时自动截断并追加省略提示，0表示不限制</summary>
    public Int32 MaxResultLength { get; set; }

    /// <summary>工具审批提供者。设置后在每次工具执行前请求审批，未设置时直接执行</summary>
    public IToolApprovalProvider? ApprovalProvider { get; set; }

    /// <summary>本次请求的工具可见性过滤集合。null 表示全量；空集合仅保留系统工具；非空集合保留系统工具 + 指定工具。
    /// 由 <see cref="GetMergedTools"/> 传入各 <see cref="IToolProvider.GetTools(ISet{String}?)"/>，实现会话级工具范围控制</summary>
    public ISet<String>? SelectedTools { get; set; }
    #endregion

    #region 方法

    /// <summary>非流式对话完成。注入工具定义并自动处理工具调用回路</summary>
    /// <param name="request">内部对话请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public override async Task<IChatResponse> GetResponseAsync(IChatRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var (mergedTools, toolMap, toolRoutingMap) = GetMergedTools(request);
        if (mergedTools.Count == 0)
            return await InnerClient.GetResponseAsync(request, cancellationToken).ConfigureAwait(false);

        // 合并工具定义到选项（不修改调用方的原始选项）
        var workOptions = MergeToolOptions(request, mergedTools);
        var workMessages = request.Messages.ToList();

        IChatResponse response;
        var iterations = 0;
        UsageDetails? accumulatedUsage = null;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            response = await InnerClient.GetResponseAsync(ChatRequest.Create(workMessages, workOptions), cancellationToken).ConfigureAwait(false);

            // 累加每轮 LLM 调用的 Token 用量（N 次工具调用 = N+1 次 LLM 调用，每轮都有独立 Usage）
            if (response.Usage != null) accumulatedUsage = accumulatedUsage?.Add(response.Usage) ?? response.Usage;

            // 从第一个 Choice 中获取工具调用
            var assistantMessage = response.Messages?.FirstOrDefault()?.Message;
            var toolCalls = assistantMessage?.ToolCalls;
            if (toolCalls == null || toolCalls.Count == 0) break;
            if (++iterations > MaxIterations) break;

            // 追加 assistant 消息（含工具调用）
            // DeepSeek 思考模式要求：有工具调用时必须将 reasoning_content 一并回传，否则 API 返回 400
            workMessages.Add(new ChatMessage
            {
                Role = "assistant",
                Content = assistantMessage?.Content,
                ReasoningContent = assistantMessage?.ReasoningContent,
                ToolCalls = toolCalls.Select(tc => new ToolCall { Id = tc.Id, Type = tc.Type, Function = tc.Function }).ToList(),
            });

            // 并行启动所有工具调用（每次调用独立 ToolCallContext，避免共享字段并发互覆）
            var perCallTasks = new List<(ToolCall tc, Task<String> task)>();
            foreach (var tc in toolCalls)
            {
                if (tc.Function == null) continue;
                var perCallCtx = new ToolCallContext { Request = request, Response = response, ToolCallId = tc.Id };
                perCallTasks.Add((tc, ExecuteToolAsync(tc.Function.Name, tc.Function.Arguments, toolMap, perCallCtx, cancellationToken)));
            }

            // 按序收集结果，并按 Routing 决定写入 LLM 消息的内容
            foreach (var (tc, task) in perCallTasks)
            {
                String result;
                try { result = await task.ConfigureAwait(false); }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { result = ToolError.Create("EXECUTION_ERROR", ex.Message).ToJson(); }

                var routing = toolRoutingMap.TryGetValue(tc.Function!.Name, out var r2) ? r2 : ToolResponseRouting.Both;
                var llmContent = routing.HasFlag(ToolResponseRouting.Llm) ? result : $"[已渲染到客户端：{tc.Function.Name}]";
                workMessages.Add(new ChatMessage { Role = "tool", ToolCallId = tc.Id, Content = llmContent });
            }
        }

        // 将所有轮次的 Token 用量累加值写回最终 response，供上层（如 InvokeLlmDirectAsync）使用
        if (accumulatedUsage != null) response.Usage = accumulatedUsage;

        return response;
    }

    /// <summary>流式对话完成。注入工具定义，流式执行多轮工具调用回路，对外透明</summary>
    /// <param name="request">内部对话请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public override async IAsyncEnumerable<IChatResponse> GetStreamingResponseAsync(
        IChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var (mergedTools, toolMap, toolRoutingMap) = GetMergedTools(request);
        if (mergedTools.Count == 0)
        {
            await foreach (var chunk in InnerClient.GetStreamingResponseAsync(request, cancellationToken).ConfigureAwait(false))
                yield return chunk;
            yield break;
        }

        var workOptions = MergeToolOptions(request, mergedTools);
        var workMessages = request.Messages.ToList();

        UsageDetails? accumulatedUsage = null;

        for (var iteration = 0; iteration < MaxIterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var toolCallCollector = new List<ToolCall>();
            String? finishReason = null;
            var assistantContent = (String?)null;
            var assistantReasoningContent = (String?)null;
            UsageDetails? iterUsage = null;

            await foreach (var chunk in InnerClient.GetStreamingResponseAsync(ChatRequest.Create(workMessages, workOptions, stream: true), cancellationToken).ConfigureAwait(false))
            {
                // 轮次内合并 chunk Usage（各协议差异由 MergeChunkUsage 虚拟方法处理）
                if (chunk.Usage != null)
                    iterUsage = MergeChunkUsage(iterUsage, chunk.Usage);

                var choice = chunk.Messages?.FirstOrDefault();
                if (choice != null)
                {
                    finishReason = choice.FinishReason?.ToApiString() ?? finishReason;
                    var delta = choice.Delta;
                    if (delta != null)
                    {
                        // 累积正文内容（供追加 assistant 消息）
                        var text = delta.Content as String;
                        if (!String.IsNullOrEmpty(text))
                            assistantContent = (assistantContent ?? String.Empty) + text;

                        // 累积思维链内容（DeepSeek 思考模式要求：有工具调用时必须将 reasoning_content 一并回传）
                        if (!String.IsNullOrEmpty(delta.ReasoningContent))
                            assistantReasoningContent = (assistantReasoningContent ?? String.Empty) + delta.ReasoningContent;

                        // 合并流式 tool_calls 增量
                        if (delta.ToolCalls != null)
                        {
                            foreach (var tc in delta.ToolCalls)
                                MergeToolCallDelta(toolCallCollector, tc);
                        }
                    }
                }

                // 始终透传原始 chunk，不做任何抑制
                yield return chunk;

                // 尽早原则：多轮场景下（有历史轮 accumulatedUsage），每个含 Usage 的 chunk 后
                // 立即追加一个运行时累计总量 chunk，让消费方随时能获取到正确的跨轮累计值
                if (chunk.Usage != null && accumulatedUsage != null)
                    yield return new ChatResponse { Usage = accumulatedUsage.Add(iterUsage!) };
            }

            // 跨轮 Token 累加：将本轮 Usage 加到全局累加值
            if (iterUsage != null)
            {
                DefaultSpan.Current?.AppendTag($"Tokens: {iterUsage.InputTokens}+{iterUsage.OutputTokens}={iterUsage.TotalTokens} finishReason: {finishReason}");

                accumulatedUsage = accumulatedUsage?.Add(iterUsage) ?? iterUsage;
            }

            var isToolRound = finishReason.EqualIgnoreCase("tool_calls") ||
                              (toolCallCollector.Count > 0 && String.IsNullOrEmpty(finishReason));

            if (!isToolRound || toolCallCollector.Count == 0)
            {
                // 兜底：最终轮无 Usage chunk 但存在历史轮（极少见），补发累计总量
                if (iterUsage == null && accumulatedUsage != null)
                    yield return new ChatResponse { Usage = accumulatedUsage };
                yield break;
            }

            // 追加 assistant 消息（含工具调用）
            workMessages.Add(new ChatMessage
            {
                Role = "assistant",
                Content = assistantContent,
                ReasoningContent = assistantReasoningContent,
                ToolCalls = toolCallCollector.ToList(),
            });

            // Step 1: yield 全部 start 事件，同时并行启动工具任务（每次调用独立上下文，无共享竞争）
            var pendingTasks = new List<(ToolCall tc, ISpan? span, Task<String> task)>();
            foreach (var tc in toolCallCollector)
            {
                if (tc.Function == null) continue;

                yield return new ChatResponse
                {
                    ToolCallEvents = [new ToolCallEventInfo("start", tc.Id, tc.Function.Name, tc.Function.Arguments)]
                };

                var span = Tracer?.NewSpan($"ai:tool:{tc.Function.Name}", tc.Function.Arguments);
                var perCallCtx = new ToolCallContext { Request = request, ToolCallId = tc.Id };
                pendingTasks.Add((tc, span, ExecuteToolAsync(tc.Function.Name, tc.Function.Arguments, toolMap, perCallCtx, cancellationToken)));
            }

            // Step 2: 按序 await（任务已并行运行），按 Routing 决定 LLM/SSE 内容
            foreach (var (tc, span, task) in pendingTasks)
            {
                // 在 try/catch 中执行工具，收集结果（yield 不能出现在 try/catch 内）
                String toolResult;
                try
                {
                    toolResult = await task.ConfigureAwait(false);
                    if (span != null && toolResult != null)
                        span.AppendTag(toolResult, toolResult.Length);
                }
                catch (Exception ex)
                {
                    span?.SetError(ex, null);
                    toolResult = ToolError.Create("EXECUTION_ERROR", ex.Message).ToJson();
                }
                finally
                {
                    span?.Dispose();
                }

                var routing = toolRoutingMap.TryGetValue(tc.Function!.Name, out var r) ? r : ToolResponseRouting.Both;

                // LLM 消息：Llm 路由时写截断版，仅 Frontend 路由时写占位（OpenAI 要求每个 tool_call 必须有对应 role=tool 回复）
                var llmContent = routing.HasFlag(ToolResponseRouting.Llm) ? TruncateResult(toolResult) : $"[已渲染到客户端：{tc.Function.Name}]";
                workMessages.Add(new ChatMessage { Role = "tool", ToolCallId = tc.Id, Content = llmContent });

                // SSE 事件：Frontend 路由时发送完整结果，Llm-only 时发送空内容的完成信号
                var eventType = ToolError.IsToolError(toolResult) ? "error" : "done";
                var sseContent = routing.HasFlag(ToolResponseRouting.Frontend) ? toolResult : null;
                yield return new ChatResponse { ToolCallEvents = [new ToolCallEventInfo(eventType, tc.Id, tc.Function.Name, sseContent)] };
            }
            // 继续下一轮（下一轮流的 chunk 透传给调用方）
        }
        // 超过最大轮次，静默退出（调用方已收到全部 chunk）
    }

    #endregion

    #region 辅助

    /// <summary>按工具名路由到对应 Provider 执行工具调用。未找到则抛 <see cref="InvalidOperationException"/></summary>
    /// <param name="toolName">工具名称</param>
    /// <param name="argumentsJson">参数 JSON 字符串（模型原文）</param>
    /// <param name="toolMap">工具名到 Provider 的路由字典</param>
    /// <param name="context">工具调用上下文，透传至工具方法</param>
    /// <param name="cancellationToken">取消令牌</param>
    private async Task<String> ExecuteToolAsync(String toolName, String? argumentsJson, Dictionary<String, IToolProvider> toolMap, ToolCallContext context, CancellationToken cancellationToken)
    {
        // 先尝试从预构建路由表查找，找不到则动态 fallback（目录调用：AI 在 system 中看到工具名但未获得 schema）
        var isCatalogCall = !toolMap.TryGetValue(toolName, out var provider);
        if (isCatalogCall)
        {
            foreach (var p in Providers)
            {
                var tools = p.GetTools(new HashSet<String>([toolName]));
                if (tools != null && tools.Count > 0)
                {
                    provider = p;
                    break;
                }
            }

            if (provider == null)
                throw new InvalidOperationException($"Tool not found: '{toolName}', searched {toolMap.Count} in {Providers.Count} providers");
        }

        // 权限三档检查（代码强制原则：权限由代码控制，不依赖提示词约束）
        var tier = ApprovalProvider?.GetToolTier(toolName) ?? ToolApprovalTier.Ask;
        if (tier == ToolApprovalTier.Deny)
            return ToolError.Create("PERMISSION_DENIED", $"工具 {toolName} 已被代码层强制阻断（高风险操作）").ToJson();

        if (tier == ToolApprovalTier.Ask && ApprovalProvider != null)
        {
            var approval = await ApprovalProvider.RequestApprovalAsync(toolName, argumentsJson, cancellationToken).ConfigureAwait(false);
            if (!approval.Approved)
                return ToolError.Create("USER_DENIED", $"工具 {toolName} 被用户拒绝执行").ToJson();
        }
        // tier == Allow：低风险工具直接放行，无需审批

        String result;
        try
        {
            result = await provider!.CallToolAsync(toolName, argumentsJson, context, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            // 目录调用（AI 未拿到 schema 就猜参数）：返回 INVALID_ARGUMENTS + schema hint，引导模型修正
            if (isCatalogCall)
            {
                var hint = GetSchemaHint(toolName, provider!);
                return ToolError.Create("INVALID_ARGUMENTS", ex.Message, hint).ToJson();
            }
            return ToolError.Create("EXECUTION_ERROR", ex.Message).ToJson();
        }

        return result!;
    }

    /// <summary>从 Provider 中提取工具的参数 Schema，作为 INVALID_ARGUMENTS 错误的修复建议</summary>
    /// <param name="toolName">工具名称</param>
    /// <param name="provider">已定位的工具提供者</param>
    /// <returns>Schema 提示文本，无法获取时返回 null</returns>
    private static String? GetSchemaHint(String toolName, IToolProvider provider)
    {
        try
        {
            var allTools = provider.GetTools(null);
            var match = allTools?.FirstOrDefault(t => t.Function?.Name != null &&
                String.Equals(t.Function.Name, toolName, StringComparison.OrdinalIgnoreCase));
            var schema = match?.Function?.Parameters;
            if (schema == null) return null;
            return $"工具 {toolName} 期望的参数 schema：{schema.ToJson()}，请按 schema 重试。";
        }
        catch
        {
            return null;
        }
    }

    /// <summary>合并流式 tool_call 增量到收集列表。OpenAI 流式协议中 tool_calls 分块到达</summary>
    private static void MergeToolCallDelta(List<ToolCall> collector, ToolCall delta)
    {
        if (delta == null) return;

        ToolCall? existing = null;
        if (!String.IsNullOrEmpty(delta.Id))
            existing = collector.FirstOrDefault(t => t.Id == delta.Id);
        else if (delta.Index != null)
            existing = collector.FirstOrDefault(t => t.Index == delta.Index);
        else if (collector.Count > 0)
            existing = collector[^1];  // 兜底取最后一个（单工具调用时常见）

        if (existing == null && !String.IsNullOrEmpty(delta.Id))
        {
            collector.Add(new ToolCall
            {
                Index = delta.Index,
                Id = delta.Id,
                Type = delta.Type,
                Function = new FunctionCall
                {
                    Name = delta.Function?.Name ?? String.Empty,
                    Arguments = delta.Function?.Arguments ?? String.Empty,
                },
            });
            return;
        }

        if (existing?.Function != null && delta.Function != null)
        {
            if (!String.IsNullOrEmpty(delta.Function.Name))
                existing.Function.Name += delta.Function.Name;
            if (!String.IsNullOrEmpty(delta.Function.Arguments))
                existing.Function.Arguments += delta.Function.Arguments;
        }
    }

    /// <summary>聚合所有提供者的工具定义，合并 options.Tools，同时建立工具名到 Provider 的路由字典和路由策略字典</summary>
    private (List<ChatTool> tools, Dictionary<String, IToolProvider> toolMap, Dictionary<String, ToolResponseRouting> toolRoutingMap) GetMergedTools(IChatRequest? options)
    {
        var tools = new List<ChatTool>();
        var toolMap = new Dictionary<String, IToolProvider>(StringComparer.OrdinalIgnoreCase);
        var toolRoutingMap = new Dictionary<String, ToolResponseRouting>(StringComparer.OrdinalIgnoreCase);
        foreach (var provider in Providers)
        {
            foreach (var t in provider.GetTools(SelectedTools))
            {
                tools.Add(t);
                var name = t.Function?.Name;
                if (!name.IsNullOrEmpty())
                {
                    toolMap[name] = provider;
                    toolRoutingMap[name] = t.Function!.Routing;
                }
            }
        }
        if (options?.Tools != null)
        {
            foreach (var t in options.Tools)
                tools.Add(t);
        }
        return (tools, toolMap, toolRoutingMap);
    }

    /// <summary>克隆 ChatOptions 并注入合并后的工具列表（不修改调用方的原始选项）</summary>
    private static ChatOptions MergeToolOptions(IChatRequest? request, List<ChatTool> mergedTools)
        => new()
        {
            Model = request?.Model,
            Temperature = request?.Temperature,
            TopP = request?.TopP,
            TopK = request?.TopK,
            MaxTokens = request?.MaxTokens,
            Stop = request?.Stop,
            PresencePenalty = request?.PresencePenalty,
            FrequencyPenalty = request?.FrequencyPenalty,
            Tools = mergedTools,
            ToolChoice = request?.ToolChoice ?? "auto",
            User = request?.User,
            EnableThinking = request?.EnableThinking,
            ResponseFormat = request?.ResponseFormat,
            ParallelToolCalls = request?.ParallelToolCalls,
            UserId = request?.UserId,
            ConversationId = request?.ConversationId,
            Items = request?.Items ?? new Dictionary<String, Object?>(),
        };

    /// <summary>按 <see cref="MaxResultLength"/> 截断过长结果，防止撑满 LLM Context Window</summary>
    /// <param name="result">工具原始返回文本</param>
    /// <returns>截断后的文本，不超限时原样返回</returns>
    private String? TruncateResult(String? result)
    {
        if (MaxResultLength <= 0 || result == null || result.Length <= MaxResultLength)
            return result;
        return result.Substring(0, MaxResultLength) + $"\n\n[... 内容已截断，原始长度 {result.Length} 字符，仅保留前 {MaxResultLength} 字符]";
    }

    #endregion

    #region 日志
    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>追踪器</summary>
    public ITracer? Tracer { get; set; }
    #endregion
}