using System.Runtime.CompilerServices;
using NewLife.AI.Clients;
using NewLife.AI.Models;
using NewLife.Log;

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
public class ToolChatClient : DelegatingChatClient, ILogFeature, ITracerFeature
{
    #region 属性

    /// <summary>工具提供者列表（按工具名直接路由执行工具调用）</summary>
    public IReadOnlyList<IToolProvider> Providers { get; }

    /// <summary>最大工具调用循环次数，防止无限递归。默认 10</summary>
    public Int32 MaxIterations { get; set; } = 10;

    /// <summary>工具结果最大字符数。超过此长度时自动截断并追加省略提示，0表示不限制</summary>
    public Int32 MaxResultLength { get; set; }
    #endregion

    #region 构造

    /// <summary>初始化工具对话客户端中间件</summary>
    /// <param name="innerClient">内层客户端</param>
    /// <param name="providers">工具提供者列表（按工具名路由；未找到则抛 <see cref="InvalidOperationException"/>）</param>
    public ToolChatClient(IChatClient innerClient, params IToolProvider[] providers) : base(innerClient)
    {
        Providers = (providers ?? []).ToList().AsReadOnly();
    }

    #endregion

    #region 方法

    /// <summary>非流式对话完成。注入工具定义并自动处理工具调用回路</summary>
    /// <param name="request">内部对话请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public override async Task<IChatResponse> GetResponseAsync(IChatRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var (mergedTools, toolMap) = GetMergedTools(request);
        if (mergedTools.Count == 0)
            return await InnerClient.GetResponseAsync(request, cancellationToken).ConfigureAwait(false);

        // 合并工具定义到选项（不修改调用方的原始选项）
        var workOptions = MergeToolOptions(request, mergedTools);
        var workMessages = request.Messages.ToList();

        IChatResponse response;
        var iterations = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            response = await InnerClient.GetResponseAsync(ChatRequest.Create(workMessages, workOptions), cancellationToken).ConfigureAwait(false);

            // 从第一个 Choice 中获取工具调用
            var assistantMessage = response.Messages?.FirstOrDefault()?.Message;
            var toolCalls = assistantMessage?.ToolCalls;
            if (toolCalls == null || toolCalls.Count == 0) break;
            if (++iterations > MaxIterations) break;

            // 追加 assistant 消息（含工具调用）
            workMessages.Add(new ChatMessage
            {
                Role = "assistant",
                Content = assistantMessage?.Content,
                ToolCalls = toolCalls.Select(tc => new ToolCall { Id = tc.Id, Type = tc.Type, Function = tc.Function }).ToList(),
            });

            // 依次执行所有工具调用
            foreach (var tc in toolCalls)
            {
                if (tc.Function == null) continue;
                var result = await ExecuteToolAsync(tc.Function.Name, tc.Function.Arguments, toolMap, cancellationToken).ConfigureAwait(false);
                workMessages.Add(new ChatMessage { Role = "tool", ToolCallId = tc.Id, Content = result });
            }
        }

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

        var (mergedTools, toolMap) = GetMergedTools(request);
        if (mergedTools.Count == 0)
        {
            await foreach (var chunk in InnerClient.GetStreamingResponseAsync(request, cancellationToken).ConfigureAwait(false))
                yield return chunk;
            yield break;
        }

        var workOptions = MergeToolOptions(request, mergedTools);
        var workMessages = request.Messages.ToList();

        for (var iteration = 0; iteration < MaxIterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var toolCallCollector = new List<ToolCall>();
            String? finishReason = null;
            var assistantContent = (String?)null;

            await foreach (var chunk in InnerClient.GetStreamingResponseAsync(ChatRequest.Create(workMessages, workOptions, stream: true), cancellationToken).ConfigureAwait(false))
            {
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

                        // 合并流式 tool_calls 增量
                        if (delta.ToolCalls != null)
                        {
                            foreach (var tc in delta.ToolCalls)
                                MergeToolCallDelta(toolCallCollector, tc);
                        }
                    }
                }

                yield return chunk;
            }

            var isToolRound = finishReason.EqualIgnoreCase("tool_calls") ||
                              (toolCallCollector.Count > 0 && String.IsNullOrEmpty(finishReason));

            if (!isToolRound || toolCallCollector.Count == 0)
                yield break;

            // 追加 assistant 消息（含工具调用）
            workMessages.Add(new ChatMessage
            {
                Role = "assistant",
                Content = assistantContent,
                ToolCalls = toolCallCollector.ToList(),
            });

            foreach (var tc in toolCallCollector)
            {
                if (tc.Function == null) continue;

                // 通知调用方：工具调用开始
                yield return new ChatResponse
                {
                    ToolCallEvents = [new ToolCallEventInfo("start", tc.Id, tc.Function.Name, tc.Function.Arguments)]
                };

                // 在 try/catch 中执行工具，收集结果（yield 不能出现在 try/catch 内）
                ToolCallEventInfo resultEvent;
                var span = Tracer?.NewSpan($"tool:{tc.Function.Name}", tc.Function.Arguments);
                try
                {
                    var toolResult = await ExecuteToolAsync(tc.Function.Name, tc.Function.Arguments, toolMap, cancellationToken).ConfigureAwait(false);
                    workMessages.Add(new ChatMessage { Role = "tool", ToolCallId = tc.Id, Content = toolResult });
                    span?.Value = toolResult?.Length ?? 0;
                    resultEvent = new ToolCallEventInfo("done", tc.Id, tc.Function.Name, toolResult);
                }
                catch (Exception ex)
                {
                    span?.SetError(ex, null);
                    // 将错误作为工具结果反馈给模型，让模型有机会修正，不中断流
                    workMessages.Add(new ChatMessage { Role = "tool", ToolCallId = tc.Id, Content = $"Error: {ex.Message}" });
                    resultEvent = new ToolCallEventInfo("error", tc.Id, tc.Function.Name, ex.Message);
                }
                finally
                {
                    span?.Dispose();
                }

                // 通知调用方：工具调用结果
                yield return new ChatResponse { ToolCallEvents = [resultEvent] };
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
    /// <param name="cancellationToken">取消令牌</param>
    private async Task<String> ExecuteToolAsync(String toolName, String? argumentsJson, Dictionary<String, IToolProvider> toolMap, CancellationToken cancellationToken)
    {
        if (!toolMap.TryGetValue(toolName, out var provider))
            throw new InvalidOperationException($"Tool not found: '{toolName}', searched {Providers.Count} providers");

        var result = await provider.CallToolAsync(toolName, argumentsJson, cancellationToken).ConfigureAwait(false);

        // 结果超长时截断并追加省略提示
        if (MaxResultLength > 0 && result != null && result.Length > MaxResultLength)
        {
            result = result.Substring(0, MaxResultLength) + $"\n\n[... 内容已截断，原始长度 {result.Length} 字符，仅保留前 {MaxResultLength} 字符]";
        }

        return result;
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
            existing = collector[collector.Count - 1];  // 兜底取最后一个（单工具调用时常见）

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

    /// <summary>聚合所有提供者的工具定义，合并 options.Tools，同时建立工具名到 Provider 的路由字典</summary>
    private (List<ChatTool> tools, Dictionary<String, IToolProvider> toolMap) GetMergedTools(IChatRequest? options)
    {
        var tools = new List<ChatTool>();
        var toolMap = new Dictionary<String, IToolProvider>(StringComparer.OrdinalIgnoreCase);
        foreach (var provider in Providers)
        {
            foreach (var t in provider.GetTools())
            {
                tools.Add(t);
                var name = t.Function?.Name;
                if (!String.IsNullOrEmpty(name))
                    toolMap[name] = provider;
            }
        }
        if (options?.Tools != null)
        {
            foreach (var t in options.Tools)
                tools.Add(t);
        }
        return (tools, toolMap);
    }

    /// <summary>克隆 ChatOptions 并注入合并后的工具列表（不修改调用方的原始选项）</summary>
    private static ChatOptions MergeToolOptions(IChatRequest? options, List<ChatTool> mergedTools)
        => new()
        {
            Model = options?.Model,
            Temperature = options?.Temperature,
            TopP = options?.TopP,
            TopK = options?.TopK,
            MaxTokens = options?.MaxTokens,
            Stop = options?.Stop,
            PresencePenalty = options?.PresencePenalty,
            FrequencyPenalty = options?.FrequencyPenalty,
            Tools = mergedTools,
            ToolChoice = options?.ToolChoice ?? "auto",
            User = options?.User,
            EnableThinking = options?.EnableThinking,
            ResponseFormat = options?.ResponseFormat,
            ParallelToolCalls = options?.ParallelToolCalls,
            UserId = options?.UserId,
            ConversationId = options?.ConversationId,
            Items = options?.Items ?? new Dictionary<String, Object?>(),
        };

    #endregion

    #region 日志
    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>追踪器</summary>
    public ITracer? Tracer { get; set; }
    #endregion
}