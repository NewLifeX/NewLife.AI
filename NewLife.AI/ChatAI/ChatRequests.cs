namespace NewLife.AI.ChatAI;

/// <summary>新建会话请求</summary>
public record CreateConversationRequest(String? Title, Int32 ModelId);

/// <summary>更新会话请求</summary>
public record UpdateConversationRequest(String? Title, Int32 ModelId);

/// <summary>发送消息请求</summary>
public record SendMessageRequest(String Content, ThinkingMode ThinkingMode, IReadOnlyList<String>? AttachmentIds)
{
    /// <summary>技能编码。激活对应技能的系统提示词</summary>
    public String? SkillCode { get; init; }

    /// <summary>模型编号。当会话未绑定模型时，使用此字段指定的模型</summary>
    public Int32 ModelId { get; init; }
};

/// <summary>编辑消息请求</summary>
public record EditMessageRequest(String Content);

/// <summary>反馈请求</summary>
public record FeedbackRequest(FeedbackType Type, String? Reason, Boolean? AllowTraining);

/// <summary>创建分享请求</summary>
public record CreateShareRequest(Int32? ExpireHours);

/// <summary>上传附件结果</summary>
public record UploadAttachmentResult(String Id, String FileName, String Url, Int64 Size);
