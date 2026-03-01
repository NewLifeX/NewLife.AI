using NewLife.AI.Models;

namespace NewLife.AI.ChatAI.Contracts;

/// <summary>对话应用服务。定义对话系统的核心业务契约</summary>
public interface IChatApplicationService
{
    #region 会话管理
    /// <summary>新建会话</summary>
    /// <param name="request">创建会话请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>会话摘要</returns>
    Task<ConversationSummaryDto> CreateConversationAsync(CreateConversationRequest request, CancellationToken cancellationToken);

    /// <summary>获取会话列表（分页）</summary>
    /// <param name="page">页码</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>分页会话列表</returns>
    Task<PagedResultDto<ConversationSummaryDto>> GetConversationsAsync(Int32 page, Int32 pageSize, CancellationToken cancellationToken);

    /// <summary>更新会话（重命名、切换模型等）</summary>
    /// <param name="conversationId">会话编号</param>
    /// <param name="request">更新请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>更新后的会话摘要，不存在时返回 null</returns>
    Task<ConversationSummaryDto?> UpdateConversationAsync(Int64 conversationId, UpdateConversationRequest request, CancellationToken cancellationToken);

    /// <summary>删除会话</summary>
    /// <param name="conversationId">会话编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否删除成功</returns>
    Task<Boolean> DeleteConversationAsync(Int64 conversationId, CancellationToken cancellationToken);

    /// <summary>置顶/取消置顶会话</summary>
    /// <param name="conversationId">会话编号</param>
    /// <param name="isPinned">是否置顶</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否操作成功</returns>
    Task<Boolean> SetPinAsync(Int64 conversationId, Boolean isPinned, CancellationToken cancellationToken);
    #endregion

    #region 消息
    /// <summary>获取会话消息列表</summary>
    /// <param name="conversationId">会话编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>消息列表</returns>
    Task<IReadOnlyList<MessageDto>> GetMessagesAsync(Int64 conversationId, CancellationToken cancellationToken);

    /// <summary>编辑消息内容</summary>
    /// <param name="messageId">消息编号</param>
    /// <param name="request">编辑请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>编辑后的消息，不存在时返回 null</returns>
    Task<MessageDto?> EditMessageAsync(Int64 messageId, EditMessageRequest request, CancellationToken cancellationToken);

    /// <summary>重新生成回复。直接替换当前 AI 回复，不保留分支</summary>
    /// <param name="messageId">消息编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>重新生成的消息，不存在时返回 null</returns>
    Task<MessageDto?> RegenerateMessageAsync(Int64 messageId, CancellationToken cancellationToken);

    /// <summary>流式发送消息并获取 AI 回复。返回 SSE 事件流</summary>
    /// <param name="conversationId">会话编号</param>
    /// <param name="request">发送消息请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>ChatStreamEvent 事件流</returns>
    IAsyncEnumerable<ChatStreamEvent> StreamMessageAsync(Int64 conversationId, SendMessageRequest request, CancellationToken cancellationToken);

    /// <summary>编辑用户消息并重新发送。更新消息内容、删除后续所有消息、流式生成新 AI 回复</summary>
    /// <param name="messageId">用户消息编号</param>
    /// <param name="newContent">编辑后的内容</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>ChatStreamEvent 事件流</returns>
    IAsyncEnumerable<ChatStreamEvent> EditAndResendStreamAsync(Int64 messageId, String newContent, CancellationToken cancellationToken);

    /// <summary>流式重新生成回复。替换当前 AI 回复，以 SSE 事件流返回</summary>
    /// <param name="messageId">消息编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>ChatStreamEvent 事件流</returns>
    IAsyncEnumerable<ChatStreamEvent> RegenerateStreamAsync(Int64 messageId, CancellationToken cancellationToken);

    /// <summary>停止生成</summary>
    /// <param name="messageId">消息编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    Task StopGenerateAsync(Int64 messageId, CancellationToken cancellationToken);

    /// <summary>异步生成会话标题。首条消息发送后调用，不阻塞主流程</summary>
    /// <param name="conversationId">会话编号</param>
    /// <param name="userMessage">用户首条消息内容</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>生成的标题，失败时返回 null</returns>
    Task<String?> GenerateTitleAsync(Int64 conversationId, String userMessage, CancellationToken cancellationToken);
    #endregion

    #region 反馈
    /// <summary>提交点赞/点踩反馈</summary>
    /// <param name="messageId">消息编号</param>
    /// <param name="request">反馈请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    Task SubmitFeedbackAsync(Int64 messageId, FeedbackRequest request, CancellationToken cancellationToken);

    /// <summary>取消反馈</summary>
    /// <param name="messageId">消息编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    Task DeleteFeedbackAsync(Int64 messageId, CancellationToken cancellationToken);
    #endregion

    #region 分享
    /// <summary>创建共享链接</summary>
    /// <param name="conversationId">会话编号</param>
    /// <param name="request">分享请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>分享链接信息</returns>
    Task<ShareLinkDto> CreateShareLinkAsync(Int64 conversationId, CreateShareRequest request, CancellationToken cancellationToken);

    /// <summary>获取共享对话内容（公开接口，无需认证）</summary>
    /// <param name="token">分享令牌</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>共享对话内容，过期或不存在时返回 null</returns>
    Task<Object?> GetShareContentAsync(String token, CancellationToken cancellationToken);

    /// <summary>撤销共享链接</summary>
    /// <param name="token">分享令牌</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否撤销成功</returns>
    Task<Boolean> RevokeShareLinkAsync(String token, CancellationToken cancellationToken);
    #endregion

    #region 附件/模型/设置
    /// <summary>上传附件</summary>
    /// <param name="fileName">文件名</param>
    /// <param name="size">文件大小</param>
    /// <param name="stream">文件流</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>上传结果（附件 ID 和 URL）</returns>
    Task<UploadAttachmentResult> UploadAttachmentAsync(String fileName, Int64 size, Stream stream, CancellationToken cancellationToken);

    /// <summary>获取可用模型列表</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>模型信息数组</returns>
    Task<ModelInfoDto[]> GetModelsAsync(CancellationToken cancellationToken);

    /// <summary>获取用户设置</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>用户设置</returns>
    Task<UserSettingsDto> GetUserSettingsAsync(CancellationToken cancellationToken);

    /// <summary>更新用户设置</summary>
    /// <param name="settings">用户设置</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>更新后的用户设置</returns>
    Task<UserSettingsDto> UpdateUserSettingsAsync(UserSettingsDto settings, CancellationToken cancellationToken);

    /// <summary>导出所有对话数据</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>导出数据流</returns>
    Task<Stream> ExportUserDataAsync(CancellationToken cancellationToken);

    /// <summary>清除所有对话</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    Task ClearUserConversationsAsync(CancellationToken cancellationToken);
    #endregion
}
