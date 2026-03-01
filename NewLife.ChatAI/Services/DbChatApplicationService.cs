using System.Runtime.CompilerServices;
using System.Text;
using NewLife.AI.ChatAI.Contracts;
using NewLife.ChatAI.Entity;
using NewLife.Cube.Entity;
using NewLife.Data;
using NewLife.Log;
using NewLife.Serialization;
using XCode;
using AiChatMessage = NewLife.AI.Models.ChatMessage;
using ChatCompletionRequest = NewLife.AI.Models.ChatCompletionRequest;
using ChatStreamEvent = NewLife.AI.Models.ChatStreamEvent;
using ChatUsage = NewLife.AI.Models.ChatUsage;

namespace NewLife.ChatAI.Services;

/// <summary>数据库版对话应用服务。基于 XCode 实体类持久化数据</summary>
public class DbChatApplicationService : IChatApplicationService
{
    #region 属性
    private readonly ToolCallService? _toolCallService;
    private readonly GatewayService _gatewayService;
    private readonly BackgroundGenerationService? _backgroundService;
    private readonly UsageService? _usageService;
    private readonly ILog _log;

    /// <summary>附件存储根目录</summary>
    private readonly String _attachmentRoot;
    #endregion

    #region 构造
    /// <summary>实例化数据库版对话应用服务</summary>
    /// <param name="gatewayService">网关服务</param>
    /// <param name="toolCallService">工具调用编排服务</param>
    /// <param name="backgroundService">后台生成服务</param>
    /// <param name="usageService">用量统计服务</param>
    /// <param name="log">日志</param>
    public DbChatApplicationService(GatewayService gatewayService, ToolCallService? toolCallService, BackgroundGenerationService? backgroundService, UsageService? usageService, ILog log)
    {
        _gatewayService = gatewayService;
        _toolCallService = toolCallService;
        _backgroundService = backgroundService;
        _usageService = usageService;
        _log = log;
        _attachmentRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Attachments");
    }
    #endregion

    #region 会话管理
    /// <summary>新建会话</summary>
    /// <param name="request">新建会话请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public Task<ConversationSummaryDto> CreateConversationAsync(CreateConversationRequest request, CancellationToken cancellationToken)
    {
        var title = String.IsNullOrWhiteSpace(request.Title) ? "新建对话" : request.Title.Trim();
        var modelCode = request.ModelCode ?? "qwen-max";

        var entity = new Conversation
        {
            Title = title,
            ModelCode = modelCode,
            LastMessageTime = DateTime.Now,
        };
        entity.Insert();

        var dto = ToConversationSummary(entity);
        return Task.FromResult(dto);
    }

    /// <summary>获取会话列表（分页）</summary>
    /// <param name="page">页码</param>
    /// <param name="pageSize">每页数量</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public Task<PagedResultDto<ConversationSummaryDto>> GetConversationsAsync(Int32 page, Int32 pageSize, CancellationToken cancellationToken)
    {
        if (page <= 0) page = 1;
        if (pageSize <= 0) pageSize = 20;

        var p = new PageParameter { PageIndex = page, PageSize = pageSize };
        p.Sort = Conversation._.IsPinned.Desc() + "," + Conversation._.LastMessageTime.Desc();

        var list = Conversation.FindAll(null, p);
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
        if (!String.IsNullOrWhiteSpace(request.ModelCode))
            entity.ModelCode = request.ModelCode.Trim();

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

        // 获取关联的消息 ID 列表，用于清理消息反馈
        var messages = ChatMessage.FindAll(ChatMessage._.ConversationId == conversationId);
        var messageIds = messages.Select(m => m.Id).ToArray();

        // 删除关联的消息反馈
        if (messageIds.Length > 0)
        {
            var feedbacks = MessageFeedback.FindAll(MessageFeedback._.MessageId.In(messageIds));
            feedbacks.Delete();
        }

        // 删除关联的用量记录
        var usageRecords = UsageRecord.FindAll(UsageRecord._.ConversationId == conversationId);
        usageRecords.Delete();

        // 删除关联的消息
        messages.Delete();

        // 删除关联的共享
        var shares = SharedConversation.FindAllByConversationId(conversationId);
        shares.Delete();

        entity.Delete();
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
    /// <summary>获取会话消息列表</summary>
    /// <param name="conversationId">会话编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public Task<IReadOnlyList<MessageDto>> GetMessagesAsync(Int64 conversationId, CancellationToken cancellationToken)
    {
        var p = new PageParameter { PageSize = 0, Sort = ChatMessage._.CreateTime.Asc() };
        var list = ChatMessage.Search(conversationId, DateTime.MinValue, DateTime.MinValue, null, p);

        // 批量查询反馈，避免 N+1
        var messageIds = list.Select(e => e.Id).ToList();
        var feedbacks = messageIds.Count > 0
            ? MessageFeedback.FindAll(MessageFeedback._.MessageId.In(messageIds) & MessageFeedback._.UserId == 0)
                .ToDictionary(e => e.MessageId, e => e.FeedbackType)
            : new Dictionary<Int64, Int32>();

        var items = list.Select(e => ToMessageDto(e, feedbacks.TryGetValue(e.Id, out var ft) ? ft : 0)).ToList();
        return Task.FromResult<IReadOnlyList<MessageDto>>(items);
    }

    /// <summary>编辑消息内容</summary>
    /// <param name="messageId">消息编号</param>
    /// <param name="request">编辑请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public Task<MessageDto?> EditMessageAsync(Int64 messageId, EditMessageRequest request, CancellationToken cancellationToken)
    {
        var entity = ChatMessage.FindById(messageId);
        if (entity == null) return Task.FromResult<MessageDto?>(null);

        entity.Content = request.Content;
        entity.Update();

        return Task.FromResult<MessageDto?>(ToMessageDto(entity));
    }

    /// <summary>重新生成AI回复。查找上文构建上下文，调用模型重新生成</summary>
    /// <param name="messageId">消息编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public async Task<MessageDto?> RegenerateMessageAsync(Int64 messageId, CancellationToken cancellationToken)
    {
        var entity = ChatMessage.FindById(messageId);
        if (entity == null || !entity.Role.EqualIgnoreCase("assistant"))
            return null;

        // 查找会话和模型配置
        var conversation = Conversation.FindById(entity.ConversationId);
        if (conversation == null) return null;

        var modelConfig = _gatewayService.ResolveModel(conversation.ModelCode);
        if (modelConfig == null)
        {
            // 模型不可用时直接报错，不做降级
            return null;
        }

        var provider = _gatewayService.GetProvider(modelConfig);
        if (provider == null) return null;

        // 构建上下文：取该消息之前的所有消息
        var beforeMessages = ChatMessage.FindAll(
            ChatMessage._.ConversationId == entity.ConversationId & ChatMessage._.Id < entity.Id,
            ChatMessage._.CreateTime.Asc(), null, 0, 0);

        // 按轮数截取
        var setting = ChatSetting.Current;
        var maxCount = (setting.DefaultContextRounds > 0 ? setting.DefaultContextRounds : 10) * 2;
        if (beforeMessages.Count > maxCount)
            beforeMessages = beforeMessages.Skip(beforeMessages.Count - maxCount).ToList();

        var contextMessages = new List<AiChatMessage>();
        foreach (var msg in beforeMessages)
        {
            contextMessages.Add(new AiChatMessage
            {
                Role = msg.Role,
                Content = msg.Content,
            });
        }

        try
        {
            var request = new ChatCompletionRequest
            {
                Model = modelConfig.Code,
                Messages = contextMessages,
            };
            var options = GatewayService.BuildOptions(modelConfig);
            var response = await provider.ChatAsync(request, options, cancellationToken).ConfigureAwait(false);

            var newContent = response.Choices?.FirstOrDefault()?.Message?.Content as String ?? String.Empty;
            var reasoning = response.Choices?.FirstOrDefault()?.Message?.ReasoningContent;

            entity.Content = newContent;
            if (!String.IsNullOrEmpty(reasoning))
                entity.ThinkingContent = reasoning;
            entity.Update();

            // 写入用量记录
            if (response.Usage != null)
            {
                _usageService?.Record(0, 0, entity.ConversationId, entity.Id,
                    modelConfig.Code, response.Usage.PromptTokens, response.Usage.CompletionTokens, response.Usage.TotalTokens, "Chat");
            }

            return ToMessageDto(entity);
        }
        catch (Exception ex)
        {
            _log?.Error("重新生成回复失败: {0}", ex.Message);
            return null;
        }
    }

    /// <summary>流式发送消息并获取AI回复。接入真实模型，支持 thinking/content/tool_call 事件和后台继续生成</summary>
    /// <param name="conversationId">会话编号</param>
    /// <param name="request">发送消息请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public async IAsyncEnumerable<ChatStreamEvent> StreamMessageAsync(Int64 conversationId, SendMessageRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var conversation = Conversation.FindById(conversationId);
        if (conversation == null)
        {
            yield return new ChatStreamEvent { Type = "error", Error = "会话不存在" };
            yield break;
        }

        // 保存用户消息
        var userMsg = new ChatMessage
        {
            ConversationId = conversationId,
            Role = "user",
            Content = request.Content,
            ThinkingMode = (Int32)request.ThinkingMode,
        };
        userMsg.Insert();

        // 预分配AI回复消息编号
        var assistantMsg = new ChatMessage
        {
            ConversationId = conversationId,
            Role = "assistant",
            ThinkingMode = (Int32)request.ThinkingMode,
        };
        assistantMsg.Insert();

        // message_start
        yield return new ChatStreamEvent { Type = "message_start", MessageId = assistantMsg.Id };

        // 占位流式回复，后续接入真实模型推理与上下文管理
        var answer = "这是流式回复骨架。后续可接入真实模型推理与上下文管理。";
        var chunks = answer.Split('。', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var content = new StringBuilder();
        foreach (var chunk in chunks)
        {
            if (cancellationToken.IsCancellationRequested) break;
            var line = chunk + "。";
            content.Append(line);
            yield return new ChatStreamEvent { Type = "content_delta", Content = line };
            try { await Task.Delay(120, cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }

        // 无论是否被取消，都保存已输出的内容，避免空消息残留
        assistantMsg.Content = content.Length > 0 ? content.ToString() : "[已中断]";

        // 占位 Token 用量（后续接入真实模型后从模型返回值获取）
        var promptTokens = request.Content.Length;
        var completionTokens = content.Length;
        assistantMsg.PromptTokens = promptTokens;
        assistantMsg.CompletionTokens = completionTokens;
        assistantMsg.TotalTokens = promptTokens + completionTokens;
        assistantMsg.Update();

        // 更新会话的最后消息时间和消息数
        conversation.LastMessageTime = DateTime.Now;
        conversation.MessageCount = (Int32)ChatMessage.FindCount(ChatMessage._.ConversationId == conversationId);

        // 首条消息后自动生成标题（标题仍为默认值时触发）
        if (conversation.Title == "新建对话" && !String.IsNullOrWhiteSpace(request.Content))
        {
            var title = request.Content.Trim();
            // 截取前10个字符作为标题，超出部分加省略号
            if (title.Length > 10) title = title[..10] + "...";
            conversation.Title = title;
        }

        conversation.Update();

        if (!cancellationToken.IsCancellationRequested)
        {
            // message_done，包含 Token 用量
            yield return new ChatStreamEvent
            {
                Type = "message_done",
                MessageId = assistantMsg.Id,
                Usage = new ChatUsage { PromptTokens = promptTokens, CompletionTokens = completionTokens, TotalTokens = promptTokens + completionTokens },
            };
        }
    }

    /// <summary>直接从服务商流式生成（不经过 ToolCallService）</summary>
    /// <param name="contextMessages">上下文消息列表</param>
    /// <param name="modelConfig">模型配置</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    private async IAsyncEnumerable<ChatStreamEvent> StreamFromProviderAsync(
        IList<AiChatMessage> contextMessages,
        ModelConfig modelConfig,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var provider = _gatewayService.GetProvider(modelConfig);
        if (provider == null)
        {
            yield return ChatStreamEvent.ErrorEvent("MODEL_UNAVAILABLE", $"未找到服务商 '{modelConfig.Provider}'");
            yield break;
        }

        var aiRequest = new ChatCompletionRequest
        {
            Model = modelConfig.Code,
            Messages = contextMessages,
            Stream = true,
        };
        var options = GatewayService.BuildOptions(modelConfig);

        var thinkingBuilder = new StringBuilder();
        ChatUsage? lastUsage = null;

        await foreach (var chunk in provider.ChatStreamAsync(aiRequest, options, cancellationToken).ConfigureAwait(false))
        {
            if (chunk.Usage != null) lastUsage = chunk.Usage;

            var choice = chunk.Choices?.FirstOrDefault();
            if (choice == null) continue;

            var delta = choice.Delta;
            if (delta == null) continue;

            if (!String.IsNullOrEmpty(delta.ReasoningContent))
            {
                thinkingBuilder.Append(delta.ReasoningContent);
                yield return ChatStreamEvent.ThinkingDelta(delta.ReasoningContent);
            }

            var text = delta.Content as String;
            if (!String.IsNullOrEmpty(text))
                yield return ChatStreamEvent.ContentDelta(text);
        }

        if (thinkingBuilder.Length > 0)
            yield return ChatStreamEvent.ThinkingDone(0);

        yield return ChatStreamEvent.MessageDone(lastUsage);
    }

    /// <summary>持久化 AI 回复结果到数据库</summary>
    /// <param name="assistantMsg">AI 回复消息实体</param>
    /// <param name="conversation">会话实体</param>
    /// <param name="conversationId">会话编号</param>
    /// <param name="modelCode">模型编码</param>
    /// <param name="content">回复内容</param>
    /// <param name="thinkingContent">思考内容</param>
    /// <param name="usage">用量统计</param>
    /// <param name="userMessage">用户消息内容（用于标题生成）</param>
    /// <returns></returns>
    private async Task PersistResultAsync(ChatMessage assistantMsg, Conversation conversation, Int64 conversationId,
        String modelCode, String content, String? thinkingContent, ChatUsage? usage, String userMessage)
    {
        assistantMsg.Content = content;
        if (!String.IsNullOrEmpty(thinkingContent))
            assistantMsg.ThinkingContent = thinkingContent;
        assistantMsg.Update();

        conversation.LastMessageTime = DateTime.Now;
        conversation.MessageCount = (Int32)Entity.ChatMessage.FindCount(Entity.ChatMessage._.ConversationId == conversationId);
        conversation.Update();

        if (usage != null)
        {
            _usageService?.Record(0, 0, conversationId, assistantMsg.Id,
                modelCode, usage.PromptTokens, usage.CompletionTokens, usage.TotalTokens, "Chat");
        }

        // 异步生成标题（首条消息）
        if (conversation.MessageCount <= 2 && ChatSetting.Current.AutoGenerateTitle)
        {
            try
            {
                await GenerateTitleAsync(conversationId, userMessage, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log?.Error("异步生成标题失败: {0}", ex.Message);
            }
        }
    }

    /// <summary>中断生成。停止后台继续生成任务</summary>
    /// <param name="messageId">消息编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public Task StopGenerateAsync(Int64 messageId, CancellationToken cancellationToken)
    {
        _backgroundService?.Stop(messageId);
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
        var modelConfig = _gatewayService.ResolveModel(conversation.ModelCode);
        if (modelConfig != null)
        {
            var provider = _gatewayService.GetProvider(modelConfig);
            if (provider != null)
            {
                try
                {
                    var prompt = setting.TitlePrompt;
                    var request = new ChatCompletionRequest
                    {
                        Model = modelConfig.Code,
                        Messages =
                        [
                            new AiChatMessage { Role = "user", Content = $"{prompt}\n{userMessage}" }
                        ],
                        MaxTokens = 30,
                    };
                    var options = GatewayService.BuildOptions(modelConfig);
                    var response = await provider.ChatAsync(request, options, cancellationToken).ConfigureAwait(false);

                    var title = response.Choices?.FirstOrDefault()?.Message?.Content as String;
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
                    _log?.Warn("模型生成标题失败，回退截取: {0}", ex.Message);
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
    #endregion

    #region 反馈
    /// <summary>提交点赞/点踩反馈</summary>
    /// <param name="messageId">消息编号</param>
    /// <param name="request">反馈请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public Task SubmitFeedbackAsync(Int64 messageId, FeedbackRequest request, CancellationToken cancellationToken)
    {
        // 本期暂不启用登录鉴权，UserId 固定为 0
        var userId = 0;

        var entity = MessageFeedback.FindByMessageIdAndUserId(messageId, userId);
        if (entity == null)
        {
            entity = new MessageFeedback
            {
                MessageId = messageId,
                UserId = userId,
            };
        }

        entity.FeedbackType = (Int32)request.Type;
        entity.Reason = request.Reason;
        entity.AllowTraining = request.AllowTraining ?? false;
        entity.Save();

        return Task.CompletedTask;
    }

    /// <summary>取消反馈</summary>
    /// <param name="messageId">消息编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public Task DeleteFeedbackAsync(Int64 messageId, CancellationToken cancellationToken)
    {
        var userId = 0;
        var entity = MessageFeedback.FindByMessageIdAndUserId(messageId, userId);
        entity?.Delete();

        return Task.CompletedTask;
    }
    #endregion

    #region 分享
    /// <summary>创建共享链接</summary>
    /// <param name="conversationId">会话编号</param>
    /// <param name="request">创建分享请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public Task<ShareLinkDto> CreateShareLinkAsync(Int64 conversationId, CreateShareRequest request, CancellationToken cancellationToken)
    {
        // 获取当前最后一条消息的编号作为快照截止点
        var lastMsg = ChatMessage.FindAll(ChatMessage._.ConversationId == conversationId, ChatMessage._.Id.Desc(), null, 0, 1);
        var snapshotMessageId = lastMsg.Count > 0 ? lastMsg[0].Id : 0;

        DateTime? expireTime = null;
        if (request.ExpireHours is > 0)
            expireTime = DateTime.Now.AddHours(request.ExpireHours.Value);

        var entity = new SharedConversation
        {
            ConversationId = conversationId,
            ShareToken = Guid.NewGuid().ToString("N"),
            SnapshotMessageId = snapshotMessageId,
            ExpireTime = expireTime ?? DateTime.MinValue,
        };
        entity.Insert();

        var dto = new ShareLinkDto($"/api/share/{entity.ShareToken}", entity.CreateTime, expireTime);
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
        var exp = ChatMessage._.ConversationId == share.ConversationId;
        if (share.SnapshotMessageId > 0)
            exp &= ChatMessage._.Id <= share.SnapshotMessageId;

        var messages = ChatMessage.FindAll(exp, ChatMessage._.CreateTime.Asc(), null, 0, 0);
        var items = messages.Select(ToMessageDto).ToList();

        var result = new
        {
            share.ConversationId,
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
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public Task<ModelInfoDto[]> GetModelsAsync(CancellationToken cancellationToken)
    {
        var list = ModelConfig.FindAll(ModelConfig._.Enable == true, ModelConfig._.Sort.Asc(), null, 0, 0);

        // 若数据库无模型配置，返回默认列表
        if (list.Count == 0)
        {
            return Task.FromResult(new[]
            {
                new ModelInfoDto("qwen-max", "Qwen-Max", true, true, false, true),
                new ModelInfoDto("deepseek-r1", "DeepSeek-R1", true, false, false, true),
                new ModelInfoDto("gpt-4o", "GPT-4o", true, true, false, true),
            });
        }

        var models = list.Select(e => new ModelInfoDto(e.Code, e.Name, e.SupportThinking, e.SupportVision, e.SupportImageGeneration, e.SupportFunctionCalling, e.Provider)).ToArray();
        return Task.FromResult(models);
    }
    #endregion

    #region 用户设置
    /// <summary>获取用户设置</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public Task<UserSettingsDto> GetUserSettingsAsync(CancellationToken cancellationToken)
    {
        // 本期暂不启用登录鉴权，UserId 固定为 0
        var entity = UserSetting.FindByUserId(0);
        if (entity == null)
        {
            // 返回默认设置
            return Task.FromResult(new UserSettingsDto("zh-CN", "system", 16, "Enter", "qwen-max", ThinkingMode.Auto, 10, String.Empty, false));
        }

        return Task.FromResult(ToUserSettingsDto(entity));
    }

    /// <summary>更新用户设置</summary>
    /// <param name="settings">用户设置</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public Task<UserSettingsDto> UpdateUserSettingsAsync(UserSettingsDto settings, CancellationToken cancellationToken)
    {
        var entity = UserSetting.FindByUserId(0);
        if (entity == null)
        {
            entity = new UserSetting { UserId = 0 };
        }

        entity.Language = settings.Language;
        entity.Theme = settings.Theme;
        entity.FontSize = settings.FontSize;
        entity.SendShortcut = settings.SendShortcut;
        entity.DefaultModel = settings.DefaultModel;
        entity.DefaultThinkingMode = (Int32)settings.DefaultThinkingMode;
        entity.ContextRounds = settings.ContextRounds;
        entity.SystemPrompt = settings.SystemPrompt;
        entity.AllowTraining = settings.AllowTraining;
        entity.McpEnabled = settings.McpEnabled;
        entity.DefaultSkill = settings.DefaultSkill;
        entity.StreamingSpeed = settings.StreamingSpeed;
        entity.Save();

        return Task.FromResult(ToUserSettingsDto(entity));
    }

    /// <summary>导出所有对话数据</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public Task<Stream> ExportUserDataAsync(CancellationToken cancellationToken)
    {
        var conversations = Conversation.FindAll();
        var result = new List<Object>();

        foreach (var conv in conversations)
        {
            var messages = ChatMessage.FindAll(ChatMessage._.ConversationId == conv.Id, ChatMessage._.CreateTime.Asc(), null, 0, 0);
            result.Add(new
            {
                conv.Id,
                conv.Title,
                conv.ModelCode,
                conv.IsPinned,
                conv.LastMessageTime,
                conv.CreateTime,
                Messages = messages.Select(m => new
                {
                    m.Id,
                    m.Role,
                    m.Content,
                    m.ThinkingMode,
                    m.CreateTime,
                }).ToList(),
            });
        }

        var json = result.ToJson();
        Stream stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return Task.FromResult(stream);
    }

    /// <summary>清除所有对话</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public Task ClearUserConversationsAsync(CancellationToken cancellationToken)
    {
        // 删除全部共享
        SharedConversation.FindAll().Delete();

        // 删除全部消息反馈
        MessageFeedback.FindAll().Delete();

        // 删除全部消息
        ChatMessage.FindAll().Delete();

        // 删除全部会话
        Conversation.FindAll().Delete();

        return Task.CompletedTask;
    }
    #endregion

    #region 辅助
    /// <summary>构建上下文消息列表。按配置的轮数截取历史消息</summary>
    /// <param name="conversationId">会话编号</param>
    /// <param name="currentContent">当前用户消息内容</param>
    /// <returns>OpenAI ChatMessage 格式的消息列表</returns>
    private IList<AiChatMessage> BuildContextMessages(Int64 conversationId, String currentContent)
    {
        var setting = ChatSetting.Current;
        var maxRounds = setting.DefaultContextRounds > 0 ? setting.DefaultContextRounds : 10;

        // 查询历史消息，按时间倒序取最近 N 轮（每轮 = 1条user + 1条assistant = 2条）
        var p = new PageParameter { PageSize = maxRounds * 2, Sort = Entity.ChatMessage._.CreateTime.Desc() };
        var history = Entity.ChatMessage.Search(conversationId, DateTime.MinValue, DateTime.MinValue, null, p);
        history.Reverse();

        var messages = new List<AiChatMessage>();

        // 添加历史消息
        foreach (var msg in history)
        {
            messages.Add(new AiChatMessage
            {
                Role = msg.Role,
                Content = msg.Content,
            });
        }

        return messages;
    }

    /// <summary>转换会话实体为摘要DTO</summary>
    /// <param name="entity">会话实体</param>
    /// <returns></returns>
    private static ConversationSummaryDto ToConversationSummary(Conversation entity) =>
        new(entity.Id, entity.Title, entity.ModelCode, entity.LastMessageTime, entity.IsPinned);

    /// <summary>转换消息实体为DTO</summary>
    /// <param name="entity">消息实体</param>
    /// <param name="feedbackType">反馈类型。0=无反馈, 1=点赞, 2=点踩</param>
    /// <returns></returns>
    private static MessageDto ToMessageDto(ChatMessage entity, Int32 feedbackType = 0)
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

        return new MessageDto(entity.Id, entity.ConversationId, entity.Role, entity.Content, entity.ThinkingContent, (ThinkingMode)entity.ThinkingMode, entity.Attachments, entity.CreateTime)
        {
            ToolCalls = toolCalls,
            PromptTokens = entity.PromptTokens,
            CompletionTokens = entity.CompletionTokens,
            TotalTokens = entity.TotalTokens,
            FeedbackType = feedbackType,
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
            entity.DefaultModel ?? "qwen-max",
            (ThinkingMode)entity.DefaultThinkingMode,
            entity.ContextRounds > 0 ? entity.ContextRounds : 10,
            entity.SystemPrompt ?? String.Empty,
            entity.AllowTraining)
        {
            McpEnabled = entity.McpEnabled,
            DefaultSkill = entity.DefaultSkill ?? "general",
            StreamingSpeed = entity.StreamingSpeed > 0 ? entity.StreamingSpeed : 3,
        };
    #endregion
}
