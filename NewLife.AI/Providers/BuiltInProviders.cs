using System.Net.Http;
using System.Net.Http.Headers;

namespace NewLife.AI.Providers;

/// <summary>Azure AI Foundry 服务商。Azure 托管的 OpenAI 模型</summary>
/// <remarks>
/// Azure OpenAI 支持两种部署模式：
/// <list type="bullet">
/// <item>Azure AI Foundry（models.inference.ai.azure.com）：无需创建资源组，按模型直接调用</item>
/// <item>Azure OpenAI Service（{resource}.openai.azure.com）：需要在 Azure 门户创建资源并部署模型</item>
/// </list>
/// Azure OpenAI Service 的 Endpoint 需要用户配置为自己的资源地址。
/// 认证方式：Bearer Token 或 api-key 头。
/// </remarks>
public class AzureAiProvider : OpenAiProvider
{
    /// <summary>服务商编码</summary>
    public override String Code => "AzureAI";

    /// <summary>服务商名称</summary>
    public override String Name => "微软Azure AI";

    /// <summary>服务商描述</summary>
    public override String? Description => "微软 Azure AI 平台，支持 OpenAI 模型及多种开源模型";

    /// <summary>默认 API 地址。Azure AI Foundry 推理入口</summary>
    public override String DefaultEndpoint => "https://models.inference.ai.azure.com";

    /// <summary>主流模型列表。Azure AI 平台托管的代表性模型</summary>
    public override AiModelInfo[] Models { get; } =
    [
        new("gpt-4o",        "GPT-4o",         new(false, true,  false, true)),
        new("gpt-4o-mini",   "GPT-4o Mini",    new(false, true,  false, true)),
        new("o3-mini",       "o3-mini",        new(true,  false, false, true)),
        new("DeepSeek-R1",   "DeepSeek-R1",    new(true,  false, false, true)),
        new("Phi-4",         "Phi-4",          new(false, true,  false, true)),
    ];
}

/// <summary>阿里百炼（DashScope）服务商。支持 Qwen/通义千问系列模型</summary>
/// <remarks>
/// 百炼平台提供 OpenAI 兼容模式接口，路径为 /compatible-mode/v1/chat/completions。
/// 官方文档：https://help.aliyun.com/zh/model-studio/
/// </remarks>
public class DashScopeProvider : OpenAiProvider
{
    /// <summary>服务商编码</summary>
    public override String Code => "DashScope";

    /// <summary>服务商名称</summary>
    public override String Name => "阿里百炼";

    /// <summary>服务商描述</summary>
    public override String? Description => "阿里云百炼大模型平台，支持 Qwen/通义千问全系列模型";

    /// <summary>默认 API 地址</summary>
    public override String DefaultEndpoint => "https://dashscope.aliyuncs.com/compatible-mode";

    /// <summary>主流模型列表。阿里百炼/通义千问各主力模型</summary>
    public override AiModelInfo[] Models { get; } =
    [
        new("qwen-max",          "Qwen Max",          new(true,  true,  false, true)),
        new("qwen-plus",         "Qwen Plus",         new(true,  true,  false, true)),
        new("qwen-turbo",        "Qwen Turbo",        new(false, false, false, true)),
        new("qwen-vl-max",       "Qwen-VL Max",       new(false, true,  false, true)),
        new("qwen3-235b-a22b",   "Qwen3-235B",        new(true,  false, false, true)),
        new("qwen3-30b-a3b",     "Qwen3-30B",         new(true,  false, false, true)),
        new("qwen3-8b",          "Qwen3-8B",          new(true,  false, false, true)),
        new("qwq-32b",           "QwQ-32B",           new(true,  false, false, true)),
        new("wanx-v1",           "通义万象",            new(false, false, true,  false)),
    ];
}
/// <remarks>
/// DeepSeek 原生支持 reasoning_content 字段返回推理思考过程。
/// 官方文档：https://platform.deepseek.com/api-docs
/// </remarks>
public class DeepSeekProvider : OpenAiProvider
{
    /// <summary>服务商编码</summary>
    public override String Code => "DeepSeek";

    /// <summary>服务商名称</summary>
    public override String Name => "深度求索";

    /// <summary>服务商描述</summary>
    public override String? Description => "深度求索，DeepSeek-V3/R1 推理模型，支持思考过程输出";

    /// <summary>默认 API 地址</summary>
    public override String DefaultEndpoint => "https://api.deepseek.com";

    /// <summary>主流模型列表。DeepSeek 各主力模型</summary>
    public override AiModelInfo[] Models { get; } =
    [
        new("deepseek-chat",     "DeepSeek V3", new(false, false, false, true)),
        new("deepseek-reasoner", "DeepSeek R1", new(true,  false, false, true)),
    ];
}

/// <summary>火山方舟服务商。字节跳动旗下 AI 服务平台，支持豆包系列模型</summary>
/// <remarks>
/// 火山方舟提供 OpenAI 兼容 API，路径为 /api/v3/chat/completions。
/// 调用需要先在控制台创建接入点（Endpoint），模型名实际使用接入点 ID。
/// 官方文档：https://www.volcengine.com/docs/82379
/// </remarks>
public class VolcEngineProvider : OpenAiProvider
{
    /// <summary>服务商编码</summary>
    public override String Code => "VolcEngine";

    /// <summary>服务商名称</summary>
    public override String Name => "字节豆包";

    /// <summary>服务商描述</summary>
    public override String? Description => "字节跳动火山方舟平台，支持豆包等大模型";

    /// <summary>默认 API 地址</summary>
    public override String DefaultEndpoint => "https://ark.cn-beijing.volces.com/api/v3";

    /// <summary>主流模型列表。字节豆包各主力模型</summary>
    public override AiModelInfo[] Models { get; } =
    [
        new("doubao-1.5-pro-32k",  "豆包 1.5 Pro",  new(true,  true,  false, true)),
        new("doubao-1.5-lite-32k", "豆包 1.5 Lite", new(false, false, false, true)),
    ];

    /// <summary>对话完成路径</summary>
    protected override String ChatPath => "/chat/completions";
}

/// <summary>智谱 AI 服务商。支持 GLM-4/CogView 系列模型</summary>
/// <remarks>
/// 智谱 API 使用 OpenAI 兼容路径 /api/paas/v4/chat/completions。
/// 官方文档：https://open.bigmodel.cn/dev/api
/// </remarks>
public class ZhipuProvider : OpenAiProvider
{
    /// <summary>服务商编码</summary>
    public override String Code => "Zhipu";

    /// <summary>服务商名称</summary>
    public override String Name => "智谱AI";

    /// <summary>服务商描述</summary>
    public override String? Description => "智谱 AI，支持 GLM-4/CogView 系列模型";

    /// <summary>默认 API 地址</summary>
    public override String DefaultEndpoint => "https://open.bigmodel.cn/api/paas/v4";

    /// <summary>主流模型列表。智谱 AI 各主力模型</summary>
    public override AiModelInfo[] Models { get; } =
    [
        new("glm-4",         "GLM-4",          new(true,  true,  false, true)),
        new("glm-4-flash",   "GLM-4 Flash",    new(false, false, false, true)),
        new("cogview-3",     "CogView-3",      new(false, false, true,  false)),
    ];

    /// <summary>对话完成路径</summary>
    protected override String ChatPath => "/chat/completions";
}

/// <summary>月之暗面（Moonshot/Kimi）服务商。支持 Kimi 系列长上下文模型</summary>
/// <remarks>
/// Moonshot 原生支持 reasoning_content 字段返回推理思考过程。
/// 支持超长上下文（128K/1M tokens）。
/// 官方文档：https://platform.moonshot.cn/docs
/// </remarks>
public class MoonshotProvider : OpenAiProvider
{
    /// <summary>服务商编码</summary>
    public override String Code => "Moonshot";

    /// <summary>服务商名称</summary>
    public override String Name => "月之暗面Kimi";

    /// <summary>服务商描述</summary>
    public override String? Description => "月之暗面 Kimi 系列，支持超长上下文和推理思考";

    /// <summary>默认 API 地址</summary>
    public override String DefaultEndpoint => "https://api.moonshot.cn";

    /// <summary>主流模型列表。月之暗面 Kimi 各主力模型</summary>
    public override AiModelInfo[] Models { get; } =
    [
        new("moonshot-v1-128k", "Kimi 128K",    new(false, false, false, true)),
        new("kimi-k1.5",        "Kimi K1.5",    new(true,  false, false, true)),
    ];
}

/// <summary>腾讯混元服务商。支持混元大模型系列</summary>
/// <remarks>
/// 混元使用 OpenAI 兼容协议。
/// 官方文档：https://cloud.tencent.com/document/product/1729
/// </remarks>
public class HunyuanProvider : OpenAiProvider
{
    /// <summary>服务商编码</summary>
    public override String Code => "Hunyuan";

    /// <summary>服务商名称</summary>
    public override String Name => "腾讯混元";

    /// <summary>服务商描述</summary>
    public override String? Description => "腾讯混元大模型";

    /// <summary>默认 API 地址</summary>
    public override String DefaultEndpoint => "https://api.hunyuan.cloud.tencent.com";

    /// <summary>主流模型列表。腾讯混元各主力模型</summary>
    public override AiModelInfo[] Models { get; } =
    [
        new("hunyuan-t1",   "混元 T1",   new(true,  true, false, true)),
        new("hunyuan-pro",  "混元 Pro",  new(false, true, false, true)),
    ];
}

/// <summary>百度千帆服务商。支持文心一言等大模型系列</summary>
/// <remarks>
/// 千帆 v2 API 使用 OpenAI 兼容格式，路径为 /chat/completions。
/// 官方文档：https://cloud.baidu.com/doc/WENXINWORKSHOP
/// </remarks>
public class QianfanProvider : OpenAiProvider
{
    /// <summary>服务商编码</summary>
    public override String Code => "Qianfan";

    /// <summary>服务商名称</summary>
    public override String Name => "百度文心";

    /// <summary>服务商描述</summary>
    public override String? Description => "百度千帆大模型平台，支持文心一言系列";

    /// <summary>默认 API 地址</summary>
    public override String DefaultEndpoint => "https://qianfan.baidubce.com/v2";

    /// <summary>主流模型列表。百度文心各主力模型</summary>
    public override AiModelInfo[] Models { get; } =
    [
        new("ernie-4.5-turbo", "ERNIE 4.5 Turbo", new(true,  true,  false, true)),
        new("ernie-speed",     "ERNIE Speed",     new(false, false, false, true)),
    ];

    /// <summary>对话完成路径</summary>
    protected override String ChatPath => "/chat/completions";
}

/// <summary>讯飞星火服务商。支持星火认知大模型系列</summary>
/// <remarks>
/// 星火 OpenAI 兼容接口使用 /v1/chat/completions 路径。
/// 官方文档：https://www.xfyun.cn/doc/spark
/// </remarks>
public class SparkProvider : OpenAiProvider
{
    /// <summary>服务商编码</summary>
    public override String Code => "Spark";

    /// <summary>服务商名称</summary>
    public override String Name => "讯飞星火";

    /// <summary>服务商描述</summary>
    public override String? Description => "讯飞星火认知大模型";

    /// <summary>默认 API 地址</summary>
    public override String DefaultEndpoint => "https://spark-api-open.xf-yun.com";

    /// <summary>主流模型列表。讯飞星火各主力模型</summary>
    public override AiModelInfo[] Models { get; } =
    [
        new("spark-4.0-ultra", "星火 4.0 Ultra", new(true,  false, false, true)),
        new("spark-3.5-max",   "星火 3.5 Max",   new(false, false, false, true)),
    ];
}

/// <summary>零一万物服务商。支持 Yi 系列大模型</summary>
/// <remarks>
/// 官方文档：https://platform.lingyiwanwu.com/docs
/// </remarks>
public class YiProvider : OpenAiProvider
{
    /// <summary>服务商编码</summary>
    public override String Code => "Yi";

    /// <summary>服务商名称</summary>
    public override String Name => "零一万物";

    /// <summary>服务商描述</summary>
    public override String? Description => "零一万物 Yi 系列大模型";

    /// <summary>默认 API 地址</summary>
    public override String DefaultEndpoint => "https://api.lingyiwanwu.com";

    /// <summary>主流模型列表。零一万物各主力模型</summary>
    public override AiModelInfo[] Models { get; } =
    [
        new("yi-large",        "Yi Large",       new(false, false, false, true)),
        new("yi-vision-plus",  "Yi Vision Plus", new(false, true,  false, true)),
    ];
}

/// <summary>MiniMax 服务商。支持 MiniMax 系列模型</summary>
/// <remarks>
/// MiniMax 同时支持 OpenAI 兼容 API 和 Anthropic 兼容 API。
/// 官方文档：https://platform.minimaxi.com/document
/// </remarks>
public class MiniMaxProvider : OpenAiProvider
{
    /// <summary>服务商编码</summary>
    public override String Code => "MiniMax";

    /// <summary>服务商名称</summary>
    public override String Name => "MiniMax";

    /// <summary>服务商描述</summary>
    public override String? Description => "MiniMax 多模态大模型";

    /// <summary>默认 API 地址</summary>
    public override String DefaultEndpoint => "https://api.minimax.chat";

    /// <summary>主流模型列表。MiniMax 各主力模型</summary>
    public override AiModelInfo[] Models { get; } =
    [
        new("MiniMax-Text-01", "MiniMax Text-01", new(false, true,  false, true)),
        new("abab6.5s",        "abab6.5s",        new(false, false, false, true)),
    ];
}

/// <summary>硅基流动服务商。AI 推理加速平台，聚合多种开源模型</summary>
/// <remarks>
/// 硅基流动提供统一的 OpenAI 兼容接口，可调用 DeepSeek/Qwen/Llama 等模型。
/// 官方文档：https://docs.siliconflow.cn
/// </remarks>
public class SiliconFlowProvider : OpenAiProvider
{
    /// <summary>服务商编码</summary>
    public override String Code => "SiliconFlow";

    /// <summary>服务商名称</summary>
    public override String Name => "硅基流动";

    /// <summary>服务商描述</summary>
    public override String? Description => "硅基流动推理加速平台，聚合多种主流开源模型";

    /// <summary>默认 API 地址</summary>
    public override String DefaultEndpoint => "https://api.siliconflow.cn";

    /// <summary>主流模型列表。硅基流动平台上的代表性模型</summary>
    public override AiModelInfo[] Models { get; } =
    [
        new("deepseek-ai/DeepSeek-R1",      "DeepSeek R1",      new(true,  false, false, true)),
        new("deepseek-ai/DeepSeek-V3",      "DeepSeek V3",      new(false, false, false, true)),
        new("Qwen/Qwen2.5-VL-72B-Instruct", "Qwen2.5-VL-72B",  new(false, true,  false, true)),
        new("stabilityai/stable-diffusion-3-5-large", "SD 3.5 Large", new(false, false, true, false)),
    ];
}

/// <summary>x.AI 服务商。支持 Grok 系列模型</summary>
/// <remarks>
/// 官方文档：https://docs.x.ai
/// </remarks>
public class XAiProvider : OpenAiProvider
{
    /// <summary>服务商编码</summary>
    public override String Code => "XAI";

    /// <summary>服务商名称</summary>
    public override String Name => "xAI Grok";

    /// <summary>服务商描述</summary>
    public override String? Description => "Elon Musk 旗下 x.AI，支持 Grok 系列模型";

    /// <summary>默认 API 地址</summary>
    public override String DefaultEndpoint => "https://api.x.ai";

    /// <summary>主流模型列表。xAI Grok 各主力模型</summary>
    public override AiModelInfo[] Models { get; } =
    [
        new("grok-3",       "Grok 3",       new(true,  true,  false, true)),
        new("grok-3-mini",  "Grok 3 Mini",  new(true,  false, false, true)),
        new("grok-2-image", "Grok 2 Image", new(false, false, true,  false)),
    ];
}

/// <summary>GitHub Models 服务商。GitHub 托管的 AI 模型市场</summary>
/// <remarks>
/// 通过 GitHub Personal Access Token 认证。
/// 官方文档：https://docs.github.com/en/github-models
/// </remarks>
public class GitHubModelsProvider : OpenAiProvider
{
    /// <summary>服务商编码</summary>
    public override String Code => "GitHubModels";

    /// <summary>服务商名称</summary>
    public override String Name => "GitHub Models";

    /// <summary>服务商描述</summary>
    public override String? Description => "GitHub 模型市场，通过 GitHub Token 调用多种模型";

    /// <summary>默认 API 地址</summary>
    public override String DefaultEndpoint => "https://models.github.ai/inference";

    /// <summary>主流模型列表。GitHub Models 平台上的代表性模型</summary>
    public override AiModelInfo[] Models { get; } =
    [
        new("gpt-4o",      "GPT-4o",    new(false, true,  false, true)),
        new("o3-mini",     "o3-mini",   new(true,  false, false, true)),
        new("Phi-4",       "Phi-4",     new(false, true,  false, true)),
    ];
}

/// <summary>OpenRouter 服务商。AI 模型聚合代理平台，支持数百种模型统一调用</summary>
/// <remarks>
/// 官方文档：https://openrouter.ai/docs
/// </remarks>
public class OpenRouterProvider : OpenAiProvider
{
    /// <summary>服务商编码</summary>
    public override String Code => "OpenRouter";

    /// <summary>服务商名称</summary>
    public override String Name => "OpenRouter";

    /// <summary>服务商描述</summary>
    public override String? Description => "OpenRouter 模型聚合平台，统一调用数百种模型";

    /// <summary>默认 API 地址</summary>
    public override String DefaultEndpoint => "https://openrouter.ai/api";

    /// <summary>主流模型列表。OpenRouter 聚合平台上的代表性模型（使用 provider/model 格式）</summary>
    public override AiModelInfo[] Models { get; } =
    [
        new("openai/gpt-4o",                    "GPT-4o",              new(false, true,  false, true)),
        new("anthropic/claude-sonnet-4-5",      "Claude Sonnet 4.5",  new(true,  true,  false, true)),
        new("deepseek/deepseek-r1",             "DeepSeek R1",        new(true,  false, false, true)),
        new("google/gemini-2.5-pro",            "Gemini 2.5 Pro",     new(true,  true,  false, true)),
    ];
}

/// <summary>Ollama 服务商。本地部署和运行开源大模型</summary>
/// <remarks>
/// Ollama 提供 OpenAI 兼容 API，默认不需要 API Key。
/// 官方文档：https://github.com/ollama/ollama/blob/main/docs/openai.md
/// </remarks>
public class OllamaProvider : OpenAiProvider
{
    /// <summary>服务商编码</summary>
    public override String Code => "Ollama";

    /// <summary>服务商名称</summary>
    public override String Name => "Ollama";

    /// <summary>服务商描述</summary>
    public override String? Description => "本地运行开源大模型，支持 Llama/Qwen/Gemma 等";

    /// <summary>默认 API 地址</summary>
    public override String DefaultEndpoint => "http://localhost:11434";

    /// <summary>主流模型列表。Ollama 本地常用开源模型（能力取决于实际加载的模型）</summary>
    public override AiModelInfo[] Models { get; } =
    [
        new("llama3.3",     "Llama 3.3",    new(false, false, false, true)),
        new("qwen2.5",      "Qwen 2.5",     new(false, false, false, true)),
        new("deepseek-r1",  "DeepSeek R1",  new(true,  false, false, false)),
        new("phi4",         "Phi-4",        new(false, false, false, true)),
    ];

    /// <summary>设置请求头。Ollama 默认不需要认证</summary>
    /// <param name="request">HTTP 请求</param>
    /// <param name="options">选项</param>
    protected override void SetHeaders(HttpRequestMessage request, AiProviderOptions options)
    {
        // Ollama 默认不需要 API Key，但如果用户配置了则传递
        if (!String.IsNullOrEmpty(options.ApiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
    }
}

/// <summary>小米 MiMo 服务商。支持 MiMo 系列推理模型</summary>
/// <remarks>
/// MiMo 支持 reasoning_content 字段返回推理思考过程。
/// </remarks>
public class MiMoProvider : OpenAiProvider
{
    /// <summary>服务商编码</summary>
    public override String Code => "MiMo";

    /// <summary>服务商名称</summary>
    public override String Name => "小米MiMo";

    /// <summary>服务商描述</summary>
    public override String? Description => "小米 MiMo 推理模型，支持思考过程输出";

    /// <summary>默认 API 地址</summary>
    public override String DefaultEndpoint => "https://api.xiaomimimo.com";

    /// <summary>主流模型列表。小米 MiMo 各主力模型</summary>
    public override AiModelInfo[] Models { get; } =
    [
        new("MiMo-7B-RL", "MiMo 7B RL", new(true, false, false, true)),
    ];
}

/// <summary>Together AI 服务商。开源模型云端推理平台</summary>
/// <remarks>
/// 支持 Llama/Mixtral/Qwen 等大量开源模型。
/// 官方文档：https://docs.together.ai
/// </remarks>
public class TogetherAiProvider : OpenAiProvider
{
    /// <summary>服务商编码</summary>
    public override String Code => "TogetherAI";

    /// <summary>服务商名称</summary>
    public override String Name => "Together AI";

    /// <summary>服务商描述</summary>
    public override String? Description => "Together AI 开源模型云端推理平台";

    /// <summary>默认 API 地址</summary>
    public override String DefaultEndpoint => "https://api.together.xyz";

    /// <summary>主流模型列表。Together AI 平台上的代表性模型</summary>
    public override AiModelInfo[] Models { get; } =
    [
        new("meta-llama/Meta-Llama-3.1-70B-Instruct", "Llama 3.1 70B", new(false, false, false, true)),
        new("deepseek-ai/DeepSeek-R1",                "DeepSeek R1",   new(true,  false, false, true)),
        new("Qwen/Qwen2-VL-72B-Instruct",             "Qwen2-VL-72B", new(false, true,  false, true)),
    ];
}

/// <summary>Groq 服务商。LPU 高速推理加速平台</summary>
/// <remarks>
/// 基于 LPU（Language Processing Unit）硬件，提供极低延迟推理。
/// 官方文档：https://console.groq.com/docs
/// </remarks>
public class GroqProvider : OpenAiProvider
{
    /// <summary>服务商编码</summary>
    public override String Code => "Groq";

    /// <summary>服务商名称</summary>
    public override String Name => "Groq";

    /// <summary>服务商描述</summary>
    public override String? Description => "Groq LPU 高速推理平台，毫秒级延迟";

    /// <summary>默认 API 地址</summary>
    public override String DefaultEndpoint => "https://api.groq.com/openai";

    /// <summary>主流模型列表。Groq LPU 平台上的代表性模型</summary>
    public override AiModelInfo[] Models { get; } =
    [
        new("llama-3.3-70b-versatile", "Llama 3.3 70B", new(false, false, false, true)),
        new("qwen-qwq-32b",            "QwQ 32B",       new(true,  false, false, true)),
    ];
}

/// <summary>Mistral AI 服务商。支持 Mistral/Mixtral/Codestral 系列模型</summary>
/// <remarks>
/// 法国 AI 公司，提供高效的开源和商业模型。
/// 官方文档：https://docs.mistral.ai
/// </remarks>
public class MistralProvider : OpenAiProvider
{
    /// <summary>服务商编码</summary>
    public override String Code => "Mistral";

    /// <summary>服务商名称</summary>
    public override String Name => "Mistral AI";

    /// <summary>服务商描述</summary>
    public override String? Description => "Mistral AI，高效的欧洲 AI 模型提供商";

    /// <summary>默认 API 地址</summary>
    public override String DefaultEndpoint => "https://api.mistral.ai";

    /// <summary>主流模型列表。Mistral AI 各主力模型</summary>
    public override AiModelInfo[] Models { get; } =
    [
        new("mistral-large-latest",  "Mistral Large",  new(false, false, false, true)),
        new("pixtral-large-latest",  "Pixtral Large",  new(false, true,  false, true)),
        new("codestral-latest",      "Codestral",      new(false, false, false, true)),
    ];
}

/// <summary>Cohere 服务商。支持 Command R 系列企业级模型</summary>
/// <remarks>
/// Cohere 的 Chat API 提供 OpenAI 兼容模式（/compatibility 路径前缀）。
/// 官方文档：https://docs.cohere.com
/// </remarks>
public class CohereProvider : OpenAiProvider
{
    /// <summary>服务商编码</summary>
    public override String Code => "Cohere";

    /// <summary>服务商名称</summary>
    public override String Name => "Cohere";

    /// <summary>服务商描述</summary>
    public override String? Description => "Cohere 企业级 AI 模型，擅长 RAG 和搜索增强";

    /// <summary>默认 API 地址</summary>
    public override String DefaultEndpoint => "https://api.cohere.com/compatibility";

    /// <summary>主流模型列表。Cohere 各主力模型</summary>
    public override AiModelInfo[] Models { get; } =
    [
        new("command-r-plus", "Command R+", new(false, false, false, true)),
        new("command-r",      "Command R",  new(false, false, false, true)),
    ];
}

/// <summary>Perplexity 服务商。支持联网搜索增强的 AI 模型</summary>
/// <remarks>
/// Perplexity 的模型自带联网搜索能力，回答会附带引用来源。
/// 官方文档：https://docs.perplexity.ai
/// </remarks>
public class PerplexityProvider : OpenAiProvider
{
    /// <summary>服务商编码</summary>
    public override String Code => "Perplexity";

    /// <summary>服务商名称</summary>
    public override String Name => "Perplexity";

    /// <summary>服务商描述</summary>
    public override String? Description => "Perplexity 联网搜索 AI，回答附带引用来源";

    /// <summary>默认 API 地址</summary>
    public override String DefaultEndpoint => "https://api.perplexity.ai";

    /// <summary>主流模型列表。Perplexity 联网搜索增强模型</summary>
    public override AiModelInfo[] Models { get; } =
    [
        new("sonar-pro",           "Sonar Pro",           new(false, false, false, false)),
        new("sonar-reasoning-pro", "Sonar Reasoning Pro", new(true,  false, false, false)),
    ];
}

/// <summary>无问芯穹（Infini-AI）服务商。国产 AI 推理云平台</summary>
/// <remarks>
/// 支持 DeepSeek/Qwen/Llama 等主流模型的高效推理。
/// 官方文档：https://cloud.infini-ai.com/docs
/// </remarks>
public class InfiniProvider : OpenAiProvider
{
    /// <summary>服务商编码</summary>
    public override String Code => "Infini";

    /// <summary>服务商名称</summary>
    public override String Name => "无问芯穹";

    /// <summary>服务商描述</summary>
    public override String? Description => "无问芯穹推理云平台，支持多种主流模型高效推理";

    /// <summary>默认 API 地址</summary>
    public override String DefaultEndpoint => "https://cloud.infini-ai.com/maas";

    /// <summary>主流模型列表。无问芯穹平台上的代表性模型</summary>
    public override AiModelInfo[] Models { get; } =
    [
        new("deepseek-r1",          "DeepSeek R1",   new(true,  false, false, true)),
        new("qwen2.5-72b-instruct", "Qwen2.5-72B",   new(false, false, false, true)),
    ];
}

/// <summary>Cerebras 服务商。基于晶圆级芯片的超高速推理平台</summary>
/// <remarks>
/// 使用 WSE（Wafer Scale Engine）芯片，推理速度极快。
/// 官方文档：https://inference-docs.cerebras.ai
/// </remarks>
public class CerebrasProvider : OpenAiProvider
{
    /// <summary>服务商编码</summary>
    public override String Code => "Cerebras";

    /// <summary>服务商名称</summary>
    public override String Name => "Cerebras";

    /// <summary>服务商描述</summary>
    public override String? Description => "Cerebras 晶圆级芯片超高速推理";

    /// <summary>默认 API 地址</summary>
    public override String DefaultEndpoint => "https://api.cerebras.ai";

    /// <summary>主流模型列表。Cerebras LPU 平台上的代表性模型</summary>
    public override AiModelInfo[] Models { get; } =
    [
        new("llama-3.3-70b", "Llama 3.3 70B", new(false, false, false, true)),
        new("qwen-3-32b",    "Qwen3 32B",     new(true,  false, false, true)),
    ];
}

/// <summary>Fireworks AI 服务商。高速模型推理平台</summary>
/// <remarks>
/// 专注于大模型推理优化，支持 Llama/Mixtral 等开源模型。
/// 官方文档：https://docs.fireworks.ai
/// </remarks>
public class FireworksProvider : OpenAiProvider
{
    /// <summary>服务商编码</summary>
    public override String Code => "Fireworks";

    /// <summary>服务商名称</summary>
    public override String Name => "Fireworks AI";

    /// <summary>服务商描述</summary>
    public override String? Description => "Fireworks AI 高速推理平台";

    /// <summary>默认 API 地址</summary>
    public override String DefaultEndpoint => "https://api.fireworks.ai/inference";

    /// <summary>主流模型列表。Fireworks AI 平台上的代表性模型</summary>
    public override AiModelInfo[] Models { get; } =
    [
        new("accounts/fireworks/models/llama-v3p1-70b-instruct", "Llama 3.1 70B", new(false, false, false, true)),
        new("accounts/fireworks/models/deepseek-r1",             "DeepSeek R1",   new(true,  false, false, true)),
    ];
}

/// <summary>SambaNova 服务商。基于 RDU 架构的 AI 推理平台</summary>
/// <remarks>
/// 使用自研 RDU（Reconfigurable Dataflow Unit）芯片。
/// 官方文档：https://community.sambanova.ai/docs
/// </remarks>
public class SambaNovaProvider : OpenAiProvider
{
    /// <summary>服务商编码</summary>
    public override String Code => "SambaNova";

    /// <summary>服务商名称</summary>
    public override String Name => "SambaNova";

    /// <summary>服务商描述</summary>
    public override String? Description => "SambaNova RDU 架构 AI 推理平台";

    /// <summary>默认 API 地址</summary>
    public override String DefaultEndpoint => "https://api.sambanova.ai";

    /// <summary>主流模型列表。SambaNova RDU 平台上的代表性模型</summary>
    public override AiModelInfo[] Models { get; } =
    [
        new("Meta-Llama-3.1-70B-Instruct", "Llama 3.1 70B", new(false, false, false, true)),
        new("DeepSeek-R1",                 "DeepSeek R1",   new(true,  false, false, true)),
    ];
}

/// <summary>LM Studio 服务商。桌面端本地模型运行工具</summary>
/// <remarks>
/// 在本地电脑上运行 GGUF 格式模型，提供 OpenAI 兼容 API。
/// 官方网站：https://lmstudio.ai
/// </remarks>
public class LMStudioProvider : OpenAiProvider
{
    /// <summary>服务商编码</summary>
    public override String Code => "LMStudio";

    /// <summary>服务商名称</summary>
    public override String Name => "LM Studio";

    /// <summary>服务商描述</summary>
    public override String? Description => "LM Studio 桌面端本地模型运行工具";

    /// <summary>默认 API 地址</summary>
    public override String DefaultEndpoint => "http://localhost:1234";

    /// <summary>主流模型列表。LM Studio 本地常用模型（能力取决于实际加载的模型）</summary>
    public override AiModelInfo[] Models { get; } =
    [
        new("llama-3.2-3b-instruct", "Llama 3.2 3B",  new(false, false, false, true)),
        new("qwen2.5-7b-instruct",   "Qwen2.5 7B",    new(false, false, false, true)),
    ];

    /// <summary>设置请求头。LM Studio 默认不需要认证</summary>
    /// <param name="request">HTTP 请求</param>
    /// <param name="options">选项</param>
    protected override void SetHeaders(HttpRequestMessage request, AiProviderOptions options)
    {
        if (!String.IsNullOrEmpty(options.ApiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
    }
}

/// <summary>vLLM 服务商。高吞吐量大模型推理引擎</summary>
/// <remarks>
/// 自部署的高性能推理引擎，提供 OpenAI 兼容 API。
/// 官方文档：https://docs.vllm.ai
/// </remarks>
public class VllmProvider : OpenAiProvider
{
    /// <summary>服务商编码</summary>
    public override String Code => "vLLM";

    /// <summary>服务商名称</summary>
    public override String Name => "vLLM";

    /// <summary>服务商描述</summary>
    public override String? Description => "vLLM 高吞吐量推理引擎，支持自部署";

    /// <summary>默认 API 地址</summary>
    public override String DefaultEndpoint => "http://localhost:8000";

    /// <summary>主流模型列表。vLLM 引擎常用开源模型（能力取决于实际部署的模型）</summary>
    public override AiModelInfo[] Models { get; } =
    [
        new("Qwen/Qwen2.5-72B-Instruct",           "Qwen2.5 72B",  new(false, false, false, true)),
        new("meta-llama/Meta-Llama-3.1-70B-Instruct", "Llama 3.1 70B", new(false, false, false, true)),
    ];

    /// <summary>设置请求头。vLLM 默认不需要认证</summary>
    /// <param name="request">HTTP 请求</param>
    /// <param name="options">选项</param>
    protected override void SetHeaders(HttpRequestMessage request, AiProviderOptions options)
    {
        if (!String.IsNullOrEmpty(options.ApiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
    }
}

/// <summary>OneAPI 服务商。开源 LLM API 管理和分发系统</summary>
/// <remarks>
/// 支持对接多个上游渠道，统一管理 API Key 和限流。
/// 官方项目：https://github.com/songquanpeng/one-api
/// </remarks>
public class OneApiProvider : OpenAiProvider
{
    /// <summary>服务商编码</summary>
    public override String Code => "OneAPI";

    /// <summary>服务商名称</summary>
    public override String Name => "OneAPI";

    /// <summary>服务商描述</summary>
    public override String? Description => "OneAPI 开源 LLM API 管理和分发系统";

    /// <summary>默认 API 地址。需要用户配置为自己的 OneAPI 部署地址</summary>
    public override String DefaultEndpoint => "http://localhost:3000";

    /// <summary>主流模型列表。OneAPI 分发系统（能力取决于上游渠道配置）</summary>
    public override AiModelInfo[] Models { get; } = [];
}

/// <summary>小马算力服务商。GPU 算力租赁平台，提供 AI 推理服务</summary>
public class XiaomaPowerProvider : OpenAiProvider
{
    /// <summary>服务商编码</summary>
    public override String Code => "XiaomaPower";

    /// <summary>服务商名称</summary>
    public override String Name => "小马算力";

    /// <summary>服务商描述</summary>
    public override String? Description => "小马算力 GPU 算力平台";

    /// <summary>默认 API 地址</summary>
    public override String DefaultEndpoint => "https://openapi.xmpower.cn";

    /// <summary>主流模型列表。小马算力平台上的代表性模型</summary>
    public override AiModelInfo[] Models { get; } =
    [
        new("deepseek-r1",  "DeepSeek R1", new(true,  false, false, true)),
        new("deepseek-v3",  "DeepSeek V3", new(false, false, false, true)),
    ];
}
