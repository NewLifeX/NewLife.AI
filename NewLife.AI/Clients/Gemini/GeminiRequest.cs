using System.Runtime.Serialization;
using NewLife.AI.Models;
using NewLife.Serialization;

namespace NewLife.AI.Clients.Gemini;

/// <summary>Google Gemini generateContent 请求体。兼容 https://ai.google.dev/api/generate-content 协议（camelCase 格式），同时实现 IChatRequest 可直接作为统一请求在管道中传递</summary>
/// <remarks>
/// 与 OpenAI Chat Completions 的主要差异：
/// <list type="bullet">
/// <item>消息列表字段名为 contents，角色使用 user / model（而非 assistant）</item>
/// <item>消息内容通过 parts 数组传递</item>
/// <item>系统指令通过独立的 systemInstruction 字段传递</item>
/// <item>生成参数封装在 generationConfig 对象中</item>
/// <item>原生 API 中 stream 通过不同端点区分，此处作为自定义扩展字段</item>
/// </list>
/// </remarks>
public class GeminiRequest : IChatRequest
{
    #region 属性
    /// <summary>模型编码。Gemini 原生 API 将模型置于 URL 路径，网关场景通过请求体传递</summary>
    public String? Model { get; set; }

    /// <summary>对话内容列表。role 为 user / model</summary>
    public IList<GeminiContent> Contents { get; set; } = [];

    /// <summary>系统指令</summary>
    public GeminiContent? SystemInstruction { get; set; }

    /// <summary>生成配置</summary>
    public GeminiGenerationConfig? GenerationConfig { get; set; }

    /// <summary>是否流式输出。Gemini 原生通过不同端点区分；NewLifeAI 网关通过此字段决定是否返回 SSE 事件流</summary>
    public Boolean Stream { get; set; }

    /// <summary>工具定义列表。Gemini 格式：[{functionDeclarations:[...]}]</summary>
    public IList<Object>? Tools { get; set; }

    /// <summary>工具声明列表。仅用于 FromChatRequest 构建时临时存储，序列化时由 Tools 输出</summary>
    [IgnoreDataMember]
    internal IList<Object>? ToolDeclarations { get => Tools; set => Tools = value; }

    /// <summary>安全设置列表。Gemini 原生安全设置（[{category, threshold}]），按类别阻断有害内容</summary>
    public IList<Object>? SafetySettings { get; set; }
    #endregion

    #region IChatRequest 适配
    /// <summary>消息列表适配。合并 SystemInstruction 与 Contents 转换为 ChatMessage</summary>
    [IgnoreDataMember]
    private IList<ChatMessage>? _chatMessages;

    /// <summary>消息列表适配</summary>
    [IgnoreDataMember]
    IList<ChatMessage> IChatRequest.Messages
    {
        get
        {
            if (_chatMessages == null)
            {
                var messages = new List<ChatMessage>();
                if (SystemInstruction?.Parts.Count > 0)
                {
                    var sysText = String.Join("\n", SystemInstruction.Parts
                        .Where(p => !String.IsNullOrEmpty(p.Text))
                        .Select(p => p.Text!));
                    if (!String.IsNullOrEmpty(sysText))
                        messages.Add(new ChatMessage { Role = "system", Content = sysText });
                }
                foreach (var content in Contents)
                {
                    var role = content.Role == "model" ? "assistant" : (content.Role ?? "user");
                    var text = String.Join("", content.Parts.Select(p => p.Text ?? ""));
                    messages.Add(new ChatMessage { Role = role, Content = text });
                }
                _chatMessages = messages;
            }
            return _chatMessages;
        }
        set => _chatMessages = value;
    }

    /// <summary>温度适配。委托到 GenerationConfig</summary>
    [IgnoreDataMember]
    Double? IChatRequest.Temperature
    {
        get => GenerationConfig?.Temperature;
        set { GenerationConfig ??= new GeminiGenerationConfig(); GenerationConfig.Temperature = value; }
    }

    /// <summary>核采样适配</summary>
    [IgnoreDataMember]
    Double? IChatRequest.TopP
    {
        get => GenerationConfig?.TopP;
        set { GenerationConfig ??= new GeminiGenerationConfig(); GenerationConfig.TopP = value; }
    }

    /// <summary>Top-K 采样适配</summary>
    [IgnoreDataMember]
    Int32? IChatRequest.TopK
    {
        get => GenerationConfig?.TopK;
        set { GenerationConfig ??= new GeminiGenerationConfig(); GenerationConfig.TopK = value; }
    }

    /// <summary>最大生成令牌数适配</summary>
    [IgnoreDataMember]
    Int32? IChatRequest.MaxTokens
    {
        get => GenerationConfig?.MaxOutputTokens;
        set { GenerationConfig ??= new GeminiGenerationConfig(); GenerationConfig.MaxOutputTokens = value; }
    }

    /// <summary>停止词列表适配</summary>
    [IgnoreDataMember]
    IList<String>? IChatRequest.Stop
    {
        get => GenerationConfig?.StopSequences;
        set { GenerationConfig ??= new GeminiGenerationConfig(); GenerationConfig.StopSequences = value; }
    }

    /// <summary>可用工具列表缓存。入站时从原生 Tools 数组惰性转换</summary>
    [IgnoreDataMember]
    private IList<ChatTool>? _chatTools;

    /// <summary>可用工具列表适配。入站时从原生 Tools（[{functionDeclarations:[...]}]）惰性转换为 ChatTool，与 Ollama 的 _chatTools 模式一致</summary>
    [IgnoreDataMember]
    IList<ChatTool>? IChatRequest.Tools
    {
        get
        {
            // A-106：自动属性在入站时保持 null（IgnoreDataMember），工具定义在网关统一化 ToChatRequest 转换中丢失。
            // 与 Ollama 一致改为惰性转换：原生 Tools 数组 → ChatTool 列表，仅在无缓存且存在工具时构建。
            if (_chatTools == null && Tools != null && Tools.Count > 0)
            {
                var list = new List<ChatTool>();
                foreach (var tool in Tools)
                {
                    // 反序列化后元素可能是 JsonElement（System.Text.Json，ASP.NET Core 入站）或 Dictionary（NewLife SystemJson）
                    var dic = tool as IDictionary<String, Object?>;
                    if (dic == null)
                    {
                        // JsonElement 等表示统一转为 JSON 文本再解析
                        String? json;
                        if (tool is String str) json = str;
                        else if (tool.GetType().FullName == "System.Text.Json.JsonElement") json = tool.ToString();
                        else json = tool.ToJson();
                        if (json.IsNullOrWhiteSpace()) continue;
                        dic = JsonParser.Decode(json);
                    }
                    if (dic == null) continue;

                    // Gemini 格式：[{functionDeclarations:[{name, description, parameters}]}]
                    if (dic["functionDeclarations"] is not IList<Object> declarations) continue;
                    foreach (var decl in declarations)
                    {
                        var ddic = decl as IDictionary<String, Object?>;
                        if (ddic == null)
                        {
                            // functionDeclarations 元素同样可能是 JsonElement 等表示
                            String? djson;
                            if (decl is String ds) djson = ds;
                            else if (decl.GetType().FullName == "System.Text.Json.JsonElement") djson = decl.ToString();
                            else djson = decl.ToJson();
                            if (djson.IsNullOrWhiteSpace()) continue;
                            ddic = JsonParser.Decode(djson);
                        }
                        if (ddic == null) continue;

                        var name = ddic["name"] as String;
                        if (name.IsNullOrEmpty()) continue;

                        list.Add(new ChatTool
                        {
                            Type = "function",
                            Function = new FunctionDefinition
                            {
                                Name = name,
                                Description = ddic.TryGetValue("description", out var desc) ? desc as String : null,
                                // Gemini 的 parameters 直接对应 OpenAI 的 parameters
                                Parameters = ddic.TryGetValue("parameters", out var ps) ? ps : null,
                            },
                        });
                    }
                }
                _chatTools = list;
            }
            return _chatTools;
        }
        set => _chatTools = value;
    }

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

    /// <summary>推理强度</summary>
    [IgnoreDataMember]
    public String? ReasoningEffort { get; set; }

    /// <summary>是否启用思考模式</summary>
    [IgnoreDataMember]
    public Boolean? EnableThinking { get; set; }

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

    #region 转换
    /// <summary>从内部统一 ChatRequest 构建 Gemini 协议请求</summary>
    /// <param name="request">内部统一请求</param>
    /// <returns>可直接 ToJson 序列化的 Gemini 协议请求</returns>
    public static GeminiRequest FromChatRequest(IChatRequest request)
    {
        var result = new GeminiRequest
        {
            Model = request.Model,
            Stream = request.Stream,
        };

        // 分离 system 消息和普通消息
        var contents = new List<GeminiContent>();
        String? systemText = null;
        foreach (var msg in request.Messages)
        {
            if (msg.Role.Equals("system", StringComparison.OrdinalIgnoreCase))
            {
                // 多条 system 消息合并（调用方可能分段注入），避免静默覆盖
                var text = msg.Content?.ToString();
                if (!String.IsNullOrEmpty(text))
                    systemText = String.IsNullOrEmpty(systemText) ? text : systemText + "\n\n" + text;
                continue;
            }

            var role = msg.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase) ? "model" : "user";
            var parts = new List<GeminiPart>();

            // 类型化多模态内容（Contents）优先：文本→text，图片/音频→inlineData（base64）/ fileData（外部 URL）
            if (msg.Contents is { Count: > 0 })
            {
                foreach (var item in msg.Contents)
                {
                    if (item is TextContent text)
                    {
                        if (!String.IsNullOrEmpty(text.Text))
                            parts.Add(new GeminiPart { Text = text.Text });
                    }
                    else if (item is ImageContent img)
                        parts.Add(BuildBinaryPart(img.Data, img.MediaType ?? "image/jpeg", img.Uri));
                    else if (item is AudioContent audio)
                        parts.Add(BuildBinaryPart(audio.Data, audio.MediaType ?? "audio/wav", audio.Uri));
                }
            }
            else if (msg.Content != null)
            {
                parts.Add(new GeminiPart { Text = msg.Content.ToString() ?? "" });
            }

            contents.Add(new GeminiContent { Role = role, Parts = parts });
        }
        if (!String.IsNullOrEmpty(systemText))
            result.SystemInstruction = new GeminiContent { Parts = [new GeminiPart { Text = systemText }] };
        result.Contents = contents;

        // 生成配置
        var hasConfig = request.Temperature != null || request.TopP != null || request.TopK != null
            || request.MaxTokens != null || request.PresencePenalty != null || request.FrequencyPenalty != null
            || (request.Stop != null && request.Stop.Count > 0);
        if (hasConfig)
        {
            result.GenerationConfig = new GeminiGenerationConfig
            {
                Temperature = request.Temperature,
                TopP = request.TopP,
                TopK = request.TopK,
                MaxOutputTokens = request.MaxTokens,
                StopSequences = request.Stop,
                PresencePenalty = request.PresencePenalty,
                FrequencyPenalty = request.FrequencyPenalty,
            };
        }

        // 思考模式：EnableThinking → generationConfig.thinkingConfig.thinkingBudget
        // Gemini 2.5 系列：thinkingBudget>0 开启思考并设预算，0 关闭思考
        if (request.EnableThinking != null)
        {
            result.GenerationConfig ??= new GeminiGenerationConfig();
            var budget = request.EnableThinking.Value
                ? (request["ThinkingBudget"] as Int32? ?? 1024)
                : 0;
            result.GenerationConfig.ThinkingConfig = new GeminiThinkingConfig { ThinkingBudget = budget };
        }

        // 响应格式：OpenAI 风格 {type:"json_object"/"json_schema", json_schema:{...}} → Gemini responseMimeType/responseSchema
        if (request.ResponseFormat != null)
        {
            result.GenerationConfig ??= new GeminiGenerationConfig();
            ApplyResponseFormat(result.GenerationConfig, request.ResponseFormat);
        }

        // 随机种子（可复现的确定性生成，Items 键名与 DashScope 约定一致）
        var seed = request["Seed"] as Int32?;
        if (seed != null)
        {
            result.GenerationConfig ??= new GeminiGenerationConfig();
            result.GenerationConfig.Seed = seed.Value;
        }

        // 工具定义 → functionDeclarations
        if (request.Tools != null && request.Tools.Count > 0)
        {
            var declarations = new List<Object>();
            foreach (var tool in request.Tools)
            {
                if (tool.Function == null) continue;
                var fn = new Dictionary<String, Object?> { ["name"] = tool.Function.Name };
                if (tool.Function.Description != null) fn["description"] = tool.Function.Description;
                if (tool.Function.Parameters != null) fn["parameters"] = tool.Function.Parameters;
                declarations.Add(fn);
            }
            result.ToolDeclarations = [new Dictionary<String, Object> { ["functionDeclarations"] = declarations }];
        }

        // 安全设置：Gemini 原生 safetySettings（经 Items["SafetySettings"] 透传，[{category, threshold}]）
        var safety = request["SafetySettings"] as IList<Object>;
        if (safety is { Count: > 0 })
            result.SafetySettings = safety;

        return result;
    }

    /// <summary>构建 Gemini 二进制内容分片。优先 base64 数据；data URI 解析为 inlineData；其余 URL 用 fileData 尽力转换</summary>
    /// <param name="data">二进制数据</param>
    /// <param name="mediaType">媒体类型</param>
    /// <param name="uri">资源地址（data URI 或外部 URL）</param>
    /// <returns>Gemini 内容分片</returns>
    private static GeminiPart BuildBinaryPart(Byte[]? data, String? mediaType, String? uri)
    {
        // 二进制数据优先
        if (data is { Length: > 0 })
            return new GeminiPart { InlineData = new GeminiInlineData { MimeType = mediaType, Data = Convert.ToBase64String(data) } };

        // data URI：data:image/jpeg;base64,xxxx → inlineData
        var b64 = AIContentHelper.ParseDataUri(uri);
        if (b64 != null)
            return new GeminiPart { InlineData = new GeminiInlineData { MimeType = mediaType, Data = b64 } };

        // 外部 URL：fileData 尽力转换（GCS 等可访问地址，不支持时服务商显式报错而非静默丢弃）
        return new GeminiPart { FileData = new GeminiFileData { FileUri = uri } };
    }

    /// <summary>映射响应格式到 Gemini 生成配置。兼容 Dictionary（FastJson）、JsonElement（SystemJson）与 JSON 字符串等表示</summary>
    /// <param name="config">Gemini 生成配置</param>
    /// <param name="responseFormat">统一响应格式对象，OpenAI 风格 {type:"json_object"/"json_schema", json_schema:{...}}</param>
    private static void ApplyResponseFormat(GeminiGenerationConfig config, Object? responseFormat)
    {
        if (responseFormat == null) return;

        // 统一转为字典：优先直接类型，否则序列化后重新解析（兼容 JsonElement 等表示）
        var dic = responseFormat as IDictionary<String, Object?>;
        if (dic == null)
        {
            var json = responseFormat as String ?? responseFormat.ToJson();
            if (json.IsNullOrEmpty()) return;
            dic = JsonParser.Decode(json);
        }
        if (dic == null) return;

        var type = dic["type"] as String;
        if (type.IsNullOrEmpty()) return;

        if (type.EqualIgnoreCase("json_object") || type.EqualIgnoreCase("json_schema"))
        {
            config.ResponseMimeType = "application/json";

            // OpenAI 风格 json_schema：{name, schema, strict}，Gemini responseSchema 承接 schema 本体
            if (type.EqualIgnoreCase("json_schema") && dic.TryGetValue("json_schema", out var js) && js is IDictionary<String, Object> jsd)
                config.ResponseSchema = jsd.TryGetValue("schema", out var s) ? s : js;
        }
    }

    /// <summary>转换为内部统一的 ChatRequest</summary>
    /// <remarks>
    /// GeminiPart 仅建模 Text/InlineData/FileData（R7-1 补充），functionCall 与 thought 分片未建模，
    /// 因此工具调用与思考内容无法从此请求对象恢复（与 FromChatRequest 的对称性限制）。
    /// </remarks>
    /// <returns>等效的 ChatRequest 实例</returns>
    public ChatRequest ToChatRequest()
    {
        var messages = new List<ChatMessage>();

        // 系统指令转为首条系统消息
        if (SystemInstruction?.Parts.Count > 0)
        {
            var sysText = String.Join("\n", SystemInstruction.Parts
                .Where(p => !String.IsNullOrEmpty(p.Text))
                .Select(p => p.Text!));
            if (!String.IsNullOrEmpty(sysText))
                messages.Add(new ChatMessage { Role = "system", Content = sysText });
        }

        // Gemini 角色 "model" → OpenAI "assistant"
        foreach (var content in Contents)
        {
            var role = content.Role == "model" ? "assistant" : (content.Role ?? "user");
            var text = String.Join("", content.Parts.Select(p => p.Text ?? ""));
            messages.Add(new ChatMessage { Role = role, Content = text });
        }

        return new ChatRequest
        {
            Model = Model,
            Messages = messages,
            MaxTokens = GenerationConfig?.MaxOutputTokens,
            Temperature = GenerationConfig?.Temperature,
            TopP = GenerationConfig?.TopP,
            TopK = GenerationConfig?.TopK,
            Stream = Stream,
            Stop = GenerationConfig?.StopSequences,
            PresencePenalty = PresencePenalty,
            FrequencyPenalty = FrequencyPenalty,
            EnableThinking = EnableThinking,
            ResponseFormat = ResponseFormat,
            ToolChoice = ToolChoice,
            ParallelToolCalls = ParallelToolCalls,
            Tools = ((IChatRequest)this).Tools,
            User = User,
            UserId = UserId,
            ConversationId = ConversationId,
            Items = Items,
        };
    }
    #endregion
}

/// <summary>Gemini 内容对象</summary>
public class GeminiContent
{
    /// <summary>角色。user / model（Gemini 将 assistant 称为 model）</summary>
    public String? Role { get; set; }

    /// <summary>内容分片列表</summary>
    public IList<GeminiPart> Parts { get; set; } = [];
}

/// <summary>Gemini 内容分片</summary>
public class GeminiPart
{
    /// <summary>文本内容</summary>
    public String? Text { get; set; }

    /// <summary>内联二进制数据（图片/音频）。对应协议 inlineData 字段</summary>
    public GeminiInlineData? InlineData { get; set; }

    /// <summary>文件引用（外部存储）。对应协议 fileData 字段</summary>
    public GeminiFileData? FileData { get; set; }
}

/// <summary>Gemini 内联数据。用于图片/音频等二进制内容，对应协议 inlineData 字段</summary>
public class GeminiInlineData
{
    /// <summary>媒体类型。如 image/jpeg、audio/wav</summary>
    public String? MimeType { get; set; }

    /// <summary>base64 编码的二进制数据</summary>
    public String? Data { get; set; }
}

/// <summary>Gemini 文件引用。用于外部存储资源，对应协议 fileData 字段</summary>
public class GeminiFileData
{
    /// <summary>文件地址。如 gs://bucket/file.png</summary>
    public String? FileUri { get; set; }
}

/// <summary>Gemini 生成配置</summary>
public class GeminiGenerationConfig
{
    /// <summary>最大输出令牌数。对应 OpenAI 的 max_tokens</summary>
    public Int32? MaxOutputTokens { get; set; }

    /// <summary>温度</summary>
    public Double? Temperature { get; set; }

    /// <summary>核采样</summary>
    public Double? TopP { get; set; }

    /// <summary>Top-K 采样</summary>
    public Int32? TopK { get; set; }

    /// <summary>存在惩罚。正值惩罚已使用的令牌，-2~2</summary>
    public Double? PresencePenalty { get; set; }

    /// <summary>频率惩罚。正值抑制重复使用令牌，-2~2</summary>
    public Double? FrequencyPenalty { get; set; }

    /// <summary>停止序列</summary>
    public IList<String>? StopSequences { get; set; }

    /// <summary>响应 MIME 类型。如 "application/json" 启用 JSON 结构化输出</summary>
    public String? ResponseMimeType { get; set; }

    /// <summary>响应 Schema。配合 <see cref="ResponseMimeType"/> 使用，定义 JSON 输出结构</summary>
    public Object? ResponseSchema { get; set; }

    /// <summary>思考配置。thinkingBudget 大于 0 时开启思考并设预算，0 时关闭</summary>
    public GeminiThinkingConfig? ThinkingConfig { get; set; }

    /// <summary>随机种子。可复现的确定性生成</summary>
    public Int32? Seed { get; set; }
}

/// <summary>Gemini 思考配置。对应 generationConfig.thinkingConfig 字段</summary>
public class GeminiThinkingConfig
{
    /// <summary>思考预算（Token 数）。0 表示关闭思考</summary>
    public Int32? ThinkingBudget { get; set; }
}
