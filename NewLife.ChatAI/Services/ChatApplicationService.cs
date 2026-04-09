using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using NewLife.AI.Models;
using NewLife.ChatAI.Entity;
using NewLife.ChatAI.Models;
using NewLife.Collections;
using NewLife.Cube.Entity;
using NewLife.Data;
using NewLife.Log;
using NewLife.Serialization;
using XCode;
using XCode.Membership;
using AiChatMessage = NewLife.AI.Models.ChatMessage;
using ChatMessage = NewLife.ChatAI.Entity.ChatMessage;

namespace NewLife.ChatAI.Services;

/// <summary>数据库版对话应用服务。基于 XCode 实体类持久化数据</summary>
/// <remarks>
/// 对话内核层：负责会话与消息的持久化管理、分享、反馈、模型与用户设置等功能。
/// 生成相关方法已提取到 <see cref="MessageService"/>。
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
    #endregion
}
