namespace NewLife.AI.Clients;

/// <summary>内置模型家族档案。集中定义主流模型系列的命名规律与能力规律（qwen/deepseek/hunyuan/glm/gpt/doubao/minimax/kimi 等）</summary>
/// <remarks>
/// <para>家族能力定义一次、全局共享：任何服务商发现家族内模型时自动按规则推断能力，新版本（如 qwen3.8、glm-5.x、gpt-5.x）零改动自动覆盖。</para>
/// <para>跨平台共享：同一模型家族在 DashScope/腾讯/火山/智谱/Moonshot 等任何 OpenAI 兼容平台托管时能力一致。
/// 家族只承载模型特性（思考/工具/视觉/音频/上下文/推理强度等），不含价格；价格由服务商层决定——
/// <see cref="AiClientModelAttribute"/> 精确注册 + 服务商层通用价格探测（<see cref="OpenAIClientBase"/>）+
/// 部署侧模型元数据表（ModelData/*.json）运行时覆盖。</para>
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
        ModelFamilyRegistry.Register(CreateHunyuan());
        ModelFamilyRegistry.Register(CreateGlm());
        ModelFamilyRegistry.Register(CreateGpt());
        ModelFamilyRegistry.Register(CreateClaude());
        ModelFamilyRegistry.Register(CreateGemini());
        ModelFamilyRegistry.Register(CreateDoubao());
        ModelFamilyRegistry.Register(CreateMiniMax());
        ModelFamilyRegistry.Register(CreateKimi());
        ModelFamilyRegistry.Register(CreateErnie());
        ModelFamilyRegistry.Register(CreateSpark());
        ModelFamilyRegistry.Register(CreateGrok());
        ModelFamilyRegistry.Register(CreateMistral());
        ModelFamilyRegistry.Register(CreateLlama());
        ModelFamilyRegistry.Register(CreateGemma());
    }

    /// <summary>确保内置家族已注册。通过触发本类型静态构造函数完成注册（幂等）</summary>
    public static void EnsureRegistered() { }

    /// <summary>qwen 媒体模型家族：语音合成 / 文生图 / 语音识别。先于 qwen 对话家族匹配</summary>
    private static ModelFamily CreateQwenMedia() => new("qwen-media",
        "qwen-tts*|qwen3-tts*|qwen-image*|qwen-audio*|qwen3-asr*|qwen-voice*")
    {
        Rules =
        [
            R("qwen-tts*|qwen3-tts*", func: false, speech: true),
            R("qwen-image*", func: false, image: true),
            R("qwen-audio*|qwen3-asr*|qwen-voice*", func: false, audio: true),
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
            // qwen3.8-max 为多模态旗舰（文本+图像+视频理解），覆盖 -max 纯文本规律
            R("qwen3.8-max*", vision: true),

            // === Omni 全模态（覆盖 qwen3* 思考规则，晚于思考段） ===
            R("qwen3.5-omni*", thinking: false, func: false, vision: true, audio: true, speech: true, context: 131_072),
            R("qwen3-omni*", thinking: true, func: false, vision: true, audio: true, speech: true, context: 131_072),
            R("qwen*-omni*", func: false, vision: true, audio: true, speech: true, context: 32_768),

            // === 上下文长度（兜底在前、具体规则在后覆盖；qwen3* 兜底曾排在 3.7/3.6 之后，导致 1M 被 131K 覆盖） ===
            R("qwen-long*", context: 1_000_000),
            R("qwen3*", context: 131_072),
            R("qwen3.8*", context: 1_048_576),
            R("qwen3.7*", context: 1_048_576),
            R("qwen3.6*", context: 1_048_576),
            R("qwen3.6-max-preview*", context: 262_144),
            R("qwen-max*|qwen-plus*|qwen-flash*|qwen-turbo*|qwen2.5*", context: 131_072),
        ],
    };

    /// <summary>qwq 专用推理家族</summary>
    private static ModelFamily CreateQwq() => new("qwq", "qwq*", 131_072)
    {
        Rules =
        [
            R("qwq*", thinking: true),
        ],
    };

    /// <summary>qvq 视觉推理家族</summary>
    private static ModelFamily CreateQvq() => new("qvq", "qvq*", 131_072)
    {
        Rules =
        [
            R("qvq*", thinking: true, vision: true),
        ],
    };

    /// <summary>deepseek 家族。能力跨平台共享：官方、DashScope、腾讯、火山等托管 deepseek 时能力一致</summary>
    private static ModelFamily CreateDeepSeek() => new("deepseek", "deepseek*")
    {
        Rules =
        [
            // 兜底（最先）：v4+ 新版本默认思考 + 工具 + 1M，被后续更具体规则覆盖
            R("deepseek*", thinking: true, func: true, context: 1_048_576),
            // reasoner：始终思考，不支持工具调用与采样参数，上下文 1M
            R("deepseek-reasoner*", thinking: true, func: false, context: 1_048_576, efforts: "high,max"),
            // V4 标准版：思考 + 工具调用 + 1M
            R("deepseek-v4-pro*", thinking: true, func: true, context: 1_048_576, efforts: "high,max"),
            // V4 快速版：思考 + 工具调用 + 1M
            R("deepseek-v4-flash*", thinking: true, func: true, context: 1_048_576, efforts: "high,max"),
            // V4 视觉版：思考 + 工具调用 + 视觉输入（deepseek-v4-flash-vision-exp 等），后规则覆盖先规则
            R("deepseek*-vision*", thinking: true, func: true, context: 1_048_576, vision: true, efforts: "high,max"),
            // chat 别名：不思考，支持工具调用
            R("deepseek-chat*", thinking: false, func: true, context: 1_048_576, efforts: "high,max"),
            // R1：始终思考，不支持工具调用，上下文 65K
            R("deepseek-r1*", thinking: true, func: false, context: 65_536),
        ],
    };

    /// <summary>腾讯混元家族。新版 TokenHub 主力 hy3（295B/21B MoE）与旧 hunyuan-* 兼容；数据源 ModelData/hunyuan.json</summary>
    private static ModelFamily CreateHunyuan() => new("hunyuan", "hunyuan*|hy3*", 131_072)
    {
        Rules =
        [
            // 兜底（最先）：现代混元默认思考+工具，具体型号后覆盖
            R("hunyuan*|hy3*", thinking: true, func: true),
            // hy3：TokenHub 主力，256K，思考三档（no_think/think_low/think_high）
            R("hy3*", vision: false, context: 262_144, efforts: "no_think,think_low,think_high"),
            // t1：思考推理 + 视觉
            R("hunyuan-t1*", vision: true, context: 262_144),
            // turbo：低成本思考 + 视觉
            R("hunyuan-turbo*", vision: true, context: 131_072),
            // pro：通用对话不思考，支持视觉
            R("hunyuan-pro*", thinking: false, vision: true, context: 131_072),
            // lite：轻量，无思考/工具/视觉
            R("hunyuan-lite*", thinking: false, func: false, vision: false, context: 32_768),
        ],
    };

    /// <summary>智谱 GLM 家族。含文生图 cogview / 视频 cogvideox；数据源 ModelData/zhipu.json 与 DashScope 百炼托管</summary>
    private static ModelFamily CreateGlm() => new("glm", "glm*|cogview*|cogvideox*", 131_072)
    {
        Rules =
        [
            // 兜底（最先）：现代 GLM 主档默认思考+工具，具体型号后覆盖
            R("glm*", thinking: true, func: true),
            // glm-5.x 主档：思考 + 视觉 + 1M（2026-Q3 官方；5v-turbo 视觉同档）
            R("glm-5*", vision: true, context: 1_048_576),
            // glm-5 flash 快速档：不思考（免费/低价档，如 glm-5.3-flash）
            R("glm-5*-flash*", thinking: false),
            // glm-4 老档：默认视觉 + 128K（-4v/-flash/-air/-plus 等子型号后续覆盖）
            R("glm-4*", vision: true, context: 131_072),
            // glm-4.7 主档：256K（flash 快速档后覆盖）
            R("glm-4.7*", context: 262_144),
            R("glm-4.7-flash*", thinking: false, vision: false, context: 200_704),
            // glm-4.5/4.6：128K（-v 视觉版不思考，后覆盖）
            R("glm-4.5*|glm-4.6*", context: 131_072),
            R("glm-4.5v*|glm-4.6v*", thinking: false),
            R("glm-4.5v*", context: 65_536),
            // glm-4v 视觉语言：不思考
            R("glm-4v*", thinking: false, context: 8_192),
            // 轻量档：不思考，通常不支持工具、无视觉
            R("glm-4-flash*", thinking: false, func: false, vision: false),
            R("glm-4-air*", thinking: false, func: false, vision: false),
            // 工具增强档：不思考但支持工具（无视觉）
            R("glm-4-alltools*", thinking: false, func: true, vision: false),
            // 文生图 / 文生视频：不支持工具
            R("cogview*", func: false, image: true),
            R("cogvideox*", func: false, video: true),
        ],
    };

    /// <summary>OpenAI GPT 家族。含 gpt-* 全系与 o1/o3/o4/o5 推理系，及 gpt-4o-audio/tts/transcribe/gpt-image 媒体子模型；
    /// 数据源 ModelData/openai.json。dall-e/sora/whisper/tts-1/text-embedding 非 gpt 前缀，仍由基类通用兜底处理</summary>
    private static ModelFamily CreateGpt() => new("gpt", "gpt*|o1*|o3*|o4*|o5*")
    {
        Rules =
        [
            // === 思考能力 ===
            // 兜底（最先）：经典 GPT 不支持思考，默认支持工具
            R("gpt*", thinking: false, func: true),
            // o 系列推理模型：思考 + 推理强度
            R("o1*|o3*|o4*|o5*", thinking: true, func: true, efforts: "low,medium,high"),
            // GPT-5 系列：思考 + 推理强度
            R("gpt-5*", thinking: true, efforts: "low,medium,high"),
            // 开源小模型（gpt-oss-*）：不思考
            R("gpt-oss*", thinking: false, vision: false),

            // === 视觉（多模态对话档位）===
            R("gpt-5*|gpt-4o*|gpt-4.1*|o4*", vision: true),
            R("o1*|o3*", vision: false),

            // === 媒体子模型（覆盖对话视觉标记；不支持工具）===
            // 语音识别转写：仅音频输入
            R("gpt-*-transcribe*", vision: false, func: false, audio: true),
            // 语音合成：仅音频输出
            R("gpt-*-tts*", vision: false, func: false, speech: true),
            // 实时音频对话：音频输入 + 输出
            R("gpt-*-audio*", vision: false, func: false, audio: true, speech: true),
            // 文生图
            R("gpt-image*", func: false, image: true),

            // === 上下文长度（宽档在前、细分在后覆盖；gpt-4* 前缀覆盖 4o/4.1/turbo 等全部 4 系）===
            R("gpt-4*", context: 8_192),
            R("gpt-3.5*", context: 16_385),
            R("gpt-4-turbo*", context: 128_000),
            R("gpt-4o*", context: 131_072),
            R("gpt-4.1*", context: 1_048_576),
            R("gpt-5*", context: 400_000),
            R("gpt-5.5*|gpt-5.4*", context: 1_050_000),
            R("gpt-5.6*", context: 922_000),
            R("gpt-5.6-luna*", context: 270_000),
            R("gpt-5.4-mini*", context: 272_000),
            R("o1*|o3*|o4*", context: 200_000),
        ],
    };

    /// <summary>Anthropic Claude 家族。opus/sonnet/haiku 三级；opus-4-7 旗舰 1M，其余 200K；全系思考+工具+视觉。数据源 ModelData/anthropic.json</summary>
    private static ModelFamily CreateClaude() => new("claude", "claude*", 200_000)
    {
        Rules =
        [
            // 兜底（最先）：Claude 全系思考 + 工具 + 视觉，200K
            R("claude*", thinking: true, func: true, vision: true, context: 200_000),
            // opus-4-7 旗舰：1M 上下文（opus-4-6 及 sonnet/haiku 4-x 仍 200K，兜底已覆盖）
            R("claude-opus-4-7*", context: 1_000_000),
        ],
    };

    /// <summary>Google Gemini 家族。pro/flash 分档，全系 1M 上下文；思考+工具+视觉。数据源 ModelData/gemini.json</summary>
    private static ModelFamily CreateGemini() => new("gemini", "gemini*", 1_048_576)
    {
        Rules =
        [
            R("gemini*", thinking: true, func: true, vision: true, context: 1_048_576),
        ],
    };

    /// <summary>火山方舟豆包家族。主力 doubao-seed 系列与 doubao-1.5 系列；数据源 ModelData/volcengine.json</summary>
    private static ModelFamily CreateDoubao() => new("doubao", "doubao*", 32_768)
    {
        Rules =
        [
            // 兜底（最先）：豆包轻量档默认不思考，seed/pro 后覆盖
            R("doubao*", thinking: false, func: true),
            // doubao-1.5 档内默认上下文（模型 ID 内嵌 token 档位）
            R("doubao-1.5-*", context: 32_768),
            // Seed 系列（1.6/2.x 及 -thinking 变体）：思考 + 视觉，256K
            R("doubao-seed*", thinking: true, vision: true, context: 262_144),
            // seed 快速档（-flash）：不思考（如 doubao-seed-1.6-flash）
            R("doubao-seed-*-flash*", thinking: false),
            // seed 翻译专用：不思考、无工具（doubao-seed-translation）
            R("doubao-seed-translation*", thinking: false, func: false),
            // 1.5 pro/vision/thinking：思考 + 视觉
            R("doubao-1.5-pro*|doubao-1.5-vision*|doubao-1.5-thinking*", thinking: true, vision: true),
            // lite 轻量：无思考/工具/视觉
            R("doubao-1.5-lite*", thinking: false, func: false, vision: false),
            // 嵌入模型：不支持工具
            R("doubao-embedding*", func: false, embedding: true),
        ],
    };

    /// <summary>MiniMax 家族。官方 API 用 MiniMax-M2.5 等标识，DashScope 托管用 MiniMax/MiniMax-M3 斜杠前缀；数据源 ModelData/minimax.json</summary>
    private static ModelFamily CreateMiniMax() => new("minimax", "minimax*|abab*", 196_608)
    {
        Rules =
        [
            // 兜底（最先）：默认不思考、支持工具，旧 abab 与未知档后覆盖
            R("minimax*|abab*", thinking: false, func: true),
            // M3 推理：思考 + 视觉 + 1M（DashScope 托管 ID 为 MiniMax/MiniMax-M3）
            R("minimax-m3*|minimax*/minimax-m3*", thinking: true, vision: true, context: 1_048_576),
            // M2.x 推理：思考（无视觉）
            R("minimax-m2*|minimax*/minimax-m2*", thinking: true, vision: false, context: 196_608),
            // Text-01：长上下文 1M，不思考
            R("minimax-text-01*", thinking: false, context: 1_048_576),
            // 语音合成
            R("minimax-tts*|minimax-speech*", func: false, speech: true),
            // 旧 abab 聊天：32K
            R("abab*", context: 32_768),
        ],
    };

    /// <summary>月之暗面 Kimi 家族。kimi-k 系列推理与旧 moonshot-v1 系列；数据源 ModelData/moonshot.json 与 DashScope 百炼托管</summary>
    private static ModelFamily CreateKimi() => new("kimi", "kimi*|moonshot-v1*", 131_072)
    {
        Rules =
        [
            // 兜底（最先）：kimi-k 系列默认思考+工具，具体型号后覆盖
            R("kimi*", thinking: true, func: true),
            // K3：思考 + 视觉，1M（2026 旗舰）
            R("kimi-k3*", vision: true, context: 1_048_576),
            // K2 系列：思考 + 视觉，256K（k2-0905 等历史纯文本版特例）
            R("kimi-k2*", vision: true, context: 262_144),
            R("kimi-k2-0905*", vision: false),
            // K1.5：思考，128K
            R("kimi-k1.5*", vision: false, context: 131_072),
            // 旧 moonshot-v1 系列：不思考、支持工具，上下文按 ID 档位
            R("moonshot-v1*", thinking: false, func: true, context: 131_072),
            R("moonshot-v1*-vision*", vision: true),
            R("moonshot-v1-8k*", context: 8_192),
            R("moonshot-v1-32k*", context: 32_768),
            R("moonshot-v1-128k*", context: 131_072),
        ],
    };

    /// <summary>百度文心家族。ernie-4.5/4.0-turbo 主档思考+视觉，speed/lite/tiny 轻量档；数据源 ModelData/qianfan.json</summary>
    private static ModelFamily CreateErnie() => new("ernie", "ernie*", 131_072)
    {
        Rules =
        [
            // 兜底（最先）：默认不思考、支持工具，主档后覆盖
            R("ernie*", thinking: false, func: true, vision: false),
            // 主档 5.x/4.x：思考 + 128K（2026-Q3 千帆口径；5.x 主档无视觉）
            R("ernie-5*|ernie-4.5*|ernie-4.0*", thinking: true, context: 131_072),
            // 4.5-turbo：视觉增强（思考 + 128K 继承主档）
            R("ernie-4.5-turbo*", vision: true),
            // -vl/-vision 视觉版：不思考 + 32K（如 ernie-4.5-turbo-vl）
            R("ernie*-vl*|ernie*-vision*", thinking: false, context: 32_768),
            // 轻量档：无思考/工具/视觉 + 32K
            R("ernie-speed*|ernie-lite*|ernie-tiny*", thinking: false, func: false, vision: false, context: 32_768),
        ],
    };

    /// <summary>讯飞星火家族。spark-4.0-ultra 主档思考，3.5-max 轻量；数据源 ModelData/spark.json</summary>
    private static ModelFamily CreateSpark() => new("spark", "spark*", 131_072)
    {
        Rules =
        [
            // 兜底（最先）：默认不思考、支持工具
            R("spark*", thinking: false, func: true),
            // 4.0+ 主档：思考
            R("spark-4*", thinking: true),
            // 3.5-max 轻量：不思考、无工具
            R("spark-3.5-max*", thinking: false, func: false),
        ],
    };

    /// <summary>xAI Grok 家族。grok-3/4 主档思考+视觉，grok-2 旧档；数据源 ModelData/xai.json</summary>
    private static ModelFamily CreateGrok() => new("grok", "grok*", 131_072)
    {
        Rules =
        [
            // 兜底（最先）：默认不思考、支持工具
            R("grok*", thinking: false, func: true),
            // 3/4 主档：思考 + 视觉
            R("grok-4*|grok-3*", thinking: true),
            R("grok-4*", vision: true),
            R("grok-3*", vision: true),
            // grok-3-mini：思考但无视觉（命中 grok-3* 宽档，细分档后置覆盖）
            R("grok-3-mini*", vision: false),
            // grok-2 旧档：不思考
            R("grok-2*", thinking: false),
            R("grok-2-vision*", vision: true, context: 32_768),
        ],
    };

    /// <summary>Mistral 家族。精简档：默认支持工具，上下文 128K；平台专属价由 [AiClientModel] 精确注册覆盖</summary>
    private static ModelFamily CreateMistral() => new("mistral", "mistral*", 131_072)
    {
        Rules =
        [
            // 兜底：不思考、支持工具
            R("mistral*", thinking: false, func: true),
            // 嵌入模型：不支持工具
            R("mistral-embed*", func: false, embedding: true),
        ],
    };

    /// <summary>Meta Llama 家族。精简档：默认支持工具与 128K 上下文，广泛托管于 Groq/Cerebras/Together 等 OpenAI 兼容平台</summary>
    private static ModelFamily CreateLlama() => new("llama", "llama*", 131_072)
    {
        Rules =
        [
            R("llama*", thinking: false, func: true),
        ],
    };

    /// <summary>Google Gemma 家族。精简档：默认支持工具，gemma3 支持 128K 上下文</summary>
    private static ModelFamily CreateGemma() => new("gemma", "gemma*", 8_192)
    {
        Rules =
        [
            R("gemma*", thinking: false, func: true),
            R("gemma3*", context: 131_072),
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
    /// <param name="embedding">是否支持嵌入向量</param>
    /// <param name="rerank">是否支持重排序</param>
    /// <param name="context">上下文窗口大小，0 不修改</param>
    /// <param name="efforts">推理强度选项</param>
    /// <returns>能力规则</returns>
    private static ModelCapabilityRule R(String pattern, Boolean? thinking = null, Boolean? func = null,
        Boolean? vision = null, Boolean? audio = null, Boolean? speech = null, Boolean? image = null,
        Boolean? video = null, Boolean? embedding = null, Boolean? rerank = null,
        Int32 context = 0, String? efforts = null)
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
            Embedding = embedding,
            Rerank = rerank,
            ContextLength = context,
            ReasoningEfforts = efforts,
        };
}
