using System.Runtime.CompilerServices;
using NewLife.AI.Clients;
using NewLife.AI.Models;
using NewLife.AI.Tools;
using NewLife.Collections;
using NewLife.Log;

namespace NewLife.AI.Services;

/// <summary>轻量 AI 对话编排服务。管理会话历史，调用 <see cref="ToolChatClient"/> 工具循环，产出规范 <see cref="ChatStreamEvent"/> 事件流</summary>
/// <remarks>
/// <para>
/// 定位：介于 <see cref="ToolChatClient"/>（裸工具循环）与 <c>MessageFlow</c>（完整对话平台，DB 持久化）之间的轻量档。
/// 无数据库依赖，会话历史默认内存（可注入 <see cref="IChatSessionStore"/>），适合页面内嵌 AI 助手等场景。
/// </para>
/// <para>
/// 事件协议为规范 <see cref="ChatStreamEvent"/> 序列（message_start / thinking_delta / content_delta / tool_call_start|done|error / message_done / error），
/// 宿主直接序列化该事件流输出（如 SSE），前端按规范协议解析，无需协议投影。
/// </para>
/// <para>
/// 使用示例：
/// <code>
/// var client = AiClientRegistry.Default.CreateClient("DeepSeek", apiKey, "deepseek-chat");
/// var ai = new AiChatService(client);
/// await foreach (var ev in ai.ChatAsync(req, systemPrompt, providers))
/// {
///     // 转 SSE 输出
/// }
/// </code>
/// </para>
/// </remarks>
/// <remarks>实例化轻量 AI 对话编排服务</remarks>
/// <param name="client">底层 AI 客户端（含工具调用能力）</param>
/// <param name="sessions">会话历史服务；为 null 时使用默认内存实现</param>
/// <param name="providers">默认工具提供者列表；为 null 时为空。调用时可按需覆盖</param>
public class AiChatService(IChatClient client, ChatSessionService? sessions = null, IList<IToolProvider>? providers = null) : ILogFeature, ITracerFeature
{
    #region 属性
    /// <summary>底层 AI 客户端（含工具调用能力）</summary>
    public IChatClient Client { get; } = client ?? throw new ArgumentNullException(nameof(client));

    /// <summary>会话历史管理</summary>
    public ChatSessionService Sessions { get; } = sessions ?? new ChatSessionService();

    /// <summary>工具调用配置。为 null 时使用 ToolChatClient 内置默认值（MaxIterations=10 等）</summary>
    public IToolSetting? ToolSetting { get; set; }

    /// <summary>工具执行回调。每次工具调用完成后触发（透传给 <see cref="ToolChatClient.OnToolExecuted"/>）</summary>
    public Func<ToolCallEventArgs, Task>? OnToolExecuted { get; set; }

    /// <summary>空响应兜底文案。AI 未返回有效结果（如工具回合未完成）时推送提示，置空可禁用</summary>
    public String EmptyResponseNote { get; set; } = "⚠️ AI 未返回有效结果。若需要数据分析/填表等工具能力，请确认 AI 服务商支持函数调用（如 DeepSeek/OpenAI/Ollama 工具模型）。";

    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>追踪器</summary>
    public ITracer? Tracer { get; set; }

    private readonly IList<IToolProvider> _providers = providers ?? [];
    #endregion

    #region 方法
    /// <summary>执行 AI 对话（含工具调用）。管理会话历史，流式/非流式统一产出规范事件序列</summary>
    /// <remarks>
    /// 事件序列：<c>message_start</c> → （<c>thinking_delta</c> / <c>content_delta</c> / <c>tool_call_*</c>）→ <c>message_done</c>；
    /// 异常时产出 <c>error</c>；无有效输出且未出错时产出空响应兜底提示。
    /// 会话编号为空时按单轮处理（不保留历史）；非空时自动追加用户消息与助手回复。
    /// </remarks>
    /// <param name="request">对话请求（含会话编号、消息、思考、流式标志）</param>
    /// <param name="systemPrompt">系统提示词（注入页面上下文），由宿主构建</param>
    /// <param name="providers">工具提供者列表（按工具名路由）；为 null 时使用构造注入的默认列表</param>
    /// <param name="options">对话选项（模型、温度、思考模式等）；为 null 时按 <see cref="AiChatRequest.Think"/> 构建默认</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>规范事件流</returns>
    public async IAsyncEnumerable<ChatStreamEvent> ChatAsync(
        AiChatRequest request,
        String systemPrompt,
        IList<IToolProvider>? providers = null,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (request.Message.IsNullOrEmpty()) throw new ArgumentNullException(nameof(request), "消息不能为空");

        // 默认选项：思考模式开思考、降温度
        options ??= new ChatOptions
        {
            EnableThinking = request.Think,
            Temperature = request.Think ? 0.5 : 0.3,
        };

        // 组装消息（system + 历史 + 当前消息）并追加用户消息到会话（供下一轮使用）
        var messages = BuildMessages(request, systemPrompt);
        if (!request.SessionId.IsNullOrEmpty())
            Sessions.Append(request.SessionId, new ChatMessage { Role = "user", Content = request.Message });

        // 工具循环客户端
        var toolClient = new ToolChatClient(Client, [.. providers ?? _providers])
        {
            Log = Log,
            Tracer = Tracer,
            ToolSetting = ToolSetting,
            OnToolExecuted = OnToolExecuted,
        };

        var state = new FlowState();
        var hasOutput = false;
        var hasError = false;
        var sb = Pool.StringBuilder.Get();

        // 开始事件
        yield return ChatStreamEvent.MessageStart(0, options.Model ?? "");

        // 核心事件源（LLM 调用 + 工具事件），异常向上传播由下方 moveNext 捕获
        var source = request.Stream
            ? StreamCoreAsync(toolClient, messages, options, state, cancellationToken)
            : OnceCoreAsync(toolClient, messages, options, state, cancellationToken);

        ChatStreamEvent? errorEvent = null;
        var enumerator = source.GetAsyncEnumerator(cancellationToken);
        try
        {
            while (true)
            {
                Boolean moved;
                try
                {
                    moved = await enumerator.MoveNextAsync().ConfigureAwait(false);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    hasError = true;
                    WriteLog("AI 对话失败", ex.ToString());
                    var info = ChatErrorHelper.Classify(ex.Message);
                    errorEvent = ChatStreamEvent.ErrorEvent(info.Code, info.Message);
                    moved = false;
                }
                if (errorEvent != null) { yield return errorEvent; break; }
                if (!moved) break;

                var ev = enumerator.Current;
                switch (ev.Type)
                {
                    case "content_delta":
                        hasOutput = true;
                        sb.Append(ev.Content);
                        yield return ev;
                        break;
                    case "thinking_delta":
                        // 思考也算有效输出，避免误触发空响应兜底
                        hasOutput = true;
                        yield return ev;
                        break;
                    default:
                        yield return ev;
                        break;
                }
            }
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
        }

        // 空响应兜底：工具回合未完成（常见于服务商不支持函数调用），给出提示而非静默结束
        if (!hasOutput && !hasError && !EmptyResponseNote.IsNullOrEmpty())
            yield return ChatStreamEvent.ContentDelta($"\n\n> {EmptyResponseNote}");

        // 保存助手回复到会话历史
        var reply = sb.Return(true);
        if (!request.SessionId.IsNullOrEmpty() && !reply.IsNullOrEmpty())
            Sessions.Append(request.SessionId, new ChatMessage { Role = "assistant", Content = reply });

        // 结束事件
        yield return ChatStreamEvent.MessageDone(state.Usage, finishReason: state.FinishReason);
    }

    /// <summary>流式核心：逐块输出文本/思考/工具事件。异常直接向上传播（不含 try-catch，迭代器限制）</summary>
    /// <param name="toolClient">工具循环客户端</param>
    /// <param name="messages">完整消息列表（含 system）</param>
    /// <param name="options">对话选项</param>
    /// <param name="state">共享状态（用量/完成原因）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>文本/思考/工具事件流</returns>
    private async IAsyncEnumerable<ChatStreamEvent> StreamCoreAsync(
        ToolChatClient toolClient, IList<ChatMessage> messages, ChatOptions options, FlowState state,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var chunk in toolClient.GetStreamingResponseAsync(ChatRequest.Create(messages, options, true), cancellationToken).ConfigureAwait(false))
        {
            // 工具调用事件：由 ToolChatClient 完成工具执行后的聚合事件
            if (chunk is ChatResponse cr && cr.ToolCallEvents is { Count: > 0 } events)
            {
                foreach (var ev in events)
                {
                    yield return ToToolEvent(ev);
                }
                continue;
            }

            if (chunk.Usage != null) state.Usage = chunk.Usage;

            var choice = chunk.Messages?.FirstOrDefault();
            if (choice == null) continue;

            if (choice.FinishReason != null)
                state.FinishReason = choice.FinishReason.Value.ToApiString();

            // 思考 + 文本增量
            var delta = choice.Delta;
            if (delta == null) continue;

            if (!String.IsNullOrEmpty(delta.ReasoningContent))
                yield return ChatStreamEvent.ThinkingDelta(delta.ReasoningContent);

            var text = delta.Content as String;
            if (!String.IsNullOrEmpty(text))
                yield return ChatStreamEvent.ContentDelta(text);
        }
    }

    /// <summary>非流式核心：一次返回完整响应（含工具调用事件与文本）。异常直接向上传播（不含 try-catch，迭代器限制）</summary>
    /// <param name="toolClient">工具循环客户端</param>
    /// <param name="messages">完整消息列表（含 system）</param>
    /// <param name="options">对话选项</param>
    /// <param name="state">共享状态（用量/完成原因）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>文本/工具事件流</returns>
    private async IAsyncEnumerable<ChatStreamEvent> OnceCoreAsync(
        ToolChatClient toolClient, IList<ChatMessage> messages, ChatOptions options, FlowState state,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var response = await toolClient.GetResponseAsync(ChatRequest.Create(messages, options, false), cancellationToken).ConfigureAwait(false);
        state.Usage = response?.Usage;

        // 完成原因：与流式路径对称，从首个 Choice 读取，供 message_done 透传
        var choice = response?.Messages?.FirstOrDefault();
        if (choice?.FinishReason != null) state.FinishReason = choice.FinishReason.Value.ToApiString();

        if (response is ChatResponse cr && cr.ToolCallEvents != null)
        {
            foreach (var ev in cr.ToolCallEvents)
            {
                yield return ToToolEvent(ev);
            }
        }

        var text = response?.Text;
        if (!text.IsNullOrEmpty())
            yield return ChatStreamEvent.ContentDelta(text);
    }

    /// <summary>组装对话消息：system + 会话历史（裁剪到上限）+ 当前用户消息</summary>
    /// <param name="request">对话请求</param>
    /// <param name="systemPrompt">系统提示词</param>
    /// <returns>完整消息列表</returns>
    private List<ChatMessage> BuildMessages(AiChatRequest request, String systemPrompt)
    {
        var history = !request.SessionId.IsNullOrEmpty() ? Sessions.GetHistory(request.SessionId) : null;

        var messages = new List<ChatMessage>
        {
            new() { Role = "system", Content = systemPrompt }
        };
        if (history != null && history.Count > 0)
        {
            // 历史已由会话服务裁剪到上限，此处防御性取尾部
            if (history.Count > Sessions.MaxHistory)
                messages.AddRange(history.Skip(history.Count - Sessions.MaxHistory));
            else
                messages.AddRange(history);
        }
        messages.Add(new ChatMessage { Role = "user", Content = request.Message });

        return messages;
    }

    /// <summary>将工具调用事件信息转换为规范 SSE 事件。done/error 补工具名，供前端/网关还原</summary>
    /// <param name="ev">工具调用事件信息</param>
    /// <returns>规范事件</returns>
    private static ChatStreamEvent ToToolEvent(ToolCallEventInfo ev)
    {
        switch (ev.Type)
        {
            case "done":
            {
                var e = ChatStreamEvent.ToolCallDone(ev.ToolCallId, ev.Value);
                e.Name = ev.Name;
                return e;
            }
            case "error":
            {
                var e = ChatStreamEvent.ToolCallError(ev.ToolCallId, ev.Value ?? String.Empty);
                e.Name = ev.Name;
                return e;
            }
            default:
                return ChatStreamEvent.ToolCallStart(ev.ToolCallId, ev.Name, ev.Value);
        }
    }

    #endregion

    #region 内部
    /// <summary>核心流程共享状态。流式/非流式核心产出期间由子迭代器填充，供外层消息完成事件使用</summary>
    private sealed class FlowState
    {
        /// <summary>令牌用量统计</summary>
        public UsageDetails? Usage;

        /// <summary>完成原因</summary>
        public String? FinishReason;
    }
    #endregion

    #region 日志
    /// <summary>写日志</summary>
    /// <param name="action">动作</param>
    /// <param name="message">消息</param>
    private void WriteLog(String action, String message) => Log.Info("[AI] {0} {1}", action, message);
    #endregion
}
