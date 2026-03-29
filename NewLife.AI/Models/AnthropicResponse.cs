using System.Runtime.Serialization;
using NewLife.Serialization;

namespace NewLife.AI.Models;

/// <summary>Anthropic Messages API 响应。兼容 https://docs.anthropic.com/en/api/messages 协议</summary>
/// <remarks>
/// 与 OpenAI ChatCompletionResponse 的主要差异：
/// <list type="bullet">
/// <item>顶级 type 为 "message"，role 为 "assistant"</item>
/// <item>回复内容为 content 数组（每项含 type 和 text），而非 choices</item>
/// <item>结束原因字段名 stop_reason（如 end_turn / max_tokens / tool_use）</item>
/// <item>Usage 使用 input_tokens/output_tokens 命名</item>
/// </list>
/// </remarks>
public class AnthropicResponse
{
    #region 属性
    /// <summary>响应编号</summary>
    public String? Id { get; set; }

    /// <summary>对象类型。固定 "message"</summary>
    public String? Type { get; set; }

    /// <summary>角色。固定 "assistant"</summary>
    public String? Role { get; set; }

    /// <summary>模型编码</summary>
    public String? Model { get; set; }

    /// <summary>内容块列表。[{type:"text", text:"..."}]</summary>
    public IList<AnthropicContentBlock>? Content { get; set; }

    /// <summary>停止原因。end_turn/max_tokens/tool_use</summary>
    [DataMember(Name = "stop_reason")]
    public String? StopReason { get; set; }

    /// <summary>令牌用量统计</summary>
    public AnthropicUsage? Usage { get; set; }
    #endregion

    #region 转换
    /// <summary>转换为内部统一 ChatResponse</summary>
    /// <param name="model">模型编码（Anthropic 响应可能不含模型信息）</param>
    /// <returns>等效的 ChatResponse 实例</returns>
    public ChatResponse ToChatResponse(String? model = null)
    {
        var response = new ChatResponse
        {
            Id = Id,
            Object = "chat.completion",
            Model = Model ?? model,
        };

        String? contentText = null;
        String? reasoningText = null;
        List<ToolCall>? toolCalls = null;

        if (Content != null)
        {
            var textParts = new List<String>();
            var reasoningParts = new List<String>();

            foreach (var block in Content)
            {
                if (block.Type == "text")
                    textParts.Add(block.Text ?? "");
                else if (block.Type == "thinking")
                    reasoningParts.Add(block.Thinking ?? block.Text ?? "");
                else if (block.Type == "tool_use")
                {
                    toolCalls ??= [];
                    var inputRaw = block.Input;
                    toolCalls.Add(new ToolCall
                    {
                        Id = block.Id ?? "",
                        Type = "function",
                        Function = new FunctionCall
                        {
                            Name = block.Name ?? "",
                            Arguments = inputRaw is IDictionary<String, Object> inputDic
                                ? inputDic.ToJson()
                                : inputRaw as String ?? "{}",
                        },
                    });
                }
            }

            contentText = textParts.Count > 0 ? String.Join("", textParts) : null;
            reasoningText = reasoningParts.Count > 0 ? String.Join("", reasoningParts) : null;
        }

        var finishReason = MapStopReason(StopReason);
        var choice = response.Add(contentText, reasoningText, finishReason);

        if (toolCalls != null && toolCalls.Count > 0)
        {
            choice.Message ??= new ChatMessage { Role = "assistant" };
            choice.Message.ToolCalls = toolCalls;
        }

        if (Usage != null)
        {
            response.Usage = new UsageDetails
            {
                InputTokens = Usage.InputTokens,
                OutputTokens = Usage.OutputTokens,
                TotalTokens = Usage.InputTokens + Usage.OutputTokens,
            };
        }

        return response;
    }

    /// <summary>映射 Anthropic stop_reason 到标准 finish_reason</summary>
    internal static String? MapStopReason(String? stopReason) => stopReason switch
    {
        "end_turn" => "stop",
        "max_tokens" => "length",
        "tool_use" => "tool_calls",
        null => null,
        _ => stopReason,
    };

    /// <summary>从内部统一响应转换为 Anthropic 非流式响应</summary>
    /// <param name="response">内部统一响应</param>
    /// <returns>Anthropic 格式响应</returns>
    public static AnthropicResponse From(ChatResponse response)
    {
        var content = new List<AnthropicContentBlock>();
        String? stopReason = null;

        if (response.Messages != null)
        {
            foreach (var choice in response.Messages)
            {
                var msg = choice.Message ?? choice.Delta;
                if (msg?.Content != null)
                {
                    var text = msg.Content is String s ? s : msg.Content.ToString();
                    content.Add(new AnthropicContentBlock { Type = "text", Text = text });
                }
                if (choice.FinishReason != null)
                    stopReason = MapFinishReason(choice.FinishReason);
            }
        }

        return new AnthropicResponse
        {
            Id = response.Id ?? $"msg_{Guid.NewGuid():N}",
            Type = "message",
            Role = "assistant",
            Model = response.Model,
            Content = content,
            StopReason = stopReason ?? "end_turn",
            Usage = response.Usage != null ? AnthropicUsage.From(response.Usage) : null,
        };
    }
    #endregion

    #region 流式事件工厂
    /// <summary>创建流式开始事件（message_start + content_block_start）</summary>
    /// <param name="model">模型编码</param>
    /// <returns>SSE 事件列表</returns>
    public static IList<AnthropicStreamEvent> CreateStreamStart(String? model) =>
    [
        new AnthropicStreamEvent
        {
            EventName = "message_start",
            Type = "message_start",
            Message = new AnthropicResponse
            {
                Id = $"msg_{Guid.NewGuid():N}",
                Type = "message",
                Role = "assistant",
                Model = model,
                Content = [],
            },
        },
        new AnthropicStreamEvent
        {
            EventName = "content_block_start",
            Type = "content_block_start",
            Index = 0,
            ContentBlock = new AnthropicContentBlock { Type = "text", Text = "" },
        },
    ];

    /// <summary>从内部统一流式块创建 Anthropic SSE 事件列表</summary>
    /// <param name="chunk">内部统一流式块</param>
    /// <returns>SSE 事件列表（可能包含 content_block_delta、content_block_stop、message_delta）</returns>
    public static IList<AnthropicStreamEvent> CreateStreamDelta(ChatResponse chunk)
    {
        var events = new List<AnthropicStreamEvent>();
        if (chunk.Messages == null) return events;

        foreach (var choice in chunk.Messages)
        {
            var msg = choice.Delta ?? choice.Message;
            if (msg?.Content != null)
            {
                var text = msg.Content is String s ? s : msg.Content.ToString();
                events.Add(new AnthropicStreamEvent
                {
                    EventName = "content_block_delta",
                    Type = "content_block_delta",
                    Index = 0,
                    Delta = new AnthropicDelta { Type = "text_delta", Text = text },
                });
            }

            if (choice.FinishReason != null)
            {
                events.Add(new AnthropicStreamEvent
                {
                    EventName = "content_block_stop",
                    Type = "content_block_stop",
                    Index = 0,
                });

                var msgDelta = new AnthropicStreamEvent
                {
                    EventName = "message_delta",
                    Type = "message_delta",
                    Delta = new AnthropicDelta { StopReason = MapFinishReason(choice.FinishReason) },
                };
                if (chunk.Usage != null)
                    msgDelta.Usage = new AnthropicUsage { OutputTokens = chunk.Usage.OutputTokens };
                events.Add(msgDelta);
            }
        }

        return events;
    }

    /// <summary>创建流式结束事件（message_stop）</summary>
    /// <returns>SSE 事件</returns>
    public static AnthropicStreamEvent CreateStreamEnd() => new()
    {
        EventName = "message_stop",
        Type = "message_stop",
    };
    #endregion

    #region 辅助
    /// <summary>将内部 finish_reason 映射为 Anthropic stop_reason</summary>
    /// <param name="reason">内部结束原因</param>
    /// <returns>Anthropic 停止原因</returns>
    private static String MapFinishReason(String? reason) => reason switch
    {
        "stop" => "end_turn",
        "length" => "max_tokens",
        "tool_calls" => "tool_use",
        _ => "end_turn",
    };
    #endregion
}

/// <summary>Anthropic 内容块</summary>
public class AnthropicContentBlock
{
    /// <summary>类型。text/image/tool_use/tool_result/thinking</summary>
    public String? Type { get; set; }

    /// <summary>文本内容（text 类型使用）</summary>
    public String? Text { get; set; }

    /// <summary>思考内容（thinking 类型使用）</summary>
    public String? Thinking { get; set; }

    /// <summary>工具调用编号（tool_use 类型使用）</summary>
    public String? Id { get; set; }

    /// <summary>工具名称（tool_use 类型使用）</summary>
    public String? Name { get; set; }

    /// <summary>工具调用参数（tool_use 类型使用）。反序列化后为 IDictionary</summary>
    public Object? Input { get; set; }
}

/// <summary>Anthropic 令牌用量统计</summary>
public class AnthropicUsage
{
    /// <summary>输入令牌数</summary>
    [DataMember(Name = "input_tokens")]
    public Int32 InputTokens { get; set; }

    /// <summary>输出令牌数</summary>
    [DataMember(Name = "output_tokens")]
    public Int32 OutputTokens { get; set; }

    /// <summary>从内部用量统计转换</summary>
    /// <param name="usage">内部用量统计</param>
    /// <returns>Anthropic 格式用量</returns>
    public static AnthropicUsage From(UsageDetails usage) => new()
    {
        InputTokens = usage.InputTokens,
        OutputTokens = usage.OutputTokens,
    };
}

/// <summary>Anthropic SSE 流式事件。可表示 message_start / content_block_start / content_block_delta / content_block_stop / message_delta / message_stop</summary>
public class AnthropicStreamEvent
{
    /// <summary>SSE event: 字段名称。序列化时忽略</summary>
    [IgnoreDataMember]
    public String? EventName { get; set; }

    /// <summary>事件类型</summary>
    public String? Type { get; set; }

    /// <summary>内容块索引</summary>
    public Int32? Index { get; set; }

    /// <summary>消息体（message_start 事件使用）</summary>
    public AnthropicResponse? Message { get; set; }

    /// <summary>内容块（content_block_start 事件使用）</summary>
    [DataMember(Name = "content_block")]
    public AnthropicContentBlock? ContentBlock { get; set; }

    /// <summary>增量数据（content_block_delta / message_delta 事件使用）</summary>
    public AnthropicDelta? Delta { get; set; }

    /// <summary>用量统计（message_delta 事件可携带）</summary>
    public AnthropicUsage? Usage { get; set; }
}

/// <summary>Anthropic 增量数据</summary>
public class AnthropicDelta
{
    /// <summary>类型。text_delta（内容增量时使用）</summary>
    public String? Type { get; set; }

    /// <summary>文本内容（text_delta 时使用）</summary>
    public String? Text { get; set; }

    /// <summary>停止原因（message_delta 时使用）</summary>
    [DataMember(Name = "stop_reason")]
    public String? StopReason { get; set; }
}
