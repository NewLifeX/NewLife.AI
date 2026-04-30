using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using NewLife.AI.Clients;
using NewLife.AI.Filters;
using NewLife.AI.Tools;
using NewLife.ChatAI.Tools;
using NewLife.Collections;
using NewLife.Log;
using NewLife.Serialization;
using XCode.Membership;
using AiFunctionCall = NewLife.AI.Models.FunctionCall;
using AiToolCall = NewLife.AI.Models.ToolCall;
using ChatResponse = NewLife.AI.Models.ChatResponse;
using ILog = NewLife.Log.ILog;
using UsageDetails = NewLife.AI.Models.UsageDetails;

namespace NewLife.ChatAI.Services;

/// <summary>消息生成流程基类。承载 <b>Validate → Prepare → Execute → Persist → PostProcess</b> 五段式模板方法，
/// 对外暴露 4 大入口：<see cref="StreamMessageAsync"/> / <see cref="RegenerateMessageAsync"/> /
/// <see cref="RegenerateStreamAsync"/> / <see cref="EditAndResendStreamAsync"/></summary>
/// <remarks>
/// <para>
/// 所有关键步骤方法均为 <c>protected virtual</c>，派生类（ChatAI 社区版 / StarChat 商用版）可按需覆盖以注入差异化逻辑。
/// 能力扩展（工具调用、技能注入）与知识进化（记忆、自学习）通过 <see cref="IChatHandler"/> 链透明接入。
/// </para>
/// <para>
/// 本基类提供 <b>简化版</b> 系统提示词与多模态构建，仅依赖 NewLife.Core / XCode / 实体，
/// 不引入 Cube.Entity.Department 与 NewLife.Office 等上层依赖；派生类按需增强。
/// </para>
/// </remarks>
/// <remarks>实例化消息流基类</remarks>
/// <param name="modelService">模型服务</param>
/// <param name="backgroundService">后台生成服务</param>
/// <param name="setting">对话配置</param>
/// <param name="tracer">追踪器</param>
/// <param name="log">日志</param>
/// <param name="services">服务提供者，用于解析 <see cref="IChatHandler"/> 链；为 null 时使用空链路（仅用于测试场景）</param>
public class MessageFlow(ModelService modelService, BackgroundGenerationService? backgroundService, IChatSetting setting, ITracer? tracer, ILog? log, IServiceProvider? services = null) : IMessageFlow
{
    #region 字段
    /// <summary>对话处理器链（DI 注册的 <see cref="IChatHandler"/>，<b>按注册顺序即外→内顺序</b>）。
    /// 为空时核心仍会执行（仅 LLM 调用，无任何事前/事后扩展）。
    /// 派生类可在构造函数体内赋值，以注入专属 Handler 子集（如 GatewayMessageFlow）</summary>
    protected IReadOnlyList<IChatHandler> Handlers = services?.GetServices<IChatHandler>().ToArray() ?? [];

    /// <summary>工具提供者集合（DbToolProvider / McpClientService 等），由 DI 解析。供 <see cref="InvokeLlmAsync"/> 使用</summary>
    protected IReadOnlyList<IToolProvider> ToolProviders = services?.GetServices<IToolProvider>().ToArray() ?? [];

    /// <summary>聊天过滤器链（日志、学习、Agent 触发等），由 DI 解析。供 <see cref="InvokeLlmAsync"/> 使用</summary>
    protected IReadOnlyList<IChatFilter> ChatFilters = services?.GetServices<IChatFilter>().ToArray() ?? [];


    #endregion

    #region 生成入口

    /// <summary>非流式重新生成 AI 回复。构建上下文后通过非流式 Handler 三段式调用链执行（OnBefore → InvokeLlmDirectAsync → OnAfter），
    /// 结果直接写入上下文收集器后由事后 Handler 持久化。<see cref="IChatInterceptor"/> 仅适用于流式路径，此方法不经过拦截器洋葱</summary>
    /// <param name="messageId">消息编号（必须为 AI 回复）</param>
    /// <param name="userId">当前用户编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>更新后的消息 DTO，失败时返回 null</returns>
    public virtual async Task<MessageDto?> RegenerateMessageAsync(Int64 messageId, Int32 userId, CancellationToken cancellationToken)
    {
        // Step1: 验证参数与准备
        var flow = CreateFlowContext(messageId, "assistant", null, null, userId);
        flow.Kind = FlowKind.Regenerate;
        if (flow.Error != null) return null;

        try
        {
            // Step2: 构建对话上下文
            await BuildContextForRegenerateAsync(flow, cancellationToken).ConfigureAwait(false);

            // Step3: 通过非流式 Handler 三段式调用链执行（持久化由事后 Handler 完成）
            await InvokeNonStreamAsync(flow, cancellationToken).ConfigureAwait(false);

            flow.AssistantMessage.ElapsedMs = (Int32)(flow.Usage?.ElapsedMs ?? 0);

            return ToMessageDto(flow.AssistantMessage);
        }
        catch (Exception ex)
        {
            DefaultSpan.Current?.SetError(ex);
            log?.Error("重新生成回复失败: {0}", ex.Message);
            return null;
        }
    }

    /// <summary>编辑用户消息并流式重新发送。依次：更新消息内容 → 删除后续所有消息 → 构建上下文 → 委托管道生成</summary>
    /// <param name="messageId">用户消息编号</param>
    /// <param name="newContent">编辑后的内容</param>
    /// <param name="userId">当前用户编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>SSE 事件流</returns>
    public virtual async IAsyncEnumerable<ChatStreamEvent> EditAndResendStreamAsync(Int64 messageId, String newContent, Int32 userId, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Step 1: 验证参数与准备
        var flow = CreateFlowContext(messageId, "user", null, null, userId);
        flow.Kind = FlowKind.EditAndResendStream;
        flow["NewUserContent"] = newContent;
        if (flow.Error != null)
        {
            yield return ChatStreamEvent.ErrorEvent(flow.Error.Code, flow.Error.Message);
            yield break;
        }

        // 更新消息内容
        var userMessage = flow.UserMessage!;
        userMessage.Content = newContent;
        userMessage.Update();

        // 预分配 AI 回复消息
        var assistantMsg = new DbChatMessage
        {
            ConversationId = userMessage.ConversationId,
            Role = "assistant",
            ThinkingMode = userMessage.ThinkingMode,
        };
        assistantMsg.Insert();
        flow.AssistantMessage = assistantMsg;

        // Step2: 构建对话上下文
        await BuildContextAsync(flow, newContent, cancellationToken).ConfigureAwait(false);

        yield return ChatStreamEvent.MessageStart(assistantMsg.Id, flow.ModelConfig.Code!, userMessage.ThinkingMode);

        // Step3: 执行 IChatHandler 三段式调用链（持久化由 PersistMessageHandler 等事后 Handler 完成）
        await foreach (var ev in InvokeChainAsync(flow, cancellationToken).ConfigureAwait(false))
        {
            yield return ev;
        }

        if (!flow.HasError && !cancellationToken.IsCancellationRequested)
            yield return new ChatStreamEvent { Type = "message_done", MessageId = assistantMsg.Id, Usage = flow.Usage, };
    }

    /// <summary>流式重新生成 AI 回复。替换当前 AI 回复并通过 SSE 事件流返回新内容</summary>
    /// <param name="messageId">消息编号（必须为 AI 回复）</param>
    /// <param name="userId">当前用户编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>SSE 事件流</returns>
    public virtual async IAsyncEnumerable<ChatStreamEvent> RegenerateStreamAsync(Int64 messageId, Int32 userId, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Step 1: 验证参数与准备
        var flow = CreateFlowContext(messageId, "assistant", null, null, userId);
        flow.Kind = FlowKind.RegenerateStream;
        if (flow.Error != null)
        {
            yield return ChatStreamEvent.ErrorEvent(flow.Error.Code, flow.Error.Message);
            yield break;
        }

        // Step2: 构建对话上下文
        await BuildContextForRegenerateAsync(flow, cancellationToken).ConfigureAwait(false);

        // message_start
        var assistantMessage = flow.AssistantMessage;
        yield return ChatStreamEvent.MessageStart(assistantMessage.Id, flow.ModelConfig.Code!, assistantMessage.ThinkingMode);

        // Step3: 执行 IChatHandler 三段式调用链（持久化由 PersistMessageHandler 等事后 Handler 完成）
        await foreach (var ev in InvokeChainAsync(flow, cancellationToken).ConfigureAwait(false))
        {
            yield return ev;
        }

        // message_done
        if (!flow.HasError && !cancellationToken.IsCancellationRequested)
            yield return new ChatStreamEvent { Type = "message_done", MessageId = assistantMessage.Id, Usage = flow.Usage };
    }

    /// <summary>流式发送消息并获取 AI 回复。依次：保存用户消息 → 构建上下文 → 委托管道流式生成 → 持久化结果 → 推送 SSE 事件</summary>
    /// <param name="conversationId">会话编号</param>
    /// <param name="request">发送消息请求</param>
    /// <param name="userId">当前用户编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>SSE 事件流，含 message_start / thinking_delta / content_delta / tool_call_* / message_done / error</returns>
    public virtual async IAsyncEnumerable<ChatStreamEvent> StreamMessageAsync(Int64 conversationId, SendMessageRequest request, Int32 userId, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Step 1: 验证参数与准备
        var flow = CreateFlowContext(null, null, conversationId, request.ModelId, userId);
        flow.Kind = FlowKind.Stream;
        if (flow.Error != null)
        {
            yield return ChatStreamEvent.ErrorEvent(flow.Error.Code, flow.Error.Message);
            yield break;
        }

        // 更新会话绑定的模型（首次发消息或 model_id=0 自动选模型时持久化实际使用的模型）
        var model = flow.ModelConfig;
        var conversation = flow.Conversation;
        if (conversation.ModelId != model.Id)
        {
            conversation.ModelId = model.Id;
            conversation.Update();
        }

        // 保存用户消息
        var userMsg = new DbChatMessage
        {
            ConversationId = conversationId,
            Role = "user",
            Content = request.Content,
            ThinkingMode = request.ThinkingMode,
            ModelName = model.Code,
        };
        if (request.AttachmentIds is { Count: > 0 })
            userMsg.Attachments = request.AttachmentIds.ToJson();
        userMsg.Insert();

        // 预分配AI回复消息编号
        var assistantMsg = new DbChatMessage
        {
            ConversationId = conversationId,
            Role = "assistant",
            ThinkingMode = request.ThinkingMode,
            ModelName = model.Code,
        };
        assistantMsg.Insert();

        flow.UserMessage = userMsg;
        flow.AssistantMessage = assistantMsg;
        flow.SkillId = conversation.SkillId;
        flow["SkillName"] = conversation.SkillName;
        flow["RequestSkillCode"] = request.SkillCode;
        flow.ThinkingMode = request.ThinkingMode;

        // Step2: 构建对话上下文
        await BuildContextAsync(flow, request.Content, cancellationToken).ConfigureAwait(false);

        // message_start
        using var span = tracer?.NewSpan($"ai:Stream:{model.Code}", request.Content);
        yield return ChatStreamEvent.MessageStart(assistantMsg.Id, model.Code!, request.ThinkingMode);

        // 注册系统消息就绪回调（在 InvokeLlmAsync 收到首个 chunk 时被调用）
        flow.OnSystemReady = sysContent =>
        {
            if (!sysContent.IsNullOrEmpty())
            {
                // 借用用户消息的思考字段来保存系统提示词内容
                userMsg.ThinkingContent = sysContent;
                userMsg.Update();
            }
        };

        // 记录请求选项供 Handler 读取
        if (request.Options is { Count: > 0 })
        {
            foreach (var kv in request.Options)
                flow.Items[kv.Key] = kv.Value;
        }

        // Step3: 执行 IChatHandler 三段式调用链（OnBefore 正序 → 核心 LLM 调用 → OnAfter 倒序）
        await foreach (var ev in InvokeChainAsync(flow, cancellationToken).ConfigureAwait(false))
        {
            yield return ev;
        }

        // message_done
        if (!flow.HasError && !cancellationToken.IsCancellationRequested)
        {
            yield return new ChatStreamEvent { Type = "message_done", MessageId = assistantMsg.Id, Usage = flow.Usage, };
        }
    }

    /// <summary>中断生成。停止后台正在运行的流式生成任务</summary>
    /// <param name="messageId">消息编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public virtual Task StopGenerateAsync(Int64 messageId, CancellationToken cancellationToken)
    {
        backgroundService?.Stop(messageId);
        return Task.CompletedTask;
    }

    /// <summary>获取后台生成任务状态。用户切换会话再切回时，可获取后台已生成的内容</summary>
    /// <param name="messageId">消息编号</param>
    /// <returns>后台任务状态信息，不存在返回 null</returns>
    public virtual BackgroundTask? GetBackgroundTask(Int64 messageId) => backgroundService?.GetTask(messageId);

    #endregion

    #region 步骤方法

    /// <summary>初始化流程上下文。按消息编号或会话编号查找实体、验证角色、解析模型，组装 <see cref="MessageFlowContext"/></summary>
    /// <remarks>
    /// 三种调用模式：
    /// <list type="bullet">
    /// <item>按消息：传 messageId + expectedRole，自动查找所属会话和模型</item>
    /// <item>按会话：传 conversationId + modelId，由调用方后续填充 UserMessage/AssistantMessage</item>
    /// <item>混合：两者都传时，messageId 优先</item>
    /// </list>
    /// </remarks>
    /// <param name="messageId">消息编号（可选，传入时查消息并验证角色）</param>
    /// <param name="expectedRole">期望消息角色（当 messageId 有值时必传，"user" 或 "assistant"）</param>
    /// <param name="conversationId">会话编号（可选，当 messageId 无值时使用）</param>
    /// <param name="modelId">请求指定的模型编号（0 或 null 时使用会话绑定模型，仅 conversationId 模式使用）</param>
    /// <param name="userId">当前用户编号</param>
    /// <returns>初始化后的流程上下文，验证失败时 <see cref="MessageFlowContext.Error"/> 不为 null</returns>
    protected virtual MessageFlowContext CreateFlowContext(Int64? messageId, String? expectedRole, Int64? conversationId, Int32? modelId, Int32 userId)
    {
        using var span = tracer?.NewSpan("ai:CreateFlowContext", new { messageId, expectedRole, conversationId, modelId, userId });

        DbChatMessage? entity = null;
        Conversation? conversation;

        if (messageId > 0)
        {
            // 按消息查找
            entity = DbChatMessage.FindById(messageId.Value);
            if (entity == null || (!expectedRole.IsNullOrEmpty() && !entity.Role.EqualIgnoreCase(expectedRole)))
                return new MessageFlowContext { Error = new ChatException("MESSAGE_NOT_FOUND", "消息不存在或角色不匹配") };

            conversation = Conversation.FindById(entity.ConversationId);
        }
        else
        {
            // 按会话查找
            conversation = conversationId > 0 ? Conversation.FindById(conversationId.Value) : null;
        }

        if (conversation == null)
            return new MessageFlowContext { Error = new ChatException("CONVERSATION_NOT_FOUND", "会话不存在") };

        // 解析模型：按消息模式用 ResolveModel + IsAvailable；按会话模式用 ResolveModelOrDefault（支持降级）
        ModelConfig? modelConfig;
        if (messageId > 0)
        {
            modelConfig = modelService.ResolveModel(conversation.ModelId);
            if (modelConfig == null || !modelService.IsAvailable(modelConfig))
                return new MessageFlowContext { Error = new ChatException("MODEL_UNAVAILABLE", $"模型 '{conversation.ModelName}' 不可用") };
        }
        else
        {
            var effectiveModelId = modelId > 0 ? modelId.Value : conversation.ModelId;
            modelConfig = modelService.ResolveModelOrDefault(effectiveModelId);
            if (modelConfig == null)
                return new MessageFlowContext { Error = new ChatException("MODEL_UNAVAILABLE", "系统暂无可用模型，请先在管理后台配置并启用至少一个模型") };
        }

        var flow = new MessageFlowContext
        {
            Conversation = conversation,
            ModelConfig = modelConfig,
            UserId = userId,
            SkillId = conversation.SkillId,
        };
        flow["SkillName"] = conversation.SkillName;

        // 按消息模式：自动填充 UserMessage 或 AssistantMessage
        if (entity != null)
        {
            if (entity.Role.EqualIgnoreCase("user"))
                flow.UserMessage = entity;
            else
                flow.AssistantMessage = entity;
        }

        return flow;
    }

    /// <summary>构建对话上下文。从历史消息构建 AI 对话上下文，包含系统提示词注入</summary>
    /// <param name="flow">流程上下文</param>
    /// <param name="currentContent">当前用户消息内容</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>OpenAI ChatMessage 格式的消息列表</returns>
    protected virtual Task<IList<AiChatMessage>> BuildContextAsync(MessageFlowContext flow, String currentContent, CancellationToken cancellationToken)
    {
        using var span = tracer?.NewSpan("ai:BuildContext");

        var maxRounds = setting.DefaultContextRounds > 0 ? setting.DefaultContextRounds : 10;
        var history = LoadHistoryMessages(flow.Conversation.Id, maxRounds);
        var contextMessages = BuildContextMessages(flow.UserId, flow.ModelConfig, history, currentContent);
        flow.ContextMessages = contextMessages;
        DefaultSpan.Current?.AppendTag(contextMessages.Join());

        return Task.FromResult(contextMessages);
    }

    /// <summary>为重新生成场景构建上下文。取目标消息之前的历史消息，注入系统提示词</summary>
    /// <param name="flow">流程上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>OpenAI ChatMessage 格式的消息列表</returns>
    protected virtual Task<IList<AiChatMessage>> BuildContextForRegenerateAsync(MessageFlowContext flow, CancellationToken cancellationToken)
    {
        using var span = tracer?.NewSpan("ai:BuildContextForRegenerate");

        var entity = flow.AssistantMessage;
        var beforeMessages = DbChatMessage.FindAllBeforeId(entity.ConversationId, entity.Id);

        var maxCount = (setting.DefaultContextRounds > 0 ? setting.DefaultContextRounds : 10) * 2;
        if (beforeMessages.Count > maxCount)
            beforeMessages = beforeMessages.Skip(beforeMessages.Count - maxCount).ToList();

        var contextMessages = BuildContextMessages(flow.UserId, flow.ModelConfig, beforeMessages);
        flow.ContextMessages = contextMessages;
        return Task.FromResult<IList<AiChatMessage>>(contextMessages);
    }

    /// <summary>构建上下文消息列表。注入系统提示词、展开 ToolCalls，并在历史消息较多时追加最新问题优先级提示</summary>
    /// <param name="userId">当前用户编号</param>
    /// <param name="modelConfig">模型配置（可选，用于注入模型级系统提示词）</param>
    /// <param name="history">已按时间升序排列的历史消息列表</param>
    /// <param name="currentContent">当前用户消息内容（有值且历史超过 4 条时追加优先级提示）</param>
    /// <returns>OpenAI ChatMessage 格式的消息列表</returns>
    protected virtual IList<AiChatMessage> BuildContextMessages(Int32 userId, ModelConfig? modelConfig, IList<DbChatMessage> history, String? currentContent = null)
    {
        var messages = new List<AiChatMessage>();

        // 注入系统提示词
        var systemMsg = BuildSystemMessage(userId, modelConfig, history.Count, tracer);
        if (systemMsg != null) messages.Add(systemMsg);

        foreach (var msg in history)
        {
            if (ShouldSkipHistoryMessage(msg)) continue;

            if (msg.Role == "assistant" && !msg.ToolCalls.IsNullOrEmpty())
            {
                IList<ToolCallDto>? storedDtos = null;
                try { storedDtos = msg.ToolCalls.ToJsonEntity<List<ToolCallDto>>(); } catch { }
                if (storedDtos != null && storedDtos.Count > 0)
                {
                    messages.Add(new AiChatMessage
                    {
                        Role = "assistant",
                        Content = null,
                        ToolCalls = storedDtos.Select(tc => new AiToolCall
                        {
                            Id = tc.Id,
                            Function = new AiFunctionCall { Name = tc.Name, Arguments = tc.Arguments },
                        }).ToList(),
                    });
                    foreach (var tc in storedDtos)
                    {
                        messages.Add(new AiChatMessage
                        {
                            Role = "tool",
                            ToolCallId = tc.Id,
                            Content = tc.Result ?? String.Empty,
                        });
                    }
                    if (!String.IsNullOrEmpty(msg.Content))
                        messages.Add(new AiChatMessage { Role = "assistant", Content = msg.Content });
                    continue;
                }
            }

            var histMsg = BuildHistoryMessage(msg);
            if (histMsg != null) messages.Add(histMsg);
        }

        if (!currentContent.IsNullOrEmpty() && history.Count > 4)
        {
            messages.Add(new AiChatMessage
            {
                Role = "system",
                Content = $"请直接针对用户最新的问题进行回答：{currentContent}",
            });
        }

        return messages;
    }

    /// <summary>执行 <see cref="IChatHandler"/> 三段式调用链：OnBefore（注册顺序） → 核心阶段（含 LLM 调用与可选拦截器洋葱） → OnAfter（注册倒序）。
    /// 任一 OnBefore 将 <see cref="IChatContext.Cancel"/> 置 true 即跳过后续 OnBefore 与整个核心阶段，但已经过的 OnAfter 仍会按倒序执行。
    /// OnBefore/OnAfter 抛出的异常将向上传播，不在此处捕获</summary>
    /// <param name="context">对话上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>事件流</returns>
    protected virtual async IAsyncEnumerable<ChatStreamEvent> InvokeChainAsync(IChatContext context, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var handlers = Handlers;
        var lastBeforeIndex = -1;

        // 1. OnBefore 正序
        for (var i = 0; i < handlers.Count; i++)
        {
            await handlers[i].OnBefore(context, cancellationToken).ConfigureAwait(false);
            lastBeforeIndex = i;
            if (context.Cancel) break;
        }

        // 2. 核心阶段（短路时跳过）
        if (!context.Cancel)
        {
            await foreach (var ev in CoreStreamAsync(context, cancellationToken).ConfigureAwait(false))
                yield return ev;
        }
        else
        {
            // 短路时回写一次 error 事件，便于客户端展示原因
            yield return ChatStreamEvent.ErrorEvent(context.CancelCode ?? "CANCELED", context.CancelMessage ?? "请求已取消");
        }

        // 3. OnAfter 倒序（已经过的 Handler）
        for (var i = lastBeforeIndex; i >= 0; i--)
        {
            await handlers[i].OnAfter(context, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>核心阶段。装配 <see cref="IChatInterceptor"/> 洋葱链（最内层为 <see cref="InvokeLlmAsync"/>），
    /// 并将事件透传给调用方的同时，写入上下文 ContentBuilder/ThinkingBuilder/ToolCalls/Usage 收集器</summary>
    /// <param name="context">对话上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>事件流</returns>
    protected virtual async IAsyncEnumerable<ChatStreamEvent> CoreStreamAsync(IChatContext context, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // 仅过滤实现 IChatInterceptor 的 Handler；DI 单注册（IChatHandler）即可同时获得两面
        var interceptors = Handlers.OfType<IChatInterceptor>().ToArray();

        // 链路终点：LLM 调用
        ChatNextDelegate next = ct => InvokeLlmAsync(context, ct);

        // 倒序构建：注册顺序 [0, 1, 2, ...] = 外→内
        for (var i = interceptors.Length - 1; i >= 0; i--)
        {
            var current = interceptors[i];
            var captured = next;
            next = ct => current.InvokeAsync(context, captured, ct);
        }

        var source = next(cancellationToken);
        var artifactDetector = new ArtifactDetector();
        var contentBuilder = context.ContentBuilder;
        var thinkingBuilder = context.ThinkingBuilder;
        var toolCalls = context.ToolCalls;

        var enumerator = source.GetAsyncEnumerator(cancellationToken);
        try
        {
            while (true)
            {
                Boolean moved;
                ChatStreamEvent? errorEvent = null;
                try
                {
                    moved = await enumerator.MoveNextAsync().ConfigureAwait(false);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    log?.Error("流式生成失败: {0}", ex.Message);
                    context.HasError = true;
                    errorEvent = ChatStreamEvent.ErrorEvent("STREAM_ERROR", ex.Message);
                    moved = false;
                }
                if (errorEvent != null) { yield return errorEvent; break; }
                if (!moved) break;

                var ev = enumerator.Current;
                switch (ev.Type)
                {
                    case "thinking_delta":
                        thinkingBuilder.Append(ev.Content);
                        yield return ev;
                        break;
                    case "content_delta":
                        contentBuilder.Append(ev.Content);
                        var artifactEvents = artifactDetector.Process(ev.Content!);
                        foreach (var ae in artifactEvents)
                        {
                            switch (ae.Kind)
                            {
                                case ArtifactEventKind.ArtifactStart: yield return ChatStreamEvent.ArtifactStart(ae.Language!); break;
                                case ArtifactEventKind.ArtifactDelta: yield return ChatStreamEvent.ArtifactDelta(ae.Content!); break;
                                case ArtifactEventKind.ArtifactEnd: yield return ChatStreamEvent.ArtifactEnd(); break;
                            }
                        }
                        yield return ev;
                        break;
                    case "message_done":
                        if (ev.Usage != null) context.Usage = ev.Usage;
                        yield return ev;
                        break;
                    case "error":
                        context.HasError = true;
                        yield return ev;
                        break;
                    case "tool_call_start":
                        toolCalls.Add(new ToolCallDto(ev.ToolCallId + "", ev.Name + "", ToolCallStatus.Calling, ev.Arguments));
                        yield return ev;
                        break;
                    case "tool_call_done":
                        UpdateToolCallStatus(toolCalls, ev.ToolCallId, ToolCallStatus.Done, ev.Result);
                        yield return ev;
                        break;
                    case "tool_call_error":
                        UpdateToolCallStatus(toolCalls, ev.ToolCallId, ToolCallStatus.Error, ev.Error);
                        yield return ev;
                        break;
                    default:
                        yield return ev;
                        break;
                }

                if (context.HasError) break;
            }
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>核心 LLM 调用。链路最内层节点：组装过滤器链 + 工具循环 + 流式 SSE 转换。
    /// 派生类可覆盖 <see cref="ApplyTools"/> / <see cref="ApplyResponseStyle"/> / <see cref="BuildScopedProviders"/> 扩展能力</summary>
    /// <param name="context">对话上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>事件流</returns>
    protected virtual async IAsyncEnumerable<ChatStreamEvent> InvokeLlmAsync(IChatContext context, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var span = tracer?.NewSpan("ai:flowInvokeLlm", new { messages = context.ContextMessages.Count });

        var contextMessages = context.ContextMessages;
        var modelConfig = (ModelConfig)context.ModelConfig;

        using var rawClient = modelService.CreateClient(modelConfig);
        if (rawClient == null)
        {
            yield return ChatStreamEvent.ErrorEvent("MODEL_UNAVAILABLE", $"未找到服务商 '{modelConfig.GetEffectiveProvider()}'");
            yield break;
        }

        var clientBuilder = rawClient.AsBuilder();
        foreach (var filter in ChatFilters)
            clientBuilder = clientBuilder.UseFilters(filter);
        var providers = BuildScopedProviders(context.SelectedTools).ToArray();
        ApplyTools(ref clientBuilder, contextMessages, providers);

        // 记录本轮可用工具
        foreach (var p in providers)
            foreach (var t in p.GetTools())
                if (t.Function?.Name != null) context.AvailableToolNames.Add(t.Function.Name);

        if (context.AvailableToolNames.Count > 0)
        {
            using var toolSchemaSpan = tracer?.NewSpan("ai:ToolSchema");
            toolSchemaSpan?.AppendTag(providers.SelectMany(p => p.GetTools()).Where(t => t.Function != null).Select(t => t.Function).ToJson());
        }

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

        context.MaxTokens = chatOptions.MaxTokens ?? 0;
        context.Temperature = chatOptions.Temperature;

        using var streamClient = clientBuilder.Build();

        var thinkingBuilder = new StringBuilder();
        UsageDetails? lastUsage = null;
        Int64 thinkingStart = 0;
        String? lastFinishReason = null;
        var streamSw = Stopwatch.StartNew();
        var sysFired = false;

        await foreach (var chunk in streamClient.GetStreamingResponseAsync(contextMessages, chatOptions, cancellationToken).ConfigureAwait(false))
        {
            if (!sysFired)
            {
                sysFired = true;
                context.SystemPrompt = contextMessages.FirstOrDefault(m => m.Role == "system")?.Content as String;
                context.OnSystemReady?.Invoke(context.SystemPrompt!);
            }

            if (chunk.Usage != null) lastUsage = chunk.Usage;

            if (chunk is ChatResponse cr && cr.ToolCallEvents is { Count: > 0 } events)
            {
                foreach (var evt in events)
                {
                    switch (evt.Type)
                    {
                        case "start": yield return ChatStreamEvent.ToolCallStart(evt.ToolCallId, evt.Name, evt.Value); break;
                        case "done": yield return ChatStreamEvent.ToolCallDone(evt.ToolCallId, evt.Value, true); break;
                        case "error": yield return ChatStreamEvent.ToolCallError(evt.ToolCallId, evt.Value ?? String.Empty); break;
                    }
                }
                continue;
            }

            var choice = chunk.Messages?.FirstOrDefault();
            if (choice == null) continue;

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

        if (!sysFired) context.SystemPrompt = contextMessages.FirstOrDefault(m => m.Role == "system")?.Content as String;
        if (thinkingBuilder.Length > 0) yield return ChatStreamEvent.ThinkingDone((Int32)(Runtime.TickCount64 - thinkingStart));

        streamSw.Stop();
        lastUsage ??= new UsageDetails();
        lastUsage.ElapsedMs = (Int32)streamSw.ElapsedMilliseconds;

        context.FinishReason = lastFinishReason;

        yield return ChatStreamEvent.MessageDone(lastUsage, finishReason: lastFinishReason);
    }

    /// <summary>非流式 LLM 调用。链路最内层节点（非流式路径专用）：组装过滤器链 + 工具装配 + 单次 GetResponseAsync。
    /// <see cref="IChatInterceptor"/> 洋葱仅适用于流式路径，此方法不经过拦截器；
    /// <see cref="IChatFilter"/> 链（<see cref="ChatFilters"/>）仍通过 <c>UseFilters</c> 正常生效</summary>
    /// <param name="context">对话上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    protected virtual async Task InvokeLlmDirectAsync(IChatContext context, CancellationToken cancellationToken)
    {
        using var span = tracer?.NewSpan("ai:flowInvokeLlmDirect", new { messages = context.ContextMessages.Count });

        var contextMessages = context.ContextMessages;
        var modelConfig = (ModelConfig)context.ModelConfig;

        using var rawClient = modelService.CreateClient(modelConfig);
        if (rawClient == null)
        {
            context.HasError = true;
            return;
        }

        var clientBuilder = rawClient.AsBuilder();
        foreach (var filter in ChatFilters)
            clientBuilder = clientBuilder.UseFilters(filter);
        var providers = BuildScopedProviders(context.SelectedTools).ToArray();
        ApplyTools(ref clientBuilder, contextMessages, providers);

        foreach (var p in providers)
            foreach (var t in p.GetTools())
                if (t.Function?.Name != null) context.AvailableToolNames.Add(t.Function.Name);

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

        context.MaxTokens = chatOptions.MaxTokens ?? 0;
        context.Temperature = chatOptions.Temperature;

        using var directClient = clientBuilder.Build();

        var sw = Stopwatch.StartNew();
        var response = ChatResponse.From(await directClient.GetResponseAsync(contextMessages, chatOptions, cancellationToken).ConfigureAwait(false));
        sw.Stop();

        // 提取系统提示词（首个 system 消息）
        context.SystemPrompt = contextMessages.FirstOrDefault(m => m.Role == "system")?.Content as String;
        context.OnSystemReady?.Invoke(context.SystemPrompt!);

        // 写入正文
        var msg = response.Messages?.FirstOrDefault()?.Message;
        if (msg != null)
        {
            if (!String.IsNullOrEmpty(msg.ReasoningContent))
                context.ThinkingBuilder.Append(msg.ReasoningContent);
            var text = msg.Content as String;
            if (!String.IsNullOrEmpty(text))
                context.ContentBuilder.Append(text);
        }

        // 写入用量
        var usage = response.Usage ?? new UsageDetails();
        usage.ElapsedMs = (Int32)sw.ElapsedMilliseconds;
        context.Usage = usage;

        context.FinishReason = response.Messages?.FirstOrDefault()?.FinishReason?.ToApiString();
    }

    /// <summary>非流式 Handler 三段式调用链：OnBefore（注册顺序） → <see cref="InvokeLlmDirectAsync"/> → OnAfter（注册倒序）。
    /// 与 <see cref="InvokeChainAsync"/> 的区别：核心阶段调用非流式 LLM，<see cref="IChatInterceptor"/> 不参与</summary>
    /// <param name="context">对话上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    protected virtual async Task InvokeNonStreamAsync(IChatContext context, CancellationToken cancellationToken)
    {
        var handlers = Handlers;
        var lastBeforeIndex = -1;

        // 1. OnBefore 正序
        for (var i = 0; i < handlers.Count; i++)
        {
            await handlers[i].OnBefore(context, cancellationToken).ConfigureAwait(false);
            lastBeforeIndex = i;
            if (context.Cancel) break;
        }

        // 2. 核心阶段（短路时跳过）
        if (!context.Cancel)
            await InvokeLlmDirectAsync(context, cancellationToken).ConfigureAwait(false);

        // 3. OnAfter 倒序（已经过的 Handler）
        for (var i = lastBeforeIndex; i >= 0; i--)
            await handlers[i].OnAfter(context, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>将工具提供者应用到客户端构建器。派生类可覆盖以实现渐进式工具发现、结果截断等策略</summary>
    /// <param name="clientBuilder">客户端构建器（ref 传入）</param>
    /// <param name="contextMessages">上下文消息列表</param>
    /// <param name="providers">已解析的工具提供者集合</param>
    protected virtual void ApplyTools(ref ChatClientBuilder clientBuilder, IList<AiChatMessage> contextMessages, IToolProvider[] providers)
    {
        if (providers.Length > 0) clientBuilder = clientBuilder.UseTools(providers);
    }

    /// <summary>根据用户回应风格设置采样参数。仅在请求未显式指定时设置</summary>
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

    #region 上下文构建

    /// <summary>从数据库加载历史消息并按时间升序排列。派生类可覆写以修改加载来源（如网关路径不从 DB 加载）</summary>
    /// <param name="conversationId">会话编号</param>
    /// <param name="maxRounds">最大保留轮数（对话轮数，非消息条数）</param>
    /// <returns>按 Id 升序排列的历史消息列表</returns>
    protected virtual IList<DbChatMessage> LoadHistoryMessages(Int64 conversationId, Int32 maxRounds)
    {
        var history = DbChatMessage.FindAllByConversationIdDesc(conversationId, maxRounds * 2);
        //history.Reverse();
        //!!! 不能使用 Reverse ，它未能让列表完全倒置
        history = history.OrderBy(e => e.Id).ToList();
        return history;
    }

    /// <summary>将单条历史消息转换为 AI 请求消息。统一扩展点：派生类覆写此方法即可对<b>所有</b>历史消息统一定制多模态构建逻辑（<see cref="BuildContextMessages"/> 负责统一调用）</summary>
    /// <remarks>基类实现：user 消息有附件时仅保留文本（丢弃附件，与原存根行为一致）；其他情况返回普通 AiChatMessage。
    /// assistant ToolCalls 展开逻辑由调用方 inline 处理，不经过此方法。</remarks>
    /// <param name="msg">历史消息实体</param>
    /// <returns>AI 请求消息；返回 null 表示跳过该条消息</returns>
    protected virtual AiChatMessage? BuildHistoryMessage(DbChatMessage msg)
    {
        if (msg.Role.EqualIgnoreCase("user") && !msg.Attachments.IsNullOrEmpty())
            // 基类降级：仅保留文本，附件内容被丢弃；派生类（如 MessageService）覆写以支持完整多模态
            return new AiChatMessage { Role = "user", Content = msg.Content };
        return new AiChatMessage { Role = msg.Role ?? "user", Content = msg.Content };
    }

    /// <summary>构建系统提示词消息。合并用户全局级和模型级系统提示词（技能提示词由管道注入）</summary>
    /// <remarks>静态公共实现：只拼接 IUser 基础信息与 UserSetting 个性化；
    /// <see cref="GatewayService"/> 与 <see cref="MessageFlow"/> 均调用此方法，避免逻辑重复。</remarks>
    /// <param name="userId">当前用户编号</param>
    /// <param name="model">模型配置（可选）</param>
    /// <param name="historyCount">当前上下文中历史消息条数，大于 0 时才注入多轮优先级提示</param>
    /// <param name="tracer">追踪器（可选）</param>
    /// <returns>系统消息，无提示词时返回 null</returns>
    public static AiChatMessage? BuildSystemMessage(Int32 userId, ModelConfig? model, Int32 historyCount = 0, ITracer? tracer = null)
    {
        using var span = tracer?.NewSpan("ai:BuildSystemMessage", new { userId, model?.Name, historyCount });
        var parts = new List<String>();

        // 0. 当前用户基础信息（基类只拼 DisplayName/Name/Roles，不查部门——派生类按需增强）
        if (userId > 0 && ManageProvider.Provider?.FindByID(userId) is IUser user)
        {
            var sb = Pool.StringBuilder.Get();
            sb.Append($"当前用户：{user.DisplayName}（{user.Name}）");
            var roles = user.Roles;
            if (roles?.Length > 0) sb.Append($"，角色：{roles.Join(",")}");
            var dept = Department.FindByID(user.DepartmentID);
            if (dept != null) sb.Append($"，部门：{dept.Name}");

            parts.Add(sb.Return(true));
        }

        // 1. 个性化定制
        var userSetting = UserSetting.FindByUserId(userId);
        if (userSetting != null)
        {
            if (!String.IsNullOrWhiteSpace(userSetting.Nickname))
                parts.Add($"用户希望你称呼他为「{userSetting.Nickname.Trim()}」");

            if (!String.IsNullOrWhiteSpace(userSetting.UserBackground))
                parts.Add($"## 用户背景信息\n{userSetting.UserBackground.Trim()}");

            var stylePrompt = userSetting.ResponseStyle switch
            {
                ResponseStyle.Precise => "请给出准确、确定性高的回答。优先引用事实和数据，避免模糊表述和不确定的推测。回答简洁有条理。",
                ResponseStyle.Vivid => "请用丰富的表达方式回答，善于使用类比、举例和故事来解释概念。让回答有温度、易于理解，适当展开讨论。",
                ResponseStyle.Creative => "请大胆发散思维，提供新颖独特的视角和创意方案。鼓励联想、跨界类比和非常规思路，不必拘泥于常规答案。",
                _ => null
            };
            if (stylePrompt != null) parts.Add(stylePrompt);
        }

        // 2. 用户自定义指令
        if (userSetting != null && !String.IsNullOrWhiteSpace(userSetting.SystemPrompt))
            parts.Add(userSetting.SystemPrompt.Trim());

        // 3. 模型级系统提示词
        if (model != null && !String.IsNullOrWhiteSpace(model.SystemPrompt))
            parts.Add(model.SystemPrompt.Trim());

        // 4. 多轮对话时强调最新消息优先级
        if (historyCount > 1)
            parts.Add("请优先回应用户的最新消息。如果最新消息与之前的对话内容存在矛盾或方向变化，以最新消息为准。");

        if (parts.Count == 0) return null;
        span?.AppendTag(null!, parts.Count);

        return new AiChatMessage { Role = "system", Content = String.Join("\n\n", parts) };
    }

    /// <summary>判断是否应跳过历史消息。用于过滤预分配但尚未写入正文的 assistant 占位消息，避免发送非法上下文给上游模型</summary>
    /// <param name="message">历史消息实体</param>
    /// <returns>应跳过返回 true，否则返回 false</returns>
    protected static Boolean ShouldSkipHistoryMessage(DbChatMessage message)
    {
        if (!message.Role.EqualIgnoreCase("assistant")) return false;

        return message.Content.IsNullOrEmpty() && message.ToolCalls.IsNullOrEmpty();
    }

    #endregion

    #region 实体映射

    /// <summary>更新工具调用列表中指定 id 的状态与结果</summary>
    /// <param name="collector">工具调用收集器</param>
    /// <param name="toolCallId">工具调用编号</param>
    /// <param name="status">新状态</param>
    /// <param name="value">结果或错误信息</param>
    protected static void UpdateToolCallStatus(List<ToolCallDto> collector, String? toolCallId, ToolCallStatus status, String? value)
    {
        for (var i = collector.Count - 1; i >= 0; i--)
        {
            if (collector[i].Id == toolCallId)
            {
                var orig = collector[i];
                collector[i] = new ToolCallDto(orig.Id, orig.Name, status, orig.Arguments, value);
                break;
            }
        }
    }

    /// <summary>转换消息实体为 DTO。供派生类在 <see cref="RegenerateMessageAsync"/> 等场景转出</summary>
    /// <param name="entity">消息实体</param>
    /// <returns>消息 DTO</returns>
    protected static MessageDto ToMessageDto(DbChatMessage entity)
    {
        IReadOnlyList<ToolCallDto>? toolCalls = null;
        if (!String.IsNullOrEmpty(entity.ToolCalls))
        {
            try { toolCalls = entity.ToolCalls.ToJsonEntity<List<ToolCallDto>>(); }
            catch { }
        }
        return new MessageDto(entity.Id, entity.ConversationId, entity.Role ?? String.Empty, entity.Content ?? String.Empty, entity.ThinkingContent, entity.ThinkingMode, entity.Attachments, entity.CreateTime)
        {
            ToolCalls = toolCalls,
            InputTokens = entity.InputTokens,
            OutputTokens = entity.OutputTokens,
            TotalTokens = entity.TotalTokens,
        };
    }

    #endregion
}
