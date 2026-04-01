using System.Runtime.Serialization;
using NewLife.AI.Models;
using NewLife.Collections;
using NewLife.Serialization;

namespace NewLife.AI.Clients.Gemini;

/// <summary>Google Gemini API 响应。兼容 generateContent 协议</summary>
/// <remarks>
/// 与 OpenAI ChatCompletionResponse 的主要差异：
/// <list type="bullet">
/// <item>顶级使用 candidates 数组（非 choices），每个 candidate 含 content.parts[].text</item>
/// <item>角色为 "model"（而非 "assistant"）</item>
/// <item>使用 camelCase 命名（finishReason / usageMetadata / promptTokenCount 等），通过 DataMember 强制指定</item>
/// <item>流式与非流式结构相同，无需 data: [DONE] 结束标记</item>
/// </list>
/// </remarks>
public class GeminiResponse : IChatResponse
{
    #region 属性
    /// <summary>候选回复列表</summary>
    public IList<GeminiCandidate>? Candidates { get; set; }

    /// <summary>令牌用量统计</summary>
    [DataMember(Name = "usageMetadata")]
    public GeminiUsageMetadata? UsageMetadata { get; set; }
    #endregion

    #region IChatResponse 适配
    /// <summary>响应标识。Gemini 原生不返回此字段</summary>
    [IgnoreDataMember]
    public String? Id { get; set; }

    /// <summary>对象类型</summary>
    [IgnoreDataMember]
    String? IChatResponse.Object { get; set; }

    /// <summary>创建时间。Gemini 原生不返回此字段</summary>
    [IgnoreDataMember]
    public DateTimeOffset Created { get; set; }

    /// <summary>模型编码</summary>
    [IgnoreDataMember]
    public String? Model { get; set; }

    /// <summary>响应消息列表适配。将 Candidates 转换为 ChatChoice 列表</summary>
    [IgnoreDataMember]
    private IList<ChatChoice>? _messages;

    /// <summary>消息列表适配</summary>
    [IgnoreDataMember]
    IList<ChatChoice>? IChatResponse.Messages
    {
        get
        {
            if (_messages == null && Candidates != null)
            {
                var list = new List<ChatChoice>();
                foreach (var candidate in Candidates)
                {
                    var (text, reasoning, toolCalls) = ExtractContent(candidate);
                    var finishReason = toolCalls?.Count > 0 ? FinishReason.ToolCalls : MapGeminiFinishReason(candidate.FinishReason);
                    var chatMsg = new ChatMessage { Role = "assistant", Content = text, ReasoningContent = reasoning, ToolCalls = toolCalls };
                    var choice = new ChatChoice
                    {
                        Index = candidate.Index,
                        FinishReason = finishReason,
                        Message = chatMsg,
                        Delta = chatMsg,  // 同时设置 Delta，兼容流式场景
                    };
                    list.Add(choice);
                }
                _messages = list;
            }
            return _messages;
        }
        set => _messages = value;
    }

    /// <summary>用量统计适配</summary>
    [IgnoreDataMember]
    private UsageDetails? _usageDetails;

    /// <summary>用量统计适配</summary>
    [IgnoreDataMember]
    UsageDetails? IChatResponse.Usage
    {
        get
        {
            if (_usageDetails == null && UsageMetadata != null)
            {
                _usageDetails = new UsageDetails
                {
                    InputTokens = UsageMetadata.PromptTokenCount,
                    OutputTokens = UsageMetadata.CandidatesTokenCount,
                    TotalTokens = UsageMetadata.TotalTokenCount,
                };
            }
            return _usageDetails;
        }
        set => _usageDetails = value;
    }

    /// <summary>首条回复文本</summary>
    [IgnoreDataMember]
    public String? Text
    {
        get
        {
            var parts = Candidates?.FirstOrDefault()?.Content?.Parts;
            if (parts == null) return null;
            return String.Join("", parts.Where(p => p.Text != null).Select(p => p.Text));
        }
    }
    #endregion

    #region 转换
    /// <summary>将 Gemini 原生响应转换为内部统一 ChatResponse</summary>
    /// <param name="model">模型编码</param>
    /// <param name="streaming">是否流式响应块</param>
    /// <returns>统一的 ChatResponse</returns>
    public ChatResponse ToChatResponse(String? model = null, Boolean streaming = false)
    {
        var response = new ChatResponse
        {
            Model = model,
            Object = streaming ? "chat.completion.chunk" : "chat.completion",
        };

        if (Candidates != null)
        {
            foreach (var candidate in Candidates)
            {
                var (text, reasoning, toolCalls) = ExtractContent(candidate);
                var finishReason = toolCalls?.Count > 0 ? FinishReason.ToolCalls : MapGeminiFinishReason(candidate.FinishReason);

                ChatChoice choice;
                if (streaming)
                    choice = response.AddDelta(text, reasoning, finishReason);
                else
                    choice = response.Add(text, reasoning, finishReason);

                if (toolCalls?.Count > 0)
                {
                    var msg = streaming ? (choice.Delta ??= new ChatMessage { Role = "model" }) : (choice.Message ??= new ChatMessage { Role = "model" });
                    msg.ToolCalls = toolCalls;
                }
            }
        }

        if (UsageMetadata != null)
        {
            response.Usage = new UsageDetails
            {
                InputTokens = UsageMetadata.PromptTokenCount,
                OutputTokens = UsageMetadata.CandidatesTokenCount,
                TotalTokens = UsageMetadata.TotalTokenCount,
            };
        }

        return response;
    }

    /// <summary>从内部统一响应转换为 Gemini 非流式响应</summary>
    /// <param name="response">内部统一响应</param>
    /// <returns>Gemini 格式响应</returns>
    public static GeminiResponse From(ChatResponse response)
    {
        var candidates = new List<GeminiCandidate>();

        if (response.Messages != null)
        {
            foreach (var choice in response.Messages)
            {
                var msg = choice.Message ?? choice.Delta;
                var parts = new List<GeminiResponsePart>();
                if (msg?.Content != null)
                {
                    var text = msg.Content is String s ? s : msg.Content.ToString();
                    parts.Add(new GeminiResponsePart { Text = text });
                }

                candidates.Add(new GeminiCandidate
                {
                    Content = new GeminiResponseContent { Parts = parts, Role = "model" },
                    FinishReason = MapFinishReason(choice.FinishReason),
                    Index = choice.Index,
                });
            }
        }

        var result = new GeminiResponse { Candidates = candidates };

        if (response.Usage != null)
            result.UsageMetadata = GeminiUsageMetadata.From(response.Usage);

        return result;
    }

    /// <summary>从内部统一流式块转换为 Gemini 流式响应块。结构与非流式相同</summary>
    /// <param name="chunk">内部统一流式块</param>
    /// <returns>Gemini 格式流式块</returns>
    public static GeminiResponse FromChunk(ChatResponse chunk) => From(chunk);
    #endregion

    #region 辅助
    /// <summary>提取候选回复中的文本内容和工具调用</summary>
    private static (String text, String? reasoning, List<ToolCall>? toolCalls) ExtractContent(GeminiCandidate candidate)
    {
        if (candidate.Content?.Parts == null) return ("", null, null);

        var sb = Pool.StringBuilder.Get();
        var reasoningSb = Pool.StringBuilder.Get();
        List<ToolCall>? toolCalls = null;

        foreach (var part in candidate.Content.Parts)
        {
            if (part.Text != null)
            {
                if (part.Thought == true)
                    reasoningSb.Append(part.Text);
                else
                    sb.Append(part.Text);
            }
            else if (part.FunctionCall != null)
            {
                toolCalls ??= [];
                var argsJson = part.FunctionCall.Args is IDictionary<String, Object> argsDic
                    ? argsDic.ToJson()
                    : part.FunctionCall.Args as String ?? "{}";
                toolCalls.Add(new ToolCall
                {
                    Id = $"call_{toolCalls.Count}",
                    Type = "function",
                    Function = new FunctionCall
                    {
                        Name = part.FunctionCall.Name ?? "",
                        Arguments = argsJson,
                    },
                });
            }
        }

        var reasoning = reasoningSb.Return(true);
        return (sb.Return(true), reasoning.Length > 0 ? reasoning : null, toolCalls);
    }

    /// <summary>映射 Gemini finishReason 到标准 finish_reason</summary>
    internal static FinishReason? MapGeminiFinishReason(String? finishReason) => finishReason switch
    {
        "STOP" => FinishReason.Stop,
        "MAX_TOKENS" => FinishReason.Length,
        "SAFETY" or "RECITATION" => FinishReason.ContentFilter,
        _ => null,
    };

    /// <summary>将内部 finish_reason 映射为 Gemini finishReason</summary>
    /// <param name="reason">内部结束原因</param>
    /// <returns>Gemini 结束原因</returns>
    private static String MapFinishReason(FinishReason? reason) => reason switch
    {
        FinishReason.Stop => "STOP",
        FinishReason.Length => "MAX_TOKENS",
        FinishReason.ContentFilter => "SAFETY",
        _ => "STOP",
    };
    #endregion
}

/// <summary>Gemini 候选回复</summary>
public class GeminiCandidate
{
    /// <summary>回复内容</summary>
    public GeminiResponseContent? Content { get; set; }

    /// <summary>结束原因。STOP/MAX_TOKENS/SAFETY</summary>
    [DataMember(Name = "finishReason")]
    public String? FinishReason { get; set; }

    /// <summary>序号</summary>
    public Int32 Index { get; set; }
}

/// <summary>Gemini 回复内容</summary>
public class GeminiResponseContent
{
    /// <summary>内容片段列表</summary>
    public IList<GeminiResponsePart>? Parts { get; set; }

    /// <summary>角色。固定 "model"</summary>
    public String? Role { get; set; }
}

/// <summary>Gemini 内容片段</summary>
public class GeminiResponsePart
{
    /// <summary>文本内容</summary>
    public String? Text { get; set; }

    /// <summary>是否为思考内容。Gemini 2.5 模型 thinking 部分返回 thought=true</summary>
    public Boolean? Thought { get; set; }

    /// <summary>函数调用</summary>
    [DataMember(Name = "functionCall")]
    public GeminiFunctionCall? FunctionCall { get; set; }
}

/// <summary>Gemini 函数调用</summary>
public class GeminiFunctionCall
{
    /// <summary>函数名称</summary>
    public String? Name { get; set; }

    /// <summary>函数参数</summary>
    public Object? Args { get; set; }
}

/// <summary>Gemini 令牌用量统计</summary>
public class GeminiUsageMetadata
{
    /// <summary>提示令牌数</summary>
    [DataMember(Name = "promptTokenCount")]
    public Int32 PromptTokenCount { get; set; }

    /// <summary>候选回复令牌数</summary>
    [DataMember(Name = "candidatesTokenCount")]
    public Int32 CandidatesTokenCount { get; set; }

    /// <summary>总令牌数</summary>
    [DataMember(Name = "totalTokenCount")]
    public Int32 TotalTokenCount { get; set; }

    /// <summary>从内部用量统计转换</summary>
    /// <param name="usage">内部用量统计</param>
    /// <returns>Gemini 格式用量</returns>
    public static GeminiUsageMetadata From(UsageDetails usage) => new()
    {
        PromptTokenCount = usage.InputTokens,
        CandidatesTokenCount = usage.OutputTokens,
        TotalTokenCount = usage.TotalTokens,
    };
}
