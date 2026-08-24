using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;

namespace NewLife.AI.Clients;

/// <summary>模型能力规则。按 glob 模式匹配模型标识，将非空能力位合并到推断结果</summary>
/// <remarks>
/// 规则按注册顺序应用，后应用的规则覆盖先应用的同名能力位（用于"先整体设置、再局部排除"）。
/// 模式支持 glob 通配：* 匹配任意序列、? 匹配单个字符、| 分隔多备选；模式两端锚定，须匹配整个模型标识。
/// </remarks>
public sealed record ModelCapabilityRule
{
    /// <summary>模型标识 glob 模式，如 qwen3*、qwen3.*-plus、deepseek-v4-*</summary>
    public String Pattern { get; init; } = "";

    /// <summary>是否支持思考。null 表示不修改该能力位</summary>
    public Boolean? Thinking { get; init; }

    /// <summary>是否支持函数调用。null 表示不修改该能力位</summary>
    public Boolean? FunctionCalling { get; init; }

    /// <summary>是否支持视觉。null 表示不修改该能力位</summary>
    public Boolean? Vision { get; init; }

    /// <summary>是否支持音频输入（ASR）。null 表示不修改该能力位</summary>
    public Boolean? Audio { get; init; }

    /// <summary>是否支持语音合成（TTS）。null 表示不修改该能力位</summary>
    public Boolean? Speech { get; init; }

    /// <summary>是否支持文生图。null 表示不修改该能力位</summary>
    public Boolean? ImageGeneration { get; init; }

    /// <summary>是否支持文生视频。null 表示不修改该能力位</summary>
    public Boolean? VideoGeneration { get; init; }

    /// <summary>是否支持嵌入向量。null 表示不修改该能力位</summary>
    public Boolean? Embedding { get; init; }

    /// <summary>是否支持重排序。null 表示不修改该能力位</summary>
    public Boolean? Rerank { get; init; }

    /// <summary>上下文窗口大小（Token 数）。0 表示不修改</summary>
    public Int32 ContextLength { get; init; }

    /// <summary>推理强度选项。null 表示不修改</summary>
    public String? ReasoningEfforts { get; init; }

    /// <summary>默认 Token 定价。null 表示不修改</summary>
    public AiModelPricing? Pricing { get; init; }

    /// <summary>将本规则非空能力位合并到指定能力上，返回新能力</summary>
    /// <param name="caps">当前能力</param>
    /// <returns>合并后的能力</returns>
    public AiProviderCapabilities Apply(AiProviderCapabilities caps) => new(
        Thinking ?? caps.SupportThinking,
        FunctionCalling ?? caps.SupportFunction,
        Vision ?? caps.SupportVision,
        Audio ?? caps.SupportAudio,
        Speech ?? caps.SupportSpeech,
        ImageGeneration ?? caps.SupportImage,
        VideoGeneration ?? caps.SupportVideo,
        Embedding ?? caps.SupportEmbedding,
        Rerank ?? caps.SupportRerank,
        ContextLength > 0 ? ContextLength : caps.ContextLength,
        ReasoningEfforts ?? caps.ReasoningEfforts,
        Pricing ?? caps.Pricing);
}

/// <summary>模型家族档案。描述一个模型系列（如 qwen、deepseek）的命名规律与能力规律，供各服务商共享复用</summary>
/// <remarks>
/// 家族能力定义一次，任何服务商（含 OpenAI 兼容的第三方平台）发现该家族模型时均可复用，
/// 实现"同一模型家族在不同平台（DashScope/腾讯/火山等）能力一致"。
/// 服务商专属差异（价格、显示名）用 <see cref="AiClientModelAttribute"/> 精确注册覆盖。
/// </remarks>
public sealed class ModelFamily(String name, String pattern, Int32 defaultContext = 0)
{
    /// <summary>家族名称。如 qwen、qwen-media、deepseek、qwq、qvq</summary>
    public String Name { get; } = name;

    /// <summary>家族成员判定 glob 模式。模型标识匹配该模式即属于本家族，如 qwen*、deepseek*</summary>
    public String Pattern { get; } = pattern;

    /// <summary>家族默认上下文窗口（Token 数）。0 表示未知，作为推断起点，具体规则可覆盖</summary>
    public Int32 DefaultContext { get; } = defaultContext;

    /// <summary>有序能力规则。按顺序应用，后规则覆盖先规则</summary>
    public ModelCapabilityRule[] Rules { get; init; } = [];

    /// <summary>判断模型标识是否属于本家族</summary>
    /// <param name="modelId">模型标识</param>
    /// <returns>true 表示属于本家族</returns>
    public Boolean IsMatch(String modelId) => GlobMatch(Pattern, modelId);

    /// <summary>按本家族规则推断模型能力。模型不属于本家族时返回 null</summary>
    /// <param name="modelId">模型标识</param>
    /// <returns>推断出的能力，不属于本家族时返回 null</returns>
    public AiProviderCapabilities? Infer(String modelId)
    {
        if (!IsMatch(modelId)) return null;

        // 对话模型默认支持函数调用；具体规则可关闭（如 reasoner、mt 等专用模型）
        var caps = new AiProviderCapabilities(SupportFunction: true, ContextLength: DefaultContext);
        foreach (var rule in Rules)
        {
            if (GlobMatch(rule.Pattern, modelId)) caps = rule.Apply(caps);
        }
        return caps;
    }

    #region glob 匹配
    private static readonly ConcurrentDictionary<String, Regex> _cache = new();

    /// <summary>glob 模式匹配。* 匹配任意序列、? 匹配单个字符、| 分隔多备选；两端锚定（须匹配整个模型标识）</summary>
    /// <param name="pattern">glob 模式</param>
    /// <param name="input">模型标识</param>
    /// <returns>true 表示匹配</returns>
    public static Boolean GlobMatch(String pattern, String input)
    {
        if (pattern.IsNullOrEmpty() || input.IsNullOrEmpty()) return false;

        // 拆分 | 备选后逐个匹配，避免"^a|b$"的正则优先级陷阱
        var alternatives = pattern.Split('|');
        foreach (var alt in alternatives)
        {
            var regex = _cache.GetOrAdd(alt, BuildRegex);
            if (regex.IsMatch(input)) return true;
        }
        return false;
    }

    /// <summary>将 glob 模式编译为正则。* → .*、? → .，其余转义，两端锚定</summary>
    /// <param name="pattern">glob 模式</param>
    /// <returns>编译后的正则</returns>
    private static Regex BuildRegex(String pattern)
    {
        var sb = new StringBuilder();
        sb.Append('^');
        foreach (var ch in pattern)
        {
            switch (ch)
            {
                case '*': sb.Append(".*"); break;
                case '?': sb.Append('.'); break;
                default: sb.Append(Regex.Escape(ch.ToString())); break;
            }
        }
        sb.Append('$');
        return new Regex(sb.ToString(), RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }
    #endregion
}
