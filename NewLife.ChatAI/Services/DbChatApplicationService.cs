using System.Runtime.CompilerServices;
using System.Text;
using NewLife.AI.ChatAI.Contracts;
using NewLife.ChatAI.Entity;
using NewLife.Cube.Entity;
using NewLife.Data;
using NewLife.Log;
using NewLife.Serialization;
using XCode;

namespace NewLife.ChatAI.Services;

/// <summary>数据库版对话应用服务。基于 XCode 实体类持久化数据</summary>
public class DbChatApplicationService : IChatApplicationService
{
    #region 属性
    private readonly ILog _log;

    /// <summary>附件存储根目录</summary>
    private readonly String _attachmentRoot;
    #endregion

    #region 构造
    /// <summary>实例化数据库版对话应用服务</summary>
    /// <param name="log">日志</param>
    public DbChatApplicationService(ILog log)
    {
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

        // 删除关联的消息
        var messages = ChatMessage.FindAll(ChatMessage._.ConversationId == conversationId);
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

    /// <summary>重新生成AI回复</summary>
    /// <param name="messageId">消息编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public Task<MessageDto?> RegenerateMessageAsync(Int64 messageId, CancellationToken cancellationToken)
    {
        var entity = ChatMessage.FindById(messageId);
        if (entity == null || !entity.Role.EqualIgnoreCase("assistant"))
            return Task.FromResult<MessageDto?>(null);

        // 占位实现，后续接入真实模型推理
        entity.Content = "这是重新生成的回复。后续将接入真实模型推理。";
        entity.Update();

        return Task.FromResult<MessageDto?>(ToMessageDto(entity));
    }

    /// <summary>流式发送消息并获取AI回复</summary>
    /// <param name="conversationId">会话编号</param>
    /// <param name="request">发送消息请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public async IAsyncEnumerable<String> StreamMessageAsync(Int64 conversationId, SendMessageRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
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

        // 占位流式回复，后续接入真实模型推理与上下文管理
        var answer = "这是流式回复骨架。后续可接入真实模型推理与上下文管理。";
        var chunks = answer.Split('。', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var content = new StringBuilder();
        foreach (var chunk in chunks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = chunk + "。";
            content.Append(line);
            yield return line;
            await Task.Delay(120, cancellationToken).ConfigureAwait(false);
        }

        // 保存AI回复消息
        var assistantMsg = new ChatMessage
        {
            ConversationId = conversationId,
            Role = "assistant",
            Content = content.ToString(),
            ThinkingMode = (Int32)request.ThinkingMode,
        };
        assistantMsg.Insert();

        // 更新会话的最后消息时间和消息数
        conversation.LastMessageTime = DateTime.Now;
        conversation.MessageCount = (Int32)ChatMessage.FindCount(ChatMessage._.ConversationId == conversationId);
        conversation.Update();
    }

    /// <summary>中断生成</summary>
    /// <param name="messageId">消息编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public Task StopGenerateAsync(Int64 messageId, CancellationToken cancellationToken) => Task.CompletedTask;
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
                new ModelInfoDto("qwen-max", "Qwen-Max", true, true),
                new ModelInfoDto("deepseek-r1", "DeepSeek-R1", true, false),
                new ModelInfoDto("gpt-4o", "GPT-4o", true, true),
            });
        }

        var models = list.Select(e => new ModelInfoDto(e.Code, e.Name, e.SupportThinking, e.SupportVision)).ToArray();
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
    /// <summary>转换会话实体为摘要DTO</summary>
    /// <param name="entity">会话实体</param>
    /// <returns></returns>
    private static ConversationSummaryDto ToConversationSummary(Conversation entity) =>
        new(entity.Id, entity.Title, entity.ModelCode, entity.LastMessageTime, entity.IsPinned);

    /// <summary>转换消息实体为DTO</summary>
    /// <param name="entity">消息实体</param>
    /// <returns></returns>
    private static MessageDto ToMessageDto(ChatMessage entity) =>
        new(entity.Id, entity.ConversationId, entity.Role, entity.Content, (ThinkingMode)entity.ThinkingMode, entity.CreateTime);

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
