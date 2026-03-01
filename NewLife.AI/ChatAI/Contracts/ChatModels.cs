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

/// <summary>模型信息</summary>
public record ModelInfoDto(String Code, String Name, Boolean SupportThinking, Boolean SupportVision);

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
public record MessageDto(Int64 Id, Int64 ConversationId, String Role, String Content, ThinkingMode ThinkingMode, DateTime CreateTime)
{
    /// <summary>消息状态</summary>
    public MessageStatus Status { get; set; } = MessageStatus.Done;

    /// <summary>思考内容</summary>
    public String? ThinkingContent { get; set; }

    /// <summary>工具调用列表</summary>
    public IReadOnlyList<ToolCallDto>? ToolCalls { get; set; }
};

/// <summary>SSE 流式事件。对话流式输出的结构化事件</summary>
/// <remarks>
/// 事件类型流程：message_start → thinking_delta* → content_delta* → tool_call_start → tool_call_done → message_done / error
/// </remarks>
public record ChatStreamEvent
{
    /// <summary>事件类型。message_start/thinking_delta/content_delta/tool_call_start/tool_call_done/message_done/error</summary>
    public String Type { get; set; } = "content_delta";

    /// <summary>消息编号。message_start 时返回</summary>
    public Int64? MessageId { get; set; }

    /// <summary>正文内容片段。content_delta 时返回</summary>
    public String? Content { get; set; }

    /// <summary>思考内容片段。thinking_delta 时返回</summary>
    public String? ThinkingContent { get; set; }

    /// <summary>工具调用信息。tool_call_start/tool_call_done 时返回</summary>
    public ToolCallDto? ToolCall { get; set; }

    /// <summary>错误信息。error 时返回</summary>
    public String? Error { get; set; }
}

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
