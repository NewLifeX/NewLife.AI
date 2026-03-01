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

    IAsyncEnumerable<ChatStreamEvent> StreamMessageAsync(Int64 conversationId, SendMessageRequest request, CancellationToken cancellationToken);
    Task StopGenerateAsync(Int64 messageId, CancellationToken cancellationToken);

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
