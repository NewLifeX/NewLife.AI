using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using NewLife.AI.Clients;
using NewLife.AI.Models;
using NewLife.AI.Services;
using NewLife.Collections;
using NewLife.Cube.Entity;
using NewLife.Data;
using NewLife.Log;
using NewLife.Serialization;
using NewLife.ChatAI.Entity;
using NewLife.ChatAI.Models;
using XCode;
using XCode.Membership;
using AiChatMessage = NewLife.AI.Models.ChatMessage;
using AiFunctionCall = NewLife.AI.Models.FunctionCall;
using AiToolCall = NewLife.AI.Models.ToolCall;
using ChatMessage = NewLife.ChatAI.Entity.ChatMessage;
using ChatStreamEvent = NewLife.AI.Models.ChatStreamEvent;
using ILog = NewLife.Log.ILog;
using UsageDetails = NewLife.AI.Models.UsageDetails;

namespace NewLife.ChatAI.Services;

/// <summary>数据库版对话应用服务。基于 XCode 实体类持久化数据</summary>
/// <remarks>
/// 对话内核层：负责会话与消息的持久化管理、上下文构建以及流式编排骨架。
/// 能力扩展层（工具调用、技能注入）与知识进化层（记忆、自学习、事件智能体）均通过 <see cref="IChatPipeline"/>
/// 在外部装配后注入，本类对其实现细节完全透明。
/// </remarks>
/// <param name="pipeline">已装配好三层能力的对话执行管道</param>
/// <param name="gatewayService">网关服务（用于模型解析）</param>
/// <param name="backgroundService">后台生成服务</param>
/// <param name="usageService">用量统计服务</param>
/// <param name="tracer">追踪器</param>
/// <param name="log">日志</param>
public class ChatApplicationService(IChatPipeline pipeline, GatewayService gatewayService, BackgroundGenerationService? backgroundService, UsageService? usageService, ITracer tracer, ILog log)
{
    #region 属性
    #endregion

    #region 会话管理
    /// <summary>新建会话</summary>
    /// <param name="request">新建会话请求</param>
    /// <param name="user">当前用户</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public Task<ConversationSummaryDto> CreateConversationAsync(CreateConversationRequest request, IUser user, CancellationToken cancellationToken)
    {
        var title = String.IsNullOrWhiteSpace(request.Title) ? "新建对话" : request.Title.Trim();

        var entity = new Conversation
        {
            UserId = user.ID,
            UserName = user + "",
            Title = title,
            ModelId = request.ModelId,
            ModelName = request.ModelId > 0 ? ModelConfig.FindById(request.ModelId)?.Name : null,
            Source = "Web",
            LastMessageTime = DateTime.Now,
        };
        entity.Insert();

        var dto = ToConversationSummary(entity);
        return Task.FromResult(dto);
    }

    /// <summary>获取会话列表（分页）</summary>
    /// <param name="userId">当前用户编号</param>
    /// <param name="keyword">标题关键字</param>
    /// <param name="page">分页参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public Task<PagedResultDto<ConversationSummaryDto>> GetConversationsAsync(Int32 userId, String? keyword, PageParameter page, CancellationToken cancellationToken)
    {
        var list = Conversation.Search(userId, keyword, page);
        var items = list.Select(ToConversationSummary).ToList();

        return Task.FromResult(new PagedResultDto<ConversationSummaryDto>(items, (Int32)page.TotalCount, page.PageIndex, page.PageSize));
    }

    /// <summary>更新会话（重命名、切换模型等）</summary>
    /// <param name="conversationId">会话编号</param>
    /// <param name="request">更新请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public Task<ConversationSummaryDto?> UpdateConversationAsync(Int64 conversationId, UpdateConversationRequest request, CancellationToken cancellationToken)
    {
        var entity = Conversation.FindById(conversationId);
        if (entity == null) return Task.FromResult<ConversationSummaryDto?>(null);

        if (!String.IsNullOrWhiteSpace(request.Title))
            entity.Title = request.Title.Trim();
        if (request.ModelId > 0)
        {
            entity.ModelId = request.ModelId;
            entity.ModelName = ModelConfig.FindById(request.ModelId)?.Name;
        }

        entity.Update();

        return Task.FromResult<ConversationSummaryDto?>(ToConversationSummary(entity));
    }

    /// <summary>删除会话</summary>
    /// <param name="conversationId">会话编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public Task<Boolean> DeleteConversationAsync(Int64 conversationId, CancellationToken cancellationToken)
    {
        var entity = Conversation.FindById(conversationId);
        if (entity == null) return Task.FromResult(false);

        using var trans = ChatMessage.Meta.CreateTrans();

        // 获取关联的消息 ID 列表，用于清理消息反馈
        var messages = ChatMessage.FindAllByConversationId(conversationId);
        var messageIds = messages.Select(m => m.Id).ToArray();

        // 删除关联的消息反馈
        if (messageIds.Length > 0)
        {
            var feedbacks = MessageFeedback.FindAllByMessageIds(messageIds);
            feedbacks.Delete();
        }

        // 删除关联的用量记录
        var usageRecords = UsageRecord.FindAllByConversationId(conversationId);
        usageRecords.Delete();

        // 删除关联的消息
        messages.Delete();

        // 删除关联的共享
        var shares = SharedConversation.FindAllByConversationId(conversationId);
        shares.Delete();

        entity.Delete();

        trans.Commit();

        return Task.FromResult(true);
    }

    /// <summary>置顶/取消置顶</summary>
    /// <param name="conversationId">会话编号</param>
    /// <param name="isPinned">是否置顶</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public Task<Boolean> SetPinAsync(Int64 conversationId, Boolean isPinned, CancellationToken cancellationToken)
    {
        var entity = Conversation.FindById(conversationId);
        if (entity == null) return Task.FromResult(false);

        entity.IsPinned = isPinned;
        entity.Update();

        return Task.FromResult(true);
    }
    #endregion

    #region 消息管理
    /// <summary>获取会话消息列表。批量查询反馈信息，避免 N+1 查询</summary>
    /// <param name="conversationId">会话编号</param>
    /// <param name="userId">当前用户编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public Task<IReadOnlyList<MessageDto>> GetMessagesAsync(Int64 conversationId, Int32 userId, CancellationToken cancellationToken)
    {
        //var p = new PageParameter { PageSize = 0, Sort = ChatMessage._.CreateTime.Asc() };
        //var list = ChatMessage.Search(conversationId, default, DateTime.MinValue, DateTime.MinValue, null, p);
        var list = ChatMessage.FindAllByConversationIdOrdered(conversationId);

        // 批量查询反馈，避免 N+1
        var messageIds = list.Select(e => e.Id).ToList();
        var feedbacks = messageIds.Count > 0
            ? MessageFeedback.FindAllByMessageIdsAndUserId(messageIds, userId)
                .ToDictionary(e => e.MessageId, e => e.FeedbackType)
            : [];

        var items = list.Select(e => ToMessageDto(e, feedbacks.TryGetValue(e.Id, out var ft) ? ft : default)).ToList();
        return Task.FromResult<IReadOnlyList<MessageDto>>(items);
    }

    /// <summary>全文搜索消息内容。在当前用户的所有会话中按关键词搜索消息</summary>
    /// <param name="userId">当前用户编号</param>
    /// <param name="keyword">搜索关键词</param>
    /// <param name="page">页码</param>
    /// <param name="pageSize">每页数量</param>
    /// <returns></returns>
    public PagedResultDto<MessageSearchResultDto> SearchMessages(Int32 userId, String keyword, Int32 page, Int32 pageSize)
    {
        if (page <= 0) page = 1;
        if (pageSize <= 0) pageSize = 20;

        // 先获取用户所有会话编号
        var convIds = Conversation.FindIdsByUserId(userId);
        if (convIds.Length == 0)
            return new PagedResultDto<MessageSearchResultDto>([], 0, page, pageSize);

        var p = new PageParameter { PageIndex = page, PageSize = pageSize };
        var list = ChatMessage.Search(convIds, keyword, p);

        var msgItems = list.Select(e => new MessageSearchResultDto
        {
            Id = e.Id,
            ConversationId = e.ConversationId,
            ConversationTitle = e.ConversationTitle ?? "",
            Role = e.Role ?? "user",
            Content = e.Content ?? "",
            CreateTime = e.CreateTime,
        }).ToList();

        return new PagedResultDto<MessageSearchResultDto>(msgItems, (Int32)p.TotalCount, page, pageSize);
    }

    /// <summary>编辑消息内容（仅修改文字，不重新生成）</summary>
    /// <param name="messageId">消息编号</param>
    /// <param name="request">编辑请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>更新后的消息 DTO，消息不存在时返回 null</returns>
    public Task<MessageDto?> EditMessageAsync(Int64 messageId, EditMessageRequest request, CancellationToken cancellationToken)
    {
        var entity = ChatMessage.FindById(messageId);
        if (entity == null) return Task.FromResult<MessageDto?>(null);

        entity.Content = request.Content;
        entity.Update();

        return Task.FromResult<MessageDto?>(ToMessageDto(entity));
    }

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
        var beforeMessages = ChatMessage.FindAllBeforeId(entity.ConversationId, entity.Id);

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
            var pipelineCtx = new ChatPipelineContext { UserId = userId + "", ConversationId = entity.ConversationId + "", SkillId = conversation.SkillId };
            var regenSw = Stopwatch.StartNew();
            var response = await pipeline.CompleteAsync(contextMessages, modelConfig, pipelineCtx, cancellationToken).ConfigureAwait(false);
            regenSw.Stop();
            var regenElapsed = (Int32)regenSw.ElapsedMilliseconds;

            var newContent = response.Messages?.FirstOrDefault()?.Message?.Content as String ?? String.Empty;
            var reasoning = response.Messages?.FirstOrDefault()?.Message?.ReasoningContent;

            entity.Content = newContent;
            if (!String.IsNullOrEmpty(reasoning))
                entity.ThinkingContent = reasoning;
            entity.ElapsedMs = regenElapsed;
            entity.Update();

            // 写入用量记录，并累计会话 Token
            if (response.Usage != null)
            {
                usageService?.Record(userId, 0, entity.ConversationId, entity.Id,
                    modelConfig.Id, response.Usage.InputTokens, response.Usage.OutputTokens, response.Usage.TotalTokens, "Chat");
                var conv = Conversation.FindById(entity.ConversationId);
                if (conv != null)
                {
                    conv.InputTokens += response.Usage.InputTokens;
                    conv.OutputTokens += response.Usage.OutputTokens;
                    conv.TotalTokens += response.Usage.TotalTokens;
                    conv.ElapsedMs += regenElapsed;
                    conv.Update();
                }
            }

            return ToMessageDto(entity);
        }
        catch (Exception ex)
        {
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
        var editPipelineCtx = new ChatPipelineContext { UserId = userId + "", ConversationId = entity.ConversationId + "", SkillId = conversation.SkillId };
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
            usageService?.Record(userId, 0, entity.ConversationId, assistantMsg.Id, modelConfig.Id, finalUsage.InputTokens, finalUsage.OutputTokens, finalUsage.TotalTokens, "Chat");

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

        var regenPipelineCtx = new ChatPipelineContext { UserId = userId + "", ConversationId = entity.ConversationId + "", SkillId = conversation.SkillId };
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
            usageService?.Record(userId, 0, entity.ConversationId, entity.Id, modelConfig.Id, finalUsage.InputTokens, finalUsage.OutputTokens, finalUsage.TotalTokens, "Chat");

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

        // 更新会话绑定的模型（首次发消息或 model_id=0 自动选模型时持久化实际使用的模型）
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

        var msgPipelineCtx = new ChatPipelineContext { UserId = userId + "", ConversationId = conversationId + "", SkillId = skillId };
        if (request.Options != null) msgPipelineCtx.Items = request.Options;

        // 预处理：注入技能提示词、解析@引用，生成 SystemPrompt
        pipeline.PrepareContext(contextMessages, msgPipelineCtx);

        // 持久化 system 消息（仅保存注入的技能提示词，便于调试分析）
        if (!msgPipelineCtx.SystemPrompt.IsNullOrEmpty())
        {
            var systemMsg = new ChatMessage
            {
                ConversationId = conversationId,
                Role = "system",
                Content = msgPipelineCtx.SystemPrompt,
            };
            systemMsg.Insert();
        }

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
            var toolNamesStr = String.Join(",", msgPipelineCtx.AvailableToolNames);
            userMsg.ToolNames = toolNamesStr;
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
            usageService?.Record(userId, 0, conversationId, assistantMsg.Id, modelConfig.Id, finalUsage.InputTokens, finalUsage.OutputTokens, finalUsage.TotalTokens, "Chat");

        // 推荐问题缓存回写：正常完成且有内容时，将结果写入匹配的推荐问题，供下次直接命中
        if (!hasError && ChatSetting.Current.EnableSuggestedQuestionCache && contentBuilder.Length > 0)
            TryWriteBackSuggestedQuestionCache(request.Content, contentBuilder.ToString(), thinkingBuilder.Length > 0 ? thinkingBuilder.ToString() : null, modelConfig.Id);

        // message_done 立即发出，不阻塞等待标题生成
        if (!hasError && !cancellationToken.IsCancellationRequested)
        {
            yield return new ChatStreamEvent { Type = "message_done", MessageId = assistantMsg.Id, Usage = finalUsage, };
        }
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
            // 已有部分内容但最终出错，追加错误信息
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

    /// <summary>中断生成。停止后台正在运行的流式生成任务</summary>
    /// <param name="messageId">消息编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public Task StopGenerateAsync(Int64 messageId, CancellationToken cancellationToken)
    {
        backgroundService?.Stop(messageId);
        return Task.CompletedTask;
    }

    /// <summary>异步生成会话标题。根据用户首条消息内容，调用模型生成简短标题</summary>
    /// <param name="conversationId">会话编号</param>
    /// <param name="userMessage">用户首条消息内容</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public async Task<String?> GenerateTitleAsync(Int64 conversationId, String userMessage, CancellationToken cancellationToken)
    {
        var conversation = Conversation.FindById(conversationId);
        if (conversation == null) return null;

        var setting = ChatSetting.Current;
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
                    // 标题生成超时取普通请求超时的 3 倍，避免网络波动误杀
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
                        // 清理标题：去除引号和多余空白
                        title = title.Trim().Trim('"', '"', '"', '\'', '「', '」');
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

    #region 反馈
    /// <summary>提交点赞/点踩反馈</summary>
    /// <param name="messageId">消息编号</param>
    /// <param name="request">反馈请求</param>
    /// <param name="userId">当前用户编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public Task SubmitFeedbackAsync(Int64 messageId, FeedbackRequest request, Int32 userId, CancellationToken cancellationToken)
    {
        var msg = ChatMessage.FindById(messageId);
        var entity = MessageFeedback.FindByMessageIdAndUserId(messageId, userId);
        entity ??= new MessageFeedback
        {
            MessageId = messageId,
            UserId = userId,
        };
        if (msg != null) entity.ConversationId = msg.ConversationId;

        entity.FeedbackType = request.Type;
        entity.Reason = request.Reason;
        entity.AllowTraining = request.AllowTraining ?? false;
        entity.Save();

        return Task.CompletedTask;
    }

    /// <summary>取消反馈</summary>
    /// <param name="messageId">消息编号</param>
    /// <param name="userId">当前用户编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public Task DeleteFeedbackAsync(Int64 messageId, Int32 userId, CancellationToken cancellationToken)
    {
        var entity = MessageFeedback.FindByMessageIdAndUserId(messageId, userId);
        entity?.Delete();

        return Task.CompletedTask;
    }
    #endregion

    #region 分享
    /// <summary>创建共享链接。按当前消息进度生成快照，支持设置有效期</summary>
    /// <param name="conversationId">会话编号</param>
    /// <param name="request">创建分享请求</param>
    /// <param name="user">当前操作用户</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>包含分享 URL 的 DTO</returns>
    public Task<ShareLinkDto> CreateShareLinkAsync(Int64 conversationId, CreateShareRequest request, IUser user, CancellationToken cancellationToken)
    {
        var conversation = Conversation.FindById(conversationId);

        // 获取当前最后一条消息的编号作为快照截止点
        var snapshotMessageId = ChatMessage.FindLastByConversationId(conversationId)?.Id ?? 0;

        DateTime? expireTime = null;
        if (request.ExpireHours is > 0)
            expireTime = DateTime.Now.AddHours(request.ExpireHours.Value);

        var entity = new SharedConversation
        {
            ConversationId = conversationId,
            ShareToken = Guid.NewGuid().ToString("N"),
            SnapshotTitle = conversation?.Title,
            SnapshotMessageId = snapshotMessageId,
            ExpireTime = expireTime ?? DateTime.MinValue,
            CreateUserID = user.ID,
            CreateUser = user.DisplayName ?? user.Name,
        };
        entity.Insert();

        var dto = new ShareLinkDto($"/share/{entity.ShareToken}", entity.CreateTime, expireTime);
        return Task.FromResult(dto);
    }

    /// <summary>获取共享对话内容</summary>
    /// <param name="token">分享令牌</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public Task<Object?> GetShareContentAsync(String token, CancellationToken cancellationToken)
    {
        var share = SharedConversation.FindByShareToken(token);
        if (share == null) return Task.FromResult<Object?>(null);

        // 检查是否已过期
        if (share.ExpireTime > DateTime.MinValue && share.ExpireTime < DateTime.Now)
            return Task.FromResult<Object?>(null);

        // 获取快照范围内的消息
        var messages = ChatMessage.FindByShareSnapshot(share.ConversationId, share.SnapshotMessageId);
        var items = messages.Select(m => ToMessageDto(m)).ToList();

        var result = new
        {
            ConversationId = share.ConversationId.ToString(),
            Messages = items,
            share.CreateTime,
            ExpireTime = share.ExpireTime > DateTime.MinValue ? (DateTime?)share.ExpireTime : null,
        };
        return Task.FromResult<Object?>(result);
    }

    /// <summary>撤销共享链接</summary>
    /// <param name="token">分享令牌</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public Task<Boolean> RevokeShareLinkAsync(String token, CancellationToken cancellationToken)
    {
        var share = SharedConversation.FindByShareToken(token);
        if (share == null) return Task.FromResult(false);

        share.Delete();
        return Task.FromResult(true);
    }
    #endregion

    #region 模型
    /// <summary>获取可用模型列表</summary>
    /// <param name="roleIds">角色组</param>
    /// <param name="departmentId">部门编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public Task<ModelInfoDto[]> GetModelsAsync(Int32[] roleIds, Int32 departmentId, CancellationToken cancellationToken)
    {
        var list = ModelConfig.FindAllByPermission(roleIds, departmentId);

        // 若数据库无模型配置，返回默认列表
        if (list.Count == 0)
        {
            return Task.FromResult(new[]
            {
                new ModelInfoDto(0, "qwen-max", "Qwen-Max", true, true, false, true),
                new ModelInfoDto(0, "deepseek-r1", "DeepSeek-R1", true, false, false, true),
                new ModelInfoDto(0, "gpt-4o", "GPT-4o", true, true, false, true),
            });
        }

        var models = list.Select(e => new ModelInfoDto(e.Id, e.Code ?? String.Empty, e.Name ?? String.Empty, e.SupportThinking, e.SupportVision, e.SupportImageGeneration, e.SupportFunctionCalling, e.ProviderInfo?.Name ?? "")).ToArray();
        return Task.FromResult(models);
    }
    #endregion

    #region 用户设置
    /// <summary>获取用户设置</summary>
    /// <param name="userId">当前用户编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public Task<UserSettingsDto> GetUserSettingsAsync(Int32 userId, CancellationToken cancellationToken)
    {
        var entity = UserSetting.FindByUserId(userId);
        if (entity == null)
        {
            // 返回默认设置
            return Task.FromResult(new UserSettingsDto("zh-CN", "system", 16, "Enter", 0, ThinkingMode.Auto, 10, String.Empty, String.Empty, ResponseStyle.Balanced, String.Empty, false)
            {
                EnableLearning = true,
            });
        }

        return Task.FromResult(ToUserSettingsDto(entity));
    }

    /// <summary>更新用户设置</summary>
    /// <param name="settings">用户设置</param>
    /// <param name="userId">当前用户编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public Task<UserSettingsDto> UpdateUserSettingsAsync(UserSettingsDto settings, Int32 userId, CancellationToken cancellationToken)
    {
        var entity = UserSetting.FindByUserId(userId);
        if (entity == null)
        {
            entity = new UserSetting { UserId = userId };
        }

        entity.Language = settings.Language;
        entity.Theme = settings.Theme;
        entity.FontSize = settings.FontSize;
        entity.SendShortcut = settings.SendShortcut;
        entity.DefaultModel = settings.DefaultModel;
        entity.DefaultThinkingMode = settings.DefaultThinkingMode;
        entity.ContextRounds = settings.ContextRounds;
        entity.Nickname = settings.Nickname;
        entity.UserBackground = settings.UserBackground;
        entity.ResponseStyle = settings.ResponseStyle;
        entity.SystemPrompt = settings.SystemPrompt;
        entity.AllowTraining = settings.AllowTraining;
        entity.McpEnabled = settings.McpEnabled;
        entity.ShowToolCalls = settings.ShowToolCalls;
        entity.DefaultSkill = settings.DefaultSkill;
        entity.EnableLearning = settings.EnableLearning;
        entity.LearningModel = settings.LearningModel;
        entity.MemoryInjectNum = settings.MemoryInjectNum;
        entity.ContentWidth = settings.ContentWidth;
        entity.Save();

        return Task.FromResult(ToUserSettingsDto(entity));
    }

    /// <summary>导出所有对话数据</summary>
    /// <param name="userId">当前用户编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public Task<Stream> ExportUserDataAsync(Int32 userId, CancellationToken cancellationToken)
    {
        var conversations = Conversation.FindAllByUserId(userId);

        var result = new List<Object>();

        foreach (var conv in conversations)
        {
            var messages = ChatMessage.FindAllByConversationIdOrdered(conv.Id);
            result.Add(new
            {
                conv.Id,
                conv.Title,
                conv.ModelId,
                conv.IsPinned,
                conv.LastMessageTime,
                conv.CreateTime,
                Messages = messages.Select(m => new
                {
                    m.Id,
                    m.Role,
                    m.Content,
                    m.ThinkingContent,
                    m.ThinkingMode,
                    m.Attachments,
                    m.CreateTime,
                }).ToList(),
            });
        }

        var json = result.ToJson();
        Stream stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return Task.FromResult(stream);
    }

    /// <summary>清除所有对话</summary>
    /// <param name="userId">当前用户编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public Task ClearUserConversationsAsync(Int32 userId, CancellationToken cancellationToken)
    {
        var conversations = Conversation.FindAllByUserId(userId);
        if (conversations.Count == 0) return Task.CompletedTask;

        var convIds = conversations.Select(e => e.Id).ToArray();

        // 按会话维度级联删除，避免误删其他用户数据
        // 删除关联的共享
        var shares = SharedConversation.FindAllByConversationIds(convIds);
        shares.Delete();

        // 获取关联的消息 ID
        var messages = ChatMessage.FindAllByConversationIds(convIds);
        var msgIds = messages.Select(e => e.Id).ToArray();

        // 删除消息反馈
        if (msgIds.Length > 0)
        {
            var feedbacks = MessageFeedback.FindAllByMessageIds(msgIds);
            feedbacks.Delete();
        }

        // 删除用量记录
        var usageRecords = UsageRecord.FindAllByConversationIds(convIds);
        usageRecords.Delete();

        // 删除消息
        messages.Delete();

        // 删除会话
        conversations.Delete();

        return Task.CompletedTask;
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

        // 读取系统全局流式速度设置，用于分块限速输出，模拟逐 token 打字机效果
        // 速度 > 5 时跳过节流，直接一次性输出全部内容
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

        // 查询历史消息，按时间倒序取最近 N 轮（每轮 = 1条user + 1条assistant = 2条）
        //var p = new PageParameter { PageSize = maxRounds * 2, Sort = ChatMessage._.CreateTime.Desc() };
        //var history = ChatMessage.Search(conversationId, default, DateTime.MinValue, DateTime.MinValue, null, p);
        var history = ChatMessage.FindAllByConversationIdDesc(conversationId, maxRounds * 2);
        history.Reverse();

        var messages = new List<AiChatMessage>();

        // 注入系统提示词（用户全局级 + 模型级；技能提示词由管道的 SkillFilter 在执行时注入）
        var systemMsg = BuildSystemMessage(userId, modelConfig, history.Count);
        if (systemMsg != null) messages.Add(systemMsg);

        // 添加历史消息（assistant 含工具调用时重建 tool 角色消息，保持多轮上下文完整性）
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
                    // 工具调用请求：assistant 发起 tool_calls
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
                    // 工具返回结果
                    foreach (var tc in storedDtos)
                    {
                        messages.Add(new AiChatMessage
                        {
                            Role = "tool",
                            ToolCallId = tc.Id,
                            Content = tc.Result ?? String.Empty,
                        });
                    }
                    // 工具调用后 assistant 的最终正文回复
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
        if (userId > 0)
        {
            if (ManageProvider.Provider?.FindByID(userId) is IUser user)
            {
                var sb = Pool.StringBuilder.Get();
                sb.Append($"当前用户：{user.DisplayName}（{user.Name}）");
                var roleIds = user.RoleIds?.SplitAsInt();
                if (roleIds?.Length > 0)
                {
                    var roleNames = roleIds.Select(id => Role.FindByID(id)?.Name).Where(n => !n.IsNullOrEmpty()).Join(",");
                    if (!roleNames.IsNullOrEmpty()) sb.Append($"，角色：{roleNames}");
                }
                if (user.DepartmentID > 0)
                {
                    var dept = Department.FindByID(user.DepartmentID);
                    if (dept != null) sb.Append($"，部门：{dept.Name}");
                }
                parts.Add(sb.Return(true));
            }
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

        // 4. 多轮对话时强调最新消息优先级，避免 LLM 注意力被早期消息稀释（第一轮无需注入）
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

    /// <summary>转换会话实体为摘要DTO</summary>
    /// <param name="entity">会话实体</param>
    /// <returns></returns>
    private static ConversationSummaryDto ToConversationSummary(Conversation entity) =>
        new(entity.Id, entity.Title ?? String.Empty, entity.ModelId, entity.LastMessageTime, entity.IsPinned);

    /// <summary>转换消息实体为DTO</summary>
    /// <param name="entity">消息实体</param>
    /// <param name="feedbackType">反馈类型。0=无反馈, 1=点赞, 2=点踩</param>
    /// <returns></returns>
    private static MessageDto ToMessageDto(ChatMessage entity, FeedbackType feedbackType = default)
    {
        // 反序列化 ToolCalls JSON
        IReadOnlyList<ToolCallDto>? toolCalls = null;
        if (!String.IsNullOrEmpty(entity.ToolCalls))
        {
            try
            {
                toolCalls = entity.ToolCalls.ToJsonEntity<List<ToolCallDto>>();
            }
            catch { }
        }

        return new MessageDto(entity.Id, entity.ConversationId, entity.Role ?? String.Empty, entity.Content ?? String.Empty, entity.ThinkingContent, entity.ThinkingMode, entity.Attachments, entity.CreateTime)
        {
            ToolCalls = toolCalls,
            InputTokens = entity.InputTokens,
            OutputTokens = entity.OutputTokens,
            TotalTokens = entity.TotalTokens,
            FeedbackType = (Int32)feedbackType,
        };
    }

    /// <summary>转换用户设置实体为DTO</summary>
    /// <param name="entity">用户设置实体</param>
    /// <returns></returns>
    private static UserSettingsDto ToUserSettingsDto(UserSetting entity) =>
        new(entity.Language ?? "zh-CN",
            entity.Theme ?? "system",
            entity.FontSize > 0 ? entity.FontSize : 16,
            entity.SendShortcut ?? "Enter",
            entity.DefaultModel,
            entity.DefaultThinkingMode,
            entity.ContextRounds > 0 ? entity.ContextRounds : 10,
            entity.Nickname ?? String.Empty,
            entity.UserBackground ?? String.Empty,
            entity.ResponseStyle,
            entity.SystemPrompt ?? String.Empty,
            entity.AllowTraining)
        {
            McpEnabled = entity.McpEnabled,
            ShowToolCalls = entity.ShowToolCalls,
            DefaultSkill = entity.DefaultSkill ?? "general",
            EnableLearning = entity.EnableLearning,
            LearningModel = entity.LearningModel ?? String.Empty,
            MemoryInjectNum = entity.MemoryInjectNum,
            ContentWidth = entity.ContentWidth,
        };

    /// <summary>根据流式速度等级（1~5）返回缓存回放时的分块参数</summary>
    /// <param name="speed">速度等级，1=慢，3=默认，5=快</param>
    /// <returns>(每块字符数, 块间延迟毫秒数)</returns>
    private static (Int32 ChunkSize, Int32 DelayMs) GetCachedStreamingParams(Int32 speed) => speed switch
    {
        1 => (4, 60),   // ~67 字/秒
        2 => (6, 30),   // ~200 字/秒
        4 => (14, 16),   // ~875 字/秒
        5 => (24, 10),   // ~2400 字/秒
        _ => (10, 20),   // 速度3（默认）：~500 字/秒，约一屏/秒
    };

    /// <summary>将文本按指定块大小拆分后逐块延迟输出，模拟逐 token 打字机效果。在 Unicode 字符边界（含 emoji、CJK）切割，避免截断多字节字符</summary>
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
    private static AiChatMessage BuildMultimodalUserMessage(String attachmentsJson, String? textContent)
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
    private static String? ExtractDocumentAsMarkdown(String filePath, String? fileName)
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
    #endregion
}
