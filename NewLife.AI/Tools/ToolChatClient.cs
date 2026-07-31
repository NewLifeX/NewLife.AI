using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using NewLife.AI.Clients;
using NewLife.AI.Models;
using NewLife.AI.Services;
using NewLife.Collections;
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
/// <item>循环重新调用模型，直到无更多工具调用（最多 <see cref="ToolSetting"/> 的 ToolMaxIterations 轮）</item>
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

    /// <summary>工具调用配置。为 null 时使用内置默认值（MaxIterations=10, MaxTotalTokens=0/不限制, MaxResultChars=0/不限制）</summary>
    public IToolSetting? ToolSetting { get; set; }

    /// <summary>是否因Token总限额触发中断</summary>
    public Boolean IsTotalTokenLimitExceeded { get; private set; }

    /// <summary>是否因工具调用轮次上限触发中断</summary>
    public Boolean IsToolLoopLimitExceeded { get; private set; }

    /// <summary>API 不返回 Usage 时的回退估算累计值（基于内联字符估算）</summary>
    private Int32 _fallbackEstimatedTokens;

    /// <summary>工具审批提供者。设置后在每次工具执行前请求审批，未设置时直接执行</summary>
    public IToolApprovalProvider? ApprovalProvider { get; set; }

    /// <summary>本次请求的工具可见性过滤集合。null 表示全量；空集合仅保留系统工具；非空集合保留系统工具 + 指定工具。
    /// 由 <see cref="GetMergedTools"/> 传入各 <see cref="IToolProvider.GetTools(ISet{String}?)"/>，实现会话级工具范围控制</summary>
    public ISet<String>? SelectedTools { get; set; }

    private Int32 _failureThreshold = 5;
    /// <summary>单 Provider 熔断失败阈值。连续失败达此数后触发熔断（Open），请求将返回降级错误而非继续调用。默认 5；设为 0 或负数时自动回退为 5</summary>
    public Int32 FailureThreshold { get => _failureThreshold; set => _failureThreshold = value > 0 ? value : 5; }

    private Int32 _cooldownSeconds = 60;
    /// <summary>熔断冷却秒数。Open 状态持续此时长后允许一次 HalfOpen 探测，探测成功则恢复 Closed。默认 60</summary>
    public Int32 CooldownSeconds { get => _cooldownSeconds; set => _cooldownSeconds = value > 0 ? value : 60; }

    /// <summary>工具执行回调。每次工具调用完成后触发，供外部监听工具调用情况。回调异常不中断工具执行</summary>
    public Func<ToolCallEventArgs, Task>? OnToolExecuted { get; set; }

    /// <summary>各 Provider 的熔断器实例（按引用相等性索引）</summary>
    private readonly ConcurrentDictionary<IToolProvider, CircuitBreakerPolicy> _breakers = new();

    /// <summary>跨轮次去重集合。记录本轮对话已执行过的 show_* 工具名+参数，避免同一工具在同一用户请求的多轮工具循环中重复执行。</summary>
    private readonly HashSet<String> _sessionDedupKeys = new(StringComparer.Ordinal);

    /// <summary>连续失败轮数计数器。整轮所有工具均失败（IsError=true）时 +1，任一个成功则归零。
    /// 达到 <see cref="EscalationThreshold"/> 时向 LLM 注入升级警告，避免死循环消耗 Token。</summary>
    private Int32 _consecutiveFailureRounds;

    private Int32 _escalationThreshold = 3;
    /// <summary>连续失败升级阈值。整轮工具调用连续失败达到此次数后，向 LLM 注入警告提示换思路。默认 3；设为 0 或负数时禁用升级检测</summary>
    public Int32 EscalationThreshold { get => _escalationThreshold; set => _escalationThreshold = value > 0 ? value : 3; }

    /// <summary>工具循环迭代回调。每轮工具执行完成后触发（在所有工具结果收集完毕、下轮 LLM 调用之前）。
    /// 回调参数包含当前迭代状态（轮次、累计 Token、工具调用历史），供外部做检查点持久化等操作。回调异常不中断循环。</summary>
    public Func<ToolLoopState, CancellationToken, Task>? OnLoopIteration { get; set; }

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

        var maxIterations = ToolSetting?.ToolMaxIterations ?? 10;
        if (maxIterations <= 0) maxIterations = 10;
        var maxTotalTokens = ToolSetting?.ToolMaxTotalTokens ?? 0;

        IChatResponse response;
        var iterations = 0;
        var executedAnyTool = false;
        UsageDetails? accumulatedUsage = null;

        _sessionDedupKeys.Clear();

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            response = await InnerClient.GetResponseAsync(ChatRequest.Create(workMessages, workOptions), cancellationToken).ConfigureAwait(false);

            // 累加每轮 LLM 调用的 Token 用量（N 次工具调用 = N+1 次 LLM 调用，每轮都有独立 Usage）
            if (response.Usage != null)
                accumulatedUsage = accumulatedUsage?.Add(response.Usage) ?? response.Usage;
            else
                _fallbackEstimatedTokens += EstimateTokens(workMessages);

            // Token 总限额检查（优先使用 API 返回值，回退字符估算）
            if (maxTotalTokens > 0)
            {
                var totalTokens = accumulatedUsage != null ? accumulatedUsage.TotalTokens : _fallbackEstimatedTokens;
                if (totalTokens >= maxTotalTokens)
                {
                    IsTotalTokenLimitExceeded = true;
                    Log.Warn("Token总限额已达到 {0:N0}（当前累计 {1:N0}），中断工具调用循环", maxTotalTokens, totalTokens);
                    break;
                }
            }

            // 从第一个 Choice 中获取工具调用
            var assistantMessage = response.Messages?.FirstOrDefault()?.Message;
            var toolCalls = assistantMessage?.ToolCalls;
            if (toolCalls == null || toolCalls.Count == 0) break;

            executedAnyTool = true;

            if (++iterations >= maxIterations)
            {
                IsToolLoopLimitExceeded = true;
                Log.Warn("工具调用轮次已达上限 {0}，中断工具调用循环", maxIterations);
                break;
            }

            // 追加 assistant 消息（含工具调用）
            // DeepSeek 思考模式要求：有工具调用时必须将 reasoning_content 一并回传，否则 API 返回 400
            workMessages.Add(new ChatMessage
            {
                Role = "assistant",
                Content = assistantMessage?.Content,
                ReasoningContent = assistantMessage?.ReasoningContent,
                ToolCalls = toolCalls.Select(tc => new ToolCall { Id = tc.Id, Type = tc.Type, Function = tc.Function }).ToList(),
            });

            // Phase 1：构造与 toolCalls 等长的任务数组，并行启动（Function 为 null 则坑位留 null，Phase 2 跳过）
            // 同轮去重：同名同参工具调用只执行第一次（ask_user 豁免）
            var dedupKeys = new HashSet<String>(StringComparer.Ordinal);
            var tasks = new Task<IToolResult>[toolCalls.Count];
            for (var i = 0; i < tasks.Length; i++)
            {
                var tc = toolCalls[i];
                if (tc.Function == null) continue;

                // 去重：重复调用不执行，直接返回占位结果（ask_user 豁免，它每次调用问题不同）
                if (!tc.Function.Name.EqualIgnoreCase("ask_user"))
                {
                    var key = tc.Function.Name + "|" + (tc.Function.Arguments ?? "");

                    // 跨轮次去重：show_* 工具在同一用户请求的多轮工具循环中只执行一次
                    if (tc.Function.Name.StartsWith("show_", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!_sessionDedupKeys.Add(key))
                        {
                            Log.Info("跳过跨轮次重复工具调用 {0}（已在上一轮执行过）", tc.Function.Name);
                            var dupInfo = "{\"kind\":\"duplicate\",\"for_user\":\"已跳过（重复调用）\"}";
                            tasks[i] = Task.FromResult<IToolResult>(
                                new ToolResult(
                                    ToolContent.ForUser(dupInfo),
                                    ToolContent.ForLlm($"[已去重：{tc.Function.Name}] 跨轮次重复调用，已跳过执行")
                                )
                            );
                            continue;
                        }
                    }

                    if (!dedupKeys.Add(key))
                    {
                        Log.Info("跳过同轮重复工具调用 {0}（同名同参）", tc.Function.Name);
                        var dupInfo = "{\"kind\":\"duplicate\",\"for_user\":\"已跳过（重复调用）\"}";
                        tasks[i] = Task.FromResult<IToolResult>(
                            new ToolResult(
                                ToolContent.ForUser(dupInfo),
                                ToolContent.ForLlm($"[已去重：{tc.Function.Name}] 调用与前序重复，已跳过执行")
                            )
                        );
                        continue;
                    }
                }

                var ctx = new ToolCallContext { Request = request, Response = response, ToolCallId = tc.Id };
                tasks[i] = ExecuteToolAsync(tc.Function!.Name, tc.Function!.Arguments, toolMap, ctx, cancellationToken);
            }

            // Phase 2：顺序 await + 写入（埋点与异常处理已在 ExecuteToolAsync 内完成，此处无需 try/catch）
            var toolResults = new Dictionary<String, IToolResult>(StringComparer.OrdinalIgnoreCase);
            var roundSummaries = new List<ToolCallSummary>();
            for (var i = 0; i < tasks.Length; i++)
            {
                if (tasks[i] == null) continue;
                var tc = toolCalls[i];
                var toolResult = await tasks[i].ConfigureAwait(false);
                toolResults[tc.Function!.Name] = toolResult;
                roundSummaries.Add(new ToolCallSummary(tc.Function.Name, toolResult.IsError, 0));
                var llmContent = GetLlmContent(toolResult, tc.Function.Name);
                workMessages.Add(new ChatMessage
                {
                    Role = "tool",
                    ToolCallId = tc.Id,
                    Content = llmContent
                });
            }

            // 连续失败检测：整轮所有工具均失败时递增，任一成功则归零。达到升级阈值时注入警告消息
            var allFailed = roundSummaries.Count > 0 && roundSummaries.All(s => s.IsError);
            if (allFailed)
                _consecutiveFailureRounds++;
            else
                _consecutiveFailureRounds = 0;

            if (EscalationThreshold > 0 && _consecutiveFailureRounds >= EscalationThreshold)
            {
                workMessages.Add(new ChatMessage
                {
                    Role = "user",
                    Content = $"[系统提示] 工具已连续失败 {_consecutiveFailureRounds} 轮。请换一种思路，或调用 ask_user 工具向用户寻求帮助。"
                });
                _consecutiveFailureRounds = 0;
            }

            // 触发循环迭代回调（检查点持久化等），回调异常不中断循环
            if (OnLoopIteration != null)
            {
                var totalTokens = accumulatedUsage?.TotalTokens ?? _fallbackEstimatedTokens;
                var state = new ToolLoopState(iterations - 1, maxIterations, totalTokens, roundSummaries, _consecutiveFailureRounds);
                _ = OnLoopIteration.Invoke(state, cancellationToken);
            }

            // 若本轮所有工具结果均无 LLM 受众内容，继续循环无意义，直接退出
            if (toolCalls.All(call => call.Function?.Name is not null && !HasLlmAudience(toolResults, call.Function.Name))) break;
        }

        // 兜底：执行过工具但最终轮未产出正文（模型只输出思考/工具调用即结束，或轮次达上限），
        // 追加提示再做一次 LLM 调用强制产出最终回答（仅一次，防死循环；Token 超限时不追加）
        if (!IsTotalTokenLimitExceeded && executedAnyTool && IsFinalContentEmpty(response))
        {
            Log.Info("最终回复内容为空，追加提示后强制产出最终回答");
            workMessages.Add(new ChatMessage
            {
                Role = "user",
                Content = "[系统提示] 请基于已有的工具调用结果，直接给出最终回答。不要再次调用工具。"
            });
            response = await InnerClient.GetResponseAsync(ChatRequest.Create(workMessages, workOptions), cancellationToken).ConfigureAwait(false);
            if (response.Usage != null)
                accumulatedUsage = accumulatedUsage?.Add(response.Usage) ?? response.Usage;
            else
                _fallbackEstimatedTokens += EstimateTokens(workMessages);
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

        var (mergedTools, toolMap) = GetMergedTools(request);
        if (mergedTools.Count == 0)
        {
            await foreach (var chunk in InnerClient.GetStreamingResponseAsync(request, cancellationToken).ConfigureAwait(false))
                yield return chunk;
            yield break;
        }

        var workOptions = MergeToolOptions(request, mergedTools);
        var workMessages = request.Messages.ToList();

        var maxIterations = ToolSetting?.ToolMaxIterations ?? 10;
        if (maxIterations <= 0) maxIterations = 10;
        var maxTotalTokens = ToolSetting?.ToolMaxTotalTokens ?? 0;

        UsageDetails? accumulatedUsage = null;

        _sessionDedupKeys.Clear();

        for (var iteration = 0; iteration < maxIterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var toolCalls = new List<ToolCall>();
            String? finishReason = null;
            var contentSb = Pool.StringBuilder.Get();
            var reasoningSb = Pool.StringBuilder.Get();
            UsageDetails? iterUsage = null;
            // 记录已在流式传输阶段提前发出 start 事件的工具调用 ID，避免 Step 1 重复发送
            var earlyStartedToolIds = new HashSet<String>();

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
                        if (!text.IsNullOrEmpty()) contentSb.Append(text);

                        // 累积思维链内容（DeepSeek 思考模式要求：有工具调用时必须将 reasoning_content 一并回传）
                        if (!delta.ReasoningContent.IsNullOrEmpty())
                            reasoningSb.Append(delta.ReasoningContent);

                        // 合并流式 tool_calls 增量
                        if (delta.ToolCalls != null)
                        {
                            foreach (var tc in delta.ToolCalls)
                            {
                                MergeToolCallDelta(toolCalls, tc);
                            }

                            // 函数名首次已知时立即发出 tool_call_start 事件，打破 SVG/HTML 大参数流式传输期间的 SSE 静默。
                            // ask_user（检查点）需要前端用完整 arguments 解析问题组，故排除在外（其参数短，不会触发长时间静默）
                            foreach (var earlyTc in toolCalls)
                            {
                                var earlyName = earlyTc.Function?.Name;
                                if (earlyName.IsNullOrEmpty()) continue;
                                if (earlyName.EqualIgnoreCase("ask_user")) continue;
                                if (earlyTc.Id.IsNullOrEmpty()) continue;
                                if (!earlyStartedToolIds.Add(earlyTc.Id)) continue;
                                yield return new ChatResponse
                                {
                                    ToolCallEvents = [new ToolCallEventInfo("start", earlyTc.Id, earlyName, null)]
                                };
                            }
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
            else
            {
                _fallbackEstimatedTokens += EstimateTokens(workMessages);
            }

            // Token 总限额检查（优先使用 API 返回值，回退字符估算）
            if (maxTotalTokens > 0)
            {
                var totalTokens = accumulatedUsage != null ? accumulatedUsage.TotalTokens : _fallbackEstimatedTokens;
                if (totalTokens >= maxTotalTokens)
                {
                    IsTotalTokenLimitExceeded = true;
                    Log.Warn("Token总限额已达到 {0:N0}（当前累计 {1:N0}），中断工具调用循环", maxTotalTokens, totalTokens);
                    // 兜底补发累计总量后退出
                    if (accumulatedUsage != null)
                        yield return new ChatResponse { Usage = accumulatedUsage };
                    yield break;
                }
            }

            var isToolRound = finishReason.EqualIgnoreCase("tool_calls") || (toolCalls.Count > 0 && finishReason.IsNullOrEmpty());

            if (!isToolRound || toolCalls.Count == 0)
            {
                Pool.StringBuilder.Return(contentSb);
                Pool.StringBuilder.Return(reasoningSb);
                // 兜底：最终轮无 Usage chunk 但存在历史轮（极少见），补发累计总量
                if (iterUsage == null && accumulatedUsage != null)
                    yield return new ChatResponse { Usage = accumulatedUsage };
                yield break;
            }

            // 若 SelectedTools 已启用过滤且 AI 调用了不在列表中的工具，动态扩展以便后续请求可见
            if (SelectedTools != null)
            {
                foreach (var tc in toolCalls)
                {
                    var name = tc.Function?.Name;
                    if (!name.IsNullOrEmpty() && !SelectedTools.Contains(name))
                        SelectedTools.Add(name);
                }
            }

            // 追加 assistant 消息（含工具调用）
            // 防御：空 arguments 替换为 "{}"，避免 liteLLM/DashScope 因 function.arguments 为空字符串返回 400
            foreach (var tc in toolCalls)
            {
                if (tc.Function != null && tc.Function.Arguments.IsNullOrEmpty())
                    tc.Function.Arguments = "{}";
            }

            workMessages.Add(new ChatMessage
            {
                Role = "assistant",
                Content = contentSb.Return(true),
                ReasoningContent = reasoningSb.Return(true),
                ToolCalls = toolCalls.ToList(),
            });

            // 同轮去重：同名同参工具调用只执行第一次（ask_user 豁免）
            var dedupKeys = new HashSet<String>(StringComparer.Ordinal);
            var isDedup = new Boolean[toolCalls.Count];

            // Step 1: yield start 事件并并行启动工具任务。
            // 始终发送含完整 arguments 的 start 事件（流式阶段的 earlyStart 仅作 UX 预览，此处补充完整参数）。
            // CoreStreamAsync 层会按 toolCallId 去重：已存在则更新 Arguments，不追加重复条目。
            var tasks = new Task<IToolResult>[toolCalls.Count];
            for (var i = 0; i < toolCalls.Count; i++)
            {
                var tc = toolCalls[i];
                if (tc.Function == null) continue;

                // 去重检查（ask_user 豁免，它每次调用问题不同）
                if (!tc.Function.Name.EqualIgnoreCase("ask_user"))
                {
                    var key = tc.Function.Name + "|" + (tc.Function.Arguments ?? "");

                    // 跨轮次去重：show_* 工具在同一用户请求的多轮工具循环中只执行一次
                    if (tc.Function.Name.StartsWith("show_", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!_sessionDedupKeys.Add(key))
                        {
                            Log.Info("跳过跨轮次重复工具调用 {0}（已在上一轮执行过）", tc.Function.Name);
                            isDedup[i] = true;
                            var dupInfo = "{\"kind\":\"duplicate\",\"for_user\":\"已跳过（重复调用）\"}";
                            tasks[i] = Task.FromResult<IToolResult>(
                                new ToolResult(
                                    ToolContent.ForUser(dupInfo),
                                    ToolContent.ForLlm($"[已去重：{tc.Function.Name}] 跨轮次重复调用，已跳过执行")
                                )
                            );
                            // 不 yield start 事件，前端不渲染重复卡片
                            continue;
                        }
                    }

                    if (!dedupKeys.Add(key))
                    {
                        Log.Info("跳过同轮重复工具调用 {0}（同名同参已执行）", tc.Function.Name);
                        isDedup[i] = true;
                        var dupInfo = "{\"kind\":\"duplicate\",\"for_user\":\"已跳过（重复调用）\"}";
                        tasks[i] = Task.FromResult<IToolResult>(
                            new ToolResult(
                                ToolContent.ForUser(dupInfo),
                                ToolContent.ForLlm($"[已去重：{tc.Function.Name}] 调用与前序重复，已跳过执行")
                            )
                        );
                        // 不 yield start 事件，前端不渲染重复卡片
                        continue;
                    }
                }

                // 对 Arguments 也施加截断，防止 AI 生成超大参数耗尽 SSE 序列化缓冲区
                var truncatedArgs = TruncateResult(tc.Function.Arguments);
                yield return new ChatResponse
                {
                    ToolCallEvents = [new ToolCallEventInfo("start", tc.Id, tc.Function.Name, truncatedArgs)]
                };

                var ctx = new ToolCallContext { Request = request, ToolCallId = tc.Id };
                tasks[i] = ExecuteToolAsync(tc.Function.Name, tc.Function.Arguments, toolMap, ctx, cancellationToken);
            }

            // Step 2: 按序 await（埋点与异常处理已在 ExecuteToolAsync 内完成，此处无需 try/catch）
            var toolResults = new Dictionary<String, IToolResult>(StringComparer.OrdinalIgnoreCase);
            var roundSummaries = new List<ToolCallSummary>();
            for (var i = 0; i < toolCalls.Count; i++)
            {
                var tc = toolCalls[i];
                if (tasks[i] == null) continue;

                var toolResult = await tasks[i].ConfigureAwait(false);
                toolResults[tc.Function!.Name] = toolResult;
                roundSummaries.Add(new ToolCallSummary(tc.Function.Name, toolResult.IsError, 0));

                // LLM 消息：提取 Llm 受众内容；无 Llm 内容时写占位（OpenAI 要求每个 tool_call 必须有对应 role=tool 回复）
                var llmContent = GetLlmContent(toolResult, tc.Function.Name);
                workMessages.Add(new ChatMessage
                {
                    Role = "tool",
                    ToolCallId = tc.Id,
                    Content = TruncateResult(llmContent)
                });

                // SSE 事件：取用户内容（重复工具调用也会发送结构化 JSON 前端）。
                // User 受众内容同样需截断，避免超大结果（如 read_file 不加限制、DB 查询返回大量行）撑爆 SSE 序列化缓冲区
                var userContent = GetUserContent(toolResult);
                var truncatedUserContent = TruncateResult(userContent);
                var eventType = toolResult.IsError ? "error" : "done";
                yield return new ChatResponse
                {
                    ToolCallEvents = [new ToolCallEventInfo(eventType, tc.Id, tc.Function.Name, truncatedUserContent, llmContent)]
                };
            }

            // 连续失败检测：整轮所有工具均失败时递增，任一成功则归零。达到升级阈值时注入警告消息
            var allFailed = roundSummaries.Count > 0 && roundSummaries.All(s => s.IsError);
            if (allFailed)
                _consecutiveFailureRounds++;
            else
                _consecutiveFailureRounds = 0;

            if (EscalationThreshold > 0 && _consecutiveFailureRounds >= EscalationThreshold)
            {
                workMessages.Add(new ChatMessage
                {
                    Role = "user",
                    Content = $"[系统提示] 工具已连续失败 {_consecutiveFailureRounds} 轮。请换一种思路，或调用 ask_user 工具向用户寻求帮助。"
                });
                _consecutiveFailureRounds = 0;
            }

            // 触发循环迭代回调（检查点持久化等），回调异常不中断循环
            if (OnLoopIteration != null)
            {
                var totalTokens = accumulatedUsage?.TotalTokens ?? _fallbackEstimatedTokens;
                var state = new ToolLoopState(iteration, maxIterations, totalTokens, roundSummaries, _consecutiveFailureRounds);
                _ = OnLoopIteration.Invoke(state, cancellationToken);
            }

            // 若本轮所有工具结果均无 LLM 受众内容，继续循环无意义，直接退出
            if (toolCalls.All(call => call.Function?.Name is not null && !HasLlmAudience(toolResults, call.Function.Name))) yield break;
            // 继续下一轮（下一轮流的 chunk 透传给调用方）
        }
        // 超过最大轮次，静默退出（调用方已收到全部 chunk）
    }

    #endregion

    #region 辅助

    /// <summary>估算消息列表的 Token 数（粗略：中文按1.5字/token，英文按4字符/token）。API 不返回 Usage 时作为回退判定依据</summary>
    /// <param name="messages">消息列表</param>
    /// <returns>Token 估算值</returns>
    private static Int32 EstimateTokens(IList<ChatMessage> messages)
    {
        if (messages == null || messages.Count == 0) return 0;

        var total = 0;
        foreach (var msg in messages)
        {
            total += 1; // role
            if (msg.Content is String text)
                total += EstimateTokens(text);
            if (msg.ToolCalls != null)
            {
                foreach (var tc in msg.ToolCalls)
                {
                    total += EstimateTokens(tc.Function?.Name);
                    total += EstimateTokens(tc.Function?.Arguments);
                }
            }
            if (!msg.ToolCallId.IsNullOrEmpty())
                total += 2;
        }
        return total;
    }

    /// <summary>估算单段文本的 Token 数</summary>
    /// <param name="text">文本内容</param>
    /// <returns>Token 估算值</returns>
    private static Int32 EstimateTokens(String? text)
    {
        if (text.IsNullOrEmpty()) return 0;

        var chineseCount = 0;
        var otherCount = 0;
        foreach (var ch in text)
        {
            if (ch >= 0x4E00 && ch <= 0x9FFF)
                chineseCount++;
            else
                otherCount++;
        }

        return (Int32)(chineseCount / 1.5 + otherCount / 4.0);
    }

    /// <summary>按工具名路由到对应 Provider 执行工具调用。未找到则抛 <see cref="InvalidOperationException"/></summary>
    /// <param name="toolName">工具名称</param>
    /// <param name="argumentsJson">参数 JSON 字符串（模型原文）</param>
    /// <param name="toolMap">工具名到 Provider 的路由字典</param>
    /// <param name="context">工具调用上下文，透传至工具方法</param>
    /// <param name="cancellationToken">取消令牌</param>
    private async Task<IToolResult> ExecuteToolAsync(String toolName, String? argumentsJson, Dictionary<String, IToolProvider> toolMap, ToolCallContext context, CancellationToken cancellationToken)
    {
        using var span = Tracer?.NewSpan($"ai:tool:{toolName}", argumentsJson);
        var sw = Stopwatch.StartNew();

        // 工具回调辅助方法：安全调用 OnToolExecuted，回调异常不中断工具执行
        async Task FireCallbackAsync(IToolResult result)
        {
            if (OnToolExecuted == null) return;
            try
            {
                var content = GetUserContent(result) ?? GetLlmContent(result, toolName);
                var summary = TruncateSummary(content);
                var args = new ToolCallEventArgs(toolName, argumentsJson, summary, result.IsError, sw.ElapsedMilliseconds);
                await OnToolExecuted(args).ConfigureAwait(false);
            }
            catch
            {
                // 回调异常不应中断工具执行流程
            }
        }

        // 先尝试从预构建路由表查找，找不到则动态 fallback（目录调用：AI 在 system 中看到工具名但未获得 schema）
        var isCatalogCall = !toolMap.TryGetValue(toolName, out var provider);
        try
        {
            if (isCatalogCall)
            {
                foreach (var p in Providers)
                {
                    var tools = p.GetTools(new HashSet<String>([toolName]));
                    if (tools != null && tools.Count > 0) { provider = p; break; }
                }
                if (provider == null)
                    throw new InvalidOperationException($"Tool not found: '{toolName}', searched {toolMap.Count} in {Providers.Count} providers");
            }

            // 权限三档检查（代码强制原则：权限由代码控制，不依赖提示词约束）
            var tier = ApprovalProvider?.GetToolTier(toolName) ?? ToolApprovalTier.Ask;
            if (tier == ToolApprovalTier.Deny)
            {
                var result = ToolErrorResult("PERMISSION_DENIED", $"工具 {toolName} 已被代码层强制阻断（高风险操作）");
                await FireCallbackAsync(result).ConfigureAwait(false);
                return result;
            }

            if (tier == ToolApprovalTier.Ask && ApprovalProvider != null)
            {
                var approval = await ApprovalProvider.RequestApprovalAsync(toolName, argumentsJson, cancellationToken).ConfigureAwait(false);
                if (!approval.Approved)
                {
                    var result = ToolErrorResult("USER_DENIED", $"工具 {toolName} 被用户拒绝执行");
                    await FireCallbackAsync(result).ConfigureAwait(false);
                    return result;
                }
            }
            // tier == Allow：低风险工具直接放行，无需审批

            // 熔断检查：Open 状态直接降级，避免雪崩效应
            var breaker = _breakers.GetOrAdd(provider!, _ => new CircuitBreakerPolicy(FailureThreshold, CooldownSeconds));
            if (!breaker.TryAcquire())
            {
                var remaining = breaker.RemainingCooldownSeconds;
                var result = ToolErrorResult("CIRCUIT_OPEN", $"工具提供者暂时不可用（熔断中），预计 {remaining}s 后恢复。如需立即重试请联系管理员重置熔断器");
                await FireCallbackAsync(result).ConfigureAwait(false);
                return result;
            }

            IToolResult callResult;
            try
            {
                callResult = await provider!.CallToolAsync(toolName, argumentsJson, context, cancellationToken).ConfigureAwait(false);
                breaker.RecordSuccess();

                // 记录工具返回结果的总字符长度，替代原始大对象序列化，避免大结果在埋点中二次膨胀
                if (callResult != null)
                {
                    span?.Value = callResult.Contents.Sum(c => c.Data?.Length ?? 0);
                }
            }
            catch (Exception)
            {
                breaker.RecordFailure();
                throw;
            }

            await FireCallbackAsync(callResult).ConfigureAwait(false);
            return callResult;
        }
        catch (ToolException toolEx)
        {
            // 工具方法抛出的结构化异常（含 ForUser/ForLlm 受众分离），包装为 ToolResult 以保留受众信息
            span?.SetError(toolEx, null);

            // 自动构建 ForLlm——工具只需提供差异化恢复指引，框架负责拼接：
            // 1. 拼接 ForUser 错误描述（若 ForLlm 未以 ForUser 开头，避免重复）
            // 2. 拼接工具名前缀 [xxx 调用失败]（若工具未自带）
            var toolPrefix = $"[{toolName} 调用失败] ";
            var llmContent = toolEx.ForLlm;
            if (!llmContent.IsNullOrEmpty() &&
                !llmContent.StartsWith(toolEx.ForUser ?? String.Empty, StringComparison.Ordinal))
            {
                llmContent = $"{toolEx.ForUser}。{llmContent}";
            }
            if (!llmContent.StartsWith(toolPrefix))
                llmContent = $"{toolPrefix}{llmContent}";

            var result = new ToolResult(
                ToolContent.ForUser(toolEx.ForUser),
                ToolContent.ForLlm(llmContent))
            { IsError = true };
            await FireCallbackAsync(result).ConfigureAwait(false);
            return result;
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            if (ex is OperationCanceledException) throw;

            // 目录调用（AI 未拿到 schema 就猜参数）：返回 INVALID_ARGUMENTS + schema hint，引导模型修正
            if (isCatalogCall && provider != null)
            {
                var hint = GetSchemaHint(toolName, provider);
                var result = ToolErrorResult("INVALID_ARGUMENTS", ex.Message, hint);
                await FireCallbackAsync(result).ConfigureAwait(false);
                return result;
            }
            var errorResult = ToolErrorResult("EXECUTION_ERROR", ex.Message);
            await FireCallbackAsync(errorResult).ConfigureAwait(false);
            return errorResult;
        }
    }

    /// <summary>从 IToolResult 中提取 LLM 受众内容。无 Llm 内容时返回占位文本</summary>
    /// <param name="result">工具结果</param>
    /// <param name="toolName">工具名称</param>
    /// <returns>LLM 受众内容或占位文本</returns>
    private static String GetLlmContent(IToolResult result, String toolName)
    {
        var llmParts = result.Contents
            .Where(c => c.Audience.HasFlag(ToolAudience.Llm))
            .Select(c => c.Data)
            .ToList();
        if (llmParts.Count > 0) return String.Join("\n", llmParts);

        // 无 Llm 内容时写占位（OpenAI 要求每个 tool_call 必须有对应 role=tool 回复）
        return $"[已渲染到客户端：{toolName}]，结果已渲染到用户界面，请勿在回复中插入图片链接或文件路径";
    }

    /// <summary>从 IToolResult 中提取前端用户内容</summary>
    /// <param name="result">工具结果</param>
    /// <returns>用户受众内容或 null</returns>
    private static String? GetUserContent(IToolResult result)
    {
        var userParts = result.Contents
            .Where(c => c.Audience.HasFlag(ToolAudience.User))
            .Select(c => c.Data)
            .ToList();
        return userParts.Count > 0 ? String.Join("\n", userParts) : null;
    }

    /// <summary>截断工具结果摘要到合理长度（默认 200 字符），供回调事件使用</summary>
    /// <param name="content">原始内容</param>
    /// <param name="maxLength">最大字符数，默认 200</param>
    /// <returns>截断后的摘要</returns>
    private static String? TruncateSummary(String? content, Int32 maxLength = 200)
    {
        if (String.IsNullOrWhiteSpace(content)) return content;
        if (content.Length <= maxLength) return content;
        return content[..maxLength] + "...";
    }

    /// <summary>检查指定工具的执行结果是否包含 LLM 受众内容</summary>
    private static Boolean HasLlmAudience(Dictionary<String, IToolResult> results, String toolName)
        => results.TryGetValue(toolName, out var result)
            && result.Contents.Any(c => c.Audience.HasFlag(ToolAudience.Llm));

    /// <summary>判断最终响应是否未产出正文内容（Content 为空，可能仅含思考或工具调用）</summary>
    /// <param name="response">最终响应</param>
    /// <returns>无正文内容返回 true</returns>
    private static Boolean IsFinalContentEmpty(IChatResponse response)
    {
        var msg = response.Messages?.FirstOrDefault()?.Message;
        if (msg == null) return true;
        return (msg.Content as String).IsNullOrEmpty();
    }

    /// <summary>创建错误工具结果</summary>
    private static ToolResult ToolErrorResult(String code, String message, String? hint = null)
    {
        var error = ToolError.Create(code, message, hint).ToJson();
        return new ToolResult(error) { IsError = true };
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

    /// <summary>聚合所有提供者的工具定义，合并 options.Tools，同时建立工具名到 Provider 的路由字典</summary>
    private (List<ChatTool> tools, Dictionary<String, IToolProvider> toolMap) GetMergedTools(IChatRequest? options)
    {
        var tools = new List<ChatTool>();
        var toolMap = new Dictionary<String, IToolProvider>(StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
        foreach (var provider in Providers)
        {
            foreach (var t in provider.GetTools(SelectedTools))
            {
                var name = t.Function?.Name;
                if (name == null || !seen.Add(name)) continue;

                tools.Add(t);
                toolMap[name] = provider;
            }
        }
        if (options?.Tools != null)
        {
            foreach (var t in options.Tools)
                tools.Add(t);
        }

        // 埋点：记录注入给 LLM 的工具名单和 schema 总字符长度，便于评估 token 消耗
        if (tools.Count > 0)
        {
            var toolNames = String.Join(",", tools.Select(t => t.Function?.Name).Where(n => !n.IsNullOrEmpty()));
            using var schemaSpan = Tracer?.NewSpan("ai:tool:schema", null, tools.Count);
            schemaSpan?.AppendTag(toolNames);
        }

        return (tools, toolMap);
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

    /// <summary>按 <see cref="ToolSetting"/> 的 ToolResultMaxChars 截断过长结果，防止撑满 LLM Context Window</summary>
    /// <param name="result">工具原始返回文本</param>
    /// <returns>截断后的文本，不超限时原样返回</returns>
    private String? TruncateResult(String? result)
    {
        var maxResultChars = ToolSetting?.ToolResultMaxChars ?? 0;
        if (maxResultChars <= 0 || result == null || result.Length <= maxResultChars)
            return result;
        return result.Substring(0, maxResultChars) + $"\n\n[... 内容已截断，原始长度 {result.Length} 字符，仅保留前 {maxResultChars} 字符]";
    }

    #endregion

    #region 日志
    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>追踪器</summary>
    public ITracer? Tracer { get; set; }
    #endregion
}

/// <summary>工具循环迭代状态快照。供 <see cref="ToolChatClient.OnLoopIteration"/> 回调使用</summary>
/// <param name="Iteration">当前迭代轮次（0-based）</param>
/// <param name="MaxIterations">最大迭代轮次</param>
/// <param name="AccumulatedTokens">累计 Token 用量（API 返回值优先，回退字符估算）</param>
/// <param name="ToolCallHistory">本轮及之前轮次的工具调用摘要列表</param>
/// <param name="ConsecutiveFailureRounds">连续失败轮数</param>
public record ToolLoopState(
    Int32 Iteration,
    Int32 MaxIterations,
    Int32 AccumulatedTokens,
    IReadOnlyList<ToolCallSummary> ToolCallHistory,
    Int32 ConsecutiveFailureRounds);

/// <summary>单次工具调用摘要，供 <see cref="ToolLoopState"/> 使用</summary>
/// <param name="ToolName">工具名称</param>
/// <param name="IsError">是否失败</param>
/// <param name="DurationMs">执行耗时（毫秒）</param>
public record ToolCallSummary(String ToolName, Boolean IsError, Int64 DurationMs);