namespace NewLife.AI.Models;

/// <summary>发送消息请求。前端发起对话/编辑/重生的统一入参</summary>
/// <param name="Content">用户消息文本（多模态场景为 JSON 编码的内容数组）</param>
/// <param name="ThinkingMode">思考模式</param>
/// <param name="AttachmentIds">附件编号列表（前端上传后返回的 Id 字符串）</param>
public record SendMessageRequest(String Content, ThinkingMode ThinkingMode, IReadOnlyList<String>? AttachmentIds)
{
    /// <summary>推理强度。由模型 ReasoningEfforts 字段定义可选值，如 high/max</summary>
    public String? ReasoningEffort { get; init; }

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

    /// <summary>语音合成模型编码。非空时表示 TTS 模式：服务端将文字转为音频并作为助手消息返回，不调用 AI 推理</summary>
    public String? TtsModel { get; init; }

    /// <summary>语音合成音色。TTS 模式专用</summary>
    public String? TtsVoice { get; init; }

    /// <summary>语音合成格式。mp3/wav/pcm，TTS 模式专用</summary>
    public String? TtsFormat { get; init; }

    /// <summary>语速倍率。0.5~2.0，TTS 模式专用</summary>
    public Double? TtsSpeed { get; init; }

    /// <summary>采样率。24000/16000/8000，TTS 模式专用</summary>
    public Int32? TtsSampleRate { get; init; }

    /// <summary>音量。0~100，默认 50，TTS 模式专用</summary>
    public Int32? TtsVolume { get; init; }

    /// <summary>音调。0.5~2.0，默认 1.0，TTS 模式专用</summary>
    public Double? TtsPitch { get; init; }
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

    /// <summary>反馈原因。点踩时的具体原因</summary>
    public String? FeedbackReason { get; set; }

    /// <summary>模型编码。实际使用的模型编码，如 gpt-4o</summary>
    public String? ModelName { get; set; }

    /// <summary>本次对话费用（元）</summary>
    public Decimal TotalCost { get; set; }
}
