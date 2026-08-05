using System.Runtime.Serialization;
using NewLife.AI.Models;
using NewLife.Data;
using NewLife.Serialization;

namespace NewLife.AI.Clients.Ollama;

/// <summary>Ollama /api/chat 对话请求，同时实现 IChatRequest 可直接作为统一请求在管道中传递</summary>
/// <remarks>
/// 对应 Ollama 原生 POST /api/chat 请求体。
/// 与 OpenAI ChatCompletionRequest 的差异：
/// <list type="bullet">
/// <item>模型参数放在 options 子对象中（非顶级），字段使用 snake_case</item>
/// <item>流式输出为 NDJSON 格式（非 SSE）</item>
/// <item>think 参数控制思考模式（Ollama 原生字段）</item>
/// </list>
/// </remarks>
public class OllamaChatRequest : IChatRequest
{
    /// <summary>模型名称</summary>
    public String? Model { get; set; }

    /// <summary>是否流式输出</summary>
    public Boolean Stream { get; set; }

    /// <summary>是否启用思考。null 时不传，由模型自身决定</summary>
    public Boolean? Think { get; set; }

    /// <summary>对话消息列表</summary>
    public IList<OllamaChatMessage> Messages { get; set; } = [];

    /// <summary>模型参数选项</summary>
    public OllamaOptions? Options { get; set; }

    /// <summary>工具定义列表</summary>
    public IList<Object>? Tools { get; set; }

    /// <summary>结构化输出格式。Ollama 通过 format 字段控制 JSON 模式（"json" 或 JSON Schema 对象）</summary>
    public Object? Format { get; set; }

    /// <summary>模型保持加载时长（秒）。0=立即卸载，-1=永久驻留，经 Items["KeepAlive"] 透传</summary>
    public Int64? KeepAlive { get; set; }

    #region IChatRequest 适配
    /// <summary>消息列表适配。将 OllamaChatMessage 转换为 ChatMessage</summary>
    [IgnoreDataMember]
    private IList<ChatMessage>? _chatMessages;

    /// <summary>消息列表适配</summary>
    [IgnoreDataMember]
    IList<ChatMessage> IChatRequest.Messages
    {
        get => _chatMessages ??= Messages.Select(e => e.ToChatMessage()).ToList();
        set => _chatMessages = value;
    }

    /// <summary>温度适配</summary>
    [IgnoreDataMember]
    Double? IChatRequest.Temperature
    {
        get => Options?.Temperature;
        set { Options ??= new OllamaOptions(); Options.Temperature = value ?? 0; }
    }

    /// <summary>核采样适配</summary>
    [IgnoreDataMember]
    Double? IChatRequest.TopP
    {
        get => Options?.TopP;
        set { Options ??= new OllamaOptions(); Options.TopP = value ?? 0; }
    }

    /// <summary>最大生成令牌数适配</summary>
    [IgnoreDataMember]
    Int32? IChatRequest.MaxTokens
    {
        get => Options?.NumPredict;
        set { Options ??= new OllamaOptions(); Options.NumPredict = value ?? 0; }
    }

    /// <summary>停止词列表适配</summary>
    [IgnoreDataMember]
    IList<String>? IChatRequest.Stop
    {
        get => Options?.Stop;
        set { Options ??= new OllamaOptions(); Options.Stop = value is List<String> list ? list : value != null ? new List<String>(value) : null; }
    }

    /// <summary>推理强度适配</summary>
    [IgnoreDataMember]
    String? IChatRequest.ReasoningEffort { get; set; }

    /// <summary>是否启用思考模式适配。映射到 Think</summary>
    [IgnoreDataMember]
    Boolean? IChatRequest.EnableThinking
    {
        get => Think;
        set => Think = value;
    }

    /// <summary>可用工具列表适配。入站时从原生 tools 数组解析为 ChatTool 列表，供管道工具链读取</summary>
    [IgnoreDataMember]
    private IList<ChatTool>? _chatTools;

    /// <summary>可用工具列表适配</summary>
    [IgnoreDataMember]
    IList<ChatTool>? IChatRequest.Tools
    {
        get
        {
            if (_chatTools == null && Tools != null && Tools.Count > 0)
            {
                var list = new List<ChatTool>(Tools.Count);
                foreach (var tool in Tools)
                {
                    // 反序列化后元素可能是 JsonElement（System.Text.Json，ASP.NET Core 入站）或 Dictionary（NewLife SystemJson）。
                    // JsonElement 不能直接用 ToJson()（会序列化为 {"ValueKind":1}），需用其 ToString() 获取 JSON 文本
                    String json;
                    if (tool is String str)
                        json = str;
                    else if (tool.GetType().FullName == "System.Text.Json.JsonElement")
                        json = tool.ToString() ?? "";
                    else
                        json = tool.ToJson();
                    if (json.IsNullOrWhiteSpace()) continue;

                    // 使用 Ollama 协议选项（snake_case 属性命名），确保 function 字段正确映射到 Function 属性
                    var ct = json.ToJsonEntity<ChatTool>(OllamaChatClient.DefaultJsonOptions);
                    if (ct != null) list.Add(ct);
                }
                _chatTools = list;
            }
            return _chatTools;
        }
        set => _chatTools = value;
    }

    /// <summary>Top-K 采样</summary>
    [IgnoreDataMember]
    public Int32? TopK { get; set; }

    /// <summary>存在惩罚</summary>
    [IgnoreDataMember]
    public Double? PresencePenalty { get; set; }

    /// <summary>频率惩罚</summary>
    [IgnoreDataMember]
    public Double? FrequencyPenalty { get; set; }

    /// <summary>工具选择策略</summary>
    [IgnoreDataMember]
    public Object? ToolChoice { get; set; }

    /// <summary>用户标识</summary>
    [IgnoreDataMember]
    public String? User { get; set; }

    /// <summary>响应格式</summary>
    [IgnoreDataMember]
    public Object? ResponseFormat { get; set; }

    /// <summary>是否允许并行工具调用</summary>
    [IgnoreDataMember]
    public Boolean? ParallelToolCalls { get; set; }

    /// <summary>用户编号。内部管道传递</summary>
    [IgnoreDataMember]
    public String? UserId { get; set; }

    /// <summary>会话编号。内部管道传递</summary>
    [IgnoreDataMember]
    public String? ConversationId { get; set; }

    /// <summary>扩展数据</summary>
    [IgnoreDataMember]
    public IDictionary<String, Object?> Items { get; set; } = new Dictionary<String, Object?>();

    /// <summary>索引器</summary>
    [IgnoreDataMember]
    public Object? this[String key] { get => Items.TryGetValue(key, out var value) ? value : null; set => Items[key] = value; }
    #endregion

    /// <summary>从通用 ChatRequest 构建 Ollama 原生请求</summary>
    /// <param name="request">通用对话请求</param>
    /// <returns>Ollama 原生请求对象</returns>
    public static OllamaChatRequest FromChatRequest(IChatRequest request)
    {
        var result = new OllamaChatRequest
        {
            Model = request.Model ?? "",
            Stream = request.Stream,
        };

        // think 参数：显式 true/false 时才传给 Ollama；null（Auto）时不传，由模型自身决定
        // 注意：不能用 ?? false 兜底，否则 Auto 模式会意外关闭思考
        if (request.EnableThinking.HasValue)
            result.Think = request.EnableThinking.Value;

        // 结构化输出：ResponseFormat → format 字段（json_object → "json"，json_schema → schema 对象）
        if (request.ResponseFormat != null)
            result.Format = MapResponseFormat(request.ResponseFormat);

        // 转换消息
        var messages = new List<OllamaChatMessage>();
        foreach (var msg in request.Messages)
        {
            var m = new OllamaChatMessage
            {
                Role = msg.Role,
                Content = msg.Content ?? "",
                // 多轮思考回放：Ollama 原生思考字段为 thinking（区别于兼容模式的 reasoning），回传上一轮推理内容保持上下文连贯
                Thinking = msg.ReasoningContent,
            };

            // 多模态图片输入：Ollama 通过 images 数组接收 base64 图片（含 data URI 解析）
            if (msg.Contents is { Count: > 0 })
            {
                List<String>? images = null;
                var textParts = new List<String>();
                foreach (var item in msg.Contents)
                {
                    if (item is TextContent text)
                    {
                        if (!String.IsNullOrEmpty(text.Text)) textParts.Add(text.Text);
                    }
                    else if (item is ImageContent img)
                    {
                        var data = img.Data;
                        var b64 = data is { Length: > 0 } ? Convert.ToBase64String(data) : AIContentHelper.ParseDataUri(img.Uri);
                        if (b64 != null)
                        {
                            images ??= [];
                            images.Add(b64);
                        }
                    }
                }
                if (images != null) m.Images = images;
                if (textParts.Count > 0) m.Content = String.Join("\n", textParts);
            }

            if (msg.ToolCalls != null && msg.ToolCalls.Count > 0)
            {
                var toolCalls = new List<OllamaToolCall>();
                foreach (var tc in msg.ToolCalls)
                {
                    var otc = new OllamaToolCall { Id = tc.Id, Type = tc.Type };
                    if (tc.Function != null)
                    {
                        // 将 arguments JSON 字符串解析为对象，以便序列化时输出 JSON 对象而非字符串
                        Object? args;
                        var argsStr = tc.Function.Arguments;
                        if (!argsStr.IsNullOrEmpty())
                            args = JsonParser.Decode(argsStr) ?? (Object)argsStr;
                        else
                            args = new Dictionary<String, Object?>();

                        otc.Function = new OllamaFunctionCall
                        {
                            Name = tc.Function.Name,
                            Arguments = args,
                        };
                    }
                    toolCalls.Add(otc);
                }
                m.ToolCalls = toolCalls;
            }

            messages.Add(m);
        }
        result.Messages = messages;

        // Ollama 的生成参数放在 options 子对象里
        var hasOptions = request.MaxTokens != null || request.Temperature != null
            || request.TopP != null || request.TopK != null || (request.Stop != null && request.Stop.Count > 0)
            || request.PresencePenalty != null || request.FrequencyPenalty != null
            || request["Seed"] != null || request["RepetitionPenalty"] != null
            || request["NumCtx"] != null;
        // 携带工具时限制思考 token 上限，防止 thinking 内容耗尽 context 导致工具调用 JSON 被截断
        var forceNumPredict = request.Tools != null && request.Tools.Count > 0 && request.MaxTokens == null;
        if (hasOptions || forceNumPredict)
        {
            var opts = new OllamaOptions();
            if (request.MaxTokens != null)
                opts.NumPredict = request.MaxTokens.Value;
            else if (forceNumPredict)
                opts.NumPredict = 4096;
            if (request.Temperature != null) opts.Temperature = request.Temperature.Value;
            if (request.TopP != null) opts.TopP = request.TopP.Value;
            if (request.TopK != null) opts.TopK = request.TopK.Value;
            if (request.Stop != null && request.Stop.Count > 0)
                opts.Stop = request.Stop is List<String> list ? list : [.. request.Stop];
            // 惩罚与确定性参数（Items 键名与 DashScope 约定一致：Seed / RepetitionPenalty）
            if (request.PresencePenalty != null) opts.PresencePenalty = request.PresencePenalty.Value;
            if (request.FrequencyPenalty != null) opts.FrequencyPenalty = request.FrequencyPenalty.Value;
            var seed = request["Seed"] as Int32?;
            if (seed != null) opts.Seed = seed.Value;
            var repeatPenalty = request["RepetitionPenalty"] as Double?;
            if (repeatPenalty != null) opts.RepeatPenalty = repeatPenalty.Value;
            // 上下文窗口大小（长对话防截断）
            var numCtx = request["NumCtx"] as Int32?;
            if (numCtx != null) opts.NumCtx = numCtx.Value;
            result.Options = opts;
        }

        // 模型保持加载时长（keep_alive 为顶层字段）
        var keepAlive = request["KeepAlive"] as Int64?;
        if (keepAlive != null) result.KeepAlive = keepAlive.Value;

        // 工具定义
        if (request.Tools != null && request.Tools.Count > 0)
        {
            var tools = new List<Object>();
            foreach (var tool in request.Tools)
            {
                var t = new Dictionary<String, Object> { ["type"] = tool.Type };
                if (tool.Function != null)
                {
                    var fn = new Dictionary<String, Object?> { ["name"] = tool.Function.Name };
                    if (tool.Function.Description != null) fn["description"] = tool.Function.Description;
                    if (tool.Function.Parameters != null) fn["parameters"] = tool.Function.Parameters;
                    t["function"] = fn;
                }
                tools.Add(t);
            }
            result.Tools = tools;
        }

        return result;
    }

    /// <summary>映射统一响应格式到 Ollama format 字段。json_object → "json"；json_schema → 协议 schema 对象或 "json"</summary>
    /// <param name="responseFormat">统一响应格式对象，OpenAI 风格 {type:"json_object"/"json_schema", json_schema:{...}}</param>
    /// <returns>Ollama format 值，无法识别时返回 null</returns>
    private static Object? MapResponseFormat(Object? responseFormat)
    {
        if (responseFormat is String str) return str;
        if (responseFormat == null) return null;

        var dic = responseFormat as IDictionary<String, Object?>;
        if (dic == null)
        {
            var json = responseFormat as String ?? responseFormat.ToJson();
            if (json.IsNullOrEmpty()) return null;
            dic = JsonParser.Decode(json);
        }
        if (dic == null) return null;

        var type = dic["type"] as String;
        if (type.IsNullOrEmpty()) return null;

        if (type.EqualIgnoreCase("json_object")) return "json";
        if (type.EqualIgnoreCase("json_schema") && dic.TryGetValue("json_schema", out var js) && js is IDictionary<String, Object> jsd)
            return jsd.TryGetValue("schema", out var s) ? s : js;
        return "json";
    }
}

/// <summary>Ollama 对话消息</summary>
public class OllamaChatMessage
{
    /// <summary>角色（user/assistant/system/tool）</summary>
    public String? Role { get; set; }

    /// <summary>消息内容</summary>
    public Object? Content { get; set; }

    /// <summary>图片列表。base64 编码，Ollama 通过 images 字段接收</summary>
    public IList<String>? Images { get; set; }

    /// <summary>思考内容（仅响应中使用）</summary>
    public String? Thinking { get; set; }

    /// <summary>工具调用列表</summary>
    public IList<OllamaToolCall>? ToolCalls { get; set; }

    /// <summary>转换为通用 ChatMessage</summary>
    /// <returns>通用消息对象</returns>
    public ChatMessage ToChatMessage()
    {
        var msg = new ChatMessage
        {
            Role = Role ?? "assistant",
            Content = Content,
            // Ollama 原生思考字段为 thinking（与兼容模式的 reasoning 不同）
            ReasoningContent = Thinking,
        };

        if (ToolCalls != null && ToolCalls.Count > 0)
        {
            var toolCalls = new List<ToolCall>();
            foreach (var tc in ToolCalls)
            {
                var call = new ToolCall
                {
                    Id = tc.Id ?? "",
                    Type = tc.Type ?? "function",
                };

                if (tc.Function != null)
                {
                    var argsRaw = tc.Function.Arguments;
                    call.Function = new FunctionCall
                    {
                        Name = tc.Function.Name ?? "",
                        Arguments = argsRaw is String s ? s
                            : argsRaw is IDictionary<String, Object> argsDic ? argsDic.ToJson()
                            : "{}",
                    };
                }

                toolCalls.Add(call);
            }
            msg.ToolCalls = toolCalls;
        }

        return msg;
    }
}

/// <summary>Ollama 工具调用</summary>
public class OllamaToolCall
{
    /// <summary>调用标识。Ollama 响应中可能为空</summary>
    public String? Id { get; set; }

    /// <summary>调用类型。Ollama 响应中可能为空，默认 function</summary>
    public String? Type { get; set; }

    /// <summary>函数调用信息</summary>
    public OllamaFunctionCall? Function { get; set; }
}

/// <summary>Ollama 函数调用</summary>
public class OllamaFunctionCall
{
    /// <summary>函数名称</summary>
    public String? Name { get; set; }

    /// <summary>函数参数。请求中序列化为 JSON 对象，响应中反序列化为 IDictionary</summary>
    public Object? Arguments { get; set; }
}
