using System.Runtime.Serialization;
using NewLife.AI.Models;
using NewLife.Serialization;

namespace NewLife.AI.Clients.Bedrock;

/// <summary>AWS Bedrock Converse API 非流式响应。兼容 https://docs.aws.amazon.com/bedrock/latest/userguide/conversation-api.html 协议</summary>
/// <remarks>
/// 与 OpenAI ChatCompletionResponse 的主要差异：
/// <list type="bullet">
/// <item>响应包含顶级 output.message 字段，其中 message 包含 role / content / stopReason</item>
/// <item>内容块为 content 数组，每项包含 text / toolUse / reasoningContent 等字段</item>
/// <item>结束原因字段名 stopReason（如 end_turn / stop_sequence / max_tokens / tool_use）</item>
/// <item>Usage 使用 inputTokens/outputTokens 命名，位于顶级</item>
/// </list>
/// </remarks>
public class BedrockResponse
{
    #region 属性
    /// <summary>响应编号</summary>
    public String? Id { get; set; }

    /// <summary>模型编码</summary>
    public String? Model { get; set; }

    /// <summary>输出消息。包含 role / content / stopReason</summary>
    public BedrockResponseOutput? Output { get; set; }

    /// <summary>停止原因的两个位置之一（某些情况直接在顶级）</summary>
    [DataMember(Name = "stopReason")]
    public String? StopReason { get; set; }

    /// <summary>令牌用量统计</summary>
    public BedrockResponseUsage? Usage { get; set; }
    #endregion

    #region 转换
    /// <summary>转换为内部统一 ChatResponse</summary>
    /// <param name="model">模型编码（响应中的模型值可能不完整）</param>
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
        String? reasoning = null;
        List<ToolCall>? toolCalls = null;
        String? finishReason = null;

        if (Output?.ResponseMessage != null)
        {
            var msg = Output.ResponseMessage;
            finishReason = MapStopReason(msg.StopReason ?? StopReason);

            if (msg.Content != null)
            {
                var textParts = new List<String>();
                var reasoningParts = new List<String>();

                foreach (var block in msg.Content)
                {
                    if (!String.IsNullOrEmpty(block.Text))
                        textParts.Add(block.Text);

                    if (block.ReasoningContent != null && !String.IsNullOrEmpty(block.ReasoningContent.ReasoningText))
                        reasoningParts.Add(block.ReasoningContent.ReasoningText);

                    if (block.ToolUse != null)
                    {
                        toolCalls ??= [];
                        var inputRaw = block.ToolUse.Input;
                        toolCalls.Add(new ToolCall
                        {
                            Id = block.ToolUse.ToolUseId ?? "",
                            Type = "function",
                            Function = new FunctionCall
                            {
                                Name = block.ToolUse.Name ?? "",
                                Arguments = inputRaw is IDictionary<String, Object> inputDic
                                    ? inputDic.ToJson()
                                    : inputRaw as String ?? "{}",
                            },
                        });
                    }
                }

                contentText = textParts.Count > 0 ? String.Join("", textParts) : null;
                reasoning = reasoningParts.Count > 0 ? String.Join("", reasoningParts) : null;
            }
        }

        var choice = response.Add(contentText, reasoning, finishReason);

        // 设置消息角色
        if (choice.Message == null && (toolCalls != null || contentText != null || reasoning != null))
            choice.Message = new ChatMessage { Role = "assistant" };
        else if (choice.Message != null)
            choice.Message.Role = "assistant";

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

    /// <summary>映射 Bedrock stopReason 到标准 finish_reason</summary>
    internal static String? MapStopReason(String? stopReason) => stopReason switch
    {
        "end_turn" => "stop",
        "stop_sequence" => "stop",
        "max_tokens" => "length",
        "tool_use" => "tool_calls",
        "content_filtered" => "content_filter",
        null => null,
        _ => stopReason,
    };

    /// <summary>从内部统一响应转换为 Bedrock 非流式响应</summary>
    /// <param name="response">内部统一响应</param>
    /// <returns>Bedrock 格式响应</returns>
    public static BedrockResponse From(ChatResponse response)
    {
        var content = new List<BedrockResponseContentBlock>();
        String? stopReason = null;

        if (response.Messages != null)
        {
            foreach (var choice in response.Messages)
            {
                var msg = choice.Message ?? choice.Delta;
                if (msg?.Content != null)
                {
                    var text = msg.Content is String s ? s : msg.Content.ToString();
                    content.Add(new BedrockResponseContentBlock { Text = text });
                }
                if (msg?.ReasoningContent != null)
                {
                    content.Add(new BedrockResponseContentBlock
                    {
                        ReasoningContent = new BedrockResponseReasoningContent
                        {
                            ReasoningText = msg.ReasoningContent
                        }
                    });
                }
                if (choice.FinishReason != null)
                    stopReason = MapFinishReason(choice.FinishReason);
            }
        }

        return new BedrockResponse
        {
            Id = response.Id ?? $"msg_{Guid.NewGuid():N}",
            Model = response.Model,
            Output = new BedrockResponseOutput
            {
                ResponseMessage = new BedrockResponseMessage
                {
                    Role = "assistant",
                    Content = content,
                    StopReason = stopReason ?? "end_turn",
                }
            },
            Usage = response.Usage != null ? BedrockResponseUsage.From(response.Usage) : null,
        };
    }
    #endregion

    #region 流式事件工厂
    /// <summary>创建流式开始事件</summary>
    /// <param name="model">模型编码</param>
    /// <returns>SSE 事件列表</returns>
    public static IList<BedrockStreamEvent> CreateStreamStart(String? model) =>
    [
        new BedrockStreamEvent
        {
            MessageStart = new BedrockStreamMessageStartEvent
            {
                Message = new BedrockStreamMessage
                {
                    Role = "assistant",
                    Content = [],
                }
            }
        },
    ];

    /// <summary>从内部统一流式块创建 Bedrock SSE 事件列表</summary>
    /// <param name="chunk">内部统一流式块</param>
    /// <returns>SSE 事件列表（可能包含 contentBlockDelta、messageStop、metadata）</returns>
    public static IList<BedrockStreamEvent> CreateStreamDelta(ChatResponse chunk)
    {
        var events = new List<BedrockStreamEvent>();
        if (chunk.Messages == null) return events;

        foreach (var choice in chunk.Messages)
        {
            var msg = choice.Delta ?? choice.Message;

            if (msg?.Content != null)
            {
                var text = msg.Content is String s ? s : msg.Content.ToString();
                events.Add(new BedrockStreamEvent
                {
                    ContentBlockDelta = new BedrockStreamContentBlockDeltaEvent
                    {
                        Delta = new BedrockStreamContentBlockDelta { Text = text }
                    }
                });
            }

            if (msg?.ReasoningContent != null)
            {
                events.Add(new BedrockStreamEvent
                {
                    ContentBlockDelta = new BedrockStreamContentBlockDeltaEvent
                    {
                        Delta = new BedrockStreamContentBlockDelta
                        {
                            ReasoningContent = new BedrockResponseReasoningContent
                            {
                                ReasoningText = msg.ReasoningContent
                            }
                        }
                    }
                });
            }

            if (choice.FinishReason != null)
            {
                events.Add(new BedrockStreamEvent
                {
                    MessageStop = new BedrockStreamMessageStopEvent
                    {
                        StopReason = MapFinishReason(choice.FinishReason),
                    }
                });
            }
        }

        if (chunk.Usage != null)
        {
            events.Add(new BedrockStreamEvent
            {
                Metadata = new BedrockStreamMetadataEvent
                {
                    Usage = new BedrockResponseUsage
                    {
                        InputTokens = chunk.Usage.InputTokens,
                        OutputTokens = chunk.Usage.OutputTokens,
                    }
                }
            });
        }

        return events;
    }

    /// <summary>创建流式结束事件</summary>
    /// <returns>SSE 事件</returns>
    public static BedrockStreamEvent CreateStreamEnd() => new();
    #endregion

    #region 辅助
    /// <summary>将内部 finish_reason 映射为 Bedrock stopReason</summary>
    private static String MapFinishReason(String? reason) => reason switch
    {
        "stop" => "end_turn",
        "length" => "max_tokens",
        "tool_calls" => "tool_use",
        _ => "end_turn",
    };
    #endregion
}

/// <summary>Bedrock 响应输出容器</summary>
public class BedrockResponseOutput
{
    /// <summary>实际消息内容</summary>
    [DataMember(Name = "message")]
    public BedrockResponseMessage? ResponseMessage { get; set; }
}

/// <summary>Bedrock 响应消息</summary>
public class BedrockResponseMessage
{
    /// <summary>角色。固定 "assistant"</summary>
    [DataMember(Name = "role")]
    public String? Role { get; set; }

    /// <summary>内容块列表</summary>
    [DataMember(Name = "content")]
    public IList<BedrockResponseContentBlock>? Content { get; set; }

    /// <summary>停止原因</summary>
    [DataMember(Name = "stopReason")]
    public String? StopReason { get; set; }
}

/// <summary>Bedrock 响应内容块</summary>
public class BedrockResponseContentBlock
{
    /// <summary>文本内容</summary>
    [DataMember(Name = "text")]
    public String? Text { get; set; }

    /// <summary>工具调用内容</summary>
    [DataMember(Name = "toolUse")]
    public BedrockResponseToolUseBlock? ToolUse { get; set; }

    /// <summary>推理内容</summary>
    [DataMember(Name = "reasoningContent")]
    public BedrockResponseReasoningContent? ReasoningContent { get; set; }
}

/// <summary>Bedrock 响应工具调用块</summary>
public class BedrockResponseToolUseBlock
{
    /// <summary>工具调用编号</summary>
    [DataMember(Name = "toolUseId")]
    public String? ToolUseId { get; set; }

    /// <summary>工具名称</summary>
    [DataMember(Name = "name")]
    public String? Name { get; set; }

    /// <summary>工具输入参数。通常为 IDictionary</summary>
    [DataMember(Name = "input")]
    public Object? Input { get; set; }
}

/// <summary>Bedrock 响应推理内容块</summary>
public class BedrockResponseReasoningContent
{
    /// <summary>推理文本</summary>
    [DataMember(Name = "reasoningText")]
    public String? ReasoningText { get; set; }
}

/// <summary>Bedrock 令牌用量统计</summary>
public class BedrockResponseUsage
{
    /// <summary>输入令牌数</summary>
    [DataMember(Name = "inputTokens")]
    public Int32 InputTokens { get; set; }

    /// <summary>输出令牌数</summary>
    [DataMember(Name = "outputTokens")]
    public Int32 OutputTokens { get; set; }

    /// <summary>从内部用量统计转换</summary>
    /// <param name="usage">内部用量统计</param>
    /// <returns>Bedrock 格式用量</returns>
    public static BedrockResponseUsage From(UsageDetails usage) => new()
    {
        InputTokens = usage.InputTokens,
        OutputTokens = usage.OutputTokens,
    };
}

/// <summary>Bedrock SSE 流式事件。可表示 messageStart / contentBlockStart / contentBlockDelta / contentBlockStop / messageStop / metadata</summary>
public class BedrockStreamEvent
{
    /// <summary>消息开始事件</summary>
    public BedrockStreamMessageStartEvent? MessageStart { get; set; }

    /// <summary>内容块增量事件</summary>
    public BedrockStreamContentBlockDeltaEvent? ContentBlockDelta { get; set; }

    /// <summary>消息停止事件</summary>
    public BedrockStreamMessageStopEvent? MessageStop { get; set; }

    /// <summary>元数据事件（包含 usage）</summary>
    public BedrockStreamMetadataEvent? Metadata { get; set; }

    /// <summary>将流式事件转换为内部统一 ChatResponse chunk</summary>
    /// <param name="model">模型编码</param>
    /// <returns>对应的 ChatResponse，无需转换时返回 null</returns>
    public ChatResponse? ToChunkResponse(String? model = null)
    {
        var response = new ChatResponse
        {
            Model = model,
            Object = "chat.completion.chunk",
        };

        if (MessageStart?.Message != null)
        {
            response.AddDelta(null, null, null);
            return response;
        }

        if (ContentBlockDelta?.Delta != null)
        {
            var delta = ContentBlockDelta.Delta;
            if (!String.IsNullOrEmpty(delta.Text))
            {
                response.AddDelta(delta.Text, null, null);
                return response;
            }
            if (delta.ReasoningContent != null && !String.IsNullOrEmpty(delta.ReasoningContent.ReasoningText))
            {
                response.AddDelta(null, delta.ReasoningContent.ReasoningText, null);
                return response;
            }
        }

        if (MessageStop?.StopReason != null)
        {
            response.AddDelta(null, null, BedrockResponse.MapStopReason(MessageStop.StopReason));
            return response;
        }

        if (Metadata?.Usage != null)
        {
            response.Usage = new UsageDetails
            {
                InputTokens = Metadata.Usage.InputTokens,
                OutputTokens = Metadata.Usage.OutputTokens,
            };
            response.AddDelta(null, null, null);
            return response;
        }

        return null;
    }
}

/// <summary>Bedrock 流式消息开始事件</summary>
public class BedrockStreamMessageStartEvent
{
    /// <summary>消息对象</summary>
    public BedrockStreamMessage? Message { get; set; }
}

/// <summary>Bedrock 流式消息</summary>
public class BedrockStreamMessage
{
    /// <summary>角色</summary>
    public String? Role { get; set; }

    /// <summary>内容块列表</summary>
    public IList<BedrockResponseContentBlock>? Content { get; set; }
}

/// <summary>Bedrock 流式内容块增量事件</summary>
public class BedrockStreamContentBlockDeltaEvent
{
    /// <summary>增量内容</summary>
    public BedrockStreamContentBlockDelta? Delta { get; set; }
}

/// <summary>Bedrock 流式内容块增量</summary>
public class BedrockStreamContentBlockDelta
{
    /// <summary>文本增量</summary>
    public String? Text { get; set; }

    /// <summary>推理内容增量</summary>
    [DataMember(Name = "reasoningContent")]
    public BedrockResponseReasoningContent? ReasoningContent { get; set; }
}

/// <summary>Bedrock 流式消息停止事件</summary>
public class BedrockStreamMessageStopEvent
{
    /// <summary>停止原因</summary>
    [DataMember(Name = "stopReason")]
    public String? StopReason { get; set; }
}

/// <summary>Bedrock 流式元数据事件</summary>
public class BedrockStreamMetadataEvent
{
    /// <summary>用量统计</summary>
    public BedrockResponseUsage? Usage { get; set; }
}
