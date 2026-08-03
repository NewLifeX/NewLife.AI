namespace NewLife.AI.Clients;

/// <summary>AI 模型 Token 定价。携带输入/输出/缓存命中/缓存创建四档价格（元/百万Token），0 表示未知</summary>
/// <param name="InputPrice">输入价格，元/百万Token。非Token模式（图片/视频/语音/嵌入/重排序）此字段承载单价</param>
/// <param name="OutputPrice">输出价格，元/百万Token。非Token模式为0</param>
/// <param name="CachedInputPrice">缓存命中价格，元/百万Token。0 时调用方回退到 InputPrice×0.1。非Token模式为0</param>
/// <param name="CacheCreationPrice">缓存创建价格，元/百万Token。0 时调用方回退到 InputPrice。非Token模式为0</param>
public record AiModelPricing(
    Decimal InputPrice = 0,
    Decimal OutputPrice = 0,
    Decimal CachedInputPrice = 0,
    Decimal CacheCreationPrice = 0);

/// <summary>AI 服务商默认能力信息。表示该服务商主力模型的典型能力</summary>
/// <remarks>这些是服务商级别的默认值，用户创建具体模型配置时可按实际模型覆盖</remarks>
/// <param name="SupportThinking">是否支持思考模式。如 DeepSeek-R1、Claude 的 extended thinking</param>
/// <param name="SupportFunction">是否支持 Function Calling / Tool Use</param>
/// <param name="SupportVision">是否支持图片/视频帧输入（视觉）。如 GPT-4V、Claude Vision、Qwen-VL</param>
/// <param name="SupportAudio">是否支持语音识别/音频输入（ASR）。如 Whisper、Qwen-Omni 的语音输入</param>
/// <param name="SupportSpeech">是否支持语音合成/音频输出（TTS）。如 CosyVoice、Qwen-TTS、Qwen-Omni 的语音输出</param>
/// <param name="SupportImage">是否支持文生图。如 DALL·E、Qwen 的图像生成</param>
/// <param name="SupportVideo">是否支持文生视频。如 Sora、Wan2</param>
/// <param name="SupportEmbedding">是否支持嵌入向量。如 text-embedding-3、text-embedding-v3，可用于 RAG/知识库场景</param>
/// <param name="SupportRerank">是否支持重排序。如 Qwen3-Rerank，用于 RAG 重排序场景</param>
/// <param name="ContextLength">上下文窗口大小（Token 数）。0 表示未知</param>
/// <param name="ReasoningEfforts">推理强度选项。逗号分隔的可用值，如 "high,max"；空=不支持</param>
/// <param name="Pricing">Token 定价信息。null 表示未知，由调用方按能力分级填充兜底价</param>
public record AiProviderCapabilities(
    Boolean SupportThinking = false,
    Boolean SupportFunction = false,
    Boolean SupportVision = false,
    Boolean SupportAudio = false,
    Boolean SupportSpeech = false,
    Boolean SupportImage = false,
    Boolean SupportVideo = false,
    Boolean SupportEmbedding = false,
    Boolean SupportRerank = false,
    Int32 ContextLength = 0,
    String? ReasoningEfforts = null,
    AiModelPricing? Pricing = null);

/// <summary>AI 模型信息。描述服务商旗下某具体模型的标识与能力</summary>
/// <param name="Model">模型标识，即 API 请求中 model 字段的值，如 "gpt-4o"</param>
/// <param name="DisplayName">模型显示名称，用于界面展示，如 "GPT-4o"</param>
/// <param name="Capabilities">该模型支持的能力</param>
/// <param name="Pricing">该模型的 Token 定价信息，null 表示未知</param>
public record AiModelInfo(String Model, String DisplayName, AiProviderCapabilities Capabilities, AiModelPricing? Pricing = null) { }

/// <summary>AI 客户端连接选项</summary>
public class AiClientOptions
{
    /// <summary>服务商编码。配置驱动场景下用于指定服务商，如 OpenAI、DashScope</summary>
    public String? Code { get; set; }

    /// <summary>API 地址。为空时使用服务商默认地址</summary>
    public String? Endpoint { get; set; }

    /// <summary>API 密钥</summary>
    public String? ApiKey { get; set; }

    /// <summary>组织编号。部分服务商需要（如 OpenAI）</summary>
    public String? Organization { get; set; }

    /// <summary>默认模型编码。客户端每次调用时若未指定模型则使用此值</summary>
    public String? Model { get; set; }

    /// <summary>协议覆盖。DashScope 等双协议服务商可通过此字段切换"DashScope"原生协议或"ChatCompletions"兼容协议</summary>
    public String? Protocol { get; set; }

    /// <summary>HTTP 请求超时时间。为空时使用 AiClientBase 默认值（300秒）</summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>失败重试次数。0 表示不重试；对 429 限流、5xx 服务端错误、网络异常（含超时）自动指数退避重试，4xx 客户端错误不重试</summary>
    public Int32 RetryCount { get; set; }

    /// <summary>重试基础间隔（毫秒）。实际等待 = 基础间隔 × 2^重试序号，上限 30 秒。默认 1000</summary>
    public Int32 RetryIntervalMs { get; set; } = 1000;

    /// <summary>获取实际使用的 API 地址</summary>
    /// <param name="defaultEndpoint">默认地址</param>
    /// <returns></returns>
    public String GetEndpoint(String defaultEndpoint) => Endpoint.IsNullOrWhiteSpace() ? defaultEndpoint : Endpoint;
}

/// <summary>AI 客户端描述符。描述一个 AI 服务商的元数据及客户端创建工厂。替代原 IAiProvider 接口</summary>
/// <remarks>
/// <para>设计原则：纯数据对象，描述服务商元数据并持有创建 IChatClient 的工厂委托。</para>
/// <para>原 34 个 xxxProvider 类全部替换为 <see cref="AiClientDescriptor"/> 实例，注册在 AiClientRegistry 中。</para>
/// </remarks>
public class AiClientDescriptor
{
    /// <summary>服务商编码。唯一标识，如 OpenAI、DashScope、DeepSeek 等</summary>
    public String Code { get; set; } = "";

    /// <summary>服务商显示名称。用于界面展示，如"OpenAI"、"阿里百炼"</summary>
    public String DisplayName { get; set; } = "";

    /// <summary>服务商描述</summary>
    public String? Description { get; set; }

    /// <summary>默认 API 地址</summary>
    public String DefaultEndpoint { get; set; } = "";

    /// <summary>API 协议类型。OpenAI / AnthropicMessages / Gemini / DashScope / Ollama</summary>
    public String Protocol { get; set; } = "OpenAI";

    /// <summary>主流模型列表。该服务商下各主流模型及其能力描述，供用户选择配置时参考</summary>
    public AiModelInfo[] Models { get; set; } = [];

    /// <summary>客户端工厂。根据连接选项创建 IChatClient 实例</summary>
    /// <remarks>每次调用均创建新实例，调用方负责释放（using）</remarks>
    public Func<AiClientOptions, IChatClient> Factory { get; set; } = _ => throw new InvalidOperationException("未配置 Factory");

    /// <summary>按模型 ID 查找已注册的模型能力信息</summary>
    /// <remarks>
    /// 优先精确匹配（大小写不敏感），未命中时尝试前缀匹配以覆盖带日期版本后缀的变体（如 qwen3-max-2025-01-01 → qwen3-max）
    /// </remarks>
    /// <param name="modelId">模型标识</param>
    /// <returns>匹配的能力信息，未找到返回 null</returns>
    public AiProviderCapabilities? FindModelCapabilities(String? modelId)
    {
        if (modelId.IsNullOrEmpty() || Models.Length == 0) return null;

        // 精确匹配
        foreach (var m in Models)
        {
            if (String.Equals(m.Model, modelId, StringComparison.OrdinalIgnoreCase))
                return m.Capabilities;
        }

        // 前缀匹配：已注册模型作为前缀匹配远端返回的带版本后缀变体
        foreach (var m in Models)
        {
            if (modelId.StartsWith(m.Model, StringComparison.OrdinalIgnoreCase) &&
                modelId.Length > m.Model.Length &&
                (modelId[m.Model.Length] == '-' || modelId[m.Model.Length] == ':'))
                return m.Capabilities;
        }

        return null;
    }

    /// <summary>按模型 ID 查找已注册的完整模型信息（显示名称 + 能力）</summary>
    /// <remarks>匹配规则与 <see cref="FindModelCapabilities"/> 相同：精确匹配优先，其次前缀匹配带版本后缀的变体</remarks>
    /// <param name="modelId">模型标识</param>
    /// <returns>匹配的模型信息，未找到返回 null</returns>
    public AiModelInfo? FindModelInfo(String? modelId)
    {
        if (modelId.IsNullOrEmpty() || Models.Length == 0) return null;

        // 精确匹配
        foreach (var m in Models)
        {
            if (String.Equals(m.Model, modelId, StringComparison.OrdinalIgnoreCase))
                return m;
        }

        // 前缀匹配：已注册模型作为前缀匹配远端返回的带版本后缀变体
        foreach (var m in Models)
        {
            if (modelId!.StartsWith(m.Model, StringComparison.OrdinalIgnoreCase) &&
                modelId.Length > m.Model.Length &&
                (modelId[m.Model.Length] == '-' || modelId[m.Model.Length] == ':'))
                return m;
        }

        return null;
    }

    /// <inheritdoc/>
    public override String ToString() => $"{Code} ({DisplayName})";
}
