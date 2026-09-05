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

    /// <summary>工具调用配置。为 null 时使用内置默认值（MaxIterations=10, ToolResultMaxChars=0/不限制）</summary>
    public IToolSetting? ToolSetting { get; set; }

    /// <summary>工具调用循环终止原因。对标 LangChain 结构化终止（AgentFinish/AgentAction）与
    /// OpenAI Agents SDK 的 run 终态，供上层结构化判断循环结束形态。循环内部各中断点在终止时赋值一次；
    /// 上层判断是否因轮次上限/上下文预算触发中断时，直接与本枚举对应值比较
    /// （如 <c>StopReason == ToolLoopStopReason.ContextLimit</c>），无需便捷布尔属性（2-属性简化收敛为单一枚举状态）</summary>
    public ToolLoopStopReason StopReason { get; set; } = ToolLoopStopReason.Completed;

    /// <summary>API 不返回 Usage 时的回退估算累计值（基于内联字符估算）</summary>
    private Int32 _fallbackEstimatedTokens;

    /// <summary>工具审批提供者。设置后在每次工具执行前请求审批，未设置时直接执行</summary>
    public IToolApprovalProvider? ApprovalProvider { get; set; }

    /// <summary>本次请求的工具可见性过滤集合。null 表示全量；空集合仅保留系统工具；非空集合保留系统工具 + 指定工具。
    /// 由 <see cref="GetMergedTools"/> 传入各 <see cref="IToolProvider.GetTools"/>，实现会话级工具范围控制</summary>
    public ISet<String>? SelectedTools { get; set; }

    private Int32 _failureThreshold = 5;
    /// <summary>单 Provider 熔断失败阈值。连续失败达此数后触发熔断（Open），请求将返回降级错误而非继续调用。默认 5；设为 0 或负数时自动回退为 5</summary>
    public Int32 FailureThreshold { get => _failureThreshold; set => _failureThreshold = value > 0 ? value : 5; }

    private Int32 _cooldownSeconds = 60;
    /// <summary>熔断冷却秒数。Open 状态持续此时长后允许一次 HalfOpen 探测，探测成功则恢复 Closed。默认 60</summary>
    public Int32 CooldownSeconds { get => _cooldownSeconds; set => _cooldownSeconds = value > 0 ? value : 60; }

    /// <summary>工具执行回调。每次工具调用完成后触发，供外部监听工具调用情况。回调异常不中断工具执行</summary>
    public Func<ToolCallEventArgs, Task>? OnToolExecuted { get; set; }

    /// <summary>各 Provider 的熔断器实例（按 Provider+工具名 组合键索引，见 ExecuteToolAsync）</summary>
    private readonly ConcurrentDictionary<(IToolProvider, String), CircuitBreakerPolicy> _breakers = new();

    /// <summary>连续失败轮数计数器。整轮所有工具均失败（IsError=true）时 +1，任一个成功则归零。
    /// 达到 <see cref="EscalationThreshold"/> 时向 LLM 注入升级警告，避免死循环消耗 Token。</summary>
    private Int32 _consecutiveFailureRounds;

    /// <summary>请求级失败登记：工具名+参数 → 失败原因摘要。相同调用已在本请求失败时，后续轮次直接拦截不重复执行，
    /// 引导模型修改参数或改用其他工具（对标 OpenAI Agents SDK 对重复失败调用的去重与错误回喂）</summary>
    private readonly Dictionary<String, String> _failedKeys = new(StringComparer.Ordinal);

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

        // 请求级状态重置：工具循环内部状态不得跨请求残留（A-73），否则上一请求的
        // 终止原因/失败轮数会污染下一请求的判定
        StopReason = ToolLoopStopReason.Completed;
        _failedKeys.Clear();
        _fallbackEstimatedTokens = 0;
        _consecutiveFailureRounds = 0;

        var (mergedTools, toolMap, providerToolNames) = GetMergedTools(request);
        if (mergedTools.Count == 0)
            return await InnerClient.GetResponseAsync(request, cancellationToken).ConfigureAwait(false);

        using var span = Tracer?.NewSpan($"ai:tool:loop");

        // 合并工具定义到选项（不修改调用方的原始选项）
        var workOptions = MergeToolOptions(request, mergedTools);
        var workMessages = request.Messages.ToList();

        var maxIterations = ToolSetting?.ToolMaxIterations ?? 10;
        if (maxIterations <= 0) maxIterations = 10;
        // 工具 schema Token 估算在循环外计算一次：工具列表在循环中不变，避免每轮重复序列化
        var toolsTokens = TokenEstimator.EstimateTokens(mergedTools);

        IChatResponse response = null!;
        var iterations = 0;
        var executedAnyTool = false;
        UsageDetails? accumulatedUsage = null;

        // 请求级跨轮去重集合（局部变量，天然隔离并发请求的共享状态）
        var sessionDedupKeys = new HashSet<String>(StringComparer.Ordinal);

        // 请求级去重复用缓存：同名同参工具首次执行结果，去重命中时复用给用户完整展示（局部变量，天然隔离并发请求）
        var dedupCache = new Dictionary<String, IToolResult>(StringComparer.Ordinal);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 上下文窗口预算检查（Token 守卫）：工具结果逐轮累积进消息列表，超限时折叠/截断，仍超限中断循环
            if (CheckContextLimit(workMessages, toolsTokens, request))
            {
                // 入口首次即超预算（尚未产生任何响应）：直接 break 会返回 null，下游 ChatResponse.From(null)
                // 读取 response.Id 抛 NRE——抛类型化异常，上层经 ChatErrorHelper 映射为 CONTEXT_TOO_LONG 友好错误
                if (response == null)
                    throw new ContextLengthExceededException(400, "输入内容已超出模型上下文窗口预算，无法发起工具调用");
                break;
            }
            if (span != null) span.Value++;

            span?.AppendTag($"workMessages: {workMessages.Count}");
            // 并行工具执行后当前上下文可能残留已结束的工具 span，每轮调用内层模型前重新挂载 loop span
            if (span != null) DefaultSpan.Current = span;

            response = await InnerClient.GetResponseAsync(ChatRequest.Create(workMessages, workOptions), cancellationToken).ConfigureAwait(false);

            // 累加每轮 LLM 调用的 Token 用量（N 次工具调用 = N+1 次 LLM 调用，每轮都有独立 Usage）
            accumulatedUsage = AccumulateUsage(accumulatedUsage, response.Usage, workMessages);

            // 从第一个 Choice 中获取工具调用
            var assistantMessage = response.Messages?.FirstOrDefault()?.Message;
            var toolCalls = assistantMessage?.ToolCalls;
            if (toolCalls == null || toolCalls.Count == 0) break;

            var toolNames = String.Join(",", toolCalls.Where(t => t.Function?.Name != null).Select(t => t.Function!.Name));
            span?.AppendTag($"toolCalls: {toolNames}");

            // 混合工具分流：含客户端工具（非任何 StarChat Provider 注册、仅客户端定义）的轮次整轮透传——
            // 不执行、不追加 tool 结果，原样返回 tool_calls 由客户端执行后在下一次请求回传。
            // 已注册但被 SelectedTools 过滤的目录工具仍走 ExecuteToolAsync 的目录回退执行
            if (HasClientToolCalls(toolCalls, toolMap, providerToolNames))
            {
                StopReason = ToolLoopStopReason.Passthrough;
                WriteLog("本轮含客户端工具调用，整轮透传：{0}", toolNames);
                break;
            }

            executedAnyTool = true;

            // 追加 assistant 消息（含工具调用，ToolCalls 浅拷贝隔离后续修改）
            // DeepSeek 思考模式要求：有工具调用时必须将 reasoning_content 一并回传，否则 API 返回 400
            workMessages.Add(new ChatMessage
            {
                Role = "assistant",
                Content = assistantMessage?.Content,
                ReasoningContent = assistantMessage?.ReasoningContent,
                ToolCalls = toolCalls.Select(tc => new ToolCall { Id = tc.Id, Type = tc.Type, Function = tc.Function }).ToList(),
                Items = assistantMessage?.Items is { Count: > 0 } ? new Dictionary<String, Object?>(assistantMessage.Items) : [],
            });

            // Phase 1：构造与 toolCalls 等长的任务数组，并行启动（Function 为 null 则坑位留 null，Phase 2 跳过）
            var dedupKeys = new HashSet<String>(StringComparer.Ordinal);
            var tasks = new Task<IToolResult>[toolCalls.Count];
            for (var i = 0; i < tasks.Length; i++)
            {
                var tc = toolCalls[i];
                if (tc.Function == null) continue;

                // 去重/失败拦截命中：直接返回占位任务（不执行）；否则启动真实执行
                var skip = TrySkipToolCall(tc, dedupKeys, sessionDedupKeys);
                if (skip != null)
                    tasks[i] = Task.FromResult(skip);
                else
                {
                    var ctx = new ToolCallContext { Request = request, Response = response, ToolCallId = tc.Id };
                    tasks[i] = ExecuteToolAsync(tc.Function.Name, tc.Function.Arguments, toolMap, ctx, span, cancellationToken);
                }
            }

            // Phase 2：顺序 await 并处理结果（埋点与异常处理已在 ExecuteToolAsync 内完成，此处无需 try/catch）。
            // 去重复用/失败登记/回喂消息收敛于 CollectToolResult（返回值供流式产出 done 事件，同步忽略）
            var toolResults = new Dictionary<String, IToolResult>(StringComparer.OrdinalIgnoreCase);
            var roundSummaries = new List<ToolCallSummary>();
            for (var i = 0; i < tasks.Length; i++)
            {
                if (tasks[i] == null) continue;
                var tc = toolCalls[i];
                if (tc.Function == null) continue;

                var toolResult = await tasks[i].ConfigureAwait(false);
                CollectToolResult(workMessages, dedupCache, tc, toolResult, toolResults, roundSummaries, sessionDedupKeys);
            }

            // 连续失败检测：整轮所有工具均失败时递增，任一成功则归零。达到升级阈值时注入警告消息
            EvaluateFailureAndEscalate(roundSummaries, workMessages);

            // 触发循环迭代回调（检查点持久化等），回调异常不中断循环
            FireLoopIteration(iterations, maxIterations, accumulatedUsage, roundSummaries, cancellationToken);

            // 若本轮所有工具结果均无 LLM 受众内容，继续循环无意义，直接退出
            if (toolCalls.All(call => call.Function?.Name is not null && !HasLlmAudience(toolResults, call.Function.Name))) break;

            // 执行满 maxIterations 轮工具后停止：本轮工具调用与结果已完整执行回传后才中断，不再发起新的 LLM-工具往返。
            // 上限检查放在工具执行后（此前放在执行前会把第 maxIterations 轮工具调用直接丢弃且不回传结果），
            // 与流式 GetStreamingResponseAsync 的执行轮数一致（对标竞品 max_turns/迭代上限语义）
            if (++iterations >= maxIterations)
            {
                StopReason = ToolLoopStopReason.MaxIterations;
                WriteLog("工具调用轮次已达上限 {0}，中断工具调用循环", maxIterations);
                break;
            }
        }

        // 兜底：执行过工具但最终轮未产出正文（模型只输出思考/工具调用即结束，或轮次达上限），
        // 追加提示再做一次 LLM 调用强制产出最终回答（仅一次，防死循环；上下文超限或累计预算超限时不追加）
        if (StopReason != ToolLoopStopReason.Passthrough
            && StopReason != ToolLoopStopReason.ContextLimit
            && executedAnyTool && response.Text.IsNullOrEmpty()
            // 兜底前再做一次预算守卫：最后一批工具结果追加后可能已逼近预算，追加提示前先折叠/截断；
            // 若仍无法降到预算内则放弃兜底（CheckContextLimit 已置 ContextLimit 终止原因）
            && !CheckContextLimit(workMessages, toolsTokens, request))
        {
            WriteLog("最终回复内容为空，追加提示后强制产出最终回答");
            workMessages.Add(new ChatMessage
            {
                Role = "user",
                Content = "[系统提示] 请基于已有的工具调用结果，直接给出最终回答。不要再次调用工具。"
            });
            // 兜底调用前同样重挂 loop span（并行工具执行后当前上下文可能残留已结束的工具 span）
            if (span != null) DefaultSpan.Current = span;

            response = await InnerClient.GetResponseAsync(ChatRequest.Create(workMessages, workOptions), cancellationToken).ConfigureAwait(false);
            accumulatedUsage = AccumulateUsage(accumulatedUsage, response.Usage, workMessages);
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

        // 请求级状态重置：工具循环内部状态不得跨请求残留（A-73），与 GetResponseAsync 保持一致
        StopReason = ToolLoopStopReason.Completed;
        _failedKeys.Clear();
        _fallbackEstimatedTokens = 0;
        _consecutiveFailureRounds = 0;

        var (mergedTools, toolMap, providerToolNames) = GetMergedTools(request);
        if (mergedTools.Count == 0)
        {
            await foreach (var chunk in InnerClient.GetStreamingResponseAsync(request, cancellationToken).ConfigureAwait(false))
                yield return chunk;
            yield break;
        }

        using var span = Tracer?.NewSpan($"ai:tool:loop");

        // 合并工具定义到选项（不修改调用方的原始选项）
        var workOptions = MergeToolOptions(request, mergedTools);
        var workMessages = request.Messages.ToList();

        var maxIterations = ToolSetting?.ToolMaxIterations ?? 10;
        if (maxIterations <= 0) maxIterations = 10;
        // 工具 schema Token 估算在循环外计算一次：工具列表在循环中不变，避免每轮重复序列化
        var toolsTokens = TokenEstimator.EstimateTokens(mergedTools);

        UsageDetails? accumulatedUsage = null;

        // 请求级跨轮去重集合（局部变量，天然隔离并发请求的共享状态）
        var sessionDedupKeys = new HashSet<String>(StringComparer.Ordinal);

        // 请求级去重复用缓存：同名同参工具首次执行结果，去重命中时复用给用户完整展示（局部变量，天然隔离并发请求）
        var dedupCache = new Dictionary<String, IToolResult>(StringComparer.Ordinal);

        for (var iteration = 0; ; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 达到工具轮次上限：执行满 maxIterations 轮工具后不再发起新的 LLM-工具往返。
            // 与同步 GetResponseAsync 一致在满轮后置位终止原因（此前静默退出不置位，两入口行为不一致）
            if (iteration >= maxIterations)
            {
                StopReason = ToolLoopStopReason.MaxIterations;
                WriteLog("工具调用轮次已达上限 {0}，中断工具调用循环", maxIterations);
                break;
            }

            // 上下文窗口预算检查（Token 守卫）：工具结果逐轮累积进消息列表，超限时折叠/截断，仍超限中断循环
            if (CheckContextLimit(workMessages, toolsTokens, request)) break;
            if (span != null) span.Value++;

            var toolCalls = new List<ToolCall>();
            String? finishReason = null;
            var contentSb = Pool.StringBuilder.Get();
            var reasoningSb = Pool.StringBuilder.Get();
            UsageDetails? iterUsage = null;
            // Anthropic 多轮思考回传：流式累积 thinking 签名与 redacted_thinking 数据
            String? thinkingSignature = null;
            List<String>? redactedThinking = null;
            // 记录已在流式传输阶段提前发出 start 事件的工具调用 ID，避免 Step 1 重复发送
            var earlyStartedToolIds = new HashSet<String>();

            span?.AppendTag($"workMessages: {workMessages.Count}");
            // async iterator 跨 yield 不保留 AsyncLocal：本方法首段设置的 ai:tool:loop 上下文在首个 chunk 后失效，
            // 每轮发起内层模型流式调用前重新挂载，使各轮 ai:Streaming:{model} 都正确成为 ai:tool:loop 的子级
            if (span != null) DefaultSpan.Current = span;

            await foreach (var chunk in InnerClient.GetStreamingResponseAsync(ChatRequest.Create(workMessages, workOptions, stream: true), cancellationToken).ConfigureAwait(false))
            {
                // 轮次内合并 chunk Usage（各协议差异由 MergeChunkUsage 虚拟方法处理）
                if (chunk.Usage != null)
                    iterUsage = MergeChunkUsage(iterUsage, chunk.Usage);

                var choice = chunk.Messages?.FirstOrDefault();
                if (choice != null)
                {
                    // 防御：部分网关/代理在 tool_calls 之后补发 finish_reason=stop，
                    // 已识别的工具回合标记不得被后续 stop 覆盖（否则工具永不执行）
                    var fr = choice.FinishReason?.ToApiString();
                    if (!fr.IsNullOrEmpty())
                    {
                        if (!finishReason.EqualIgnoreCase("tool_calls", "stop"))
                            finishReason = fr;
                    }
                    var delta = choice.Delta;
                    if (delta != null)
                    {
                        // 累积正文内容（供追加 assistant 消息）
                        var text = delta.Content as String;
                        if (!text.IsNullOrEmpty()) contentSb.Append(text);

                        // 累积思维链内容（DeepSeek 思考模式要求：有工具调用时必须将 reasoning_content 一并回传）
                        if (!delta.ReasoningContent.IsNullOrEmpty())
                            reasoningSb.Append(delta.ReasoningContent);

                        // 累积 Anthropic 思考签名与 redacted_thinking 数据（多轮/工具轮次原样回传必需）
                        if (delta["Signature"] is String sig && !sig.IsNullOrEmpty())
                            thinkingSignature = sig;
                        if (delta["RedactedThinking"] is IList<String> reds)
                        {
                            redactedThinking ??= [];
                            redactedThinking.AddRange(reds);
                        }

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

                // 尽早原则：多轮场景下（有历史轮累计量），每个含 Usage 的 chunk 后
                // 立即追加一个运行时累计总量 chunk，让消费方随时能获取到正确的跨轮累计值
                if (chunk.Usage != null && accumulatedUsage != null)
                    yield return new ChatResponse { Usage = accumulatedUsage.Add(iterUsage!) };
            }

            // 跨轮 Token 累加：将本轮 Usage 加到全局累加值（缺失时回退字符估算）
            accumulatedUsage = AccumulateUsage(accumulatedUsage, iterUsage, workMessages);

            var isToolRound = finishReason.EqualIgnoreCase("tool_calls") || (toolCalls.Count > 0 && finishReason.IsNullOrEmpty());

            if (!isToolRound || toolCalls.Count == 0)
            {
                contentSb.Return();
                reasoningSb.Return();
                // 兜底：最终轮无 Usage chunk 但存在历史轮（极少见），补发累计总量
                if (iterUsage == null && accumulatedUsage != null)
                    yield return new ChatResponse { Usage = accumulatedUsage };
                yield break;
            }

            var toolNames = String.Join(",", toolCalls.Where(t => t.Function?.Name != null).Select(t => t.Function!.Name));
            span?.AppendTag($"toolCalls: {toolNames}");

            // 混合工具分流：含客户端工具（非任何 StarChat Provider 注册、仅客户端定义）的轮次整轮透传——
            // 不执行、不追加 tool 结果，仅补发 start/done 事件，由客户端执行后在下一次请求回传
            if (HasClientToolCalls(toolCalls, toolMap, providerToolNames))
            {
                contentSb.Return();
                reasoningSb.Return();
                StopReason = ToolLoopStopReason.Passthrough;
                WriteLog("本轮含客户端工具调用，整轮透传：{0}", toolNames);
                foreach (var tc in toolCalls)
                {
                    if (tc.Function == null) continue;
                    var args = tc.Function.Arguments.IsNullOrEmpty() ? "{}" : tc.Function.Arguments;
                    yield return new ChatResponse { ToolCallEvents = [new ToolCallEventInfo("start", tc.Id, tc.Function.Name, args)] };
                    yield return new ChatResponse { ToolCallEvents = [new ToolCallEventInfo("done", tc.Id, tc.Function.Name, args)] };
                }
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

            var assistantContent = contentSb.Return(true);
            var assistantReasoning = reasoningSb.Return(true);

            // 构建本轮聚合响应，供工具上下文访问（流式下每轮由多个 chunk 拼合，工具通过 ToolCallContext.Response 读取本轮模型输出）
            var roundResponse = BuildRoundResponse(assistantContent, assistantReasoning, toolCalls, finishReason);

            // 追加 assistant 消息（思考签名/redacted_thinking 放入 Items 原样回传，ToolCalls 浅拷贝隔离后续修改）
            var assistantItems = new Dictionary<String, Object?>();
            if (thinkingSignature != null) assistantItems["Signature"] = thinkingSignature;
            if (redactedThinking != null) assistantItems["RedactedThinking"] = redactedThinking;

            workMessages.Add(new ChatMessage
            {
                Role = "assistant",
                Content = assistantContent,
                ReasoningContent = assistantReasoning,
                ToolCalls = toolCalls.Select(tc => new ToolCall { Id = tc.Id, Type = tc.Type, Function = tc.Function }).ToList(),
                Items = assistantItems.Count > 0 ? new Dictionary<String, Object?>(assistantItems) : [],
            });

            // 同轮去重：同名同参工具调用只执行第一次（ask_user 豁免）
            var dedupKeys = new HashSet<String>(StringComparer.Ordinal);
            var tasks = new Task<IToolResult>[toolCalls.Count];

            // Step 1: yield start 事件并并行启动工具任务。
            // 始终发送含完整 arguments 的 start 事件（流式阶段的 earlyStart 仅作 UX 预览，此处补充完整参数）。
            // CoreStreamAsync 层会按 toolCallId 去重：已存在则更新 Arguments，不追加重复条目。
            for (var i = 0; i < toolCalls.Count; i++)
            {
                var tc = toolCalls[i];
                if (tc.Function == null) continue;

                // 去重/失败拦截命中：返回占位任务且不 yield start（前端不渲染重复卡片）
                var skip = TrySkipToolCall(tc, dedupKeys, sessionDedupKeys);
                if (skip != null)
                {
                    tasks[i] = Task.FromResult(skip);
                    continue;
                }

                // 真实执行：先发含完整 Arguments 的 start 事件再启动任务（ToolResultMaxChars 仅控制发给 AI 的内容长度，
                // 前端 ToolCallBadge 展示、ask_user 解析问题组都需要完整参数，不截断）
                yield return new ChatResponse
                {
                    ToolCallEvents = [new ToolCallEventInfo("start", tc.Id, tc.Function.Name, tc.Function.Arguments)]
                };

                var ctx = new ToolCallContext { Request = request, Response = roundResponse, ToolCallId = tc.Id };
                tasks[i] = ExecuteToolAsync(tc.Function.Name, tc.Function.Arguments, toolMap, ctx, span, cancellationToken);
            }

            // Step 2: 按序 await（埋点与异常处理已在 ExecuteToolAsync 内完成，此处无需 try/catch）。
            // 每个工具结果经 CollectToolResult 处理后立即产出事件，保持"每工具完成即时报到"的实时性
            var toolResults = new Dictionary<String, IToolResult>(StringComparer.OrdinalIgnoreCase);
            var roundSummaries = new List<ToolCallSummary>();
            for (var i = 0; i < toolCalls.Count; i++)
            {
                var tc = toolCalls[i];
                if (tasks[i] == null) continue;
                if (tc.Function == null) continue;

                var toolResult = await tasks[i].ConfigureAwait(false);
                var evt = CollectToolResult(workMessages, dedupCache, tc, toolResult, toolResults, roundSummaries, sessionDedupKeys);
                if (evt != null)
                    yield return new ChatResponse { ToolCallEvents = [evt] };
            }

            // 连续失败检测：整轮所有工具均失败时递增，任一成功则归零。达到升级阈值时注入警告消息
            EvaluateFailureAndEscalate(roundSummaries, workMessages);

            // 触发循环迭代回调（检查点持久化等），回调异常不中断循环
            FireLoopIteration(iteration, maxIterations, accumulatedUsage, roundSummaries, cancellationToken);

            // 若本轮所有工具结果均无 LLM 受众内容，继续循环无意义，直接退出
            if (toolCalls.All(call => call.Function?.Name is not null && !HasLlmAudience(toolResults, call.Function.Name))) yield break;
            // 继续下一轮（下一轮流的 chunk 透传给调用方）
        }
        // 超过最大轮次：StopReason 已在循环头置位，退出（调用方已收到全部 chunk）
    }

    #endregion

    #region 辅助

    /// <summary>工具执行前的跳过判定：同轮/跨轮去重命中与跨轮重复失败拦截返回占位结果（不执行工具，前端不渲染卡片）；
    /// 未命中返回 null，由调用方启动真实执行。是否跳过需在执行前显式得知（决定流式 start 事件与占位分支），
    /// 不能依赖任务完成状态判断——真实执行的 async 任务在同步完成的工具下也可能立即完成</summary>
    /// <param name="tc">工具调用</param>
    /// <param name="dedupKeys">同轮去重集合（同名同参只执行第一次，轮级）</param>
    /// <param name="sessionDedupKeys">请求级跨轮去重集合（show_* 工具整请求只执行一次）</param>
    /// <returns>占位结果（去重/失败拦截命中）；未命中返回 null 表示应正常执行</returns>
    private IToolResult? TrySkipToolCall(ToolCall tc, HashSet<String> dedupKeys, HashSet<String> sessionDedupKeys)
    {
        var name = tc.Function!.Name;

        // 去重命中：直接返回占位结果，不执行工具
        var dup = TryBuildDedupResult(name, tc.Function.Arguments, dedupKeys, sessionDedupKeys);
        if (dup != null) return dup;

        // 跨轮重复失败拦截：相同工具+参数已在本请求失败过 → 不再执行，回传失败原因引导模型修改参数/换工具
        var failedKey = name + "|" + (tc.Function.Arguments ?? "");
        if (!name.EqualIgnoreCase("ask_user") && _failedKeys.TryGetValue(failedKey, out var failedReason))
            return BuildRepeatFailureResult(name, failedReason);

        return null;
    }

    /// <summary>处理单个工具执行结果：去重复用（占位换缓存）、失败登记、追加 role=tool 回喂消息，更新工具结果表与轮摘要。
    /// 返回流式需产出的事件数据（成功 done / 失败 error；Value=用户完整内容，LlmResult=截断后内容），同步忽略返回值。
    /// 工作消息/去重缓存/失败登记等可变状态由调用方持有并以参数传入（就地更新）</summary>
    /// <param name="workMessages">工作消息列表（追加 role=tool 回喂消息）</param>
    /// <param name="dedupCache">请求级去重复用缓存（同名同参首次结果复用）</param>
    /// <param name="tc">工具调用</param>
    /// <param name="toolResult">工具执行结果</param>
    /// <param name="toolResults">本轮工具结果表（按工具名）</param>
    /// <param name="roundSummaries">本轮工具调用摘要</param>
    /// <returns>流式需产出的事件数据；无 LLM 受众占位等场景调用方无需 yield 时返回 null 由调用方决定</returns>
    private ToolCallEventInfo? CollectToolResult(List<ChatMessage> workMessages, Dictionary<String, IToolResult> dedupCache,
        ToolCall tc, IToolResult toolResult, Dictionary<String, IToolResult> toolResults, List<ToolCallSummary> roundSummaries,
        HashSet<String> sessionDedupKeys)
    {
        var name = tc.Function!.Name;

        // 去重复用：占位结果且缓存已有首次结果 → 替换为复用结果（用户端完整展示，LLM 端简短说明不重复消耗 Token）；
        // 首次成功执行 → 写入缓存供后续去重复用
        var key = name + "|" + (tc.Function.Arguments ?? "");
        if (toolResult is ToolResult { IsError: false } tr && tr["DedupPlaceholder"] is true
            && dedupCache.TryGetValue(key, out var cached) && cached != null)
            toolResult = BuildReuseResult(name, cached);
        else if (!toolResult.IsError && GetUserContent(toolResult) != null)
        {
            dedupCache[key] = toolResult;
            // show_* 跨轮去重：仅首次真实执行成功后才占键（T-4——原在执行前占键：首次执行失败后模型重试
            // 会被去重当"已跳过执行"成功语义回喂，吞掉失败反馈；且与跨轮失败自修复引导矛盾）
            if (name.StartsWith("show_", StringComparison.OrdinalIgnoreCase)
                && toolResult is ToolResult t2 && t2["DedupPlaceholder"] is not true)
                sessionDedupKeys.Add(key);
        }

        // 失败登记：同参失败写入请求级集合，供后续轮次相同调用直接拦截（防反复重试同一失败调用）
        if (toolResult.IsError && !_failedKeys.ContainsKey(key))
        {
            var reason = GetUserContent(toolResult) ?? GetLlmContent(toolResult, name);
            _failedKeys[key] = TruncateSummary(reason) ?? "未知错误";
        }

        toolResults[name] = toolResult;
        roundSummaries.Add(new ToolCallSummary(name, toolResult.IsError, 0));

        // LLM 消息：提取 Llm 受众内容；无 Llm 内容时写占位（OpenAI 要求每个 tool_call 必须有对应 role=tool 回复）
        var llmContent = GetLlmContent(toolResult, name);
        workMessages.Add(new ChatMessage
        {
            Role = "tool",
            ToolCallId = tc.Id,
            Content = TruncateResult(llmContent)
        });

        // SSE 事件数据（流式方 yield）：取用户内容（不截断——ToolResultMaxChars 仅控制发给 AI 的内容长度，
        // 给用户展示的 SVG/HTML/图表 JSON 必须完整，否则前端解析失败）
        var userContent = GetUserContent(toolResult);
        var eventType = toolResult.IsError ? "error" : "done";
        // LlmResult 供历史回放（落库后下轮对话展开给 LLM），必须与发送给 LLM 的截断一致（5-A）：
        // 此前落库为未截断完整结果，导致历史底账拖着每个旧工具完整结果，多轮对话上下文持续膨胀
        return new ToolCallEventInfo(eventType, tc.Id, name, userContent, TruncateResult(llmContent));
    }

    /// <summary>累加每轮 LLM 调用的 Token 用量（N 次工具调用 = N+1 次 LLM 调用，每轮都有独立 Usage）。
    /// API 缺失 Usage 时回退基于工作消息的字符估算</summary>
    /// <param name="accumulated">既有累计值（跨轮，可为 null）</param>
    /// <param name="usage">本轮 Usage，可为 null（缺失时走回退估算）</param>
    /// <param name="workMessages">当前工作消息（回退估算依据）</param>
    /// <returns>累加后的新累计值（缺失时返回原值）</returns>
    private UsageDetails? AccumulateUsage(UsageDetails? accumulated, UsageDetails? usage, List<ChatMessage> workMessages)
    {
        if (usage != null)
        {
            DefaultSpan.Current?.AppendTag($"Tokens: {usage.InputTokens}+{usage.OutputTokens}={usage.TotalTokens}");
            return accumulated?.Add(usage) ?? usage;
        }

        _fallbackEstimatedTokens += TokenEstimator.EstimateTokens(workMessages);
        return accumulated;
    }

    /// <summary>构建流式单轮聚合响应。流式下每轮由多个 chunk 拼合，供工具方法通过 <see cref="ToolCallContext.Response"/> 访问本轮模型输出</summary>
    /// <param name="content">本轮正文内容</param>
    /// <param name="reasoning">本轮思考内容</param>
    /// <param name="toolCalls">本轮工具调用列表</param>
    /// <param name="finishReason">本轮结束原因（API 字符串）</param>
    /// <returns>聚合后的单轮响应</returns>
    private static ChatResponse BuildRoundResponse(String? content, String? reasoning, List<ToolCall> toolCalls, String? finishReason)
    {
        var response = new ChatResponse { Object = "chat.completion.chunk" };
        var choice = response.Add(content, reasoning, FinishReasonHelper.Parse(finishReason));
        if (toolCalls.Count > 0)
        {
            choice.Message ??= new ChatMessage { Role = "assistant" };
            choice.Message.ToolCalls = toolCalls.ToList();
        }
        return response;
    }

    /// <summary>触发工具循环迭代回调（检查点持久化等）。同步异常与异步异常均记录日志，不中断工具循环</summary>
    /// <param name="iteration">当前迭代轮次（0-based）</param>
    /// <param name="maxIterations">最大迭代轮次</param>
    /// <param name="accumulatedUsage">累计 Token 用量</param>
    /// <param name="roundSummaries">本轮工具调用摘要</param>
    /// <param name="cancellationToken">取消令牌</param>
    private void FireLoopIteration(Int32 iteration, Int32 maxIterations, UsageDetails? accumulatedUsage, List<ToolCallSummary> roundSummaries, CancellationToken cancellationToken)
    {
        if (OnLoopIteration == null) return;

        var totalTokens = accumulatedUsage?.TotalTokens ?? _fallbackEstimatedTokens;
        var state = new ToolLoopState(iteration, maxIterations, totalTokens, roundSummaries, _consecutiveFailureRounds);
        try
        {
            var task = OnLoopIteration.Invoke(state, cancellationToken);
            if (task != null)
                _ = task.ContinueWith(t =>
                {
                    if (t.IsFaulted)
                        WriteLog("工具循环迭代回调异常：{0}", t.Exception?.GetBaseException().Message ?? "");
                }, TaskScheduler.Default);
        }
        catch (Exception ex)
        {
            WriteLog("工具循环迭代回调异常：{0}", ex.Message);
        }
    }

    /// <summary>同轮/跨轮去重检查。命中去重时返回占位结果（ask_user 豁免），未命中返回 null 表示正常执行</summary>
    /// <param name="toolName">工具名称</param>
    /// <param name="arguments">参数 JSON 字符串（模型原文）</param>
    /// <param name="dedupKeys">同轮去重集合（同名同参只执行第一次）</param>
    /// <param name="sessionDedupKeys">请求级跨轮去重集合（show_* 工具整请求只执行一次）</param>
    /// <returns>去重占位结果，未命中返回 null</returns>
    private IToolResult? TryBuildDedupResult(String toolName, String? arguments, HashSet<String> dedupKeys, HashSet<String> sessionDedupKeys)
    {
        // ask_user 豁免：它每次调用问题不同
        if (toolName.EqualIgnoreCase("ask_user")) return null;

        var key = toolName + "|" + (arguments ?? "");

        // 跨轮次去重：show_* 工具在同一用户请求的多轮工具循环中只执行一次。
        // 占键在首次成功执行后（CollectToolResult）——执行前占键会吞掉失败后的重试（T-4）
        if (toolName.StartsWith("show_", StringComparison.OrdinalIgnoreCase))
        {
            if (sessionDedupKeys.Contains(key))
            {
                WriteLog("跳过跨轮次重复工具调用 {0}（已在上一轮执行过）", toolName);
                return BuildDedupResult(toolName, "跨轮次重复调用，已跳过执行");
            }
        }

        if (!dedupKeys.Add(key))
        {
            WriteLog("跳过同轮重复工具调用 {0}（同名同参）", toolName);
            return BuildDedupResult(toolName, "调用与前序重复，已跳过执行");
        }

        return null;
    }

    /// <summary>构建去重占位结果。用户受众为 duplicate 标记，LLM 受众为去重说明。
    /// 占位结果带 <c>DedupPlaceholder</c> 标记，供 Phase 2 顺序 await 时替换为复用结果（覆盖同轮并行竞态）</summary>
    /// <param name="toolName">工具名称</param>
    /// <param name="llmText">LLM 受众去重说明</param>
    private static ToolResult BuildDedupResult(String toolName, String llmText)
    {
        var dupInfo = "{\"kind\":\"duplicate\",\"for_user\":\"已跳过（重复调用）\"}";
        var result = ToolResult.ForAudiences(dupInfo, $"[已去重：{toolName}] {llmText}");
        result["DedupPlaceholder"] = true;
        return result;
    }

    /// <summary>构建去重复用结果。用户受众复用首次执行结果的完整内容（前端正常渲染图表/文本），
    /// LLM 受众为简短说明（不重复发送大结果，节省 Token）</summary>
    /// <param name="toolName">工具名称</param>
    /// <param name="cached">首次执行结果（请求级去重复用缓存）</param>
    /// <returns>复用结果</returns>
    private static ToolResult BuildReuseResult(String toolName, IToolResult cached)
    {
        var userContent = GetUserContent(cached) ?? "";
        return ToolResult.ForAudiences(userContent, $"[已复用：{toolName}] 参数与前序调用相同，已复用此前生成的结果");
    }

    /// <summary>构建跨轮重复失败占位结果。同一请求内相同工具+参数已失败过时不再执行，回传失败原因引导模型修改参数或换工具/思路</summary>
    /// <param name="toolName">工具名称</param>
    /// <param name="reason">此前失败原因摘要</param>
    private static ToolResult BuildRepeatFailureResult(String toolName, String? reason)
    {
        var dupInfo = "{\"kind\":\"repeat_failure\",\"for_user\":\"相同调用已失败，已跳过重复执行\"}";
        var llmText = $"[重复失败：{toolName}] 相同参数调用此前已失败：{(reason.IsNullOrEmpty() ? "未知原因" : reason)}。请勿原样重试，请修改参数或改用其他工具/思路。";
        return ToolResult.ForAudiences(dupInfo, llmText, true);
    }

    /// <summary>连续失败检测与升级。整轮所有工具均失败时递增计数，任一成功则归零；达到升级阈值时向消息列表注入换思路提示</summary>
    /// <param name="roundSummaries">本轮工具调用摘要</param>
    /// <param name="workMessages">工作消息列表（升级提示注入于此）</param>
    private void EvaluateFailureAndEscalate(List<ToolCallSummary> roundSummaries, List<ChatMessage> workMessages)
    {
        var allFailed = roundSummaries.Count > 0 && roundSummaries.All(s => s.IsError);
        if (allFailed)
            _consecutiveFailureRounds++;
        else
            _consecutiveFailureRounds = 0;

        if (EscalationThreshold > 0 && _consecutiveFailureRounds >= EscalationThreshold)
        {
            // 具名失败工具（对标 Harness 实证：具体错误/指引比泛化提示显著提升模型自修复率 31%→78%）
            var failedNames = String.Join("、", roundSummaries.Where(s => s.IsError).Select(s => s.ToolName).Distinct());
            workMessages.Add(new ChatMessage
            {
                Role = "user",
                Content = failedNames.IsNullOrEmpty()
                    ? $"[系统提示] 工具已连续失败 {_consecutiveFailureRounds} 轮。请换一种思路，或调用 ask_user 工具向用户寻求帮助。"
                    : $"[系统提示] 工具调用已连续失败 {_consecutiveFailureRounds} 轮（失败工具：{failedNames}）。请停止重试相同工具，修改参数或改用其他工具/思路，必要时调用 ask_user 工具向用户寻求帮助。"
            });
            _consecutiveFailureRounds = 0;
        }
    }

    /// <summary>上下文窗口预算检查（Token 守卫）。工具结果逐轮累积进消息列表，超限时优先折叠早期已消费
    /// 工具步、再截断内容、仍超才中断循环。字节不单独守卫：主流 Agent 框架均仅按 token 治理上下文，
    /// 网关字节上限（LiteLLM 默认 6MB）相对 token 窗口足够宽松（中文 1 token≈3B、ASCII≈4B，≤6B/token），
    /// Token 预算（窗口×0.85）到位时请求体字节不会先超</summary>
    /// <remarks>
    /// <para>预算由请求级 <c>MaxInputTokens</c>（token，模型窗口×0.85）传入。</para>
    /// <para>降级顺序：① 超折叠阈值（预算×0.6）时折叠早期已消费
    /// 工具步为摘要，消除每轮整包重发的二次方累积，保留最近一组完整；② 内容截断（只截不删保持
    /// assistant-tool 配对完整）；③ 全部截到最短仍超限 → 中断循环置 <see cref="ToolLoopStopReason.ContextLimit"/>。</para>
    /// <para>未设置预算（库直接使用者）时自动禁用，不影响既有行为。</para>
    /// </remarks>
    /// <param name="workMessages">当前待发送的消息列表（含已累积的工具结果）</param>
    /// <param name="toolsTokens">工具 schema 的 Token 估算（循环外一次计算）</param>
    /// <param name="request">原始请求，读取请求级预算</param>
    /// <returns>超限且无法降级时返回 true，调用方应中断循环</returns>
    private Boolean CheckContextLimit(List<ChatMessage> workMessages, Int32 toolsTokens, IChatRequest? request)
    {
        var maxInput = request?["MaxInputTokens"]?.ToInt() ?? 0;
        if (maxInput <= 0) return false;

        var estimated = TokenEstimator.EstimateTokens(workMessages) + toolsTokens;
        DefaultSpan.Current?.AppendTag($"CheckContextLimit Tokens: {estimated} (budget {maxInput})");

        // ① 折叠早期已消费工具步：一旦超过 预算×0.6 即触发，而非等顶到硬预算才一次性压缩，
        //    使每轮整包重发的体积持续受控于 预算×0.6 附近，消除二次方累积（固定 0.6，平衡保留细节与受控体积）
        if (estimated >= maxInput * 0.6
            && TryFoldConsumedToolSteps(workMessages, maxInput, toolsTokens))
        {
            // 折叠后重新计量；仍超硬预算则继续走下方截断（单组超大结果等场景）
            estimated = TokenEstimator.EstimateTokens(workMessages) + toolsTokens;
            WriteLog("上下文接近预算（估算 {0:N0} tokens，预算 {1:N0}），已折叠早期工具调用步骤，继续循环", estimated, maxInput);
        }

        // ② 未超硬预算则无需截断/中断
        if (estimated < maxInput) return false;

        // ③ 超硬预算：先尝试截断消息内容（只截不删，保持 assistant-tool 配对完整），截到预算内则继续循环
        if (TokenEstimator.TryTruncateToBudget(workMessages, maxInput - toolsTokens))
        {
            WriteLog("上下文预算已接近 {0:N0} tokens（估算 {1:N0}），已截断工具结果内容，继续工具调用循环", maxInput, estimated);
            return false;
        }

        StopReason = ToolLoopStopReason.ContextLimit;
        WriteLog("上下文预算已达到 {0:N0} tokens（当前估算 {1:N0}），且无法进一步降级，中断工具调用循环", maxInput, estimated);
        return true;
    }

    /// <summary>折叠早期已消费工具调用步骤。将"最早且已被后续回复消费"的工具步组 [assistant(tool_calls) + 其 tool 结果]
    /// 折叠为一条 assistant 摘要消息，直到累积量降到 预算×0.6 之下或仅剩最近 1 组。
    /// 只折叠非最近组（最近组是模型待回复的活跃工具步，必须保留完整），保证 assistant↔tool 协议配对不被破坏。
    /// 折叠目标：使整包重发体积受控，消除每轮整包重发的二次方累积</summary>
    /// <param name="workMessages">工作消息列表（会被就地折叠）</param>
    /// <param name="maxInput">Token 预算（≤0 不检查）</param>
    /// <param name="toolsTokens">工具 schema Token 估算</param>
    /// <returns>折叠了至少一组返回 true</returns>
    private static Boolean TryFoldConsumedToolSteps(List<ChatMessage> workMessages, Int32 maxInput, Int32 toolsTokens)
    {
        if (workMessages == null || workMessages.Count < 4) return false;

        var folded = false;
        while (true)
        {
            if (maxInput <= 0 || TokenEstimator.EstimateTokens(workMessages) + toolsTokens < maxInput * 0.6) break;

            var group = FindEarliestFoldableGroup(workMessages);
            if (group == null) break;

            var digest = BuildFoldDigest(workMessages[group.Value.Start], workMessages.GetRange(group.Value.Start + 1, group.Value.End - group.Value.Start));
            workMessages.RemoveRange(group.Value.Start, group.Value.End - group.Value.Start + 1);

            // 与紧邻的前一条折叠摘要合并（对标 LangGraph RemoveMessage：已消费步删除/合并而非堆积），
            // 避免每轮折叠都新增长摘要消息导致摘要随轮次线性膨胀
            if (group.Value.Start > 0 && IsFoldDigest(workMessages[group.Value.Start - 1]))
            {
                var prev = workMessages[group.Value.Start - 1];
                var prevText = prev.Content as String;
                prev.Content = (prevText.IsNullOrEmpty() ? "" : prevText) + "\n" + digest.Content;
            }
            else
            {
                // 首次折叠：加头部说明（仅首个折叠摘要携带，后续合并直接追加正文避免重复）并打 FoldDigest 标记供合并识别
                digest.Content = "[系统提示] 以下早期工具调用结果已压缩为摘要（如需细节请重新调用工具获取）：\n" + (digest.Content as String);
                digest.Items["FoldDigest"] = true;
                workMessages.Insert(group.Value.Start, digest);
            }
            folded = true;
        }
        return folded;
    }

    /// <summary>定位最早可折叠工具步组 [assistant(tool_calls) + 连续 tool 结果]。仅当存在至少 2 组时，
    /// 折叠除最近一组外的组（最近组为模型待回复的活跃步骤，必须保留）；无 tool 结果的组不折叠</summary>
    /// <param name="workMessages">工作消息列表</param>
    /// <returns>最早可折叠组的下标范围 [Start, End]（含 tool 结果），无可折叠组返回 null</returns>
    private static (Int32 Start, Int32 End)? FindEarliestFoldableGroup(List<ChatMessage> workMessages)
    {
        // 收集所有 assistant(tool_calls) 组 [start, end]，end 为其后连续 tool 结果的最后一条
        var groups = new List<(Int32 Start, Int32 End)>();
        for (var i = 0; i < workMessages.Count; i++)
        {
            var msg = workMessages[i];
            if (msg == null || !msg.Role.EqualIgnoreCase("assistant")) continue;
            var tcs = msg.ToolCalls;
            if (tcs == null || tcs.Count == 0) continue;

            var j = i + 1;
            while (j < workMessages.Count && workMessages[j].Role.EqualIgnoreCase("tool")) j++;
            if (j > i + 1) groups.Add((i, j - 1));
        }

        // 至少 2 组才有可折叠对象（保留最近一组完整）
        if (groups.Count < 2) return null;
        return groups[0];
    }

    /// <summary>构建工具步组的折叠摘要正文（assistant 角色文本，不带头部说明，由调用方统一加头并支持合并）。
    /// 按 tool_call_id 配对工具名，结果做头尾预览保留要点</summary>
    /// <param name="assistant">组内 assistant 消息（含 tool_calls）</param>
    /// <param name="toolMessages">组内连续的 tool 结果消息</param>
    /// <returns>折叠摘要正文消息</returns>
    private static ChatMessage BuildFoldDigest(ChatMessage assistant, List<ChatMessage> toolMessages)
    {
        var idNames = new Dictionary<String, String>();
        if (assistant.ToolCalls != null)
        {
            foreach (var tc in assistant.ToolCalls)
            {
                if (tc?.Id != null && tc.Function?.Name != null) idNames[tc.Id] = tc.Function.Name;
            }
        }

        var sb = Pool.StringBuilder.Get();
        foreach (var tm in toolMessages)
        {
            var name = tm.ToolCallId != null && idNames.TryGetValue(tm.ToolCallId, out var n) ? n : (tm.Name ?? "tool");
            // 每条工具结果保留头尾合计 300 字符，结果做头尾预览保留要点
            var brief = TruncatePreview(tm.Content as String, 300);
            if (brief.IsNullOrEmpty()) continue;
            sb.AppendLine($"- {name}: {brief}");
        }

        return new ChatMessage
        {
            Role = "assistant",
            Content = sb.Return(true),
        };
    }

    /// <summary>判断消息是否为折叠摘要（带 FoldDigest 标记的 assistant 文本），用于相邻摘要合并识别</summary>
    /// <param name="msg">消息</param>
    /// <returns>是否为折叠摘要</returns>
    private static Boolean IsFoldDigest(ChatMessage msg)
        => msg != null && msg.Role.EqualIgnoreCase("assistant")
            && (msg.ToolCalls == null || msg.ToolCalls.Count == 0)
            && msg.Items != null && msg.Items.ContainsKey("FoldDigest");

    /// <summary>文本头尾预览。超长时保留头部与尾部各半（结论性信息常在尾部），中略并标注省略字符数；
    /// 避免盲截只留头部丢失尾部结论；代理对（emoji/生僻汉字）边界安全切分（对标 OpenAI ToolOutputTrimmer 预览策略）</summary>
    /// <param name="content">原文</param>
    /// <param name="keepChars">保留字符上限（头尾合计）</param>
    /// <returns>预览文本；不超长或 keepChars≤0 时原样返回</returns>
    private static String? TruncatePreview(String? content, Int32 keepChars)
    {
        if (String.IsNullOrWhiteSpace(content)) return content;
        if (keepChars <= 0 || content!.Length <= keepChars) return content;

        var half = Math.Max(1, keepChars / 2);
        var headLen = half;
        if (Char.IsHighSurrogate(content[headLen - 1])) headLen--;
        var tailStart = content.Length - half;
        if (Char.IsLowSurrogate(content[tailStart])) tailStart++;
        // keepChars 过小导致头尾重叠时退化为纯头部截断
        if (headLen >= tailStart) return content.Substring(0, keepChars) + "...";

        var omitted = content.Length - headLen - (content.Length - tailStart);
        return content.Substring(0, headLen) +
            $"\n...(内容过长已省略中间 {omitted} 字符，原始 {content.Length} 字符；如需完整内容请缩小查询范围或重新调用工具)...\n" +
            content.Substring(tailStart);
    }

    /// <summary>按工具名路由到对应 Provider 执行工具调用。未找到则抛 <see cref="InvalidOperationException"/></summary>
    /// <param name="toolName">工具名称</param>
    /// <param name="argumentsJson">参数 JSON 字符串（模型原文）</param>
    /// <param name="toolMap">工具名到 Provider 的路由字典</param>
    /// <param name="context">工具调用上下文，透传至工具方法</param>
    /// <param name="parentSpan">父级埋点</param>
    /// <param name="cancellationToken">取消令牌</param>
    private async Task<IToolResult> ExecuteToolAsync(String toolName, String? argumentsJson, Dictionary<String, IToolProvider> toolMap, ToolCallContext context, ISpan? parentSpan, CancellationToken cancellationToken)
    {
        // async iterator 跨 yield 不保留 AsyncLocal：工具调用发生在多次 yield 之后，当前上下文已不是 ai:tool:loop。
        // NewSpan 前重新挂载父级埋点为当前上下文，使 ParentId/TraceId 由 Start 自然继承；并行多工具各自重挂互不污染。
        // （此前 NewSpan 后补 ParentId 的方案不修 TraceId，且工具存活期间上下文不一致）
        if (parentSpan != null) DefaultSpan.Current = parentSpan;

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

            // 熔断检查：Open 状态直接降级，避免雪崩效应。按 Provider+工具名 粒度熔断，避免单工具连续失败连坐同 Provider 全部工具
            var breakerKey = (provider!, toolName);
            var breaker = _breakers.GetOrAdd(breakerKey, _ => new CircuitBreakerPolicy(FailureThreshold, CooldownSeconds));
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

            // 工具返回 null 视为执行失败，转为错误结果避免下游 NRE
            callResult ??= ToolErrorResult("NULL_RESULT", $"工具 {toolName} 返回了空结果");

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
            var llmContent = toolEx.ForLlm ?? String.Empty;
            if (!llmContent.IsNullOrEmpty() &&
                !llmContent.StartsWith(toolEx.ForUser ?? String.Empty, StringComparison.Ordinal))
            {
                llmContent = $"{toolEx.ForUser}。{llmContent}";
            }
            if (!llmContent.StartsWith(toolPrefix))
                llmContent = $"{toolPrefix}{llmContent}";

            var result = ToolResult.ForAudiences(toolEx.ForUser ?? String.Empty, llmContent, true);
            await FireCallbackAsync(result).ConfigureAwait(false);
            return result;
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            // 记录完整异常（含堆栈），否则 EXECUTION_ERROR 仅回传 ex.Message，生产事故无法定位根因
            WriteLog("工具 {0} 执行异常：{1}", toolName, ex);
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
        if (content!.Length <= maxLength) return content;
        return content[..maxLength] + "...";
    }

    /// <summary>检查指定工具的执行结果是否包含 LLM 受众内容</summary>
    private static Boolean HasLlmAudience(Dictionary<String, IToolResult> results, String toolName)
        => results.TryGetValue(toolName, out var result)
            && result.Contents.Any(c => c.Audience.HasFlag(ToolAudience.Llm));

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

        if (existing == null)
        {
            // 首块可能无 Id（部分网关只发 index 或两者都不带），无 Id 也需创建条目，
            // 否则该工具调用的所有后续增量块都会因收集器为空而丢失（A-73）
            collector.Add(new ToolCall
            {
                Index = delta.Index,
                Id = delta.Id ?? String.Empty,
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

    /// <summary>判断本轮工具调用是否包含客户端工具（非任何 StarChat Provider 注册、仅客户端定义）。含任一客户端工具时整轮透传，
    /// 由客户端执行后在下一次请求回传；仅当全部工具均为 StarChat 工具（含 SelectedTools 过滤外的目录工具）时才由服务端执行</summary>
    /// <param name="toolCalls">本轮工具调用列表</param>
    /// <param name="toolMap">工具名到 Provider 的路由字典（仅当前 SelectedTools 可见工具）</param>
    /// <param name="providerToolNames">全部 Provider 注册的工具名（不受 SelectedTools 过滤），用于区分客户端工具与目录工具</param>
    /// <returns>true=含客户端工具，需整轮透传</returns>
    private static Boolean HasClientToolCalls(IList<ToolCall> toolCalls, Dictionary<String, IToolProvider> toolMap, HashSet<String> providerToolNames)
        => toolCalls.Any(tc => tc.Function?.Name != null
            && !toolMap.ContainsKey(tc.Function.Name)
            && !providerToolNames.Contains(tc.Function.Name));

    /// <summary>聚合所有提供者的工具定义，合并 options.Tools，同时建立工具名到 Provider 的路由字典与全部 Provider 工具名集合</summary>
    private (List<ChatTool> tools, Dictionary<String, IToolProvider> toolMap, HashSet<String> providerToolNames) GetMergedTools(IChatRequest? options)
    {
        var tools = new List<ChatTool>();
        var toolMap = new Dictionary<String, IToolProvider>(StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
        // 全部 Provider 注册的工具路由（不受 SelectedTools 过滤），用于目录工具识别与同名客户端工具路由
        var providerNames = new Dictionary<String, IToolProvider>(StringComparer.OrdinalIgnoreCase);
        foreach (var provider in Providers)
        {
            foreach (var t in provider.GetTools(null))
            {
                var n = t.Function?.Name;
                if (!n.IsNullOrEmpty() && !providerNames.ContainsKey(n))
                    providerNames[n] = provider;
            }
        }
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
            // options.Tools 中的工具尝试按名字路由到已有 Provider（同名 StarChat 优先）；
            // 路由不到且非 Provider 注册 → 客户端工具，整轮透传由客户端执行
            foreach (var t in options.Tools)
            {
                var name = t.Function?.Name;
                if (!name.IsNullOrEmpty() && !toolMap.ContainsKey(name) && providerNames.TryGetValue(name, out var owner))
                    toolMap[name] = owner;
                else if (!name.IsNullOrEmpty() && !providerNames.ContainsKey(name))
                    WriteLog("options.Tools 中工具 {0} 无对应 Provider 路由，将由客户端透传执行", name);
                tools.Add(t);
            }
        }

        // 埋点：记录注入给 LLM 的工具名单和 schema 总字符长度，便于评估 token 消耗
        if (tools.Count > 0)
        {
            var toolNames = String.Join(",", tools.Select(t => t.Function?.Name).Where(n => !n.IsNullOrEmpty()));
            using var schemaSpan = Tracer?.NewSpan("ai:tool:schema", null, tools.Count);
            schemaSpan?.AppendTag(toolNames);
        }

        return (tools, toolMap, [.. providerNames.Keys]);
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
            // 直接引用共享 request.Items（故意设计）：与 ChatRequest.Create 保持一致，
            // 工具循环内协议层写入的扩展键值同步反映到原始 request，多轮持续保留。
            // 勿改为拷贝（A-53 曾误改，已恢复）
            Items = request?.Items ?? new Dictionary<String, Object?>(),
        };

    /// <summary>按 <see cref="ToolSetting"/> 的 ToolResultMaxChars 截断过长结果，防止撑满 LLM Context Window。
    /// 头尾预览策略（保留首尾各半）：避免盲截只留头部丢失位于结果尾部的结论性信息（SQL 汇总、搜索结论常在末尾）</summary>
    /// <param name="result">工具原始返回文本</param>
    /// <returns>截断后的文本，不超限时原样返回</returns>
    private String? TruncateResult(String? result)
    {
        var maxResultChars = ToolSetting?.ToolResultMaxChars ?? 0;
        if (maxResultChars <= 0 || result == null) return result;

        return TruncatePreview(result, maxResultChars);
    }

    #endregion

    #region 日志
    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>追踪器</summary>
    public ITracer? Tracer { get; set; }

    /// <summary>写日志并同步写入当前埋点标签，便于分析跟踪（同一调用链上标签与日志对应）。
    /// 统一 Info 级输出，级别区分依赖埋点标签与日志详情</summary>
    /// <param name="format">日志格式</param>
    /// <param name="args">格式化参数</param>
    private void WriteLog(String format, params Object[] args)
    {
        var msg = args == null || args.Length == 0 ? format : String.Format(format, args);

        // 日志全文落盘；埋点标签截断防超大文本（异常堆栈等）撑爆标签存储
        DefaultSpan.Current?.AppendTag(msg.Length <= 1000 ? msg : msg[..1000]);
        Log?.Info(msg);
    }
    #endregion
}

/// <summary>工具调用循环终止原因。供上层结构化判断循环结束形态；对标 LangChain AgentFinish/AgentAction 与 OpenAI Agents SDK 的 run 终态</summary>
public enum ToolLoopStopReason
{
    /// <summary>正常完成：模型产出最终回复（无更多工具调用），或未执行任何工具</summary>
    Completed = 0,

    /// <summary>达到工具调用轮次上限 <see cref="ToolSetting"/> 的 ToolMaxIterations</summary>
    MaxIterations,

    /// <summary>上下文窗口预算超限中断（单请求窗口 Token/字节双守卫）。上层用 <c>StopReason == ToolLoopStopReason.ContextLimit</c> 判断</summary>
    ContextLimit,

    /// <summary>本轮含客户端工具调用，整轮透传给客户端执行</summary>
    Passthrough,
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