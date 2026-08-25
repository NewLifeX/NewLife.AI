namespace NewLife.AI.Clients;

/// <summary>内置模型家族档案。集中定义主流模型系列的命名规律与能力规律（qwen/deepseek/qwq/qvq）</summary>
/// <remarks>
/// <para>家族能力定义一次、全局共享：任何服务商发现家族内模型时自动按规则推断能力，新版本（如 qwen3.8）零改动自动覆盖。</para>
/// <para>跨平台共享：deepseek 家族在 DashScope/腾讯/火山等任何 OpenAI 兼容平台托管时能力一致；
/// 服务商专属价格用 <see cref="AiClientModelAttribute"/> 精确注册覆盖（其优先级高于家族默认价）。</para>
/// <para>规则语义：按顺序应用，后规则覆盖先规则（用于"先整体设置、再局部排除"），详见 <see cref="ModelCapabilityRule"/>。</para>
/// </remarks>
public static class ModelFamilies
{
    static ModelFamilies()
    {
        ModelFamilyRegistry.Register(CreateQwenMedia());
        ModelFamilyRegistry.Register(CreateQwen());
        ModelFamilyRegistry.Register(CreateQwq());
        ModelFamilyRegistry.Register(CreateQvq());
        ModelFamilyRegistry.Register(CreateDeepSeek());
    }

    /// <summary>确保内置家族已注册。通过触发本类型静态构造函数完成注册（幂等）</summary>
    public static void EnsureRegistered() { }

    /// <summary>qwen 媒体模型家族：语音合成 / 文生图 / 语音识别。先于 qwen 对话家族匹配</summary>
    private static ModelFamily CreateQwenMedia() => new("qwen-media",
        "qwen-tts*|qwen3-tts*|qwen-image*|qwen-audio*|qwen3-asr*|qwen-voice*")
    {
        Rules =
        [
            R("qwen-tts*|qwen3-tts*", func: false, speech: true, pricing: Price(0.2m)),
            R("qwen-image*", func: false, image: true, pricing: Price(0.2m)),
            R("qwen-audio*|qwen3-asr*|qwen-voice*", func: false, audio: true, pricing: Price(0.2m)),
        ],
    };

    /// <summary>qwen 对话家族。基于 2026-Q2 官方命名规律，通配覆盖 qwen3.x 全部新版本</summary>
    private static ModelFamily CreateQwen() => new("qwen", "qwen*", 32_768)
    {
        Rules =
        [
            // 专用翻译模型：不支持函数调用
            R("qwen-mt*", func: false),

            // 视觉语言系列（含 qwen3-vl、qwen-vl 及带版本后缀变体）
            R("qwen*-vl*", vision: true),

            // === 思考能力 ===
            // qwen3 时代全系列（含 qwen3.5/3.6/3.7/3.8 等新版本）默认支持思考
            R("qwen3*", thinking: true),
            // coder 与 -instruct 后缀为指令微调版，不支持思考
            R("qwen3-coder*", thinking: false),
            R("qwen3*-instruct*", thinking: false),
            // 稳定版别名均指向 qwen3 时代（含日期变体）
            R("qwen-max*|qwen-plus*|qwen-flash*|qwen-turbo*", thinking: true),
            // 旧模型不支持思考
            R("qwen-long*|qwen2*|qwen1*", thinking: false),

            // === 视觉（qwen3.x 系列分档：-plus/-flash/-turbo 多模态，-max 纯文本；* 容忍日期后缀） ===
            R("qwen3.*-plus*", vision: true),
            R("qwen3.*-flash*", vision: true),
            R("qwen3.*-turbo*", vision: true),
            R("qwen3.*-max*", vision: false),

            // === Omni 全模态（覆盖 qwen3* 思考规则，晚于思考段） ===
            R("qwen3.5-omni*", thinking: false, func: false, vision: true, audio: true, speech: true, context: 131_072),
            R("qwen3-omni*", thinking: true, func: false, vision: true, audio: true, speech: true, context: 131_072),
            R("qwen*-omni*", func: false, vision: true, audio: true, speech: true, context: 32_768),

            // === 上下文长度（兜底在前、具体规则在后覆盖；qwen3* 兜底曾排在 3.7/3.6 之后，导致 1M 被 131K 覆盖） ===
            R("qwen-long*", context: 1_000_000),
            R("qwen3*", context: 131_072),
            R("qwen3.7*", context: 1_048_576),
            R("qwen3.6*", context: 1_048_576),
            R("qwen3.6-max-preview*", context: 262_144),
            R("qwen-max*|qwen-plus*|qwen-flash*|qwen-turbo*|qwen2.5*", context: 131_072),

            // === 默认价格（元/百万Token，兜底价在前，具体系列价在后覆盖；服务商可精确注册覆盖） ===
            R("qwen*", pricing: Price(0.7m, 2.8m, 0.07m)),
            R("qwen-vl-max*", pricing: Price(3m, 18m, 0.3m)),
            R("qwen-vl-plus*", pricing: Price(1.5m, 9m, 0.15m)),
            R("qwen3.5-omni*|qwen3-omni*", pricing: Price(3.5m, 14m, 0.35m)),
            R("qwen*-omni*", pricing: Price(2m, 8m, 0.2m)),
            R("qwen3.7-max*", pricing: Price(12m, 36m, 2.4m, 15m)),
            R("qwen3.7-plus*", pricing: Price(2m, 8m, 0.4m, 2.5m)),
            R("qwen3.6-max*", pricing: Price(2m, 12m, 0.2m)),
            R("qwen3.6-plus*", pricing: Price(1.4m, 5.6m, 0.14m)),
            R("qwen3.6-flash*", pricing: Price(0.7m, 2.8m, 0.07m)),
            R("qwen3-max*", pricing: Price(2.4m, 14.4m, 0.24m)),
            R("qwen3-plus*", pricing: Price(0.8m, 3.2m, 0.08m)),
            R("qwen3-turbo*", pricing: Price(0.3m, 1.2m, 0.03m)),
            R("qwen3-235b*", pricing: Price(2m, 8m, 0.2m)),
            R("qwen-max*", pricing: Price(2.4m, 14.4m, 0.24m)),
            R("qwen-plus*", pricing: Price(0.8m, 3.2m, 0.08m)),
            R("qwen-turbo*", pricing: Price(0.3m, 1.2m, 0.03m)),
        ],
    };

    /// <summary>qwq 专用推理家族</summary>
    private static ModelFamily CreateQwq() => new("qwq", "qwq*", 131_072)
    {
        Rules =
        [
            R("qwq*", thinking: true, pricing: Price(2m, 12m, 0.2m)),
        ],
    };

    /// <summary>qvq 视觉推理家族</summary>
    private static ModelFamily CreateQvq() => new("qvq", "qvq*", 131_072)
    {
        Rules =
        [
            R("qvq*", thinking: true, vision: true, pricing: Price(2.4m, 14.4m, 0.24m)),
        ],
    };

    /// <summary>deepseek 家族。能力跨平台共享：官方、DashScope、腾讯、火山等托管 deepseek 时能力一致</summary>
    private static ModelFamily CreateDeepSeek() => new("deepseek", "deepseek*")
    {
        Rules =
        [
            // 兜底（最先）：v4+ 新版本默认思考 + 工具 + 1M，被后续更具体规则覆盖
            R("deepseek*", thinking: true, func: true, context: 1_048_576, pricing: Price(3m, 9m, 0.1m)),
            // reasoner：始终思考，不支持工具调用与采样参数，上下文 1M
            R("deepseek-reasoner*", thinking: true, func: false, context: 1_048_576, efforts: "high,max", pricing: Price(9m, 27m, 0.3m)),
            // V4 标准版：思考 + 工具调用 + 1M
            R("deepseek-v4-pro*", thinking: true, func: true, context: 1_048_576, efforts: "high,max", pricing: Price(9m, 27m, 0.3m)),
            // V4 快速版：思考 + 工具调用 + 1M
            R("deepseek-v4-flash*", thinking: true, func: true, context: 1_048_576, efforts: "high,max", pricing: Price(3m, 9m, 0.1m)),
            // chat 别名：不思考，支持工具调用
            R("deepseek-chat*", thinking: false, func: true, context: 1_048_576, efforts: "high,max", pricing: Price(3m, 9m, 0.1m)),
            // R1：始终思考，不支持工具调用，上下文 65K
            R("deepseek-r1*", thinking: true, func: false, context: 65_536, pricing: Price(9m, 27m, 0.3m)),
        ],
    };

    /// <summary>创建能力规则。null 参数表示不修改对应能力位</summary>
    /// <param name="pattern">glob 模式</param>
    /// <param name="thinking">是否支持思考</param>
    /// <param name="func">是否支持函数调用</param>
    /// <param name="vision">是否支持视觉</param>
    /// <param name="audio">是否支持音频输入</param>
    /// <param name="speech">是否支持语音合成</param>
    /// <param name="image">是否支持文生图</param>
    /// <param name="video">是否支持文生视频</param>
    /// <param name="context">上下文窗口大小，0 不修改</param>
    /// <param name="efforts">推理强度选项</param>
    /// <param name="pricing">默认定价</param>
    /// <returns>能力规则</returns>
    private static ModelCapabilityRule R(String pattern, Boolean? thinking = null, Boolean? func = null,
        Boolean? vision = null, Boolean? audio = null, Boolean? speech = null, Boolean? image = null,
        Boolean? video = null, Int32 context = 0, String? efforts = null, AiModelPricing? pricing = null)
        => new()
        {
            Pattern = pattern,
            Thinking = thinking,
            FunctionCalling = func,
            Vision = vision,
            Audio = audio,
            Speech = speech,
            ImageGeneration = image,
            VideoGeneration = video,
            ContextLength = context,
            ReasoningEfforts = efforts,
            Pricing = pricing,
        };

    /// <summary>创建默认定价</summary>
    /// <param name="input">输入价格，元/百万Token</param>
    /// <param name="output">输出价格，元/百万Token，默认 0</param>
    /// <param name="cached">缓存命中价格，0 时调用方回退到输入价×0.1</param>
    /// <param name="cacheCreate">缓存创建价格，0 时调用方回退到输入价</param>
    /// <returns>定价</returns>
    private static AiModelPricing Price(Decimal input, Decimal output = 0, Decimal cached = 0, Decimal cacheCreate = 0)
        => new(input, output, cached, cacheCreate);
}
