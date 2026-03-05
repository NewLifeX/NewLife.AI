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

    /// <summary>默认能力信息。Azure AI 支持思考/视觉/图像生成/函数调用</summary>
    public override AiProviderCapabilities DefaultCapabilities { get; } = new(true, true, true, true);
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

    /// <summary>默认能力信息。百炼支持思考/视觉/图像生成/函数调用</summary>
    public override AiProviderCapabilities DefaultCapabilities { get; } = new(true, true, true, true);
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

    /// <summary>默认能力信息。DeepSeek 支持思考和函数调用</summary>
    public override AiProviderCapabilities DefaultCapabilities { get; } = new(true, false, false, true);
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

    /// <summary>默认能力信息。火山方舟支持思考/视觉/函数调用</summary>
    public override AiProviderCapabilities DefaultCapabilities { get; } = new(true, true, false, true);

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

    /// <summary>默认能力信息。智谱支持思考/视觉/图像生成/函数调用</summary>
    public override AiProviderCapabilities DefaultCapabilities { get; } = new(true, true, true, true);

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

    /// <summary>默认能力信息。Moonshot 支持思考和函数调用</summary>
    public override AiProviderCapabilities DefaultCapabilities { get; } = new(true, false, false, true);
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

    /// <summary>默认能力信息。混元支持思考/视觉/函数调用</summary>
    public override AiProviderCapabilities DefaultCapabilities { get; } = new(true, true, false, true);
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

    /// <summary>默认能力信息。千帆支持思考/视觉/函数调用</summary>
    public override AiProviderCapabilities DefaultCapabilities { get; } = new(true, true, false, true);

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

    /// <summary>默认能力信息。星火支持思考和函数调用</summary>
    public override AiProviderCapabilities DefaultCapabilities { get; } = new(true, false, false, true);
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

    /// <summary>默认能力信息。Yi 支持视觉和函数调用</summary>
    public override AiProviderCapabilities DefaultCapabilities { get; } = new(false, true, false, true);
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

    /// <summary>默认能力信息。MiniMax 支持视觉和函数调用</summary>
    public override AiProviderCapabilities DefaultCapabilities { get; } = new(false, true, false, true);
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

    /// <summary>默认能力信息。硅基流动支持思考/视觉/图像生成/函数调用</summary>
    public override AiProviderCapabilities DefaultCapabilities { get; } = new(true, true, true, true);
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

    /// <summary>默认能力信息。Grok 支持思考/视觉/图像生成/函数调用</summary>
    public override AiProviderCapabilities DefaultCapabilities { get; } = new(true, true, true, true);
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

    /// <summary>默认能力信息。GitHub Models 支持多种模型能力</summary>
    public override AiProviderCapabilities DefaultCapabilities { get; } = new(true, true, true, true);
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

    /// <summary>默认能力信息。OpenRouter 支持多种模型能力</summary>
    public override AiProviderCapabilities DefaultCapabilities { get; } = new(true, true, true, true);
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

    /// <summary>默认能力信息。本地部署，能力取决于实际加载的模型</summary>
    public override AiProviderCapabilities DefaultCapabilities { get; } = new();

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

    /// <summary>默认能力信息。MiMo 支持思考和函数调用</summary>
    public override AiProviderCapabilities DefaultCapabilities { get; } = new(true, false, false, true);
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

    /// <summary>默认能力信息。Together AI 支持思考/视觉/函数调用</summary>
    public override AiProviderCapabilities DefaultCapabilities { get; } = new(true, true, true, true);
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

    /// <summary>默认能力信息。Groq 支持思考/视觉/函数调用</summary>
    public override AiProviderCapabilities DefaultCapabilities { get; } = new(true, true, false, true);
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

    /// <summary>默认能力信息。Mistral 支持思考/视觉/图像生成/函数调用</summary>
    public override AiProviderCapabilities DefaultCapabilities { get; } = new(true, true, true, true);
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

    /// <summary>默认能力信息。Cohere 支持函数调用，擅长 RAG</summary>
    public override AiProviderCapabilities DefaultCapabilities { get; } = new(false, false, false, true);
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

    /// <summary>默认能力信息。Perplexity 专注联网搜索增强</summary>
    public override AiProviderCapabilities DefaultCapabilities { get; } = new(true, false, false, false);
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

    /// <summary>默认能力信息。无问芯穹支持思考/视觉/函数调用</summary>
    public override AiProviderCapabilities DefaultCapabilities { get; } = new(true, true, false, true);
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

    /// <summary>默认能力信息。Cerebras 支持函数调用</summary>
    public override AiProviderCapabilities DefaultCapabilities { get; } = new(false, false, false, true);
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

    /// <summary>默认能力信息。Fireworks 支持思考/视觉/图像生成/函数调用</summary>
    public override AiProviderCapabilities DefaultCapabilities { get; } = new(true, true, true, true);
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

    /// <summary>默认能力信息。SambaNova 支持思考/视觉/函数调用</summary>
    public override AiProviderCapabilities DefaultCapabilities { get; } = new(true, true, false, true);
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

    /// <summary>默认能力信息。本地部署，能力取决于实际加载的模型</summary>
    public override AiProviderCapabilities DefaultCapabilities { get; } = new();

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

    /// <summary>默认能力信息。自部署引擎，能力取决于实际加载的模型</summary>
    public override AiProviderCapabilities DefaultCapabilities { get; } = new();

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

    /// <summary>默认能力信息。分发系统，能力取决于上游渠道配置</summary>
    public override AiProviderCapabilities DefaultCapabilities { get; } = new();
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

    /// <summary>默认能力信息。小马算力支持思考和函数调用</summary>
    public override AiProviderCapabilities DefaultCapabilities { get; } = new(true, false, false, true);
}
