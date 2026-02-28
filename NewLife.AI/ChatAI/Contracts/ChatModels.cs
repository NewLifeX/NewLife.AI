namespace NewLife.AI.ChatAI.Contracts;

/// <summary>思考模式</summary>
public enum ThinkingMode
{
    Auto = 0,
    Think = 1,
    Fast = 2
}

/// <summary>反馈类型</summary>
public enum FeedbackType
{
    Like = 1,
    Dislike = 2
}

/// <summary>模型信息</summary>
public record ModelInfoDto(String Code, String Name, Boolean SupportThinking, Boolean SupportVision, Boolean SupportImageGeneration, Boolean SupportFunctionCalling);

/// <summary>会话摘要</summary>
public record ConversationSummaryDto(Int64 Id, String Title, String ModelCode, DateTime LastMessageTime, Boolean IsPinned);

/// <summary>消息数据</summary>
public record MessageDto(Int64 Id, Int64 ConversationId, String Role, String Content, String? ThinkingContent, ThinkingMode ThinkingMode, String? Attachments, DateTime CreateTime);

/// <summary>附件信息</summary>
public record AttachmentInfoDto(Int64 Id, String FileName, Int64 Size, String Url, Boolean IsImage);

/// <summary>分页结果</summary>
public record PagedResultDto<T>(IReadOnlyList<T> Items, Int32 Total, Int32 Page, Int32 PageSize);

/// <summary>分享链接</summary>
public record ShareLinkDto(String Url, DateTime CreateTime, DateTime? ExpireTime);

/// <summary>用户设置</summary>
public record UserSettingsDto(String Language, String Theme, Int32 FontSize, String SendShortcut, String DefaultModel, ThinkingMode DefaultThinkingMode, Int32 ContextRounds, String SystemPrompt, Boolean AllowTraining);
