namespace NewLife.AI.Models;

/// <summary>发送消息请求。前端发起对话/编辑/重生的统一入参</summary>
/// <param name="Content">用户消息文本（多模态场景为 JSON 编码的内容数组）</param>
/// <param name="ThinkingMode">思考模式</param>
/// <param name="AttachmentIds">附件编号列表（前端上传后返回的 Id 字符串）</param>
public record SendMessageRequest(String Content, ThinkingMode ThinkingMode, IReadOnlyList<String>? AttachmentIds)
{
    /// <summary>技能编码。激活对应技能的系统提示词</summary>
    public String? SkillCode { get; init; }

    /// <summary>模型编号。当会话未绑定模型时，使用此字段指定的模型</summary>
    public Int32 ModelId { get; init; }

    /// <summary>扩展选项。传递服务商专属参数，最终通过 ChatOptions.Items 注入管道。
    /// 支持的键（DashScope 原生协议）：
    /// EnableSearch(bool) / SearchStrategy(string) / EnableSource(bool) / ForcedSearch(bool)
    /// ThinkingBudget(int) / TopK(int) / Seed(int) / N(int)
    /// RepetitionPenalty(double) / Logprobs(bool) / TopLogprobs(int)
    /// EnableCodeInterpreter(bool) / VlHighResolutionImages(bool) / MaxPixels(int)
    /// </summary>
    public IDictionary<String, Object?>? Options { get; init; }
}

/// <summary>消息数据。向前端返回的消息 DTO，包含内容、思考、工具调用、用量统计与反馈</summary>
/// <param name="Id">消息编号</param>
/// <param name="ConversationId">会话编号</param>
/// <param name="Role">角色（user/assistant/system/tool）</param>
/// <param name="Content">消息正文</param>
/// <param name="ThinkingContent">思考内容</param>
/// <param name="ThinkingMode">思考模式</param>
/// <param name="Attachments">附件 Id 列表（JSON 字符串）</param>
/// <param name="CreateTime">创建时间</param>
public record MessageDto(Int64 Id, Int64 ConversationId, String Role, String Content, String? ThinkingContent, ThinkingMode ThinkingMode, String? Attachments, DateTime CreateTime)
{
    /// <summary>消息状态</summary>
    public MessageStatus Status { get; set; } = MessageStatus.Done;

    /// <summary>工具调用列表</summary>
    public IReadOnlyList<ToolCallDto>? ToolCalls { get; set; }

    /// <summary>输入Token数</summary>
    public Int32 InputTokens { get; set; }

    /// <summary>输出Token数</summary>
    public Int32 OutputTokens { get; set; }

    /// <summary>总Token数</summary>
    public Int32 TotalTokens { get; set; }

    /// <summary>反馈类型。Like=1, Dislike=2, 0=无反馈</summary>
    public Int32 FeedbackType { get; set; }
}
