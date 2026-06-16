using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using NewLife.AI.Clients;
using NewLife.AI.Filters;
using NewLife.AI.Tools;
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
    /// <summary>对话处理器调用链管理器。根据 <see cref="ChatHandlerOrderAttribute"/> 属性排序 Handler，
    /// 对外暴露 <see cref="ChatHandlerChain.BeforeHandlers"/> / <see cref="ChatHandlerChain.AfterHandlers"/> / <see cref="ChatHandlerChain.Interceptors"/> 三个有序视图。
    /// 为空链时核心仍会执行（仅 LLM 调用，无任何事前/事后扩展）。
    /// 派生类可在构造函数体内覆盖，以注入专属 Handler 子集（如 GatewayMessageFlow）</summary>
    protected ChatHandlerChain Chain = services?.GetService<ChatHandlerChain>() ?? new ChatHandlerChain(services?.GetServices<IChatHandler>() ?? []);

    /// <summary>工具提供者集合（DbToolProvider / McpClientService 等），由 DI 解析。供 <see cref="InvokeLlmAsync"/> 使用</summary>
    protected IToolProvider[] ToolProviders = services?.GetServices<IToolProvider>().ToArray() ?? [];

    /// <summary>聊天过滤器链（日志、学习、Agent 触发等），由 DI 解析。供 <see cref="InvokeLlmAsync"/> 使用</summary>
    protected IReadOnlyList<IChatFilter> ChatFilters = services?.GetServices<IChatFilter>().ToArray() ?? [];
    #endregion

    #region 生成入口

    /// <summary>非流式重新生成 AI 回复。构建上下文后通过非流式 Handler 三段式调用链执行（OnBefore → InvokeLlmDirectAsync → OnAfter），
    /// 结果直接写入上下文收集器后由事后 Handler 持久化。拦截器洋葱（<see cref="ChatHandlerCapabilities.Interceptor"/>）仅适用于流式路径，此方法不经过拦截器洋葱</summary>
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

        // 软删除旧消息，保留历史版本；为本次重新生成创建新消息实体
        var oldMsg = flow.AssistantMessage;
        var newMsg = new DbChatMessage
        {
            ConversationId = oldMsg.ConversationId,
            Role = "assistant",
            ThinkingMode = oldMsg.ThinkingMode,
            Enable = true,
        };
        newMsg.Insert();
        oldMsg.Enable = false;
        oldMsg.Update();
        flow.HistoryMessages.Remove(oldMsg);
        flow.HistoryMessages.Add(newMsg);
        flow.AssistantMessage = newMsg;

        // 显式查找并设置 UserMessage：从历史中取 oldMsg 之前最后一条 user 角色消息
        flow.UserMessage = flow.HistoryMessages.LastOrDefault(e => e.Id < oldMsg.Id && e.Role.EqualIgnoreCase("user"));

        try
        {
            // Step2: 构建对话上下文
            await BuildContextForRegenerateAsync(flow, cancellationToken).ConfigureAwait(false);

            // Step3: 通过非流式 Handler 三段式调用链执行（持久化由事后 Handler 完成）
            await InvokeNonStreamAsync(flow, cancellationToken).ConfigureAwait(false);

            //flow.AssistantMessage.ElapsedMs = flow.Usage?.ElapsedMs ?? 0;

            return ChatApplicationService.ToMessageDto(flow.AssistantMessage);
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
            Enable = true,
        };
        assistantMsg.Insert();
        flow.AssistantMessage = assistantMsg;
        flow.HistoryMessages.Add(assistantMsg);

        // Step2: 构建对话上下文
        await BuildContextAsync(flow, newContent, cancellationToken).ConfigureAwait(false);

        yield return ChatStreamEvent.MessageStart(assistantMsg.Id, flow.ModelConfig.Code!, userMessage.ThinkingMode);

        // Step3: 执行 IChatHandler 三段式调用链（持久化由 PersistMessageHandler 等事后 Handler 完成）
        var useBackground = backgroundService != null && setting.BackgroundGeneration;
        if (useBackground)
        {
            backgroundService!.Register(
                assistantMsg.Id,
                InvokeChainAsync(flow, CancellationToken.None),
                task => OnBackgroundCompleteAsync(task, flow));

            var hasError = false;
            await foreach (var ev in backgroundService.Subscribe(assistantMsg.Id, cancellationToken).ConfigureAwait(false))
            {
                if (ev.Type == "error") hasError = true;
                yield return ev;
                if (hasError) break;
            }

            if (!hasError)
            {
                var bgTask = backgroundService.GetTask(assistantMsg.Id);
                if (bgTask is { Status: BackgroundTaskStatus.Failed, Error: not null })
                {
                    hasError = true;
                    yield return ChatStreamEvent.ErrorEvent("STREAM_ERROR", bgTask.Error);
                }
            }

            if (!hasError && !cancellationToken.IsCancellationRequested)
            {
                var bgTask = backgroundService.GetTask(assistantMsg.Id);
                yield return new ChatStreamEvent
                {
                    Type = "message_done",
                    MessageId = assistantMsg.Id,
                    Usage = bgTask?.Usage ?? flow.Usage,
                };
            }
        }
        else
        {
            await foreach (var ev in InvokeChainAsync(flow, cancellationToken).ConfigureAwait(false))
            {
                yield return ev;
            }

            if (!flow.HasError && !cancellationToken.IsCancellationRequested)
                yield return new ChatStreamEvent { Type = "message_done", MessageId = assistantMsg.Id, Usage = flow.Usage, };
        }
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

        // 软删除旧消息，保留历史版本；为本次重新生成创建新消息实体
        var oldMsg = flow.AssistantMessage;

        // 重新生成次数检查：同一消息最多允许 3 次连续重新生成
        var regenKey = $"RegenerateCount_{oldMsg.Id}";
        var regenCount = flow.Items.TryGetValue(regenKey, out var raw) && raw is Int32 val ? val : 0;
        if (regenCount >= 3)
        {
            yield return ChatStreamEvent.ErrorEvent("REGENERATE_LIMIT", "该消息已达到重新生成次数上限（3 次），请发送新消息开始新一轮对话");
            yield break;
        }
        flow.Items[regenKey] = regenCount + 1;

        var newMsg = new DbChatMessage
        {
            ConversationId = oldMsg.ConversationId,
            Role = "assistant",
            ThinkingMode = oldMsg.ThinkingMode,
            Enable = true,
        };
        newMsg.Insert();
        if (newMsg.Id <= 0)
            throw new InvalidOperationException("新消息插入失败，未获取有效 Id");
        oldMsg.Enable = false;
        oldMsg.Update();
        flow.HistoryMessages.Remove(oldMsg);
        flow.HistoryMessages.Add(newMsg);
        flow.AssistantMessage = newMsg;

        // 显式查找并设置 UserMessage：从历史中取 oldMsg 之前最后一条 user 角色消息
        flow.UserMessage = flow.HistoryMessages.LastOrDefault(e => e.Id < oldMsg.Id && e.Role.EqualIgnoreCase("user"));

        // Step2: 构建对话上下文
        await BuildContextForRegenerateAsync(flow, cancellationToken).ConfigureAwait(false);

        // message_start
        var assistantMessage = flow.AssistantMessage;
        yield return ChatStreamEvent.MessageStart(assistantMessage.Id, flow.ModelConfig.Code!, assistantMessage.ThinkingMode);

        // Step3: 执行 IChatHandler 三段式调用链（持久化由 PersistMessageHandler 等事后 Handler 完成）
        var useBackground = backgroundService != null && setting.BackgroundGeneration;
        if (useBackground)
        {
            backgroundService!.Register(
                assistantMessage.Id,
                InvokeChainAsync(flow, CancellationToken.None),
                task => OnBackgroundCompleteAsync(task, flow));

            var hasError = false;
            await foreach (var ev in backgroundService.Subscribe(assistantMessage.Id, cancellationToken).ConfigureAwait(false))
            {
                if (ev.Type == "error") hasError = true;
                yield return ev;
                if (hasError) break;
            }

            if (!hasError)
            {
                var bgTask = backgroundService.GetTask(assistantMessage.Id);
                if (bgTask is { Status: BackgroundTaskStatus.Failed, Error: not null })
                {
                    hasError = true;
                    yield return ChatStreamEvent.ErrorEvent("STREAM_ERROR", bgTask.Error);
                }
            }

            if (!hasError && !cancellationToken.IsCancellationRequested)
            {
                var bgTask = backgroundService.GetTask(assistantMessage.Id);
                yield return new ChatStreamEvent
                {
                    Type = "message_done",
                    MessageId = assistantMessage.Id,
                    Usage = bgTask?.Usage ?? flow.Usage,
                };
            }
        }
        else
        {
            await foreach (var ev in InvokeChainAsync(flow, cancellationToken).ConfigureAwait(false))
            {
                yield return ev;
            }

            if (!flow.HasError && !cancellationToken.IsCancellationRequested)
                yield return new ChatStreamEvent { Type = "message_done", MessageId = assistantMessage.Id, Usage = flow.Usage };
        }
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
            Enable = true,
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
            Enable = true,
        };
        assistantMsg.Insert();

        flow.HistoryMessages.Add(userMsg);
        flow.HistoryMessages.Add(assistantMsg);

        flow.UserMessage = userMsg;
        flow.AssistantMessage = assistantMsg;
        flow.SkillId = conversation.SkillId;
        flow["RequestSkillCode"] = request.SkillCode;
        flow.ThinkingMode = request.ThinkingMode;
        flow.Options.EnableThinking = request.ThinkingMode switch
        {
            ThinkingMode.Think => true,
            ThinkingMode.Clarify => true, // 澄清模式底层使用深度思考，再叠加系统提示词
            ThinkingMode.Fast => false,
            _ => flow.ModelConfig.SupportThinking ? true : null,
        };
        flow.Options.ReasoningEffort = request.ReasoningEffort;

        // Step2: 构建对话上下文
        await BuildContextAsync(flow, request.Content, cancellationToken).ConfigureAwait(false);

        // message_start
        using var span = tracer?.NewSpan($"ai:Stream:{model.Code}", request.Content);
        yield return ChatStreamEvent.MessageStart(assistantMsg.Id, model.Code!, request.ThinkingMode);

        // 记录请求选项供 Handler 读取
        if (request.Options is { Count: > 0 })
        {
            foreach (var kv in request.Options)
                flow.Items[kv.Key] = kv.Value;
        }

        // Step3: 执行 IChatHandler 三段式调用链（OnBefore 正序 → 核心 LLM 调用 → OnAfter 倒序）
        var useBackground = backgroundService != null && setting.BackgroundGeneration;
        if (useBackground)
        {
            // 后台生成路径：Register 独立消费 InvokeChainAsync（使用 CancellationToken.None 确保客户端断连不影响生成），
            // SSE 通过 Subscribe 回放 BackgroundTask.Events 中的事件
            backgroundService!.Register(
                assistantMsg.Id,
                InvokeChainAsync(flow, CancellationToken.None),
                task => OnBackgroundCompleteAsync(task, flow));

            var hasError = false;
            await foreach (var ev in backgroundService.Subscribe(assistantMsg.Id, cancellationToken).ConfigureAwait(false))
            {
                if (ev.Type == "error") hasError = true;
                yield return ev;
                if (hasError) break;
            }

            // 检查后台任务错误（InvokeChainAsync 内部异常不会生成 error 事件，需单独检查）
            if (!hasError)
            {
                var bgTask = backgroundService.GetTask(assistantMsg.Id);
                if (bgTask is { Status: BackgroundTaskStatus.Failed, Error: not null })
                {
                    hasError = true;
                    yield return ChatStreamEvent.ErrorEvent("STREAM_ERROR", bgTask.Error);
                }
            }

            if (!hasError && !cancellationToken.IsCancellationRequested)
            {
                var bgTask = backgroundService.GetTask(assistantMsg.Id);
                yield return new ChatStreamEvent
                {
                    Type = "message_done",
                    MessageId = assistantMsg.Id,
                    Usage = bgTask?.Usage ?? flow.Usage,
                };
            }
        }
        else
        {
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

    /// <summary>后台生成完成回调。确保消息内容已持久化到数据库，作为 PersistMessageHandler.OnAfter 的兜底</summary>
    /// <param name="task">已完成的后台任务</param>
    /// <param name="flow">流程上下文</param>
    protected virtual async Task OnBackgroundCompleteAsync(BackgroundTask task, MessageFlowContext flow)
    {
        try
        {
            var msg = flow.AssistantMessage;
            msg ??= DbChatMessage.FindById(task.MessageId);
            if (msg == null) return;

            // PersistMessageHandler.OnAfter 可能已写入，此处作为兜底
            if (msg.Content.IsNullOrEmpty() && task.ContentBuilder.Length > 0)
                msg.Content = task.ContentBuilder.ToString();
            if (msg.ThinkingContent.IsNullOrEmpty() && task.ThinkingBuilder.Length > 0)
                msg.ThinkingContent = task.ThinkingBuilder.ToString();
            if (msg.ToolCalls.IsNullOrEmpty() && task.ToolCalls.Count > 0)
                msg.ToolCalls = task.ToolCalls.ToJson();

            // 用量兜底
            if (task.Usage is { TotalTokens: > 0 } && msg.TotalTokens <= 0)
            {
                msg.InputTokens = task.Usage.InputTokens;
                msg.OutputTokens = task.Usage.OutputTokens;
                msg.TotalTokens = task.Usage.TotalTokens;
            }

            // 错误状态
            if (task.Status == BackgroundTaskStatus.Failed && !task.Error.IsNullOrEmpty())
                msg.Content = task.Error;

            msg.Update();
        }
        catch (Exception ex)
        {
            log?.Error("后台生成完成回调失败: {0}", ex.Message);
        }
    }

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

        DbChatMessage? message = null;
        Conversation? conversation;

        if (messageId > 0)
        {
            // 按消息查找
            message = DbChatMessage.FindById(messageId.Value);
            if (message == null || (!expectedRole.IsNullOrEmpty() && !message.Role.EqualIgnoreCase(expectedRole)))
                return new MessageFlowContext { Error = new ChatException("MESSAGE_NOT_FOUND", "消息不存在或角色不匹配") };

            conversation = Conversation.FindById(message.ConversationId);
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

        // 按消息模式：自动填充 UserMessage 或 AssistantMessage，并从消息继承 ThinkingMode
        // （避免因默认 Auto 在支持思考的模型上意外开启推理）
        if (message != null)
        {
            if (message.Role.EqualIgnoreCase("user"))
                flow.UserMessage = message;
            else
                flow.AssistantMessage = message;

            flow.ThinkingMode = message.ThinkingMode;
        }

        var maxRounds = setting.DefaultContextRounds > 0 ? setting.DefaultContextRounds : 10;
        flow.HistoryMessages = LoadHistoryMessages(conversation.Id, maxRounds);

        // 预初始化调用选项。按会话模式（StreamMessageAsync）会用请求参数覆盖 EnableThinking
        flow.Options.Model = modelConfig.GetEffectiveModelCode();
        flow.Options.EnableThinking = flow.ThinkingMode switch
        {
            ThinkingMode.Think => true,
            ThinkingMode.Clarify => true, // 澄清模式底层使用深度思考，再叠加系统提示词
            ThinkingMode.Fast => false,
            _ => modelConfig.SupportThinking ? true : null,
        };
        flow.Options.UserId = userId > 0 ? userId.ToString() : null;
        flow.Options.User = userId > 0 ? userId.ToString() : null; // 透传给 LLM 服务商（内容安全/KVCache 隔离）
        flow.Options.ConversationId = conversation.Id > 0 ? conversation.Id.ToString() : null;
        ApplyResponseStyle(flow.Options, flow.Options.UserId);
        //// Options.Items 与 context.Items 共享同一引用，后续对 context.Items 的写入无需再复制
        //flow.Options.Items = flow.Items;

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

        var contextMessages = BuildContextMessages(flow.UserId, flow.ModelConfig, flow.HistoryMessages, currentContent);
        flow.ContextMessages = contextMessages;
        DefaultSpan.Current?.AppendTag(contextMessages.Join("\n"));

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
        var beforeMessages = flow.HistoryMessages.Where(e => e.Id < entity.Id).ToList();

        // 防御：确保上下文至少包含一条用户消息，否则模型将无法理解任务
        var hasUserMessage = beforeMessages.Any(e => e.Role.EqualIgnoreCase("user"));
        if (!hasUserMessage)
        {
            var errMsg = $"重新生成上下文缺少用户消息。AssistantMessage.Id={entity.Id}, HistoryMessages.Count={flow.HistoryMessages.Count}, beforeMessages.Count={beforeMessages.Count}";
            log?.Error(errMsg);
            throw new InvalidOperationException(errMsg);
        }

        var maxCount = (setting.DefaultContextRounds > 0 ? setting.DefaultContextRounds : 10) * 2;
        if (beforeMessages.Count > maxCount)
            beforeMessages = beforeMessages.Skip(beforeMessages.Count - maxCount).ToList();

        var contextMessages = BuildContextMessages(flow.UserId, flow.ModelConfig, beforeMessages);
        flow.ContextMessages = contextMessages;
        DefaultSpan.Current?.AppendTag(contextMessages.Join("\n"));

        return Task.FromResult(contextMessages);
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
        var userHistoryCount = history.Count(e => e.Role == "user");
        var systemMsg = BuildSystemMessage(userId, modelConfig, userHistoryCount, tracer);
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
                        // 思考模式下工具调用的推理内容必须回传，否则 DeepSeek 返回 400
                        ReasoningContent = msg.ThinkingContent.IsNullOrEmpty() ? null : msg.ThinkingContent,
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

        if (!currentContent.IsNullOrEmpty() && userHistoryCount >= 2)
        {
            messages.Add(new AiChatMessage
            {
                Role = "system",
                Content = $"请直接针对用户最新的问题进行回答：{currentContent}",
            });
        }

        // 模型配置启用提示缓存时，给 system prompt 和首条用户消息打上 cache_control 标记
        if (modelConfig != null)
            ApplyCacheControl(messages, modelConfig);

        return messages;
    }

    /// <summary>对消息列表添加缓存标记。给 system 消息和第一条 user 消息的 TextContent 设置 CacheControl="ephemeral"</summary>
    /// <param name="messages">构建完成的上下文消息列表</param>
    /// <param name="modelConfig">模型配置，用于读取 EnablePromptCache 开关</param>
    internal static void ApplyCacheControl(IList<AiChatMessage> messages, ModelConfig modelConfig)
    {
        if (!modelConfig.EnablePromptCache) return;

        // 最多标记 2 个消息：system prompt + 首条用户消息，遵守 ≤4 个 cache_control 约束
        foreach (var msg in messages)
        {
            if (msg.Role != "system" && msg.Role != "user") continue;

            var text = msg.Content as String;
            if (text.IsNullOrEmpty()) continue;

            // 将 Content(String) 转为 Contents(TextContent)，确保 build 时输出 cache_control
            msg.Contents = [new TextContent(text) { CacheControl = "ephemeral" }];
            msg.Content = null;

            // 只标记 system + 首条 user
            if (msg.Role == "user") break;
        }
    }

    /// <summary>执行 <see cref="IChatHandler"/> 三段式调用链：OnBefore（BeforeOrder 升序） → 核心阶段（含 LLM 调用与可选拦截器洋葱） → OnAfter（AfterOrder 升序）。
    /// 任一 OnBefore 将 <see cref="IChatContext.FlowControl"/> 设为 <see cref="ChatFlowControl.SkipRemaining"/> 即跳过后续 OnBefore，但仍执行 LLM 核心阶段；
    /// 设为 <see cref="ChatFlowControl.Cancel"/> 则同时跳过后续 OnBefore 与整个核心阶段。已经过的 OnAfter 仍会按序执行。
    /// OnBefore/OnAfter 抛出的异常将向上传播，不在此处捕获</summary>
    /// <param name="context">对话上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>事件流</returns>
    protected virtual async IAsyncEnumerable<ChatStreamEvent> InvokeChainAsync(IChatContext context, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // ranBefore：记录哪些处理器的 OnBefore 确实被执行过（短路时后续的 Before 不推入此集合）
        // After-only（无 Before 能力）的处理器不在此集合内，OnAfter 阶段无条件调用
        var ranBefore = new HashSet<IChatHandler>(ReferenceEqualityComparer.Instance);

        // 1. OnBefore 按 BeforeOrder 升序执行
        foreach (var handler in Chain.BeforeHandlers)
        {
            var name = handler.GetType().Name.TrimSuffix("Handler");
            using var span = tracer?.NewSpan($"handler:OnBefore:{name}");
            try
            {
                await handler.OnBefore(context, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                span?.SetError(ex);
                throw;
            }

            ranBefore.Add(handler);
            if (context.FlowControl != ChatFlowControl.Continue) break;
        }

        // 所有 OnBefore 完成后，将 SystemSegments 结果统一写入 system 消息
        FlushSystemSegments(context);

        // 2. 核心阶段（Cancel 时跳过；SkipRemaining 时仍正常运行）
        if (context.FlowControl != ChatFlowControl.Cancel)
        {
            await foreach (var ev in CoreStreamAsync(context, cancellationToken).ConfigureAwait(false))
                yield return ev;
        }
        else
        {
            // 取消时回写一次 error 事件，便于客户端展示原因
            yield return ChatStreamEvent.ErrorEvent(context.CancelCode ?? "CANCELED", context.CancelMessage ?? "请求已取消");
        }

        // 3. OnAfter 按 AfterOrder 升序执行
        // 调用规则：After-only（无 Before 能力）的处理器无条件调用；Before+After 的处理器仅当其 OnBefore 确实执行过才调用
        foreach (var handler in Chain.AfterHandlers)
        {
            var hasBefore = handler.Capabilities.HasFlag(ChatHandlerCapabilities.Before);
            if (hasBefore && !ranBefore.Contains(handler)) continue;

            var name = handler.GetType().Name.TrimSuffix("Handler");
            using var span = tracer?.NewSpan($"handler:OnAfter:{name}");
            try
            {
                await handler.OnAfter(context, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                span?.SetError(ex);
                throw;
            }
        }
    }

    /// <summary>核心阶段。按 <see cref="ChatHandlerChain.Interceptors"/> 列表（注册序）倒序包裹 <see cref="InvokeLlmAsync"/> 构建洋葱链，
    /// 并将事件透传给调用方的同时，写入上下文 ContentBuilder/ThinkingBuilder/ToolCalls/Usage 收集器</summary>
    /// <param name="context">对话上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>事件流</returns>
    protected virtual async IAsyncEnumerable<ChatStreamEvent> CoreStreamAsync(IChatContext context, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // 链路终点：LLM 调用
        ChatNextDelegate next = ct => InvokeLlmAsync(context, ct);

        // 倒序包裹：Interceptors[0] 为最外层，Interceptors[^1] 为最内层（紧挨 LLM 调用）
        var interceptors = Chain.Interceptors;
        for (var i = interceptors.Count - 1; i >= 0; i--)
        {
            var handler = interceptors[i];

            var captured = next;
            next = ct =>
            {
                var name = handler.GetType().Name.TrimSuffix("Handler");
                using var span = tracer?.NewSpan($"handler:Invoke:{name}");
                try
                {
                    return handler.InvokeAsync(context, captured, ct);
                }
                catch (Exception ex)
                {
                    span?.SetError(ex);
                    throw;
                }
            };
        }

        var source = next(cancellationToken);
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
                        yield return ev;
                        break;
                    case "message_done":
                        // 仅当 Usage 含有效 Token 数时才覆盖，避免 LLM 未返回 usage 时
                        // InvokeLlmAsync 创建的全零 UsageDetails 覆盖已在流中正确设置的值
                        if (ev.Usage is { TotalTokens: > 0 }) context.Usage = ev.Usage;
                        yield return ev;
                        break;
                    case "error":
                        context.HasError = true;
                        yield return ev;
                        break;
                    case "tool_call_start":
                        // 去重：相同 id 的 start（如 earlyStart 无参 + Step1 完整参），更新已有条目的 Arguments 而非追加
                        var existingIdx = toolCalls.FindIndex(t => t.Id == ev.ToolCallId);
                        if (existingIdx >= 0)
                        {
                            var existing = toolCalls[existingIdx];
                            toolCalls[existingIdx] = existing with { Arguments = ev.Arguments };
                        }
                        else
                        {
                            toolCalls.Add(new ToolCallDto(ev.ToolCallId + "", ev.Name + "", ToolCallStatus.Calling, ev.Arguments, ContentOffset: contentBuilder.Length));
                        }
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
    /// 派生类可覆盖 <see cref="ApplyTools"/> / <see cref="ApplyResponseStyle"/> 扩展能力</summary>
    /// <param name="context">对话上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>事件流</returns>
    protected virtual async IAsyncEnumerable<ChatStreamEvent> InvokeLlmAsync(IChatContext context, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var span = tracer?.NewSpan("ai:flowInvokeLlm", new { messages = context.ContextMessages.Count });

        var contextMessages = context.ContextMessages;
        var model = (ModelConfig)context.ModelConfig;

        using var rawClient = modelService.CreateClient(model);
        if (rawClient == null)
        {
            yield return ChatStreamEvent.ErrorEvent("MODEL_UNAVAILABLE", $"未找到服务商 '{model.GetEffectiveProvider()}'");
            yield break;
        }

        using var streamClient = BuildPipelineClient(rawClient, context);

        // 防御：上下文缺少用户消息时直接报错，避免模型在无任务约束下空转
        var hasUserMessage = contextMessages.Any(m => m.Role == "user");
        if (!hasUserMessage)
        {
            log?.Warn("LLM 上下文缺少用户消息，即将跳过调用。ContextMessages.Count={0}", contextMessages.Count);
            yield return ChatStreamEvent.ErrorEvent("EMPTY_CONTEXT", "对话上下文异常：缺少用户问题，请刷新页面后重试");
            yield break;
        }

        var thinkingBuilder = new StringBuilder();
        UsageDetails? lastUsage = null;
        Int64 thinkingStart = 0;
        String? lastFinishReason = null;
        var streamSw = Stopwatch.StartNew();

        await foreach (var chunk in streamClient.GetStreamingResponseAsync(contextMessages, context.Options, cancellationToken).ConfigureAwait(false))
        {
            // ToolChatClient 已在最终轮末尾 yield 包含全局累加 Usage 的专用 chunk
            // MessageFlow 只需取最后一次非空 Usage，无需跨 chunk 自行累加
            // 立即写入 context.Usage，确保流式中途异常中断时 Token 用量不丢失
            if (chunk.Usage != null)
            {
                lastUsage = chunk.Usage;
                context.Usage = chunk.Usage;
            }

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

        if (thinkingBuilder.Length > 0) yield return ChatStreamEvent.ThinkingDone((Int32)(Runtime.TickCount64 - thinkingStart));

        streamSw.Stop();
        if (lastUsage != null)
        {
            lastUsage.ElapsedMs = (Int32)streamSw.ElapsedMilliseconds;
        }

        context.FinishReason = lastFinishReason;

        // 仅当 LLM 返回有效 usage 时才发送 MessageDone（含 Token 统计），
        // 避免用全零 UsageDetails 覆盖 context.Usage 中已在流式循环中正确设置的值
        yield return ChatStreamEvent.MessageDone(lastUsage, finishReason: lastFinishReason);
    }

    /// <summary>非流式 LLM 调用。链路最内层节点（非流式路径专用）：组装过滤器链 + 工具装配 + 单次 GetResponseAsync。
    /// 拦截器洋葱（<see cref="ChatHandlerCapabilities.Interceptor"/>）仅适用于流式路径，此方法不经过拦截器；
    /// <see cref="IChatFilter"/> 链（<see cref="ChatFilters"/>）仍通过 <c>UseFilters</c> 正常生效</summary>
    /// <param name="context">对话上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    protected virtual async Task InvokeLlmDirectAsync(IChatContext context, CancellationToken cancellationToken)
    {
        using var span = tracer?.NewSpan("ai:flowInvokeLlmDirect", new { messages = context.ContextMessages.Count });

        var contextMessages = context.ContextMessages;
        var model = (ModelConfig)context.ModelConfig;

        using var rawClient = modelService.CreateClient(model);
        if (rawClient == null)
        {
            context.HasError = true;
            return;
        }

        using var directClient = BuildPipelineClient(rawClient, context);

        var sw = Stopwatch.StartNew();
        var response = ChatResponse.From(await directClient.GetResponseAsync(contextMessages, context.Options, cancellationToken).ConfigureAwait(false));
        sw.Stop();

        //// 提取系统提示词（首个 system 消息）
        //context.SystemPrompt = contextMessages.FirstOrDefault(m => m.Role == "system")?.Content as String;
        //context.OnSystemReady?.Invoke(context.SystemPrompt!);

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

        // 写入用量（仅当 LLM 返回有效 usage 时；否则保持 context.Usage 原值，避免覆盖为全零）
        if (response.Usage != null)
        {
            var usage = response.Usage;
            usage.ElapsedMs = (Int32)sw.ElapsedMilliseconds;
            context.Usage = usage;
        }

        context.FinishReason = response.Messages?.FirstOrDefault()?.FinishReason?.ToApiString();
    }

    /// <summary>非流式 Handler 三段式调用链：OnBefore（注册顺序） → <see cref="InvokeLlmDirectAsync"/> → OnAfter（注册倒序）。
    /// 与 <see cref="InvokeChainAsync"/> 的区别：核心阶段调用非流式 LLM，拦截器洋葱（<see cref="ChatHandlerCapabilities.Interceptor"/>）不参与</summary>
    /// <param name="context">对话上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    protected virtual async Task InvokeNonStreamAsync(IChatContext context, CancellationToken cancellationToken)
    {
        var ranBefore = new HashSet<IChatHandler>(ReferenceEqualityComparer.Instance);

        // 1. OnBefore 按 BeforeOrder 升序执行
        foreach (var handler in Chain.BeforeHandlers)
        {
            var name = handler.GetType().Name.TrimSuffix("Handler");
            using var span = tracer?.NewSpan($"handler:OnBefore:{name}");
            try
            {
                await handler.OnBefore(context, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                span?.SetError(ex);
                throw;
            }

            ranBefore.Add(handler);
            if (context.FlowControl != ChatFlowControl.Continue) break;
        }

        // 所有 OnBefore 完成后，将 SystemSegments 结果统一写入 system 消息
        FlushSystemSegments(context);

        // 2. 核心阶段（Cancel 时跳过；SkipRemaining 时仍正常运行）
        if (context.FlowControl != ChatFlowControl.Cancel)
            await InvokeLlmDirectAsync(context, cancellationToken).ConfigureAwait(false);

        // 3. OnAfter 按 AfterOrder 升序执行
        // 调用规则：After-only（无 Before 能力）的处理器无条件调用；Before+After 的处理器仅当其 OnBefore 确实执行过才调用
        foreach (var handler in Chain.AfterHandlers)
        {
            var hasBefore = handler.Capabilities.HasFlag(ChatHandlerCapabilities.Before);
            if (hasBefore && !ranBefore.Contains(handler)) continue;

            var name = handler.GetType().Name.TrimSuffix("Handler");
            using var span = tracer?.NewSpan($"handler:OnAfter:{name}");
            try
            {
                await handler.OnAfter(context, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                span?.SetError(ex);
                throw;
            }
        }
    }

    /// <summary>将 <see cref="IChatContext.SystemSegments"/>（中段）和 <see cref="IChatContext.TailSegments"/>（末段）
    /// 中的所有文本片段以 <c>"\n\n"</c> 拼接后按顺序追加到 system 消息末尾。
    /// 注入顺序：基础 System Prompt（固定头）→ SystemSegments（中段，记忆/图谱/技能）→ TailSegments（末段，RAG/痛觉）→ user 消息。
    /// 在所有 OnBefore 处理器执行完成后、核心 LLM 调用之前调用</summary>
    /// <param name="context">对话上下文</param>
    protected virtual void FlushSystemSegments(IChatContext context)
    {
        var hasMiddle = context.SystemSegments.Count > 0;
        var hasTail = context.TailSegments.Count > 0;
        if (!hasMiddle && !hasTail) return;

        var messages = context.ContextMessages;
        if (messages == null) return;
        var systemMsg = messages.FirstOrDefault(m => m.Role == "system");

        // 构建追加内容（中段 + 末段按顺序拼接）
        var parts = new List<String>(context.SystemSegments.Count + context.TailSegments.Count);
        foreach (var s in context.SystemSegments)
            parts.Add(s);
        foreach (var s in context.TailSegments)
            parts.Add(s);

        var extra = String.Join("\n\n", parts);
        if (systemMsg != null)
        {
            var existing = systemMsg.Content as String ?? String.Empty;
            systemMsg.Content = existing.Length > 0 ? existing + "\n\n" + extra : extra;
        }
        else
            messages.Insert(0, new AiChatMessage { Role = "system", Content = extra });
    }

    /// <summary>在 rawClient 上组装过滤器链 + 工具层，构建完整管道客户端，并将实际注入的工具名写入 context.AvailableToolNames</summary>
    /// <param name="rawClient">原始模型客户端（生命周期由调用方管理）</param>
    /// <param name="context">对话上下文</param>
    /// <returns>管道客户端（调用方负责 Dispose）</returns>
    protected virtual IChatClient BuildPipelineClient(IChatClient rawClient, IChatContext context)
    {
        var clientBuilder = rawClient.AsBuilder();
        foreach (var filter in ChatFilters)
            clientBuilder = clientBuilder.UseFilters(filter);

        var providers = ToolProviders;
        if (providers.Length > 0)
        {
            clientBuilder = clientBuilder.UseTools(setting.ToolMaxIterations, setting.ToolResultMaxChars, context.SelectedTools, providers);

            // 记录本轮实际注入的工具（与 AI 收到的工具集一致）
            foreach (var p in providers)
                foreach (var t in p.GetTools(context.SelectedTools))
                    if (t.Function?.Name != null) context.AvailableToolNames.Add(t.Function.Name);
        }

        return clientBuilder.Build();
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

    #endregion

    #region 上下文构建

    /// <summary>从数据库加载历史消息并按时间升序排列。派生类可覆写以修改加载来源（如网关路径不从 DB 加载）</summary>
    /// <param name="conversationId">会话编号</param>
    /// <param name="maxRounds">最大保留轮数（对话轮数，非消息条数）</param>
    /// <returns>按 Id 升序排列的历史消息列表</returns>
    protected virtual IList<DbChatMessage> LoadHistoryMessages(Int64 conversationId, Int32 maxRounds)
    {
        // 倒序加载最新的一批消息，再重排为升序返回
        var history = DbChatMessage.FindAllByConversationIdDesc(conversationId, maxRounds * 2);
        //history.Reverse();
        //!!! 不能使用 Reverse ，它未能让列表完全倒置
        history = history.OrderBy(e => e.Id).ToList();

        // 过滤空内容的 assistant 占位消息（多次重新生成失败留下的残留）
        history = history.Where(e => !ShouldSkipHistoryMessage(e)).ToList();

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
        var aiMsg = new AiChatMessage { Role = msg.Role ?? "user", Content = msg.Content };
        // DeepSeek 思考模式要求：历史 assistant 消息有 reasoning_content 时必须原样回传，否则 API 返回 400
        if (msg.Role.EqualIgnoreCase("assistant") && !msg.ThinkingContent.IsNullOrEmpty())
            aiMsg.ReasoningContent = msg.ThinkingContent;
        return aiMsg;
    }

    /// <summary>构建系统提示词消息。合并用户全局级和模型级系统提示词（技能提示词由管道注入）</summary>
    /// <remarks>静态公共实现：只拼接 IUser 基础信息与 UserSetting 个性化；
    /// <see cref="GatewayService"/> 与 <see cref="MessageFlow"/> 均调用此方法，避免逻辑重复。</remarks>
    /// <param name="userId">当前用户编号</param>
    /// <param name="model">模型配置（可选）</param>
    /// <param name="userHistoryCount">当前上下文中历史消息条数，大于 0 时才注入多轮优先级提示</param>
    /// <param name="tracer">追踪器（可选）</param>
    /// <returns>系统消息，无提示词时返回 null</returns>
    public static AiChatMessage? BuildSystemMessage(Int32 userId, ModelConfig? model, Int32 userHistoryCount = 0, ITracer? tracer = null)
    {
        using var span = tracer?.NewSpan("ai:BuildSystemMessage", new { userId, model?.Name, userHistoryCount });
        var parts = new List<String>();

        // 0. 当前日期时间（置首，确保 AI 在处理"今天"/"2号那天"等模糊表达时有明确锚点）
        {
            parts.Add($"当前时间：{DateTimeOffset.Now:O}");
        }

        // 1. 当前用户基础信息（基类只拼 DisplayName/Name/Roles，不查部门——派生类按需增强）
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

        // 2. 个性化定制
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

        // 3. 用户自定义指令
        if (userSetting != null && !String.IsNullOrWhiteSpace(userSetting.SystemPrompt))
            parts.Add(userSetting.SystemPrompt.Trim());

        // 4. 模型级系统提示词
        if (model != null && !String.IsNullOrWhiteSpace(model.SystemPrompt))
            parts.Add(model.SystemPrompt.Trim());

        // 5. 多轮对话时强调最新消息优先级
        if (userHistoryCount > 1)
            parts.Add("请优先回应用户的最新消息。如果最新消息与之前的对话内容存在矛盾或方向变化，以最新消息为准。");

        if (parts.Count == 0) return null;
        span?.AppendTag(null!, parts.Count);

        return new AiChatMessage { Role = "system", Content = String.Join("\n\n", parts) };
    }

    /// <summary>构建项目维度系统提示词消息。通过项目密钥（AppKey.ProjectId > 0）接入时使用，不含用户个人信息与个性化设置</summary>
    /// <remarks>项目密钥由用户在项目内创建，代表项目接入而非个人使用；注入项目级指令 + 时间 + 模型级指令。AppKey.SystemPrompt（业务层）由调用方在合并时置首</remarks>
    /// <param name="projectId">项目编号。0 或不传时跳过项目级信息读取</param>
    /// <param name="model">模型配置（可选）</param>
    /// <param name="userHistoryCount">当前上下文中历史消息条数，大于 0 时才注入多轮优先级提示</param>
    /// <param name="tracer">追踪器（可选）</param>
    /// <returns>系统消息，无提示词时返回 null</returns>
    public static AiChatMessage? BuildSystemMessageForProject(Int32 projectId, ModelConfig? model, Int32 userHistoryCount = 0, ITracer? tracer = null)
    {
        using var span = tracer?.NewSpan("ai:BuildSystemMessageForProject", new { projectId, model?.Name, userHistoryCount });
        var parts = new List<String>();

        // 0. 当前日期时间
        parts.Add($"当前时间：{DateTimeOffset.Now:O}");

        // 1. 项目级系统提示词（StarChat 独有；AgentProject 不在 ChatAI 实体，故用条件编译）
        // 项目级指令定义 AI 整体行为与角色，优先级仅次于 AppKey.SystemPrompt（业务层）
#if STARCHAT
        if (projectId > 0)
        {
            var project = NewLife.StarChat.Entity.AgentProject.FindById(projectId);
            if (project != null && !String.IsNullOrWhiteSpace(project.SystemPrompt))
                parts.Add(project.SystemPrompt.Trim());
        }
#endif

        // 2. 模型级系统提示词（项目接入仍遵从模型配置指令）
        if (model != null && !String.IsNullOrWhiteSpace(model.SystemPrompt))
            parts.Add(model.SystemPrompt.Trim());

        // 3. 多轮对话时强调最新消息优先级
        if (userHistoryCount > 1)
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
                collector[i] = new ToolCallDto(orig.Id, orig.Name, status, orig.Arguments, value, null, null, orig.ContentOffset);
                break;
            }
        }
    }
    #endregion
}
