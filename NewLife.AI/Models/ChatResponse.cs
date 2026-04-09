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

    /// <summary>缓存输入Token数</summary>
    public Int32 CachedInputTokens { get; set; }

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
}

/// <summary>工具调用事件信息。由 ToolChatClient 在工具执行前后注入到 ChatResponse.ToolCallEvents</summary>
/// <param name="Type">事件类型。start/done/error</param>
/// <param name="ToolCallId">工具调用编号</param>
/// <param name="Name">工具名称</param>
/// <param name="Value">事件值。start 时为 Arguments，done 时为 Result，error 时为错误信息</param>
public record ToolCallEventInfo(String Type, String ToolCallId, String Name, String? Value);
