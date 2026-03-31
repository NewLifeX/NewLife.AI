using System.Runtime.Serialization;
using NewLife.AI.Models;
using NewLife.Data;
using NewLife.Serialization;

namespace NewLife.AI.Clients.OpenAI;

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

    /// <summary>流式选项。Stream=true 时附带，请求包含用量统计</summary>
    [DataMember(Name = "stream_options")]
    public IDictionary<String, Object>? StreamOptions { get; set; }

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
    [IgnoreDataMember]
    public IDictionary<String, Object?> Items { get; set; } = new Dictionary<String, Object?>();

    /// <summary>索引器，方便访问扩展数据</summary>
    [IgnoreDataMember]
    public Object? this[String key] { get => Items.TryGetValue(key, out var value) ? value : null; set => Items[key] = value; }
    #endregion

    #region 方法
    /// <summary>从内部统一 ChatRequest 构建 OpenAI 协议请求</summary>
    /// <param name="request">内部统一请求</param>
    /// <returns>可直接 ToJson 序列化的 OpenAI 协议请求</returns>
    public static ChatCompletionRequest FromChatRequest(IChatRequest request)
    {
        var result = new ChatCompletionRequest
        {
            Model = request.Model,
            Stream = request.Stream,
            Temperature = request.Temperature,
            TopP = request.TopP,
            TopK = request.TopK,
            MaxTokens = request.MaxTokens,
            Stop = request.Stop,
            PresencePenalty = request.PresencePenalty,
            FrequencyPenalty = request.FrequencyPenalty,
            ToolChoice = request.ToolChoice,
            User = request.User,
            EnableThinking = request.EnableThinking,
            ResponseFormat = request.ResponseFormat,
            ParallelToolCalls = request.ParallelToolCalls,
        };

        if (request.Stream)
            result.StreamOptions = new Dictionary<String, Object> { ["include_usage"] = true };

        // 转换消息列表：处理 Contents（类型化多模态内容）→ Content（OpenAI 协议格式）
        var messages = new List<ChatMessage>();
        foreach (var msg in request.Messages)
        {
            var cm = new ChatMessage
            {
                Role = msg.Role,
                Name = msg.Name,
                ToolCallId = msg.ToolCallId,
                ToolCalls = msg.ToolCalls,
            };

            if (msg.Contents != null && msg.Contents.Count > 0)
                cm.Content = BuildContent(msg.Contents);
            else
                cm.Content = msg.Content;

            messages.Add(cm);
        }
        result.Messages = messages;

        // 转换工具定义
        if (request.Tools != null && request.Tools.Count > 0)
            result.Tools = request.Tools;

        return result;
    }

    /// <summary>将 AIContent 集合转换为 OpenAI 格式的 content 字段值</summary>
    /// <param name="contents">AIContent 列表</param>
    /// <returns>字符串（单一文本）或内容数组（多模态）</returns>
    public static Object BuildContent(IList<AIContent> contents)
    {
        if (contents.Count == 1 && contents[0] is TextContent singleText)
            return singleText.Text;

        var parts = new List<Object>(contents.Count);
        foreach (var item in contents)
        {
            if (item is TextContent text)
            {
                parts.Add(new Dictionary<String, Object> { ["type"] = "text", ["text"] = text.Text });
            }
            else if (item is ImageContent img)
            {
                String url;
                if (img.Data != null && img.Data.Length > 0)
                    url = $"data:{img.MediaType ?? "image/jpeg"};base64,{Convert.ToBase64String(img.Data)}";
                else
                    url = img.Uri ?? "";

                var imgDic = new Dictionary<String, Object> { ["url"] = url };
                if (img.Detail != null) imgDic["detail"] = img.Detail;
                parts.Add(new Dictionary<String, Object> { ["type"] = "image_url", ["image_url"] = imgDic });
            }
        }
        return parts;
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
