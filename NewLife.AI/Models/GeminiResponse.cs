using System.Runtime.Serialization;

namespace NewLife.AI.Models;

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
public class GeminiResponse
{
    #region 属性
    /// <summary>候选回复列表</summary>
    public IList<GeminiCandidate>? Candidates { get; set; }

    /// <summary>令牌用量统计</summary>
    [DataMember(Name = "usageMetadata")]
    public GeminiUsageMetadata? UsageMetadata { get; set; }
    #endregion

    #region 转换
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
    /// <summary>将内部 finish_reason 映射为 Gemini finishReason</summary>
    /// <param name="reason">内部结束原因</param>
    /// <returns>Gemini 结束原因</returns>
    private static String MapFinishReason(String? reason) => reason switch
    {
        "stop" => "STOP",
        "length" => "MAX_TOKENS",
        "content_filter" => "SAFETY",
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
