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

        var items = list.Select(ToMessageDto).ToList();
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
    /// <returns>ChatStreamEvent 事件流</returns>
    public async IAsyncEnumerable<ChatStreamEvent> StreamMessageAsync(Int64 conversationId, SendMessageRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var conversation = Conversation.FindById(conversationId);
        if (conversation == null) yield break;

        // 保存用户消息
        var userMsg = new ChatMessage
        {
            ConversationId = conversationId,
            Role = "user",
            Content = request.Content,
            ThinkingMode = (Int32)request.ThinkingMode,
        };
        userMsg.Insert();

        // 查找模型配置
        var modelConfig = _gatewayService.ResolveModel(conversation.ModelCode);
        if (modelConfig == null)
        {
            yield return ChatStreamEvent.ErrorEvent("MODEL_UNAVAILABLE", $"模型 '{conversation.ModelCode}' 不可用");
            yield break;
        }

        // 创建 AI 回复消息占位
        var assistantMsg = new ChatMessage
        {
            ConversationId = conversationId,
            Role = "assistant",
            ThinkingMode = (Int32)request.ThinkingMode,
        };
        assistantMsg.Insert();

        // 发送 message_start
        yield return ChatStreamEvent.MessageStart(assistantMsg.Id, modelConfig.Code, (Int32)request.ThinkingMode);

        // 构建上下文消息列表
        var contextMessages = BuildContextMessages(conversationId, request.Content);

        // 创建模型事件流
        var setting = ChatSetting.Current;
        IAsyncEnumerable<ChatStreamEvent> modelStream;
        if (_toolCallService != null && setting.EnableFunctionCalling)
        {
            modelStream = _toolCallService.StreamWithToolsAsync(contextMessages, modelConfig, CancellationToken.None);
        }
        else
        {
            modelStream = StreamFromProviderAsync(contextMessages, modelConfig, CancellationToken.None);
        }

        // 注册后台任务，确保浏览器断开后模型继续生成
        if (setting.BackgroundGeneration && _backgroundService != null)
        {
            _backgroundService.Register(assistantMsg.Id, modelStream, async task =>
            {
                // 后台任务完成回调：持久化结果
                await PersistResultAsync(assistantMsg, conversation, conversationId, modelConfig.Code,
                    task.ContentBuilder.ToString(),
                    task.ThinkingBuilder.Length > 0 ? task.ThinkingBuilder.ToString() : null,
                    task.Usage, request.Content).ConfigureAwait(false);
            });

            // 从后台任务获取事件进行 SSE 输出
            var bgTask = _backgroundService.GetTask(assistantMsg.Id);
            if (bgTask != null)
            {
                var lastIndex = 0;
                while (bgTask.Status == BackgroundTaskStatus.Running || lastIndex < bgTask.Events.Count)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // 输出新增的事件
                    while (lastIndex < bgTask.Events.Count)
                    {
                        yield return bgTask.Events[lastIndex++];
                    }

                    if (bgTask.Status == BackgroundTaskStatus.Running)
                        await Task.Delay(10, cancellationToken).ConfigureAwait(false);
                }

                // 输出最后的事件
                while (lastIndex < bgTask.Events.Count)
                {
                    yield return bgTask.Events[lastIndex++];
                }
            }
        }
        else
        {
            // 无后台生成，直接流式输出
            var contentBuilder = new StringBuilder();
            var thinkingBuilder = new StringBuilder();
            ChatUsage? lastUsage = null;

            await foreach (var ev in modelStream.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                if (ev.Type == "content_delta" && ev.Content != null)
                    contentBuilder.Append(ev.Content);
                else if (ev.Type == "thinking_delta" && ev.Content != null)
                    thinkingBuilder.Append(ev.Content);
                else if (ev.Type == "message_done")
                    lastUsage = ev.Usage;

                yield return ev;
            }

            await PersistResultAsync(assistantMsg, conversation, conversationId, modelConfig.Code,
                contentBuilder.ToString(),
                thinkingBuilder.Length > 0 ? thinkingBuilder.ToString() : null,
                lastUsage, request.Content).ConfigureAwait(false);
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

        var models = list.Select(e => new ModelInfoDto(e.Code, e.Name, e.SupportThinking, e.SupportVision, e.SupportImageGeneration, e.SupportFunctionCalling)).ToArray();
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
    /// <returns></returns>
    private static MessageDto ToMessageDto(ChatMessage entity) =>
        new(entity.Id, entity.ConversationId, entity.Role, entity.Content, entity.ThinkingContent, (ThinkingMode)entity.ThinkingMode, entity.Attachments, entity.CreateTime);

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
            entity.AllowTraining);
    #endregion
}
