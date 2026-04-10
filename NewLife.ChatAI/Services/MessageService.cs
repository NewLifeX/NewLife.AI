using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using NewLife.AI.Clients;
using NewLife.AI.Models;
using NewLife.AI.Services;
using NewLife.ChatAI.Entity;
using NewLife.ChatAI.Models;
using NewLife.Collections;
using NewLife.Cube.Entity;
using NewLife.Log;
using NewLife.Serialization;
using AiChatMessage = NewLife.AI.Models.ChatMessage;
using AiFunctionCall = NewLife.AI.Models.FunctionCall;
using AiToolCall = NewLife.AI.Models.ToolCall;
using ChatMessage = NewLife.ChatAI.Entity.ChatMessage;
using ChatStreamEvent = NewLife.AI.Models.ChatStreamEvent;
using ILog = NewLife.Log.ILog;
using UsageDetails = NewLife.AI.Models.UsageDetails;

namespace NewLife.ChatAI.Services;

/// <summary>消息生成服务。负责流式生成、重新生成等核心对话方法的实现</summary>
/// <remarks>
/// 从 ChatApplicationService 中独立出来，专注于流式编排、上下文构建与持久化。
/// 能力扩展层（工具调用、技能注入）与知识进化层（记忆、自学习）均通过 <see cref="IChatPipeline"/> 透明注入。
/// </remarks>
/// <param name="pipeline">已装配好三层能力的对话执行管道</param>
/// <param name="gatewayService">网关服务（用于模型解析）</param>
/// <param name="backgroundService">后台生成服务</param>
/// <param name="usageService">用量统计服务</param>
/// <param name="tracer">追踪器</param>
/// <param name="log">日志</param>
public class MessageService(IChatPipeline pipeline, GatewayService gatewayService, BackgroundGenerationService? backgroundService, UsageService? usageService, ITracer tracer, ILog log)
{
    #region 生成方法

    /// <summary>非流式重新生成 AI 回复。构建上下文后委托管道完成，结果直接写回消息记录</summary>
    /// <param name="messageId">消息编号（必须为 AI 回复）</param>
    /// <param name="userId">当前用户编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>更新后的消息 DTO，失败时返回 null</returns>
    public async Task<MessageDto?> RegenerateMessageAsync(Int64 messageId, Int32 userId, CancellationToken cancellationToken)
    {
        var entity = ChatMessage.FindById(messageId);
        if (entity == null || !entity.Role.EqualIgnoreCase("assistant"))
            return null;

        // 查找会话和模型配置
        var conversation = Conversation.FindById(entity.ConversationId);
        if (conversation == null) return null;

        var modelConfig = gatewayService.ResolveModel(conversation.ModelId);
        if (modelConfig == null)
        {
            // 模型不可用时直接报错，不做降级
            return null;
        }

        // 服务商不可用时提前返回（管道内部也有校验，但提前失败更友好）
        if (gatewayService.GetDescriptor(modelConfig) == null) return null;

        // 构建上下文：取该消息之前的所有消息（注入系统提示词）
        var beforeMessages = ChatMessage.FindAllBeforeId(entity.ConversationId, entity.Id).Where(e => e.IsMain).ToList();

        // 按轮数截取
        var setting = ChatSetting.Current;
        var maxCount = (setting.DefaultContextRounds > 0 ? setting.DefaultContextRounds : 10) * 2;
        if (beforeMessages.Count > maxCount)
            beforeMessages = beforeMessages.Skip(beforeMessages.Count - maxCount).ToList();

        var contextMessages = new List<AiChatMessage>();

        // 注入系统提示词（用户全局级 + 模型级；技能提示词由管道的技能注入层负责）
        var systemMsg = BuildSystemMessage(userId, modelConfig, beforeMessages.Count);
        if (systemMsg != null) contextMessages.Add(systemMsg);

        foreach (var msg in beforeMessages)
        {
            contextMessages.Add(new AiChatMessage
            {
                Role = msg.Role ?? "user",
                Content = msg.Content,
            });
        }

        try
        {
            // 委托管道执行（能力扩展层 + 知识进化层由管道内部处理）
            var pipelineCtx = new ChatPipelineContext
            {
                UserId = userId + "",
                ConversationId = entity.ConversationId + "",
                SkillId = conversation.SkillId
            };
            var regenSw = Stopwatch.StartNew();
            var response = await pipeline.CompleteAsync(contextMessages, modelConfig, pipelineCtx, cancellationToken).ConfigureAwait(false);
            regenSw.Stop();
            var regenElapsed = (Int32)regenSw.ElapsedMilliseconds;

            var newContent = response.Messages?.FirstOrDefault()?.Message?.Content as String ?? String.Empty;
            var reasoning = response.Messages?.FirstOrDefault()?.Message?.ReasoningContent;

            entity.Content = newContent;
            if (!String.IsNullOrEmpty(reasoning)) entity.ThinkingContent = reasoning;
            entity.ElapsedMs = regenElapsed;
            entity.Update();

            // 写入用量记录，并累计会话 Token（直接使用已有的 conversation 对象，不重复查询）
            if (response.Usage != null)
            {
                usageService?.Record(userId, 0, entity.ConversationId, entity.Id, modelConfig.Id, response.Usage, "Chat");
                conversation.InputTokens += response.Usage.InputTokens;
                conversation.OutputTokens += response.Usage.OutputTokens;
                conversation.TotalTokens += response.Usage.TotalTokens;
                conversation.ElapsedMs += regenElapsed;
                conversation.Update();
            }

            return ToMessageDto(entity);
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
    public async IAsyncEnumerable<ChatStreamEvent> EditAndResendStreamAsync(Int64 messageId, String newContent, Int32 userId, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var entity = ChatMessage.FindById(messageId);
        if (entity == null || !entity.Role.EqualIgnoreCase("user"))
        {
            yield return ChatStreamEvent.ErrorEvent("MESSAGE_NOT_FOUND", "消息不存在或非用户消息");
            yield break;
        }

        var conversation = Conversation.FindById(entity.ConversationId);
        if (conversation == null)
        {
            yield return ChatStreamEvent.ErrorEvent("CONVERSATION_NOT_FOUND", "会话不存在");
            yield break;
        }

        // 1. 更新消息内容
        entity.Content = newContent;
        entity.Update();

        // 2. 删除该消息之后的所有消息（包括 AI 回复及后续对话）
        var deleted = ChatMessage.FindAllAfterId(entity.ConversationId, entity.Id);
        foreach (var msg in deleted)
        {
            msg.Delete();
        }

        // 3. 解析模型
        var modelConfig = gatewayService.ResolveModel(conversation.ModelId);
        if (modelConfig == null)
        {
            yield return ChatStreamEvent.ErrorEvent("MODEL_UNAVAILABLE", $"模型 '{conversation.ModelId}' 不可用");
            yield break;
        }

        // 4. 构建上下文（包含编辑后的用户消息；技能提示词由管道注入）
        var contextMessages = BuildContextMessages(userId, entity.ConversationId, newContent, modelConfig);

        // 5. 创建新的 AI 回复消息
        var assistantMsg = new ChatMessage
        {
            ConversationId = entity.ConversationId,
            Role = "assistant",
            ThinkingMode = entity.ThinkingMode,
        };
        assistantMsg.Insert();

        // message_start
        yield return ChatStreamEvent.MessageStart(assistantMsg.Id, modelConfig.Code ?? String.Empty, entity.ThinkingMode);

        // 6. 委托管道流式执行（能力扩展层 + 知识进化层由管道内部处理）
        var editPipelineCtx = new ChatPipelineContext
        {
            UserId = userId + "",
            ConversationId = entity.ConversationId + "",
            SkillId = conversation.SkillId
        };
        var contentBuilder = new StringBuilder();
        var thinkingBuilder = new StringBuilder();
        UsageDetails? finalUsage = null;
        var hasError = false;
        ChatStreamEvent? deferredErrorEvent = null;

        var pipelineStream = pipeline.StreamAsync(contextMessages, modelConfig, entity.ThinkingMode, editPipelineCtx, cancellationToken);
        await foreach (var ev in DrainPipelineAsync(pipelineStream, contentBuilder, thinkingBuilder, null,
            u => finalUsage = u, (err, e) => { hasError = err; deferredErrorEvent = e; }, "编辑重发流式生成失败", cancellationToken).ConfigureAwait(false))
        {
            yield return ev;
            if (hasError) break;
        }

        if (deferredErrorEvent != null)
            yield return deferredErrorEvent;

        // 7. 持久化
        assistantMsg.Content = contentBuilder.Length > 0 ? contentBuilder.ToString() : null;
        if (thinkingBuilder.Length > 0)
            assistantMsg.ThinkingContent = thinkingBuilder.ToString();
        ApplyUsageToMessage(assistantMsg, finalUsage, hasError, deferredErrorEvent?.Error);
        ApplyRequestParams(assistantMsg, modelConfig, editPipelineCtx);
        assistantMsg.Update();

        ApplyUsageToConversation(conversation, entity.ConversationId, finalUsage);
        conversation.Update();

        if (finalUsage != null)
            usageService?.Record(userId, 0, entity.ConversationId, assistantMsg.Id, modelConfig.Id, finalUsage, "Chat");

        if (!hasError && !cancellationToken.IsCancellationRequested)
        {
            yield return new ChatStreamEvent { Type = "message_done", MessageId = assistantMsg.Id, Usage = finalUsage, };
        }
    }

    /// <summary>流式重新生成 AI 回复。替换当前 AI 回复并通过 SSE 事件流返回新内容</summary>
    /// <param name="messageId">消息编号（必须为 AI 回复）</param>
    /// <param name="userId">当前用户编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>SSE 事件流</returns>
    public async IAsyncEnumerable<ChatStreamEvent> RegenerateStreamAsync(Int64 messageId, Int32 userId, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var entity = ChatMessage.FindById(messageId);
        if (entity == null || !entity.Role.EqualIgnoreCase("assistant"))
        {
            yield return ChatStreamEvent.ErrorEvent("MESSAGE_NOT_FOUND", "消息不存在或非AI回复");
            yield break;
        }

        var conversation = Conversation.FindById(entity.ConversationId);
        if (conversation == null)
        {
            yield return ChatStreamEvent.ErrorEvent("CONVERSATION_NOT_FOUND", "会话不存在");
            yield break;
        }

        var modelConfig = gatewayService.ResolveModel(conversation.ModelId);
        if (modelConfig == null)
        {
            yield return ChatStreamEvent.ErrorEvent("MODEL_UNAVAILABLE", $"模型 '{conversation.ModelId}' 不可用");
            yield break;
        }

        // 构建上下文：取该消息之前的所有消息
        var beforeMessages = ChatMessage.FindAllBeforeId(entity.ConversationId, entity.Id);

        var setting = ChatSetting.Current;
        var maxCount = (setting.DefaultContextRounds > 0 ? setting.DefaultContextRounds : 10) * 2;
        if (beforeMessages.Count > maxCount)
            beforeMessages = beforeMessages.Skip(beforeMessages.Count - maxCount).ToList();

        var contextMessages = new List<AiChatMessage>();

        // 注入系统提示词（技能提示词由管道注入）
        var systemMsg = BuildSystemMessage(userId, modelConfig, beforeMessages.Count);
        if (systemMsg != null) contextMessages.Add(systemMsg);

        foreach (var msg in beforeMessages)
        {
            if (msg.Role.EqualIgnoreCase("user") && !msg.Attachments.IsNullOrEmpty())
            {
                contextMessages.Add(BuildMultimodalUserMessage(msg.Attachments, msg.Content));
                continue;
            }
            contextMessages.Add(new AiChatMessage { Role = msg.Role ?? "user", Content = msg.Content });
        }

        // message_start
        yield return ChatStreamEvent.MessageStart(entity.Id, modelConfig.Code ?? String.Empty, entity.ThinkingMode);

        // 委托管道流式执行
        var contentBuilder = new StringBuilder();
        var thinkingBuilder = new StringBuilder();
        UsageDetails? finalUsage = null;
        var hasError = false;
        ChatStreamEvent? deferredErrorEvent = null;

        var regenPipelineCtx = new ChatPipelineContext
        {
            UserId = userId + "",
            ConversationId = entity.ConversationId + "",
            SkillId = conversation.SkillId
        };
        var pipelineStream = pipeline.StreamAsync(contextMessages, modelConfig, entity.ThinkingMode, regenPipelineCtx, cancellationToken);
        await foreach (var ev in DrainPipelineAsync(pipelineStream, contentBuilder, thinkingBuilder, null,
            u => finalUsage = u, (err, e) => { hasError = err; deferredErrorEvent = e; }, "流式重新生成失败", cancellationToken).ConfigureAwait(false))
        {
            yield return ev;
            if (hasError) break;
        }

        if (deferredErrorEvent != null)
            yield return deferredErrorEvent;

        // 持久化：覆盖原消息内容
        entity.Content = contentBuilder.Length > 0 ? contentBuilder.ToString() : null;
        if (thinkingBuilder.Length > 0)
            entity.ThinkingContent = thinkingBuilder.ToString();
        ApplyUsageToMessage(entity, finalUsage, hasError, deferredErrorEvent?.Error);
        ApplyRequestParams(entity, modelConfig, regenPipelineCtx);
        entity.Update();

        // 累计会话 Token（重新生成：叠加本次 API 消耗，不扣减被替换消息的旧 Token）
        // 注意：重新生成不需要更新 MessageCount，仅叠加统计
        conversation.LastMessageTime = DateTime.Now;
        if (finalUsage != null)
        {
            conversation.InputTokens += finalUsage.InputTokens;
            conversation.OutputTokens += finalUsage.OutputTokens;
            conversation.TotalTokens += finalUsage.TotalTokens;
            conversation.ElapsedMs += finalUsage.ElapsedMs;
            conversation.Update();
        }

        // 记录用量
        if (finalUsage != null)
            usageService?.Record(userId, 0, entity.ConversationId, entity.Id, modelConfig.Id, finalUsage, "Chat");

        // message_done
        if (!hasError && !cancellationToken.IsCancellationRequested)
        {
            yield return new ChatStreamEvent { Type = "message_done", MessageId = entity.Id, Usage = finalUsage, };
        }
    }

    /// <summary>流式发送消息并获取 AI 回复。依次：保存用户消息 → 构建上下文 → 委托管道流式生成 → 持久化结果 → 推送 SSE 事件</summary>
    /// <param name="conversationId">会话编号</param>
    /// <param name="request">发送消息请求</param>
    /// <param name="userId">当前用户编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>SSE 事件流，含 message_start / thinking_delta / content_delta / tool_call_* / message_done / error</returns>
    public async IAsyncEnumerable<ChatStreamEvent> StreamMessageAsync(Int64 conversationId, SendMessageRequest request, Int32 userId, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var conversation = Conversation.FindById(conversationId);
        if (conversation == null)
        {
            yield return ChatStreamEvent.ErrorEvent("CONVERSATION_NOT_FOUND", "会话不存在");
            yield break;
        }

        // 保存用户消息
        var userMsg = new ChatMessage
        {
            ConversationId = conversationId,
            Role = "user",
            Content = request.Content,
            ThinkingMode = request.ThinkingMode,
        };
        if (request.AttachmentIds is { Count: > 0 })
            userMsg.Attachments = request.AttachmentIds.ToJson();
        userMsg.Insert();

        // 推荐问题缓存匹配：精确匹配且当天有缓存时，直接返回缓存响应，不请求大模型
        if (ChatSetting.Current.EnableSuggestedQuestionCache)
        {
            var cached = SuggestedQuestion.FindCachedTodayByQuestion(request.Content);
            if (cached != null)
            {
                await foreach (var ev in StreamSuggestedCacheAsync(conversationId, conversation, cached, request.ThinkingMode, cancellationToken))
                    yield return ev;
                yield break;
            }
        }

        // 解析模型配置（在插入 assistant 消息之前，避免模型不可用时留下空消息残留）
        // 优先使用请求携带的模型（前端本轮选择），其次回退到会话绑定的模型（上次使用的默认）
        // 切换后更新会话绑定，形成 sticky 效果；model_id=0 时自动降级为第一个可用模型
        var modelId = request.ModelId > 0 ? request.ModelId : conversation.ModelId;
        var modelConfig = gatewayService.ResolveModelOrDefault(modelId);
        if (modelConfig == null)
        {
            yield return ChatStreamEvent.ErrorEvent("MODEL_UNAVAILABLE", "系统暂无可用模型，请先在管理后台配置并启用至少一个模型");
            yield break;
        }

        // 更新会话绑定的模型
        if (conversation.ModelId != modelConfig.Id)
        {
            conversation.ModelId = modelConfig.Id;
            conversation.Update();
        }

        // 处理技能激活：每轮均可切换技能，sticky 更新会话绑定（仅更新会话元数据；技能提示词由管道注入）
        // SkillCode 非空且有效  → 切换到新技能，写回会话
        // SkillCode = "none"   → 清除技能绑定，回到通用对话，写回会话
        // SkillCode 为空       → 不变，沿用会话上次的技能
        // 工具集由技能定义决定，切换技能即切换本轮可用工具；全局 MCP 开关由用户设置控制
        var skillId = conversation.SkillId;
        var skillName = conversation.SkillName;
        if (!String.IsNullOrEmpty(request.SkillCode))
        {
            if (request.SkillCode.EqualIgnoreCase("none"))
            {
                // 清除技能绑定，回到通用对话
                skillId = 0;
                skillName = null;
                if (conversation.SkillId != 0)
                {
                    conversation.SkillId = 0;
                    conversation.SkillName = null;
                    conversation.Update();
                }
            }
            else
            {
                var skill = Skill.FindByCode(request.SkillCode);
                if (skill != null && skill.Enable)
                {
                    skillId = skill.Id;
                    skillName = skill.Name;
                    if (conversation.SkillId != skillId)
                    {
                        conversation.SkillId = skillId;
                        conversation.SkillName = skillName;
                        conversation.Update();
                    }
                }
            }
        }

        // 记录本轮激活的技能名称到用户消息
        if (skillId > 0 && !skillName.IsNullOrEmpty())
        {
            userMsg.SkillNames = skillName;
            userMsg.Update();
        }

        // 构建上下文（在插入空 assistant 消息 Id 之前，避免空消息被包含在上下文中）
        var contextMessages = BuildContextMessages(userId, conversationId, request.Content, modelConfig);

        // 预分配AI回复消息编号
        var assistantMsg = new ChatMessage
        {
            ConversationId = conversationId,
            Role = "assistant",
            ThinkingMode = request.ThinkingMode,
        };
        assistantMsg.Insert();

        // message_start（含完整字段）
        using var span = tracer?.NewSpan($"ai:Stream:{modelConfig.Code}", request.Content);
        yield return ChatStreamEvent.MessageStart(assistantMsg.Id, modelConfig.Code ?? String.Empty, request.ThinkingMode);

        // 提前启动标题生成（与流式内容并行执行，不阻塞 SSE 流）
        // 标题只需要用户问题文本，无需等待 AI 回复完成
        if (conversation.MessageCount == 0 && ChatSetting.Current.AutoGenerateTitle)
        {
            _ = Task.Run(() => GenerateTitleAsync(conversationId, request.Content, CancellationToken.None));
        }

        // 委托管道流式执行（能力扩展层 + 知识进化层由管道内部处理）
        var contentBuilder = new StringBuilder();
        var thinkingBuilder = new StringBuilder();
        UsageDetails? finalUsage = null;
        var hasError = false;
        ChatStreamEvent? deferredErrorEvent = null;
        var toolCallsCollector = new List<ToolCallDto>();

        var msgPipelineCtx = new ChatPipelineContext
        {
            UserId = userId + "",
            ConversationId = conversationId + "",
            SkillId = skillId
        };
        if (request.Options != null) msgPipelineCtx.Items = request.Options;

        // 预处理：注入技能提示词、解析@引用，生成 SystemPrompt
        pipeline.PrepareContext(contextMessages, msgPipelineCtx);

        // 注册系统消息就绪回调（管道收到第一个 chunk 时触发，早于整个流结束，且已包含 LearningFilter 注入的完整用户记忆）
        msgPipelineCtx.OnSystemReady = sysContent =>
        {
            if (!sysContent.IsNullOrEmpty())
                new ChatMessage { ConversationId = conversationId, Role = "system", Content = sysContent }.Insert();
        };

        var pipelineStream = pipeline.StreamAsync(contextMessages, modelConfig, request.ThinkingMode, msgPipelineCtx, cancellationToken);

        await foreach (var ev in DrainPipelineAsync(pipelineStream, contentBuilder, thinkingBuilder, toolCallsCollector,
            u => finalUsage = u, (err, e) => { hasError = err; deferredErrorEvent = e; }, "流式生成失败", cancellationToken).ConfigureAwait(false))
        {
            yield return ev;
            if (hasError) break;
        }

        // 延迟发送错误事件（不能在 try-catch 中 yield）
        if (deferredErrorEvent != null)
            yield return deferredErrorEvent;

        // 持久化结果（无论成功或中断都保存已输出内容，避免空消息残留）
        assistantMsg.Content = contentBuilder.Length > 0 ? contentBuilder.ToString() : null;
        if (thinkingBuilder.Length > 0)
            assistantMsg.ThinkingContent = thinkingBuilder.ToString();
        if (toolCallsCollector.Count > 0)
            assistantMsg.ToolCalls = toolCallsCollector.ToJson();
        // 记录本轮技能与工具信息
        if (msgPipelineCtx.AvailableToolNames.Count > 0)
        {
            userMsg.ToolNames = String.Join(",", msgPipelineCtx.AvailableToolNames);
            userMsg.Update();
        }
        if (skillId > 0 && !skillName.IsNullOrEmpty())
            assistantMsg.SkillNames = skillName;
        // 追加管道中解析到的所有技能名称
        if (msgPipelineCtx.ResolvedSkillNames.Count > 0)
        {
            var existing = assistantMsg.SkillNames;
            var allNames = existing.IsNullOrEmpty()
                ? String.Join(",", msgPipelineCtx.ResolvedSkillNames)
                : existing + "," + String.Join(",", msgPipelineCtx.ResolvedSkillNames);
            assistantMsg.SkillNames = allNames;
        }
        if (toolCallsCollector.Count > 0)
            assistantMsg.ToolNames = String.Join(",", toolCallsCollector.Select(t => t.Name));
        ApplyUsageToMessage(assistantMsg, finalUsage, hasError, deferredErrorEvent?.Error);
        ApplyRequestParams(assistantMsg, modelConfig, msgPipelineCtx);
        assistantMsg.Update();

        // 更新会话
        ApplyUsageToConversation(conversation, conversationId, finalUsage);
        conversation.ModelName = modelConfig.Name;
        conversation.Update();

        // 记录用量
        if (finalUsage != null)
            usageService?.Record(userId, 0, conversationId, assistantMsg.Id, modelConfig.Id, finalUsage, "Chat");

        // 推荐问题缓存回写
        if (!hasError && ChatSetting.Current.EnableSuggestedQuestionCache && contentBuilder.Length > 0)
            TryWriteBackSuggestedQuestionCache(request.Content, contentBuilder.ToString(), thinkingBuilder.Length > 0 ? thinkingBuilder.ToString() : null, modelConfig.Id);

        // message_done
        if (!hasError && !cancellationToken.IsCancellationRequested)
        {
            yield return new ChatStreamEvent { Type = "message_done", MessageId = assistantMsg.Id, Usage = finalUsage, };
        }
    }

    /// <summary>中断生成。停止后台正在运行的流式生成任务</summary>
    /// <param name="messageId">消息编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public Task StopGenerateAsync(Int64 messageId, CancellationToken cancellationToken)
    {
        backgroundService?.Stop(messageId);
        return Task.CompletedTask;
    }

    /// <summary>获取后台生成任务状态。用户切换会话再切回时，可获取后台已生成的内容</summary>
    /// <param name="messageId">消息编号</param>
    /// <returns>后台任务状态信息，不存在返回 null</returns>
    public BackgroundTask? GetBackgroundTask(Int64 messageId) => backgroundService?.GetTask(messageId);

    /// <summary>异步生成会话标题。根据用户首条消息内容，调用模型生成简短标题</summary>
    /// <param name="conversationId">会话编号</param>
    /// <param name="userMessage">用户首条消息内容</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public async Task<String?> GenerateTitleAsync(Int64 conversationId, String userMessage, CancellationToken cancellationToken)
    {
        var conversation = Conversation.FindById(conversationId);
        if (conversation == null) return null;

        using var span = tracer?.NewSpan("ai:GenerateTitle");

        // 尝试通过模型生成标题
        var modelConfig = gatewayService.ResolveModel(conversation.ModelId);
        if (modelConfig != null)
        {
            var descriptor = gatewayService.GetDescriptor(modelConfig);
            if (descriptor != null)
            {
                try
                {
                    var prompt = "请用16个字以内为以下对话生成一个简短标题，只输出标题文字，不要加任何标点和引号：";
                    var options = GatewayService.BuildOptions(modelConfig);
                    using var titleClient = descriptor.Factory(options);
                    var clientTimeout = (titleClient as AiClientBase)?.Timeout ?? TimeSpan.FromSeconds(30);
                    using var titleCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    titleCts.CancelAfter(clientTimeout * 3);
                    var response = await titleClient.GetResponseAsync(
                        [new AiChatMessage { Role = "user", Content = $"{prompt}\n{userMessage}" }],
                        new ChatOptions { Model = modelConfig.Code, MaxTokens = 30 },
                        titleCts.Token).ConfigureAwait(false);

                    var title = response.Messages?.FirstOrDefault()?.Message?.Content as String;
                    if (!String.IsNullOrWhiteSpace(title))
                    {
                        title = title.Trim().Trim('"', '\u201c', '\u201d', '\'', '\u300a', '\u300b');
                        if (title.Length > 30) title = title[..30];
                        span?.AppendTag(title!);

                        conversation.Title = title;
                        conversation.Update();
                        return title;
                    }
                }
                catch (Exception ex)
                {
                    span?.SetError(ex);
                    log?.Warn("模型生成标题失败，回退截取: {0}", ex.Message);
                }
            }
        }

        // 回退：截取前16个字符
        var fallbackTitle = userMessage.Length > 16 ? userMessage[..16] : userMessage;
        fallbackTitle = fallbackTitle.Replace("\n", " ").Replace("\r", "").Trim();

        if (!String.IsNullOrWhiteSpace(fallbackTitle) && fallbackTitle != conversation.Title)
        {
            conversation.Title = fallbackTitle;
            conversation.Update();
        }

        return fallbackTitle;
    }

    #endregion

    #region 辅助

    /// <summary>命中推荐问题缓存时，流式输出缓存响应。插入 assistant 消息，按节流配置逐块推送内容，最后更新会话计数</summary>
    /// <param name="conversationId">会话编号</param>
    /// <param name="conversation">会话实体</param>
    /// <param name="cached">命中的推荐问题缓存条目</param>
    /// <param name="thinkingMode">思考模式</param>
    /// <param name="cancellationToken">取消令牌</param>
    private async IAsyncEnumerable<ChatStreamEvent> StreamSuggestedCacheAsync(Int64 conversationId, Conversation conversation, SuggestedQuestion cached, ThinkingMode thinkingMode, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var cachedMsg = new ChatMessage
        {
            ConversationId = conversationId,
            Role = "assistant",
            Content = cached.Response,
            ThinkingContent = cached.ThinkingResponse.IsNullOrEmpty() ? null : cached.ThinkingResponse,
        };
        cachedMsg.Insert();

        var streamingSpeed = ChatSetting.Current.StreamingSpeed;

        yield return ChatStreamEvent.MessageStart(cachedMsg.Id, cached.Model?.Code ?? String.Empty, thinkingMode);

        if (!cached.ThinkingResponse.IsNullOrEmpty())
        {
            if (streamingSpeed > 5)
            {
                yield return new ChatStreamEvent { Type = "thinking_delta", Content = cached.ThinkingResponse };
            }
            else
            {
                var (tChunkSize, tDelayMs) = GetCachedStreamingParams(streamingSpeed);
                await foreach (var chunk in ThrottleTextAsync(cached.ThinkingResponse, tChunkSize, tDelayMs, cancellationToken))
                    yield return new ChatStreamEvent { Type = "thinking_delta", Content = chunk };
            }
        }

        if (streamingSpeed > 5)
        {
            yield return new ChatStreamEvent { Type = "content_delta", Content = cached.Response };
        }
        else
        {
            var (chunkSize, delayMs) = GetCachedStreamingParams(streamingSpeed);
            await foreach (var chunk in ThrottleTextAsync(cached.Response, chunkSize, delayMs, cancellationToken))
                yield return new ChatStreamEvent { Type = "content_delta", Content = chunk };
        }

        // 更新会话
        conversation.LastMessageTime = DateTime.Now;
        conversation.MessageCount = (Int32)ChatMessage.FindCount(ChatMessage._.ConversationId == conversationId);
        conversation.Update();

        yield return new ChatStreamEvent { Type = "message_done", MessageId = cachedMsg.Id };
    }

    /// <summary>回写推荐问题缓存。将本次 AI 回复写入匹配的推荐问题，供下次直接命中，当天已更新时跳过</summary>
    /// <param name="question">用户提问内容</param>
    /// <param name="content">AI 回复正文</param>
    /// <param name="thinking">AI 思考内容（可为 null）</param>
    /// <param name="modelId">使用的模型编号</param>
    private static void TryWriteBackSuggestedQuestionCache(String question, String content, String? thinking, Int32 modelId)
    {
        var sq = SuggestedQuestion.FindCachedByQuestion(question);
        if (sq == null || (!sq.Response.IsNullOrEmpty() && sq.UpdateTime.Date >= DateTime.Today)) return;

        sq.Response = content;
        sq.ThinkingResponse = thinking;
        sq.ModelId = modelId;
        sq.Update();
    }

    /// <summary>构建上下文消息列表。按配置的轮数截取历史消息，并注入系统提示词</summary>
    /// <param name="userId">当前用户编号</param>
    /// <param name="conversationId">会话编号</param>
    /// <param name="currentContent">当前用户消息内容</param>
    /// <param name="modelConfig">模型配置（可选，用于注入模型级系统提示词）</param>
    /// <returns>OpenAI ChatMessage 格式的消息列表</returns>
    private IList<AiChatMessage> BuildContextMessages(Int32 userId, Int64 conversationId, String currentContent, ModelConfig? modelConfig = null)
    {
        var setting = ChatSetting.Current;
        var maxRounds = setting.DefaultContextRounds > 0 ? setting.DefaultContextRounds : 10;

        var history = ChatMessage.FindAllByConversationIdDesc(conversationId, maxRounds * 2);
        history.Reverse();

        var messages = new List<AiChatMessage>();

        // 注入系统提示词
        var systemMsg = BuildSystemMessage(userId, modelConfig, history.Count);
        if (systemMsg != null) messages.Add(systemMsg);

        foreach (var msg in history)
        {
            if (msg.Role == "assistant" && !msg.ToolCalls.IsNullOrEmpty())
            {
                IList<ToolCallDto>? storedDtos = null;
                try
                {
                    storedDtos = msg.ToolCalls.ToJsonEntity<List<ToolCallDto>>();
                }
                catch { }
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
            if (msg.Role.EqualIgnoreCase("user") && !msg.Attachments.IsNullOrEmpty())
            {
                messages.Add(BuildMultimodalUserMessage(msg.Attachments, msg.Content));
                continue;
            }
            messages.Add(new AiChatMessage
            {
                Role = msg.Role!,
                Content = msg.Content,
            });
        }

        if (history.Count > 4 && !currentContent.IsNullOrEmpty())
        {
            messages.Add(new AiChatMessage
            {
                Role = "system",
                Content = $"请直接针对用户最新的问题进行回答：{currentContent}",
            });
        }

        return messages;
    }

    /// <summary>构建系统提示词消息。合并用户全局级和模型级系统提示词（技能提示词由管道注入）</summary>
    /// <param name="userId">当前用户编号</param>
    /// <param name="modelConfig">模型配置（可选）</param>
    /// <param name="historyCount">当前上下文中历史消息条数，大于 0 时才注入多轮优先级提示</param>
    /// <returns>系统消息，无提示词时返回 null</returns>
    private AiChatMessage? BuildSystemMessage(Int32 userId, ModelConfig? modelConfig, Int32 historyCount = 0)
    {
        using var span = tracer?.NewSpan(nameof(BuildSystemMessage), new { userId });
        var parts = new List<String>();

        // 0. 当前用户基础信息
        if (userId > 0 && XCode.Membership.ManageProvider.Provider?.FindByID(userId) is XCode.Membership.IUser user)
        {
            var sb = Pool.StringBuilder.Get();
            sb.Append($"当前用户：{user.DisplayName}（{user.Name}）");
            var roles = user.Roles;
            if (roles?.Length > 0) sb.Append($"，角色：{roles.Join(",")}");
            if (user.DepartmentID > 0)
            {
                var dept = XCode.Membership.Department.FindByID(user.DepartmentID);
                if (dept != null) sb.Append($"，部门：{dept.Name}");
            }
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
        if (modelConfig != null && !String.IsNullOrWhiteSpace(modelConfig.SystemPrompt))
            parts.Add(modelConfig.SystemPrompt.Trim());

        // 4. 多轮对话时强调最新消息优先级
        if (historyCount > 0)
            parts.Add("请优先回应用户的最新消息。如果最新消息与之前的对话内容存在矛盾或方向变化，以最新消息为准。");

        if (parts.Count == 0) return null;
        span?.Value = parts.Count;

        return new AiChatMessage
        {
            Role = "system",
            Content = String.Join("\n\n", parts),
        };
    }

    /// <summary>枚举管道流，收集 content/thinking/usage，将可透传事件实时 yield，并在循环结束后写入收集结果</summary>
    /// <param name="source">管道事件流</param>
    /// <param name="contentBuilder">正文内容收集器</param>
    /// <param name="thinkingBuilder">思考内容收集器</param>
    /// <param name="toolCallsCollector">工具调用收集器（可为 null）</param>
    /// <param name="setFinalUsage">用量回调：收到 message_done 事件时调用</param>
    /// <param name="setErrorState">错误回调：发生异常时调用，传入 (hasError, deferredErrorEvent)</param>
    /// <param name="errorLogPrefix">错误日志前缀</param>
    /// <param name="cancellationToken">取消令牌</param>
    private async IAsyncEnumerable<ChatStreamEvent> DrainPipelineAsync(
        IAsyncEnumerable<ChatStreamEvent> source,
        StringBuilder contentBuilder,
        StringBuilder thinkingBuilder,
        List<ToolCallDto>? toolCallsCollector,
        Action<UsageDetails?> setFinalUsage,
        Action<Boolean, ChatStreamEvent?> setErrorState,
        String errorLogPrefix,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ISpan? toolSpan = null;

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
                    log?.Error("{0}: {1}", errorLogPrefix, ex.Message);
                    setErrorState(true, ChatStreamEvent.ErrorEvent("STREAM_ERROR", ex.Message));
                    break;
                }

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
                        setFinalUsage(ev.Usage);
                        break;
                    case "error":
                        setErrorState(true, ev);
                        break;
                    case "tool_call_start" when toolCallsCollector != null:
                        toolSpan = tracer?.NewSpan($"ai:ToolCall:{ev.Name}");
                        toolCallsCollector.Add(new ToolCallDto(ev.ToolCallId + "", ev.Name + "", ToolCallStatus.Calling, ev.Arguments));
                        yield return ev;
                        break;
                    case "tool_call_done" when toolCallsCollector != null:
                        toolSpan?.AppendTag(ev.Result!);
                        toolSpan?.Dispose();
                        toolSpan = null;
                        UpdateToolCallStatus(toolCallsCollector, ev.ToolCallId, ToolCallStatus.Done, ev.Result);
                        yield return ev;
                        break;
                    case "tool_call_error" when toolCallsCollector != null:
                        toolSpan?.SetError(new Exception(ev.Error));
                        toolSpan?.Dispose();
                        toolSpan = null;
                        UpdateToolCallStatus(toolCallsCollector, ev.ToolCallId, ToolCallStatus.Error, ev.Error);
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
    }

    /// <summary>更新工具调用列表中指定 id 的状态与结果</summary>
    /// <param name="collector">工具调用收集器</param>
    /// <param name="toolCallId">工具调用编号</param>
    /// <param name="status">新状态</param>
    /// <param name="value">结果或错误信息</param>
    private static void UpdateToolCallStatus(List<ToolCallDto> collector, String? toolCallId, ToolCallStatus status, String? value)
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

    /// <summary>将用量统计写入 AI 回复消息实体（不保存，调用方负责 Update）</summary>
    /// <param name="msg">消息实体</param>
    /// <param name="usage">用量统计</param>
    /// <param name="hasError">是否有错误</param>
    /// <param name="errorDetail">错误详情</param>
    private static void ApplyUsageToMessage(ChatMessage msg, UsageDetails? usage, Boolean hasError, String? errorDetail = null)
    {
        if (msg.Content.IsNullOrEmpty())
        {
            if (hasError)
                msg.Content = errorDetail.IsNullOrEmpty() ? "[生成失败]" : $"[生成失败] {errorDetail}";
            else
                msg.Content = "[已中断]";
        }
        else if (hasError && !errorDetail.IsNullOrEmpty())
        {
            msg.Content += $"\n\n[错误] {errorDetail}";
        }
        if (usage != null)
        {
            msg.InputTokens = usage.InputTokens;
            msg.OutputTokens = usage.OutputTokens;
            msg.TotalTokens = usage.TotalTokens;
            msg.ElapsedMs = usage.ElapsedMs;
        }
    }

    /// <summary>将请求参数写入 AI 回复消息实体（不保存，调用方负责 Update）</summary>
    /// <param name="msg">消息实体</param>
    /// <param name="modelConfig">模型配置</param>
    /// <param name="context">管道上下文（携带 MaxTokens/Temperature/FinishReason）</param>
    private static void ApplyRequestParams(ChatMessage msg, ModelConfig modelConfig, ChatPipelineContext context)
    {
        msg.ModelName = modelConfig.Code;
        if (context.MaxTokens > 0) msg.MaxTokens = context.MaxTokens;
        if (context.Temperature != null) msg.Temperature = context.Temperature.Value;
        if (!context.FinishReason.IsNullOrEmpty()) msg.FinishReason = context.FinishReason;
    }

    /// <summary>将用量统计累加到会话实体并更新最后消息时间（不保存，调用方负责 Update）</summary>
    /// <param name="conversation">会话实体</param>
    /// <param name="conversationId">会话编号</param>
    /// <param name="usage">用量统计</param>
    private static void ApplyUsageToConversation(Conversation conversation, Int64 conversationId, UsageDetails? usage)
    {
        conversation.LastMessageTime = DateTime.Now;
        conversation.MessageCount = (Int32)ChatMessage.FindCount(ChatMessage._.ConversationId == conversationId);
        if (usage != null)
        {
            conversation.InputTokens += usage.InputTokens;
            conversation.OutputTokens += usage.OutputTokens;
            conversation.TotalTokens += usage.TotalTokens;
            conversation.ElapsedMs += usage.ElapsedMs;
        }
    }

    /// <summary>根据流式速度等级（1~5）返回缓存回放时的分块参数</summary>
    /// <param name="speed">速度等级，1=慢，3=默认，5=快</param>
    /// <returns>(每块字符数, 块间延迟毫秒数)</returns>
    private static (Int32 ChunkSize, Int32 DelayMs) GetCachedStreamingParams(Int32 speed) => speed switch
    {
        1 => (4, 60),
        2 => (6, 30),
        4 => (14, 16),
        5 => (24, 10),
        _ => (10, 20),
    };

    /// <summary>将文本按指定块大小拆分后逐块延迟输出，模拟逐 token 打字机效果</summary>
    /// <param name="text">待输出文本</param>
    /// <param name="chunkSize">每块字符数</param>
    /// <param name="delayMs">块间延迟毫秒数</param>
    /// <param name="cancellationToken">取消令牌</param>
    private static async IAsyncEnumerable<String> ThrottleTextAsync(String text, Int32 chunkSize, Int32 delayMs, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (text.IsNullOrEmpty()) yield break;

        var enumerator = StringInfo.GetTextElementEnumerator(text);
        var buf = new StringBuilder(chunkSize * 4);
        var count = 0;
        while (enumerator.MoveNext())
        {
            buf.Append(enumerator.GetTextElement());
            count++;
            if (count >= chunkSize)
            {
                yield return buf.ToString();
                buf.Clear();
                count = 0;
                await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
            }
        }
        if (buf.Length > 0) yield return buf.ToString();
    }

    /// <summary>将用户消息的附件与文本内容组合为多模态消息。图片读取文件字节后以 base64 data URI 传给 LLM；非图片附件暂忽略</summary>
    /// <param name="attachmentsJson">附件ID列表 JSON（Int64/String 数组）</param>
    /// <param name="textContent">文本内容</param>
    /// <returns>多模态 AiChatMessage，无有效附件时退化为纯文本消息</returns>
    internal static AiChatMessage BuildMultimodalUserMessage(String attachmentsJson, String? textContent)
    {
        var contents = new List<AIContent>();
        var docParts = new List<String>();

        // 前端发送的 attachmentIds 为字符串数组 ["123","456"]，兼容 Int64 数组 [123,456]
        var ids = ParseAttachmentIds(attachmentsJson);
        if (ids != null)
        {
            foreach (var id in ids)
            {
                try
                {
                    var att = Attachment.FindById(id);
                    if (att == null || !att.Enable) continue;

                    var filePath = att.GetFilePath();
                    if (filePath.IsNullOrEmpty() || !File.Exists(filePath)) continue;

                    if (!att.ContentType.IsNullOrEmpty() && att.ContentType.StartsWithIgnoreCase("image/"))
                        contents.Add(new ImageContent { Data = File.ReadAllBytes(filePath), MediaType = att.ContentType });
                    else
                    {
                        // 非图片文件：用 NewLife.Office 提取文本，作为上下文注入消息
                        var docText = ExtractDocumentAsMarkdown(filePath!, att.FileName);
                        if (!docText.IsNullOrEmpty())
                            docParts.Add($"【附件：{att.FileName}】\n{docText}");
                    }
                }
                catch (Exception ex)
                {
                    XTrace.WriteException(ex);
                }
            }
        }

        // 将文档内容前置注入到用户文本
        if (docParts.Count > 0)
        {
            var docContext = String.Join("\n\n---\n\n", docParts);
            textContent = docContext + (textContent.IsNullOrEmpty() ? String.Empty : $"\n\n---\n\n{textContent}");
        }

        if (!textContent.IsNullOrEmpty())
            contents.Add(new TextContent(textContent));

        // 无图片附件时退化为纯文本
        if (contents.Count == 0 || (contents.Count == 1 && contents[0] is TextContent))
            return new AiChatMessage { Role = "user", Content = textContent };

        return new AiChatMessage { Role = "user", Contents = contents };
    }

    /// <summary>使用 NewLife.Office 将文档文件提取为 Markdown 文本。支持 docx/doc/pdf/xlsx/xls/pptx/ppt/txt/csv/md</summary>
    /// <param name="filePath">文件在磁盘上的完整路径</param>
    /// <param name="fileName">原始文件名（用于按扩展名路由及错误提示）</param>
    /// <returns>提取的 markdown 文本，无法识别格式时返回 null</returns>
    internal static String? ExtractDocumentAsMarkdown(String filePath, String? fileName)
    {
        var ext = Path.GetExtension(fileName ?? filePath).ToLowerInvariant();
        try
        {
            switch (ext)
            {
                case ".docx":
                case ".doc":
                    {
                        using var reader = new NewLife.Office.WordReader(filePath);
                        var sb = Pool.StringBuilder.Get();
                        foreach (var para in reader.ReadParagraphs())
                        {
                            sb.AppendLine(para);
                        }
                        // 将表格格式化为 markdown
                        foreach (var table in reader.ReadTables())
                        {
                            if (table.Length == 0) continue;
                            sb.AppendLine();
                            foreach (var row in table)
                            {
                                sb.Append("| ");
                                sb.Append(String.Join(" | ", row.Select(c => (c ?? String.Empty).Replace("|", "\\|"))));
                                sb.AppendLine(" |");
                            }
                            sb.AppendLine();
                        }
                        return sb.Return(true);
                    }
                case ".pdf":
                    {
                        using var reader = new NewLife.Office.PdfReader(filePath);
                        return reader.ExtractText();
                    }
                case ".xlsx":
                case ".xls":
                    {
                        using var reader = new NewLife.Office.ExcelReader(filePath);
                        var sb = Pool.StringBuilder.Get();
                        var sheets = reader.Sheets;
                        if (sheets != null)
                        {
                            foreach (var sheet in sheets)
                            {
                                sb.AppendLine($"## {sheet}");
                                sb.AppendLine();
                                var rows = reader.ReadRows(sheet).ToList();
                                for (var i = 0; i < rows.Count; i++)
                                {
                                    var row = rows[i];
                                    var cells = row.Select(c => Convert.ToString(c) ?? String.Empty);
                                    sb.Append("| ");
                                    sb.Append(String.Join(" | ", cells.Select(c => c.Replace("|", "\\|"))));
                                    sb.AppendLine(" |");
                                    // 首行后插入分隔线
                                    if (i == 0)
                                    {
                                        sb.Append("| ");
                                        sb.Append(String.Join(" | ", row.Select(_ => "---")));
                                        sb.AppendLine(" |");
                                    }
                                }
                                sb.AppendLine();
                            }
                        }
                        return sb.Return(true);
                    }
                case ".pptx":
                case ".ppt":
                    {
                        using var reader = new NewLife.Office.PptxReader(filePath);
                        return reader.ReadAllText();
                    }
                case ".txt":
                case ".csv":
                case ".md":
                    return File.ReadAllText(filePath, System.Text.Encoding.UTF8);
                default:
                    return null;
            }
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
            return null;
        }
    }

    /// <summary>解析附件ID列表 JSON。兼容字符串数组和整数数组两种格式</summary>
    /// <param name="json">附件ID列表 JSON</param>
    /// <returns>ID 列表，解析失败返回 null</returns>
    private static IList<Int64>? ParseAttachmentIds(String json)
    {
        // 优先尝试 Int64 数组
        var ids = json.ToJsonEntity<List<Int64>>();
        if (ids != null && ids.Count > 0 && ids[0] != 0) return ids;

        // 前端 attachmentIds.map(String) 产生字符串数组 ["123","456"]
        var strIds = json.ToJsonEntity<List<String>>();
        if (strIds != null && strIds.Count > 0)
            return strIds.Select(s => s.ToLong()).Where(v => v > 0).ToList();

        return null;
    }

    /// <summary>转换消息实体为DTO（内部辅助，供回调闭包使用）</summary>
    /// <param name="entity">消息实体</param>
    /// <returns></returns>
    private static MessageDto ToMessageDto(ChatMessage entity)
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
