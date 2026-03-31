using System.Runtime.Serialization;
using NewLife.Serialization;

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
    [DataMember(Name = "model")]
    public String? Model { get; set; }

    /// <summary>消息列表。role 为 user / assistant</summary>
    [DataMember(Name = "messages")]
    public IList<AnthropicMessage> Messages { get; set; } = [];

    /// <summary>系统提示词。Anthropic 将 system 作为独立顶级字段，转换时放入 messages 首条</summary>
    [DataMember(Name = "system")]
    public String? System { get; set; }

    /// <summary>最大生成令牌数。Anthropic 中为必填字段</summary>
    [DataMember(Name = "max_tokens")]
    public Int32? MaxTokens { get; set; }

    /// <summary>温度。0~1</summary>
    [DataMember(Name = "temperature")]
    public Double? Temperature { get; set; }

    /// <summary>核采样。0~1，与 Temperature 二选一</summary>
    [DataMember(Name = "top_p")]
    public Double? TopP { get; set; }

    /// <summary>Top-K 采样</summary>
    [DataMember(Name = "top_k")]
    public Int32? TopK { get; set; }

    /// <summary>是否流式输出</summary>
    [DataMember(Name = "stream")]
    public Boolean Stream { get; set; }

    /// <summary>停止序列。对应 OpenAI 的 stop 字段</summary>
    [DataMember(Name = "stop_sequences")]
    public IList<String>? StopSequences { get; set; }

    /// <summary>可用工具列表。Anthropic 格式：name/description/input_schema</summary>
    [DataMember(Name = "tools")]
    public IList<Object>? Tools { get; set; }
    #endregion

    #region 转换
    /// <summary>从内部统一 ChatRequest 构建 Anthropic 协议请求</summary>
    /// <param name="request">内部统一请求</param>
    /// <returns>可直接 ToJson 序列化的 Anthropic 协议请求</returns>
    public static AnthropicRequest FromChatRequest(ChatRequest request)
    {
        var result = new AnthropicRequest
        {
            Model = request.Model ?? "",
            MaxTokens = request.MaxTokens ?? 4096, // Anthropic 中为必填项
            Temperature = request.Temperature,
            TopP = request.TopP,
            TopK = request.TopK,
            Stream = request.Stream,
        };

        if (request.Stop != null && request.Stop.Count > 0)
            result.StopSequences = request.Stop;

        // 分离 system 消息和普通消息
        var messages = new List<AnthropicMessage>();
        foreach (var msg in request.Messages)
        {
            if (msg.Role.Equals("system", StringComparison.OrdinalIgnoreCase))
            {
                result.System = msg.Content?.ToString();
                continue;
            }

            var role = msg.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase) ? "assistant" : "user";
            var am = new AnthropicMessage { Role = role };

            if (msg.ToolCallId != null)
            {
                // 工具结果消息 → tool_result 内容块
                am.Role = "user";
                am.Content = new List<Object>
                {
                    new Dictionary<String, Object?>
                    {
                        ["type"] = "tool_result",
                        ["tool_use_id"] = msg.ToolCallId,
                        ["content"] = msg.Content?.ToString() ?? "",
                    }
                };
            }
            else if (msg.ToolCalls != null && msg.ToolCalls.Count > 0)
            {
                // assistant 工具调用 → tool_use 内容块
                var contentBlocks = new List<Object>();
                if (msg.Content != null)
                    contentBlocks.Add(new Dictionary<String, Object> { ["type"] = "text", ["text"] = msg.Content.ToString()! });
                foreach (var tc in msg.ToolCalls)
                {
                    Object input = tc.Function?.Arguments != null
                        ? (JsonParser.Decode(tc.Function.Arguments) ?? new Dictionary<String, Object?>())
                        : new Dictionary<String, Object?>();
                    contentBlocks.Add(new Dictionary<String, Object?>
                    {
                        ["type"] = "tool_use",
                        ["id"] = tc.Id,
                        ["name"] = tc.Function?.Name ?? "",
                        ["input"] = input,
                    });
                }
                am.Content = contentBlocks;
            }
            else
            {
                am.Content = msg.Content;
            }

            messages.Add(am);
        }
        result.Messages = messages;

        // 转换工具定义：OpenAI function → Anthropic tool（name/description/input_schema）
        if (request.Tools != null && request.Tools.Count > 0)
        {
            var tools = new List<Object>();
            foreach (var tool in request.Tools)
            {
                if (tool.Function == null) continue;
                tools.Add(new Dictionary<String, Object?>
                {
                    ["name"] = tool.Function.Name,
                    ["description"] = tool.Function.Description,
                    ["input_schema"] = tool.Function.Parameters ?? (Object)new Dictionary<String, Object> { ["type"] = "object" },
                });
            }
            result.Tools = tools;
        }

        return result;
    }

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
    [DataMember(Name = "role")]
    public String Role { get; set; } = "";

    /// <summary>消息内容。可以是字符串或内容块数组（text / image_url 等）</summary>
    [DataMember(Name = "content")]
    public Object? Content { get; set; }
}
