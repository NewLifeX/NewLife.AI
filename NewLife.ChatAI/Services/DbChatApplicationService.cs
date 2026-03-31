using System.Diagnostics;
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
using UsageDetails = NewLife.AI.Models.UsageDetails;
using ILog = NewLife.Log.ILog;

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
/// <param name="tracer">追踪器</param>
/// <param name="log">日志</param>
public class ChatApplicationService(IChatPipeline pipeline, GatewayService gatewayService, BackgroundGenerationService? backgroundService, ITracer tracer, ILog log)
{
    #region 属性
    /// <summary>附件存储根目录</summary>
    private readonly String _attachmentRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Attachments");
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
            UserName = user.DisplayName ?? user.Name,
            Title = title,
            ModelId = request.ModelId,
            ModelName = request.ModelId > 0 ? ModelConfig.FindById(request.ModelId)?.Name : null,
            LastMessageTime = DateTime.Now,
        };
        entity.Insert();

        var dto = ToConversationSummary(entity);
        return Task.FromResult(dto);
    }

    /// <summary>获取会话列表（分页）</summary>
    /// <param name="userId">当前用户编号</param>
    /// <param name="page">页码</param>
    /// <param name="pageSize">每页数量</param>
    /// <param name="keyword">搜索关键词，按标题模糊匹配</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public Task<PagedResultDto<ConversationSummaryDto>> GetConversationsAsync(Int32 userId, Int32 page, Int32 pageSize, String? keyword, CancellationToken cancellationToken)
    {
        if (page <= 0) page = 1;
        if (pageSize <= 0) pageSize = 20;

        var p = new PageParameter
        {
            PageIndex = page,
            PageSize = pageSize,
            Sort = Conversation._.IsPinned.Desc() + "," + Conversation._.LastMessageTime.Desc()
        };

        var exp = Conversation._.UserId == userId;
        if (!String.IsNullOrWhiteSpace(keyword))
            exp &= Conversation._.Title.Contains(keyword.Trim());
        var list = Conversation.FindAll(exp, p);
        var items = list.Select(ToConversationSummary).ToList();

        return Task.FromResult(new PagedResultDto<ConversationSummaryDto>(items, (Int32)p.TotalCount, page, pageSize));
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
            ? MessageFeedback.FindAll(MessageFeedback._.MessageId.In(messageIds) & MessageFeedback._.UserId == userId)
                .ToDictionary(e => e.MessageId, e => e.FeedbackType)
            : [];

        var items = list.Select(e => ToMessageDto(e, feedbacks.TryGetValue(e.Id, out var ft) ? ft : default)).ToList();
        return Task.FromResult<IReadOnlyList<MessageDto>>(items);
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

    /// <summary>删除单条消息</summary>
    /// <param name="messageId">消息编号</param>
    /// <param name="userId">当前用户编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否删除成功</returns>
    public Task<Boolean> DeleteMessageAsync(Int64 messageId, Int32 userId, CancellationToken cancellationToken)
    {
        var entity = ChatMessage.FindById(messageId);
        if (entity == null) return Task.FromResult(false);

        // 验证消息所属会话归当前用户
        var conversation = Conversation.FindById(entity.ConversationId);
        if (conversation == null || conversation.UserId != userId) return Task.FromResult(false);

        // 同时删除该消息的反馈
        var feedback = MessageFeedback.FindByMessageIdAndUserId(entity.Id, userId);
        feedback?.Delete();

        entity.Delete();
        return Task.FromResult(true);
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
        var systemMsg = BuildSystemMessage(userId, modelConfig);
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
            var pipelineCtx = new ChatPipelineContext { UserId = userId + "", ConversationId = entity.ConversationId + "" };
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
                var conv = Conversation.FindById(entity.ConversationId);
                if (conv != null)
                {
                    conv.TotalPromptTokens += response.Usage.InputTokens;
                    conv.TotalCompletionTokens += response.Usage.OutputTokens;
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
        var editPipelineCtx = new ChatPipelineContext { UserId = userId + "", ConversationId = entity.ConversationId + "" };
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
        ApplyUsageToMessage(assistantMsg, finalUsage, hasError);
        assistantMsg.Update();

        ApplyUsageToConversation(conversation, entity.ConversationId, finalUsage);
        conversation.Update();

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
        var systemMsg = BuildSystemMessage(userId, modelConfig);
        if (systemMsg != null) contextMessages.Add(systemMsg);

        foreach (var msg in beforeMessages)
        {
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

        var regenPipelineCtx = new ChatPipelineContext { UserId = userId + "", ConversationId = entity.ConversationId + "" };
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
        ApplyUsageToMessage(entity, finalUsage, hasError);
        entity.Update();

        // 累计会话 Token（重新生成：叠加本次 API 消耗，不扣减被替换消息的旧 Token）
        // 注意：重新生成不需要更新 MessageCount，仅叠加统计
        conversation.LastMessageTime = DateTime.Now;
        if (finalUsage != null)
        {
            conversation.TotalPromptTokens += finalUsage.InputTokens;
            conversation.TotalCompletionTokens += finalUsage.OutputTokens;
            conversation.TotalTokens += finalUsage.TotalTokens;
            conversation.ElapsedMs += finalUsage.ElapsedMs;
            conversation.Update();
        }

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

        // 解析模型配置（在插入 assistant 消息之前，避免模型不可用时留下空消息残留）
        // 优先使用会话绑定的模型，其次使用请求携带的模型（前端当前选择）
        var modelId = conversation.ModelId;
        if (modelId <= 0 && request.ModelId > 0)
        {
            modelId = request.ModelId;
            conversation.ModelId = modelId;
            conversation.Update();
        }
        var modelConfig = gatewayService.ResolveModel(modelId);
        if (modelConfig == null)
        {
            yield return ChatStreamEvent.ErrorEvent("MODEL_UNAVAILABLE", $"模型 '{modelId}' 不可用，请在模型选择器中选择一个可用模型");
            yield break;
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
        using var span = tracer?.NewSpan("chat:Stream", modelConfig.Code);
        yield return ChatStreamEvent.MessageStart(assistantMsg.Id, modelConfig.Code ?? String.Empty, request.ThinkingMode);

        // 委托管道流式执行（能力扩展层 + 知识进化层由管道内部处理）
        var contentBuilder = new StringBuilder();
        var thinkingBuilder = new StringBuilder();
        UsageDetails? finalUsage = null;
        var hasError = false;
        ChatStreamEvent? deferredErrorEvent = null;
        var toolCallsCollector = new List<ToolCallDto>();

        var msgPipelineCtx = new ChatPipelineContext { UserId = userId + "", ConversationId = conversationId + "" };
        if (request.Options != null) msgPipelineCtx.Items = request.Options;
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
        ApplyUsageToMessage(assistantMsg, finalUsage, hasError);
        assistantMsg.Update();

        // 更新会话
        ApplyUsageToConversation(conversation, conversationId, finalUsage);
        conversation.ModelName = modelConfig.Name;
        conversation.Update();

        // 异步生成标题（首条消息时）
        String? title = null;
        if (!hasError && conversation.MessageCount <= 2 && ChatSetting.Current.AutoGenerateTitle)
        {
            using var span2 = tracer?.NewSpan("chat:GenerateTitle");
            try
            {
                title = await GenerateTitleAsync(conversationId, request.Content, CancellationToken.None).ConfigureAwait(false);
                span?.AppendTag(title!);
            }
            catch (Exception ex)
            {
                span2?.SetError(ex);
                log?.Error("异步生成标题失败: {0}", ex.Message);
            }
        }

        // message_done（含 MessageId、Usage、Title）
        if (!hasError && !cancellationToken.IsCancellationRequested)
        {
            yield return new ChatStreamEvent { Type = "message_done", MessageId = assistantMsg.Id, Usage = finalUsage, Title = title, };
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
                        toolSpan = tracer?.NewSpan($"chat:ToolCall:{ev.Name}");
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
    private static void ApplyUsageToMessage(ChatMessage msg, UsageDetails? usage, Boolean hasError)
    {
        if (msg.Content.IsNullOrEmpty())
            msg.Content = hasError ? "[生成失败]" : "[已中断]";
        if (usage != null)
        {
            msg.PromptTokens = usage.InputTokens;
            msg.CompletionTokens = usage.OutputTokens;
            msg.TotalTokens = usage.TotalTokens;
            msg.ElapsedMs = usage.ElapsedMs;
        }
    }

    /// <summary>将用量统计累加到会话实体并更新最后消息时间（不保存，调用方负责 Update）</summary>
    private static void ApplyUsageToConversation(Conversation conversation, Int64 conversationId, UsageDetails? usage)
    {
        conversation.LastMessageTime = DateTime.Now;
        conversation.MessageCount = (Int32)ChatMessage.FindCount(ChatMessage._.ConversationId == conversationId);
        if (usage != null)
        {
            conversation.TotalPromptTokens += usage.InputTokens;
            conversation.TotalCompletionTokens += usage.OutputTokens;
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

        // 尝试通过模型生成标题
        var modelConfig = gatewayService.ResolveModel(conversation.ModelId);
        if (modelConfig != null)
        {
            var descriptor = gatewayService.GetDescriptor(modelConfig);
            if (descriptor != null)
            {
                try
                {
                    var prompt = setting.TitlePrompt;
                    var options = GatewayService.BuildOptions(modelConfig);
                    using var titleClient = descriptor.Factory(options);
                    var response = await titleClient.GetResponseAsync(
                        [new AiChatMessage { Role = "user", Content = $"{prompt}\n{userMessage}" }],
                        new ChatOptions { Model = modelConfig.Code, MaxTokens = 30 },
                        cancellationToken).ConfigureAwait(false);

                    var title = response.Messages?.FirstOrDefault()?.Message?.Content as String;
                    if (!String.IsNullOrWhiteSpace(title))
                    {
                        // 清理标题：去除引号和多余空白
                        title = title.Trim().Trim('"', '"', '"', '\'', '「', '」');
                        if (title.Length > 30) title = title.Substring(0, 30);

                        conversation.Title = title;
                        conversation.Update();
                        return title;
                    }
                }
                catch (Exception ex)
                {
                    log?.Warn("模型生成标题失败，回退截取: {0}", ex.Message);
                }
            }
        }

        // 回退：截取前10个字符
        var fallbackTitle = userMessage.Length > 10 ? userMessage.Substring(0, 10) : userMessage;
        fallbackTitle = fallbackTitle.Replace("\n", " ").Replace("\r", "").Trim();

        if (!String.IsNullOrWhiteSpace(fallbackTitle) && fallbackTitle != conversation.Title)
        {
            conversation.Title = fallbackTitle;
            conversation.Update();
        }

        return fallbackTitle;
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
        var convIds = Conversation.FindAll(Conversation._.UserId == userId, null, Conversation._.Id, 0, 0)
            .Select(e => e.Id).ToArray();
        if (convIds.Length == 0)
            return new PagedResultDto<MessageSearchResultDto>([], 0, page, pageSize);

        var p = new PageParameter
        {
            PageIndex = page,
            PageSize = pageSize,
            Sort = ChatMessage._.Id.Desc()
        };

        var exp = ChatMessage._.ConversationId.In(convIds) & ChatMessage._.Content.Contains(keyword.Trim());
        var list = ChatMessage.FindAll(exp, p);

        var items = list.Select(e => new MessageSearchResultDto
        {
            Id = e.Id.ToString(),
            ConversationId = e.ConversationId.ToString(),
            ConversationTitle = e.ConversationTitle ?? "",
            Role = e.Role ?? "user",
            Content = e.Content ?? "",
            CreateTime = e.CreateTime.ToString("yyyy-MM-dd HH:mm:ss"),
        }).ToList();

        return new PagedResultDto<MessageSearchResultDto>(items, (Int32)p.TotalCount, page, pageSize);
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

    #region 附件
    /// <summary>上传附件</summary>
    /// <param name="fileName">文件名</param>
    /// <param name="size">文件大小</param>
    /// <param name="stream">文件流</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public async Task<UploadAttachmentResult> UploadAttachmentAsync(String fileName, Int64 size, Stream stream, CancellationToken cancellationToken)
    {
        // 确保目录存在
        if (!Directory.Exists(_attachmentRoot))
            Directory.CreateDirectory(_attachmentRoot);

        // 生成唯一文件路径
        var datePath = DateTime.Now.ToString("yyyyMMdd");
        var dir = Path.Combine(_attachmentRoot, datePath);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var ext = Path.GetExtension(fileName);
        var storedName = Guid.NewGuid().ToString("N") + ext;
        var filePath = Path.Combine(dir, storedName);

        // 写入磁盘
        using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await stream.CopyToAsync(fs, cancellationToken).ConfigureAwait(false);

        // 推断 ContentType
        var contentType = ext?.ToLower() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".pdf" => "application/pdf",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".txt" => "text/plain",
            ".md" => "text/markdown",
            ".csv" => "text/csv",
            _ => "application/octet-stream",
        };

        // 保存附件记录
        var entity = new Attachment
        {
            FileName = fileName,
            FilePath = Path.Combine(datePath, storedName),
            ContentType = contentType,
            Size = size,
        };
        entity.Insert();

        return new UploadAttachmentResult(entity.Id.ToString(), entity.FileName, $"/api/attachments/{entity.Id}", entity.Size);
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
            return Task.FromResult(new UserSettingsDto("zh-CN", "system", 16, "Enter", 0, ThinkingMode.Auto, 10, String.Empty));
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
        entity.SystemPrompt = settings.SystemPrompt;
        entity.McpEnabled = settings.McpEnabled;
        entity.StreamingSpeed = settings.StreamingSpeed;
        entity.AllowTraining = settings.AllowTraining;
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
        var conversations = Conversation.FindAll(Conversation._.UserId == userId, null, null, 0, 0);
        if (conversations.Count == 0) return Task.CompletedTask;

        var convIds = conversations.Select(e => e.Id).ToArray();

        // 按会话维度级联删除，避免误删其他用户数据
        // 删除关联的共享
        var shares = SharedConversation.FindAll(SharedConversation._.ConversationId.In(convIds));
        shares.Delete();

        // 获取关联的消息 ID
        var messages = ChatMessage.FindAll(ChatMessage._.ConversationId.In(convIds));
        var msgIds = messages.Select(e => e.Id).ToArray();

        // 删除消息反馈
        if (msgIds.Length > 0)
        {
            var feedbacks = MessageFeedback.FindAll(MessageFeedback._.MessageId.In(msgIds));
            feedbacks.Delete();
        }

        // 删除消息
        messages.Delete();

        // 删除会话
        conversations.Delete();

        return Task.CompletedTask;
    }
    #endregion

    #region 辅助
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
        var systemMsg = BuildSystemMessage(userId, modelConfig);
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
    /// <returns>系统消息，无提示词时返回 null</returns>
    private AiChatMessage? BuildSystemMessage(Int32 userId, ModelConfig? modelConfig)
    {
        using var span = tracer?.NewSpan(nameof(BuildSystemMessage), new { userId });
        var parts = new List<String>();

        // 0. 当前用户基础信息
        if (userId > 0)
        {
            var iuser = ManageProvider.Provider?.FindByID(userId) as IUser;
            if (iuser != null)
            {
                var sb = Pool.StringBuilder.Get();
                sb.Append($"当前用户：{iuser.DisplayName}（{iuser.Name}）");
                var roleIds = iuser.RoleIds?.SplitAsInt();
                if (roleIds?.Length > 0)
                {
                    var roleNames = roleIds.Select(id => Role.FindByID(id)?.Name).Where(n => !n.IsNullOrEmpty()).Join(",");
                    if (!roleNames.IsNullOrEmpty()) sb.Append($"，角色：{roleNames}");
                }
                if (iuser.DepartmentID > 0)
                {
                    var dept = Department.FindByID(iuser.DepartmentID);
                    if (dept != null) sb.Append($"，部门：{dept.Name}");
                }
                parts.Add(sb.Put(true));
            }
        }

        // 1. 用户全局系统提示词
        var userSetting = UserSetting.FindByUserId(userId);
        if (userSetting != null && !String.IsNullOrWhiteSpace(userSetting.SystemPrompt))
            parts.Add(userSetting.SystemPrompt.Trim());

        // 2. 模型级系统提示词
        if (modelConfig != null && !String.IsNullOrWhiteSpace(modelConfig.SystemPrompt))
            parts.Add(modelConfig.SystemPrompt.Trim());

        if (parts.Count == 0) return null;
        if (span != null) span.Value = parts.Count;

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
            PromptTokens = entity.PromptTokens,
            CompletionTokens = entity.CompletionTokens,
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
            entity.SystemPrompt ?? String.Empty)
        {
            McpEnabled = entity.McpEnabled,
            StreamingSpeed = entity.StreamingSpeed > 0 ? entity.StreamingSpeed : 3,
            AllowTraining = entity.AllowTraining,
        };
    #endregion
}
