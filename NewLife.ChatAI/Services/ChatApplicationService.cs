using System.Text;
using NewLife.Data;
using NewLife.Serialization;
using XCode;
using XCode.Membership;
using ChatMessage = NewLife.ChatAI.Entity.ChatMessage;

namespace NewLife.ChatAI.Services;

/// <summary>数据库版对话应用服务。基于 XCode 实体类持久化数据</summary>
/// <remarks>
/// 对话内核层：负责会话与消息的持久化管理、分享、反馈、模型与用户设置等功能。
/// 生成相关方法已提取到 <see cref="MessageFlowForWeb"/>。
/// </remarks>
public class ChatApplicationService
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
            Enable = true,
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
    /// <param name="userId">当前用户编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>更新后的会话摘要，会话不存在或无权访问时返回 null</returns>
    public Task<ConversationSummaryDto?> UpdateConversationAsync(Int64 conversationId, UpdateConversationRequest request, Int32 userId, CancellationToken cancellationToken)
    {
        var entity = Conversation.FindById(conversationId);
        if (entity == null || entity.UserId != userId) return Task.FromResult<ConversationSummaryDto?>(null);

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
    /// <param name="userId">当前用户编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>删除成功返回 true，会话不存在或无权访问返回 false</returns>
    public Task<Boolean> DeleteConversationAsync(Int64 conversationId, Int32 userId, CancellationToken cancellationToken)
    {
        var entity = Conversation.FindById(conversationId);
        if (entity == null || entity.UserId != userId) return Task.FromResult(false);

        // 软删除：保留历史记录和用量记录，仅标记为停用
        entity.Enable = false;
        entity.Update();

        // 软删除关联消息（不参与历史上下文构建，不展示给用户）
        var messages = ChatMessage.FindAllByConversationId(conversationId);
        foreach (var msg in messages)
        {
            msg.Enable = false;
            msg.Update();
        }

        // 删除关联的共享链接（分享链接在会话删除后应失效）
        var shares = SharedConversation.FindAllByConversationId(conversationId);
        shares.Delete();

        // 不删除用量记录：Token 消耗是实际发生的，不因会话删除而消除

        return Task.FromResult(true);
    }

    /// <summary>若会话无已启用消息则软删除。专用于前端切走时自动清理空会话，服务端权威判断防止前端误删</summary>
    /// <param name="conversationId">会话编号</param>
    /// <param name="userId">当前用户编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>true=会话确为空已删除；false=会话含消息未删除；null=会话不存在或无权访问</returns>
    public Task<Boolean?> DeleteIfEmptyAsync(Int64 conversationId, Int32 userId, CancellationToken cancellationToken)
    {
        var entity = Conversation.FindById(conversationId);
        if (entity == null || entity.UserId != userId || !entity.Enable) return Task.FromResult<Boolean?>(null);

        // 服务端权威判断：是否存在已启用消息
        if (ChatMessage.CountByConversationId(conversationId) > 0) return Task.FromResult<Boolean?>(false);

        // 真正的空会话：软删除
        entity.Enable = false;
        entity.Update();

        return Task.FromResult<Boolean?>(true);
    }

    /// <summary>置顶/取消置顶</summary>
    /// <param name="conversationId">会话编号</param>
    /// <param name="isPinned">是否置顶</param>
    /// <param name="userId">当前用户编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作成功返回 true，会话不存在或无权访问返回 false</returns>
    public Task<Boolean> SetPinAsync(Int64 conversationId, Boolean isPinned, Int32 userId, CancellationToken cancellationToken)
    {
        var entity = Conversation.FindById(conversationId);
        if (entity == null || entity.UserId != userId) return Task.FromResult(false);

        entity.IsPinned = isPinned;
        entity.Update();

        return Task.FromResult(true);
    }

    /// <summary>验证当前用户是否有权访问指定会话</summary>
    /// <param name="conversationId">会话编号</param>
    /// <param name="userId">当前用户编号</param>
    /// <returns>会话存在且属于该用户则返回 true</returns>
    public Boolean CanAccessConversation(Int64 conversationId, Int32 userId)
    {
        var conv = Conversation.FindById(conversationId);
        return conv != null && conv.UserId == userId && conv.Enable;
    }

    /// <summary>验证当前用户是否有权访问指定消息（通过所属会话校验）</summary>
    /// <param name="messageId">消息编号</param>
    /// <param name="userId">当前用户编号</param>
    /// <returns>消息及所属会话存在且属于该用户则返回 true</returns>
    public Boolean CanAccessMessage(Int64 messageId, Int32 userId)
    {
        var msg = ChatMessage.FindById(messageId);
        if (msg == null) return false;
        var conv = Conversation.FindById(msg.ConversationId);
        return conv != null && conv.UserId == userId;
    }
    #endregion

    #region 消息管理
    /// <summary>获取会话消息列表。批量查询反馈信息，避免 N+1 查询</summary>
    /// <param name="conversationId">会话编号</param>
    /// <param name="userId">当前用户编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>消息列表，会话不存在或无权访问时返回 null</returns>
    public Task<IReadOnlyList<MessageDto>?> GetMessagesAsync(Int64 conversationId, Int32 userId, CancellationToken cancellationToken)
    {
        var conversation = Conversation.FindById(conversationId);
        if (conversation == null || conversation.UserId != userId) return Task.FromResult<IReadOnlyList<MessageDto>?>(null);

        //var p = new PageParameter { PageSize = 0, Sort = ChatMessage._.CreateTime.Asc() };
        //var list = ChatMessage.Search(conversationId, default, DateTime.MinValue, DateTime.MinValue, null, p);
        var list = ChatMessage.FindAllByConversationIdOrdered(conversationId)
            .Where(e => e.IsMain).ToList();

        var items = list.Select(e => ToMessageDto(e)).ToList();
        return Task.FromResult<IReadOnlyList<MessageDto>?>(items);
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
            ConversationTitle = e.Title ?? "",
            Role = e.Role ?? "user",
            Content = e.Content ?? "",
            CreateTime = e.CreateTime,
        }).ToList();

        return new PagedResultDto<MessageSearchResultDto>(msgItems, (Int32)p.TotalCount, page, pageSize);
    }

    /// <summary>编辑消息内容（仅修改文字，不重新生成）</summary>
    /// <param name="messageId">消息编号</param>
    /// <param name="request">编辑请求</param>
    /// <param name="userId">当前用户编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>更新后的消息 DTO，消息不存在或无权访问时返回 null</returns>
    public Task<MessageDto?> EditMessageAsync(Int64 messageId, EditMessageRequest request, Int32 userId, CancellationToken cancellationToken)
    {
        var entity = ChatMessage.FindById(messageId);
        if (entity == null) return Task.FromResult<MessageDto?>(null);

        var conv = Conversation.FindById(entity.ConversationId);
        if (conv == null || conv.UserId != userId) return Task.FromResult<MessageDto?>(null);

        entity.Content = request.Content;
        entity.Update();

        return Task.FromResult<MessageDto?>(ToMessageDto(entity));
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
        if (msg == null) return Task.CompletedTask;

        msg.FeedbackType = request.Type;
        msg.FeedbackReason = request.Reason;
        msg.Update();

        return Task.CompletedTask;
    }

    /// <summary>取消反馈</summary>
    /// <param name="messageId">消息编号</param>
    /// <param name="userId">当前用户编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public Task DeleteFeedbackAsync(Int64 messageId, Int32 userId, CancellationToken cancellationToken)
    {
        var msg = ChatMessage.FindById(messageId);
        if (msg != null)
        {
            msg.FeedbackType = FeedbackType.None;
            msg.FeedbackReason = null;
            msg.Update();
        }

        return Task.CompletedTask;
    }
    #endregion

    #region 分享
    /// <summary>创建共享链接。按当前消息进度生成快照，支持设置有效期</summary>
    /// <param name="conversationId">会话编号</param>
    /// <param name="request">创建分享请求</param>
    /// <param name="user">当前操作用户</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>包含分享 URL 的 DTO，会话不存在或无权访问时返回 null</returns>
    public Task<ShareLinkDto?> CreateShareLinkAsync(Int64 conversationId, CreateShareRequest request, IUser user, CancellationToken cancellationToken)
    {
        var conversation = Conversation.FindById(conversationId);
        if (conversation == null || conversation.UserId != user.ID) return Task.FromResult<ShareLinkDto?>(null);

        // 获取当前最后一条消息的编号作为快照截止点
        var snapshotMessageId = ChatMessage.FindLastByConversationId(conversationId)?.Id ?? 0;

        DateTime? expireTime = null;
        if (request.ExpireHours is > 0)
            expireTime = DateTime.Now.AddHours(request.ExpireHours.Value);
        else if (ChatSetting.Current.ShareExpireMinutes > 0)
            expireTime = DateTime.Now.AddMinutes(ChatSetting.Current.ShareExpireMinutes);

        var entity = new SharedConversation
        {
            ConversationId = conversationId,
            ShareToken = Guid.NewGuid().ToString("N"),
            SnapshotTitle = conversation.Title,
            SnapshotMessageId = snapshotMessageId,
            ExpireTime = expireTime ?? DateTime.MinValue,
            CreateUserID = user.ID,
            CreateUser = user.DisplayName ?? user.Name,
        };
        entity.Insert();

        var dto = new ShareLinkDto($"/share/{entity.ShareToken}", entity.CreateTime, expireTime);
        return Task.FromResult<ShareLinkDto?>(dto);
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

        // 获取快照范围内的已启用消息（FindByShareSnapshot 已过滤 Enable == true）
        var messages = ChatMessage.FindByShareSnapshot(share.ConversationId, share.SnapshotMessageId);
        var items = messages.Select(m =>
        {
            return new
            {
                Id = m.Id.ToString(),
                ConversationId = m.ConversationId.ToString(),
                m.Role,
                Content = m.Content ?? String.Empty,
                CreatedAt = m.CreateTime,
                // ThinkingContent 和 ToolCalls 不对外暴露（分享页仅展示主要内容）
            };
        }).ToList();

        var result = new
        {
            ConversationId = share.ConversationId.ToString(),
            Messages = items,
            share.CreateTime,
            ExpireTime = share.ExpireTime > DateTime.MinValue ? (DateTime?)share.ExpireTime : null,
            AnchorMessageId = share.SnapshotMessageId > 0 ? share.SnapshotMessageId.ToString() : (String?)null,
            share.SnapshotTitle,
            CreatorName = share.CreateUser,
            SiteTitle = ChatSetting.Current.SiteTitle,
        };
        return Task.FromResult<Object?>(result);
    }

    /// <summary>撤销共享链接</summary>
    /// <param name="token">分享令牌</param>
    /// <param name="userId">当前用户编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>撤销成功返回 true，链接不存在或无权操作返回 false</returns>
    public Task<Boolean> RevokeShareLinkAsync(String token, Int32 userId, CancellationToken cancellationToken)
    {
        var share = SharedConversation.FindByShareToken(token);
        if (share == null || share.CreateUserID != userId) return Task.FromResult(false);

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
                new ModelInfoDto(0, "qwen-max", "Qwen-Max", true, true, true, false, false, false, false, 131_072),
                new ModelInfoDto(0, "deepseek-r1", "DeepSeek-R1", true, true, false, false, false, false, false, 65_536),
                new ModelInfoDto(0, "gpt-4o", "GPT-4o", true, true, true, false, false, false, false, 128_000),
            });
        }

        var models = list.Select(e => new ModelInfoDto(e.Id, e.Code ?? String.Empty, e.Name ?? String.Empty, e.SupportThinking, e.SupportFunction, e.SupportVision, e.SupportAudio, e.SupportImage, e.SupportVideo, e.SupportEmbedding, e.ContextLength, e.ProviderInfo?.Name ?? "")).ToArray();
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
            entity = new UserSetting
            {
                UserId = userId,
                Language = "zh-CN",
                Theme = "system",
                FontSize = 16,
                SendShortcut = "Enter",
                ContextRounds = 10,
                DefaultSkill = "general",
                EnableLearning = true,
            };
            if (userId > 0) entity.Insert(); // 新登录用户首次访问，持久化默认设置
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
        entity.ThinkingCollapsed = settings.ThinkingCollapsed;
        entity.DefaultSkill = settings.DefaultSkill;
        entity.EnableLearning = settings.EnableLearning;
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
        var conversations = Conversation.FindAllByUserId(userId, Int32.MaxValue);

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

    /// <summary>导入用户数据。接受与导出格式一致的 JSON</summary>
    /// <param name="userId">当前用户编号</param>
    /// <param name="stream">JSON 数据流</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>导入的会话数量</returns>
    public async Task<Int32> ImportUserDataAsync(Int32 userId, Stream stream, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var json = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        if (json.IsNullOrWhiteSpace()) return 0;

        var items = json.ToJsonEntity<List<ImportConversationItem>>();
        if (items == null || items.Count == 0) return 0;

        var count = 0;
        foreach (var item in items)
        {
            // 创建会话
            var conv = new Conversation
            {
                UserId = userId,
                Title = item.Title,
                ModelId = item.ModelId,
                IsPinned = item.IsPinned,
                CreateTime = item.CreateTime,
                LastMessageTime = item.LastMessageTime,
                Enable = true,
            };
            conv.Insert();

            // 导入消息
            if (item.Messages != null)
            {
                foreach (var msg in item.Messages)
                {
                    var entity = new ChatMessage
                    {
                        ConversationId = conv.Id,
                        Role = msg.Role,
                        Content = msg.Content,
                        ThinkingContent = msg.ThinkingContent,
                        ThinkingMode = (ThinkingMode)msg.ThinkingMode,
                        Attachments = msg.Attachments,
                        Enable = true,
                        CreateTime = msg.CreateTime,
                        CreateUserID = userId,
                    };
                    entity.Insert();
                }

                conv.MessageCount = item.Messages.Count;
                conv.Update();
            }

            count++;
        }

        return count;
    }

    private class ImportConversationItem
    {
        public String? Title { get; set; }
        public Int32 ModelId { get; set; }
        public Boolean IsPinned { get; set; }
        public DateTime CreateTime { get; set; }
        public DateTime LastMessageTime { get; set; }
        public List<ImportMessageItem>? Messages { get; set; }
    }

    private class ImportMessageItem
    {
        public String? Role { get; set; }
        public String? Content { get; set; }
        public String? ThinkingContent { get; set; }
        public Int32 ThinkingMode { get; set; }
        public String? Attachments { get; set; }
        public DateTime CreateTime { get; set; }
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

        // 删除关联的共享链接（分享链接在会话清除后应失效）
        var shares = SharedConversation.FindAllByConversationIds(convIds);
        shares.Delete();

        // 软删除关联消息
        var messages = ChatMessage.FindAllByConversationIds(convIds);
        foreach (var msg in messages)
        {
            msg.Enable = false;
            msg.Update();
        }

        // 软删除会话（保留历史记录和用量记录）
        foreach (var conv in conversations)
        {
            conv.Enable = false;
            conv.Update();
        }

        // 不删除用量记录：Token 消耗是实际发生的，不因会话清除而消除

        return Task.CompletedTask;
    }
    #endregion

    #region 辅助
    /// <summary>转换会话实体为摘要DTO</summary>
    /// <param name="entity">会话实体</param>
    /// <returns></returns>
    private static ConversationSummaryDto ToConversationSummary(Conversation entity) =>
        new(entity.Id, entity.Title ?? String.Empty, entity.ModelId, entity.LastMessageTime, entity.IsPinned);

    /// <summary>转换消息实体为DTO</summary>
    /// <param name="entity">消息实体</param>
    /// <returns></returns>
    public static MessageDto ToMessageDto(ChatMessage entity)
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
            FeedbackType = (Int32)entity.FeedbackType,
            FeedbackReason = entity.FeedbackReason,
            ModelName = entity.ModelName,
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
            ThinkingCollapsed = entity.ThinkingCollapsed,
            DefaultSkill = entity.DefaultSkill ?? "general",
            EnableLearning = entity.EnableLearning,
            ContentWidth = entity.ContentWidth,
        };
    #endregion
}
