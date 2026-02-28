namespace NewLife.AI.ChatAI.Contracts;

/// <summary>对话应用服务</summary>
public interface IChatApplicationService
{
    Task<ConversationSummaryDto> CreateConversationAsync(CreateConversationRequest request, CancellationToken cancellationToken);
    Task<PagedResultDto<ConversationSummaryDto>> GetConversationsAsync(Int32 page, Int32 pageSize, CancellationToken cancellationToken);
    Task<ConversationSummaryDto?> UpdateConversationAsync(Int64 conversationId, UpdateConversationRequest request, CancellationToken cancellationToken);
    Task<Boolean> DeleteConversationAsync(Int64 conversationId, CancellationToken cancellationToken);
    Task<Boolean> SetPinAsync(Int64 conversationId, Boolean isPinned, CancellationToken cancellationToken);

    Task<IReadOnlyList<MessageDto>> GetMessagesAsync(Int64 conversationId, CancellationToken cancellationToken);
    Task<MessageDto?> EditMessageAsync(Int64 messageId, EditMessageRequest request, CancellationToken cancellationToken);
    Task<MessageDto?> RegenerateMessageAsync(Int64 messageId, CancellationToken cancellationToken);

    IAsyncEnumerable<String> StreamMessageAsync(Int64 conversationId, SendMessageRequest request, CancellationToken cancellationToken);
    Task StopGenerateAsync(Int64 messageId, CancellationToken cancellationToken);

    /// <summary>异步生成会话标题。首条消息发送后调用，不阻塞主流程</summary>
    /// <param name="conversationId">会话编号</param>
    /// <param name="userMessage">用户首条消息内容</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>生成的标题，失败时返回 null</returns>
    Task<String?> GenerateTitleAsync(Int64 conversationId, String userMessage, CancellationToken cancellationToken);

    Task SubmitFeedbackAsync(Int64 messageId, FeedbackRequest request, CancellationToken cancellationToken);
    Task DeleteFeedbackAsync(Int64 messageId, CancellationToken cancellationToken);

    Task<ShareLinkDto> CreateShareLinkAsync(Int64 conversationId, CreateShareRequest request, CancellationToken cancellationToken);
    Task<Object?> GetShareContentAsync(String token, CancellationToken cancellationToken);
    Task<Boolean> RevokeShareLinkAsync(String token, CancellationToken cancellationToken);

    Task<UploadAttachmentResult> UploadAttachmentAsync(String fileName, Int64 size, Stream stream, CancellationToken cancellationToken);
    Task<ModelInfoDto[]> GetModelsAsync(CancellationToken cancellationToken);
    Task<UserSettingsDto> GetUserSettingsAsync(CancellationToken cancellationToken);
    Task<UserSettingsDto> UpdateUserSettingsAsync(UserSettingsDto settings, CancellationToken cancellationToken);
    Task<Stream> ExportUserDataAsync(CancellationToken cancellationToken);
    Task ClearUserConversationsAsync(CancellationToken cancellationToken);
}
