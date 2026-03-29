using System.Runtime.Serialization;
using NewLife.Data;
using NewLife.Serialization;

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
    public ChatRequest ToChatRequest()
    {
        var messages = new List<ChatMessage>();
        if (Messages != null)
        {
            foreach (var msg in Messages)
            {
                var cm = new ChatMessage
                {
                    Role = msg.Role,
                    ToolCalls = msg.ToolCalls,
                    Contents = msg.Contents,
                };

                // Contents 已有类型化内容时直接使用；否则从 Content 解析多模态数组
                if (cm.Contents == null || cm.Contents.Count == 0)
                {
                    if (msg.Content is String str)
                        cm.Content = str;
                    else if (msg.Content != null)
                    {
                        // Content 可能是 OpenAI 格式的多模态数组（IList/JsonElement 等）
                        var contents = ParseMultimodalContent(msg.Content);
                        if (contents != null && contents.Count > 0)
                            cm.Contents = contents;
                        else
                            cm.Content = msg.Content + "";
                    }
                }

                messages.Add(cm);
            }
        }

        var req = new ChatRequest()
        {
            Model = Model,
            Messages = messages,
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

        return req;
    }

    /// <summary>尝试将 OpenAI 格式的多模态内容数组解析为 AIContent 列表</summary>
    /// <param name="content">Content 值，可能是 IList（SystemJson 转换后）或 JsonElement 等</param>
    /// <returns>解析成功返回 AIContent 列表，否则返回 null</returns>
    private static IList<AIContent>? ParseMultimodalContent(Object content)
    {
        IList<Object>? items = null;

        // NewLife SystemJson 转换器将 JSON 数组转为 IList<Object>
        if (content is IList<Object> list)
        {
            items = list;
        }
        else
        {
            // 可能是 JsonElement 等类型，通过 ToString() 获取 JSON 再解析
            var json = content.ToString();
            if (json == null || !json.StartsWith("[")) return null;

            try
            {
                // 包装为对象以便 JsonParser.Decode 解析
                var wrapper = JsonParser.Decode("{\"items\":" + json + "}");
                items = wrapper?["items"] as IList<Object>;
            }
            catch { return null; }
        }

        if (items == null || items.Count == 0) return null;

        var result = new List<AIContent>();
        foreach (var item in items)
        {
            if (item is not IDictionary<String, Object> dic) continue;

            var type = dic.TryGetValue("type", out var t) ? t + "" : null;
            if (type == "text")
            {
                var text = dic.TryGetValue("text", out var v) ? v + "" : "";
                result.Add(new TextContent(text));
            }
            else if (type == "image_url")
            {
                if (dic.TryGetValue("image_url", out var imgObj) && imgObj is IDictionary<String, Object> imgDic)
                {
                    var url = imgDic.TryGetValue("url", out var u) ? u + "" : null;
                    var img = new ImageContent { Uri = url };
                    if (imgDic.TryGetValue("detail", out var d))
                        img.Detail = d + "";
                    result.Add(img);
                }
            }
        }

        return result.Count > 0 ? result : null;
    }
    #endregion
}
