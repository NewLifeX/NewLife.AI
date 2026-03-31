using System.Runtime.Serialization;

namespace NewLife.AI.Models;

/// <summary>Google Gemini generateContent 请求体。兼容 https://ai.google.dev/api/generate-content 协议（camelCase 格式）</summary>
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
public class GeminiRequest
{
    #region 属性
    /// <summary>模型编码。Gemini 原生 API 将模型置于 URL 路径，此处作为扩展字段</summary>
    [IgnoreDataMember]
    public String? Model { get; set; }

    /// <summary>对话内容列表。role 为 user / model</summary>
    public IList<GeminiContent> Contents { get; set; } = [];

    /// <summary>系统指令</summary>
    [DataMember(Name = "systemInstruction")]
    public GeminiContent? SystemInstruction { get; set; }

    /// <summary>生成配置</summary>
    [DataMember(Name = "generationConfig")]
    public GeminiGenerationConfig? GenerationConfig { get; set; }

    /// <summary>是否流式输出。Gemini 原生通过不同端点区分，此处作为扩展字段</summary>
    [IgnoreDataMember]
    public Boolean Stream { get; set; }

    /// <summary>工具定义列表。Gemini 格式：[{functionDeclarations:[...]}]</summary>
    public IList<Object>? Tools { get; set; }

    /// <summary>工具声明列表。仅用于 FromChatRequest 构建时临时存储，序列化时由 Tools 输出</summary>
    [IgnoreDataMember]
    internal IList<Object>? ToolDeclarations { get => Tools; set => Tools = value; }
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
        foreach (var msg in request.Messages)
        {
            if (msg.Role.Equals("system", StringComparison.OrdinalIgnoreCase))
            {
                var text = msg.Content?.ToString();
                if (!String.IsNullOrEmpty(text))
                    result.SystemInstruction = new GeminiContent { Parts = [new GeminiPart { Text = text }] };
                continue;
            }

            var role = msg.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase) ? "model" : "user";
            var parts = new List<GeminiPart>();
            if (msg.Content != null)
                parts.Add(new GeminiPart { Text = msg.Content.ToString() ?? "" });

            contents.Add(new GeminiContent { Role = role, Parts = parts });
        }
        result.Contents = contents;

        // 生成配置
        var hasConfig = request.Temperature != null || request.TopP != null || request.TopK != null
            || request.MaxTokens != null || (request.Stop != null && request.Stop.Count > 0);
        if (hasConfig)
        {
            result.GenerationConfig = new GeminiGenerationConfig
            {
                Temperature = request.Temperature,
                TopP = request.TopP,
                TopK = request.TopK,
                MaxOutputTokens = request.MaxTokens,
                StopSequences = request.Stop,
            };
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

        return result;
    }

    /// <summary>转换为内部统一的 ChatRequest</summary>
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
}

/// <summary>Gemini 生成配置</summary>
public class GeminiGenerationConfig
{
    /// <summary>最大输出令牌数。对应 OpenAI 的 max_tokens</summary>
    [DataMember(Name = "maxOutputTokens")]
    public Int32? MaxOutputTokens { get; set; }

    /// <summary>温度</summary>
    public Double? Temperature { get; set; }

    /// <summary>核采样</summary>
    public Double? TopP { get; set; }

    /// <summary>Top-K 采样</summary>
    public Int32? TopK { get; set; }

    /// <summary>停止序列</summary>
    [DataMember(Name = "stopSequences")]
    public IList<String>? StopSequences { get; set; }
}
