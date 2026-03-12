namespace NewLife.AI.ChatAI;

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
    /// <summary>无反馈</summary>
    None = 0,
    /// <summary>点赞</summary>
    Like = 1,
    /// <summary>点踩</summary>
    Dislike = 2
}

/// <summary>消息状态</summary>
public enum MessageStatus
{
    /// <summary>流式输出中</summary>
    Streaming = 0,
    /// <summary>已完成</summary>
    Done = 1,
    /// <summary>出错</summary>
    Error = 2
}

/// <summary>工具调用状态</summary>
public enum ToolCallStatus
{
    /// <summary>调用中</summary>
    Calling = 0,
    /// <summary>已完成</summary>
    Done = 1,
    /// <summary>出错</summary>
    Error = 2
}

/// <summary>MCP传输类型</summary>
public enum McpTransportType
{
    /// <summary>HTTP</summary>
    Http = 0,
    /// <summary>SSE（Server-Sent Events）</summary>
    Sse = 1,
    /// <summary>标准输入输出</summary>
    Stdio = 2,
}

/// <summary>模型信息</summary>
public record ModelInfoDto(String Code, String Name, Boolean SupportThinking, Boolean SupportVision, Boolean SupportImageGeneration, Boolean SupportFunctionCalling, String? Provider = null);

/// <summary>工具调用信息</summary>
public record ToolCallDto(String Id, String Name, ToolCallStatus Status, String? Arguments = null, String? Result = null);

/// <summary>会话摘要</summary>
public record ConversationSummaryDto(Int64 Id, String Title, String ModelCode, DateTime LastMessageTime, Boolean IsPinned)
{
    /// <summary>会话图标</summary>
    public String? Icon { get; set; }

    /// <summary>图标颜色</summary>
    public String? IconColor { get; set; }
};

/// <summary>消息数据</summary>
public record MessageDto(Int64 Id, Int64 ConversationId, String Role, String Content, String? ThinkingContent, ThinkingMode ThinkingMode, String? Attachments, DateTime CreateTime)
{
    /// <summary>消息状态</summary>
    public MessageStatus Status { get; set; } = MessageStatus.Done;

    /// <summary>工具调用列表</summary>
    public IReadOnlyList<ToolCallDto>? ToolCalls { get; set; }

    /// <summary>提示Token数</summary>
    public Int32 PromptTokens { get; set; }

    /// <summary>回复Token数</summary>
    public Int32 CompletionTokens { get; set; }

    /// <summary>总Token数</summary>
    public Int32 TotalTokens { get; set; }

    /// <summary>反馈类型。Like=1, Dislike=2, 0=无反馈</summary>
    public Int32 FeedbackType { get; set; }
};

/// <summary>附件信息</summary>
public record AttachmentInfoDto(Int64 Id, String FileName, Int64 Size, String Url, Boolean IsImage);

/// <summary>分页结果</summary>
public record PagedResultDto<T>(IReadOnlyList<T> Items, Int32 Total, Int32 Page, Int32 PageSize);

/// <summary>分享链接</summary>
public record ShareLinkDto(String Url, DateTime CreateTime, DateTime? ExpireTime);

/// <summary>用户设置</summary>
public record UserSettingsDto(String Language, String Theme, Int32 FontSize, String SendShortcut, String DefaultModel, ThinkingMode DefaultThinkingMode, Int32 ContextRounds, String SystemPrompt, Boolean AllowTraining)
{
    /// <summary>是否启用 MCP</summary>
    public Boolean McpEnabled { get; set; } = true;

    /// <summary>默认技能</summary>
    public String DefaultSkill { get; set; } = "general";

    /// <summary>流式输出速度</summary>
    public Int32 StreamingSpeed { get; set; } = 3;
};

/// <summary>用户资料</summary>
public record UserProfileDto(String Nickname, String Account, String? Avatar);
