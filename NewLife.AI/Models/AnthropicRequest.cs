using System.Runtime.Serialization;

namespace NewLife.AI.Models;

/// <summary>Anthropic Messages API 请求体。兼容 https://docs.anthropic.com/en/api/messages 协议（snake_case 格式）</summary>
/// <remarks>
/// 与 OpenAI Chat Completions 的主要差异：
/// <list type="bullet">
/// <item>system 作为独立的顶级字段，而非 messages 中的一条</item>
/// <item>停止词字段名为 stop_sequences（OpenAI 为 stop）</item>
/// <item>额外支持 top_k 采样参数</item>
/// <item>消息内容可以是字符串或内容块数组（text / image / tool_use / tool_result）</item>
/// </list>
/// </remarks>
public class AnthropicRequest
{
    #region 属性
    /// <summary>模型编码</summary>
    public String? Model { get; set; }

    /// <summary>消息列表。role 为 user / assistant</summary>
    public IList<AnthropicMessage> Messages { get; set; } = [];

    /// <summary>系统提示词。Anthropic 将 system 作为独立顶级字段，转换时放入 messages 首条</summary>
    public String? System { get; set; }

    /// <summary>最大生成令牌数。Anthropic 中为必填字段</summary>
    [DataMember(Name = "max_tokens")]
    public Int32? MaxTokens { get; set; }

    /// <summary>温度。0~1</summary>
    public Double? Temperature { get; set; }

    /// <summary>核采样。0~1，与 Temperature 二选一</summary>
    [DataMember(Name = "top_p")]
    public Double? TopP { get; set; }

    /// <summary>Top-K 采样</summary>
    [DataMember(Name = "top_k")]
    public Int32? TopK { get; set; }

    /// <summary>是否流式输出</summary>
    public Boolean Stream { get; set; }

    /// <summary>停止序列。对应 OpenAI 的 stop 字段</summary>
    [DataMember(Name = "stop_sequences")]
    public IList<String>? StopSequences { get; set; }
    #endregion

    #region 转换
    /// <summary>转换为内部统一的 ChatRequest</summary>
    /// <returns>等效的 ChatRequest 实例</returns>
    public ChatRequest ToChatRequest()
    {
        var messages = new List<ChatMessage>();

        // 将顶级 system 字段转为首条系统消息
        if (!String.IsNullOrEmpty(System))
            messages.Add(new ChatMessage { Role = "system", Content = System });

        foreach (var msg in Messages)
            messages.Add(new ChatMessage { Role = msg.Role, Content = msg.Content });

        return new ChatRequest
        {
            Model = Model,
            Messages = messages,
            MaxTokens = MaxTokens,
            Temperature = Temperature,
            TopP = TopP,
            TopK = TopK,
            Stream = Stream,
            Stop = StopSequences,
        };
    }
    #endregion
}

/// <summary>Anthropic 消息</summary>
public class AnthropicMessage
{
    /// <summary>角色。user / assistant</summary>
    public String Role { get; set; } = "";

    /// <summary>消息内容。可以是字符串或内容块数组（text / image_url 等）</summary>
    public Object? Content { get; set; }
}
