using System.Runtime.Serialization;
using NewLife.AI.Clients;

namespace NewLife.AI.Models;

/// <summary>对话完成响应。内部统一模型，由各协议专用响应类（ChatCompletionResponse / AnthropicResponse / GeminiResponse）转换后输出</summary>
public class ChatResponse : IChatResponse
{
    #region 属性
    /// <summary>响应编号</summary>
    public String? Id { get; set; }

    /// <summary>对象类型。chat.completion 或 chat.completion.chunk</summary>
    public String? Object { get; set; }

    /// <summary>创建时间戳（Unix 秒）</summary>
    public DateTimeOffset Created { get; set; }

    /// <summary>模型编码</summary>
    public String? Model { get; set; }

    /// <summary>消息选择列表</summary>
    public IList<ChatChoice>? Messages { get; set; }

    /// <summary>工具调用事件列表。由 ToolChatClient 在工具执行前后注入，供管道层转换为 SSE 事件</summary>
    public IList<ToolCallEventInfo>? ToolCallEvents { get; set; }

    /// <summary>令牌用量统计</summary>
    public UsageDetails? Usage { get; set; }
    #endregion

    #region 便捷属性
    /// <summary>获取回复文本。返回第一个选择项的消息内容</summary>
    [IgnoreDataMember]
    public String? Text
    {
        get
        {
            var value = Messages?.FirstOrDefault()?.Message?.Content ?? Messages?.FirstOrDefault()?.Delta?.Content;
            if (value == null) return null;
            if (value is IList<Object> list) value = list.FirstOrDefault();
            if (value is String str) return str;
            if (value is IDictionary<String, Object?> dic)
            {
                if (dic.Count == 0) return String.Empty;
                if (dic.TryGetValue("text", out var text)) return text + "";

                return dic.FirstOrDefault().Value + "";
            }

            return value.ToString();
        }
    }
    #endregion

    #region 方法
    /// <summary>添加消息项。返回新添加的项，便于后续修改</summary>
    public ChatChoice Add(Object? content, String? reasoning = null, FinishReason? finishReason = null)
    {
        var msgs = Messages ??= [];

        var choice = new ChatChoice
        {
            Index = msgs.Count,
            FinishReason = finishReason
        };
        if (content != null || !reasoning.IsNullOrEmpty())
            choice.Message = new ChatMessage { Content = content, ReasoningContent = reasoning, };

        msgs.Add(choice);

        return choice;
    }

    /// <summary>从 IChatResponse 转换为 ChatResponse。若已是 ChatResponse 则直接返回，否则从接口属性构建新实例</summary>
    /// <param name="response">任意协议的响应对象</param>
    /// <returns>ChatResponse 实例</returns>
    public static ChatResponse From(IChatResponse response)
    {
        if (response is ChatResponse cr) return cr;

        return new ChatResponse
        {
            Id = response.Id,
            Object = response.Object,
            Created = response.Created,
            Model = response.Model,
            Messages = response.Messages,
            Usage = response.Usage,
        };
    }

    /// <summary>添加增量消息项。返回新添加的项，便于后续修改</summary>
    public ChatChoice AddDelta(Object? content, String? reasoning = null, FinishReason? finishReason = null)
    {
        var msgs = Messages ??= [];

        var choice = new ChatChoice
        {
            Index = msgs.Count,
            FinishReason = finishReason
        };
        if (content != null || !reasoning.IsNullOrEmpty())
            choice.Delta = new ChatMessage { Content = content, ReasoningContent = reasoning, };

        msgs.Add(choice);

        return choice;
    }

    /// <summary>添加工具调用增量块。用于透传模式下将 tool_call delta 转换为 OpenAI 兼容的流式块</summary>
    /// <param name="toolCallId">工具调用编号</param>
    /// <param name="toolName">工具名称</param>
    /// <param name="arguments">当前已累积的参数字符串</param>
    /// <param name="finishReason">结束原因</param>
    /// <returns>新添加的项</returns>
    public ChatChoice AddToolCallDelta(String toolCallId, String toolName, String? arguments = null, FinishReason? finishReason = null)
    {
        var msgs = Messages ??= [];

        var choice = new ChatChoice
        {
            Index = 0,
            FinishReason = finishReason
        };
        choice.Delta = new ChatMessage
        {
            Role = "assistant",
            ToolCalls =
            [
                new ToolCall
                {
                    Index = 0,
                    Id = toolCallId,
                    Type = "function",
                    Function = new FunctionCall
                    {
                        Name = toolName,
                        Arguments = arguments,
                    },
                }
            ]
        };

        msgs.Add(choice);

        return choice;
    }
    #endregion
}

/// <summary>对话消息项</summary>
public class ChatChoice
{
    /// <summary>序号</summary>
    public Int32 Index { get; set; }

    /// <summary>消息内容（非流式）</summary>
    public ChatMessage? Message { get; set; }

    /// <summary>增量内容（流式）</summary>
    public ChatMessage? Delta { get; set; }

    /// <summary>结束原因</summary>
    public FinishReason? FinishReason { get; set; }
}

/// <summary>Token用量统计</summary>
public class UsageDetails
{
    /// <summary>输入Token数</summary>
    public Int32 InputTokens { get; set; }

    /// <summary>输出Token数</summary>
    public Int32 OutputTokens { get; set; }

    /// <summary>总Token数</summary>
    public Int32 TotalTokens { get; set; }

    /// <summary>命中缓存的输入Token数（隐式缓存或显式缓存命中）</summary>
    public Int32 CachedInputTokens { get; set; }

    /// <summary>创建显式缓存消耗的Token数（首次命中缓存标记时）</summary>
    public Int32 CacheCreationTokens { get; set; }

    /// <summary>推理Token数</summary>
    public Int32 ReasoningTokens { get; set; }

    /// <summary>音频输入Token数</summary>
    public Int32 InputAudioTokens { get; set; }

    /// <summary>文本输入Token数</summary>
    public Int32 InputTextTokens { get; set; }

    /// <summary>音频输出Token数</summary>
    public Int32 OutputAudioTokens { get; set; }

    /// <summary>文本输出Token数</summary>
    public Int32 OutputTextTokens { get; set; }

    /// <summary>耗时。本次LLM调用的端到端毫秒数</summary>
    public Int32 ElapsedMs { get; set; }

    /// <summary>同一次 LLM 调用内的 chunk 用量局部填充。适用于 Anthropic 等将 Usage 拆成多个互补 chunk 的协议：
    /// incoming 中非零字段覆盖当前实例对应字段，零字段保留当前值，返回新实例</summary>
    /// <param name="incoming">新 chunk 携带的增量 Usage</param>
    /// <returns>填充后的新 <see cref="UsageDetails"/> 实例</returns>
    public UsageDetails Merge(UsageDetails incoming)
    {
        if (incoming == null) return this;
        var result = new UsageDetails
        {
            InputTokens = incoming.InputTokens != 0 ? incoming.InputTokens : InputTokens,
            OutputTokens = incoming.OutputTokens != 0 ? incoming.OutputTokens : OutputTokens,
            TotalTokens = incoming.TotalTokens != 0 ? incoming.TotalTokens : TotalTokens,
            CachedInputTokens = incoming.CachedInputTokens != 0 ? incoming.CachedInputTokens : CachedInputTokens,
            CacheCreationTokens = incoming.CacheCreationTokens != 0 ? incoming.CacheCreationTokens : CacheCreationTokens,
            ReasoningTokens = incoming.ReasoningTokens != 0 ? incoming.ReasoningTokens : ReasoningTokens,
            InputAudioTokens = incoming.InputAudioTokens != 0 ? incoming.InputAudioTokens : InputAudioTokens,
            InputTextTokens = incoming.InputTextTokens != 0 ? incoming.InputTextTokens : InputTextTokens,
            OutputAudioTokens = incoming.OutputAudioTokens != 0 ? incoming.OutputAudioTokens : OutputAudioTokens,
            OutputTextTokens = incoming.OutputTextTokens != 0 ? incoming.OutputTextTokens : OutputTextTokens,
            ElapsedMs = incoming.ElapsedMs != 0 ? incoming.ElapsedMs : ElapsedMs,
        };
        // 填充后若 TotalTokens 仍为 0 但已有分项，则由分项推算（Anthropic 分块场景兜底）
        if (result.TotalTokens == 0 && (result.InputTokens > 0 || result.OutputTokens > 0))
            result.TotalTokens = result.InputTokens + result.OutputTokens;
        return result;
    }

    /// <summary>累加另一轮 LLM 调用的用量，返回新实例。用于多轮工具调用时汇总所有 LLM 调用的 Token 消耗</summary>
    /// <param name="other">另一轮的用量统计</param>
    /// <returns>合并后的新 <see cref="UsageDetails"/> 实例</returns>
    public UsageDetails Add(UsageDetails other)
    {
        if (other == null) return this;
        return new UsageDetails
        {
            InputTokens = InputTokens + other.InputTokens,
            OutputTokens = OutputTokens + other.OutputTokens,
            TotalTokens = TotalTokens + other.TotalTokens,
            CachedInputTokens = CachedInputTokens + other.CachedInputTokens,
            CacheCreationTokens = CacheCreationTokens + other.CacheCreationTokens,
            ReasoningTokens = ReasoningTokens + other.ReasoningTokens,
            InputAudioTokens = InputAudioTokens + other.InputAudioTokens,
            InputTextTokens = InputTextTokens + other.InputTextTokens,
            OutputAudioTokens = OutputAudioTokens + other.OutputAudioTokens,
            OutputTextTokens = OutputTextTokens + other.OutputTextTokens,
            ElapsedMs = ElapsedMs + other.ElapsedMs,
        };
    }
}

/// <summary>工具调用事件信息。由 ToolChatClient 在工具执行前后注入到 ChatResponse.ToolCallEvents</summary>
/// <param name="Type">事件类型。start/done/error</param>
/// <param name="ToolCallId">工具调用编号</param>
/// <param name="Name">工具名称</param>
/// <param name="Value">事件值。start 时为 Arguments，done 时为 Result，error 时为错误信息</param>
/// <param name="LlmResult">LLM 摘要。done 事件专用，role=tool 历史回放时优先使用；null 时回退到 Value</param>
public record ToolCallEventInfo(String Type, String ToolCallId, String Name, String? Value, String? LlmResult = null);
