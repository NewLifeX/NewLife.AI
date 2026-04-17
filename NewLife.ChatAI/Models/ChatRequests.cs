using NewLife.AI.Models;

namespace NewLife.ChatAI.Models;

/// <summary>新建会话请求</summary>
public record CreateConversationRequest(String? Title, Int32 ModelId);

/// <summary>更新会话请求</summary>
public record UpdateConversationRequest(String? Title, Int32 ModelId);

/// <summary>编辑消息请求</summary>
public record EditMessageRequest(String Content);

/// <summary>反馈请求</summary>
public record FeedbackRequest(FeedbackType Type, String? Reason, Boolean? AllowTraining);

/// <summary>创建分享请求</summary>
public record CreateShareRequest(Int32? ExpireHours);

/// <summary>上传附件结果</summary>
public record UploadAttachmentResult(Int64 Id, String FileName, String Url, Int64 Size);

/// <summary>附件元信息</summary>
public record AttachmentInfoResult(Int64 Id, String FileName, Int64 Size, String Url, Boolean IsImage);
