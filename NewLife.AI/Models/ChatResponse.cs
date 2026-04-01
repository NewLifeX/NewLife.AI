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

/// <summary>令牌用量统计</summary>
public class UsageDetails
{
    /// <summary>提示令牌数</summary>
    public Int32 InputTokens { get; set; }

    /// <summary>回复令牌数</summary>
    public Int32 OutputTokens { get; set; }

    /// <summary>总令牌数</summary>
    public Int32 TotalTokens { get; set; }

    /// <summary>缓存输入令牌数</summary>
    public Int32 CachedInputTokens { get; set; }

    /// <summary>推理令牌数</summary>
    public Int32 ReasoningTokens { get; set; }

    /// <summary>音频输入令牌数</summary>
    public Int32 InputAudioTokens { get; set; }

    /// <summary>文本输入令牌数</summary>
    public Int32 InputTextTokens { get; set; }

    /// <summary>音频输出令牌数</summary>
    public Int32 OutputAudioTokens { get; set; }

    /// <summary>文本输出令牌数</summary>
    public Int32 OutputTextTokens { get; set; }

    /// <summary>耗时。本次LLM调用的端到端毫秒数</summary>
    public Int32 ElapsedMs { get; set; }
}
