using System.Runtime.Serialization;
using NewLife.Data;

namespace NewLife.AI.Models;

/// <summary>对话完成请求。兼容 OpenAI ChatCompletion 标准</summary>
public class ChatCompletionRequest : IExtend
{
    #region 属性
    /// <summary>模型编码</summary>
    public String? Model { get; set; }

    /// <summary>消息列表</summary>
    public IList<ChatMessage> Messages { get; set; } = [];

    /// <summary>温度。0~2，越高越随机，默认1</summary>
    public Double? Temperature { get; set; }

    /// <summary>核采样。0~1，与Temperature二选一</summary>
    [DataMember(Name = "top_p")]
    public Double? TopP { get; set; }

    /// <summary>Top K</summary>
    [DataMember(Name = "top_k")]
    public Int32? TopK { get; set; }

    /// <summary>最大生成令牌数</summary>
    [DataMember(Name = "max_tokens")]
    public Int32? MaxTokens { get; set; }

    /// <summary>是否流式输出</summary>
    public Boolean Stream { get; set; }

    /// <summary>停止词列表</summary>
    public IList<String>? Stop { get; set; }

    /// <summary>存在惩罚。-2~2</summary>
    [DataMember(Name = "presence_penalty")]
    public Double? PresencePenalty { get; set; }

    /// <summary>频率惩罚。-2~2</summary>
    [DataMember(Name = "frequency_penalty")]
    public Double? FrequencyPenalty { get; set; }

    /// <summary>可用工具列表。用于函数调用</summary>
    public IList<ChatTool>? Tools { get; set; }

    /// <summary>工具选择策略。auto/none/required 或指定工具名</summary>
    [DataMember(Name = "tool_choice")]
    public Object? ToolChoice { get; set; }

    /// <summary>用户标识。用于追踪和限流</summary>
    public String? User { get; set; }

    /// <summary>是否启用思考模式。null=不设置，true=开启，false=关闭。仅支持的模型有效（如 Qwen3 系列、QwQ 等）</summary>
    [DataMember(Name = "enable_thinking")]
    public Boolean? EnableThinking { get; set; }

    /// <summary>响应格式。用于结构化输出，如 {"type":"json_schema","json_schema":{...}}。支持的服务商：DashScope、OpenAI 等</summary>
    [DataMember(Name = "response_format")]
    public Object? ResponseFormat { get; set; }

    /// <summary>是否允许并行工具调用。null=不设置，true=允许，false=禁止</summary>
    [DataMember(Name = "parallel_tool_calls")]
    public Boolean? ParallelToolCalls { get; set; }

    /// <summary>扩展数据。用于在中间件管道中传递非结构化的自定义上下文</summary>
    public IDictionary<String, Object?> Items { get; set; } = new Dictionary<String, Object?>();

    /// <summary>索引器，方便访问扩展数据</summary>
    public Object? this[String key] { get => Items.TryGetValue(key, out var value) ? value : null; set => Items[key] = value; }
    #endregion

    #region 方法
    /// <summary>应用对话选项。将 ChatOptions 中的非空字段合并到当前请求</summary>
    /// <param name="options">对话选项，null 字段不覆盖</param>
    /// <returns>当前请求实例（支持链式调用）</returns>
    public ChatCompletionRequest Apply(ChatOptions? options)
    {
        if (options == null) return this;

        Model ??= options.Model;
        Temperature ??= options.Temperature;
        TopP ??= options.TopP;
        TopK ??= options.TopK;
        MaxTokens ??= options.MaxTokens;
        Stop ??= options.Stop;
        PresencePenalty ??= options.PresencePenalty;
        FrequencyPenalty ??= options.FrequencyPenalty;
        User ??= options.User;
        EnableThinking ??= options.EnableThinking;
        ResponseFormat ??= options.ResponseFormat;
        ParallelToolCalls ??= options.ParallelToolCalls;

        if (options.Tools != null && options.Tools.Count > 0)
        {
            Tools ??= [];
            foreach (var t in options.Tools)
            {
                Tools.Add(t);
            }
        }
        ToolChoice ??= options.ToolChoice;

        // 合并扩展数据。选项中的键值对覆盖请求中同名键值
        if (options.Items != null && options.Items.Count > 0)
        {
            if (Items == null || Items.Count == 0)
                Items = options.Items;
            else
            {
                foreach (var kv in options.Items)
                {
                    Items[kv.Key] = kv.Value;
                }
            }
        }

        return this;
    }

    /// <summary>根据消息列表和可选对话选项创建请求</summary>
    /// <param name="messages">消息列表</param>
    /// <param name="options">对话选项</param>
    /// <param name="stream">是否流式</param>
    /// <returns>对话请求实例</returns>
    public static ChatCompletionRequest Create(IList<ChatMessage> messages, ChatOptions? options = null, Boolean stream = false)
    {
        var request = new ChatCompletionRequest
        {
            Messages = messages,
            Stream = stream,
        };
        return request.Apply(options);
    }

    /// <summary>转换为内部统一的 ChatRequest</summary>
    /// <returns>等效的 ChatRequest 实例</returns>
    public ChatRequest ToChatRequest() => new()
    {
        Model = Model,
        Messages = Messages,
        Stream = Stream,
        Temperature = Temperature,
        TopP = TopP,
        TopK = TopK,
        MaxTokens = MaxTokens,
        Stop = Stop,
        PresencePenalty = PresencePenalty,
        FrequencyPenalty = FrequencyPenalty,
        Tools = Tools,
        ToolChoice = ToolChoice,
        User = User,
        EnableThinking = EnableThinking,
        ResponseFormat = ResponseFormat,
        ParallelToolCalls = ParallelToolCalls,
        Items = Items,
    };
    #endregion
}
