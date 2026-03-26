using NewLife.AI.Providers;

namespace NewLife.AI.Clients;

// ── 国内外兼容 OpenAI 协议的服务商 ────────────────────────────────────────────────────
[AiClient("DeepSeek", "深度求索", "https://api.deepseek.com", Description = "DeepSeek 系列推理和对话模型", Order = 2)]
[AiClientModel("deepseek-chat", "DeepSeek Chat", Code = "DeepSeek", FunctionCalling = true)]
[AiClientModel("deepseek-reasoner", "DeepSeek Reasoner", Code = "DeepSeek", Thinking = true)]
[AiClient("AzureAI", "Azure AI", "https://models.inference.ai.azure.com", Description = "微软 Azure AI Foundry 模型托管服务", Order = 3)]
[AiClient("VolcEngine", "字节豆包", "https://ark.cn-beijing.volces.com/api/v3",
    Description = "字节跳动火山方舟平台，支持豆包等大模型", ChatPath = "/chat/completions", Order = 4)]
[AiClientModel("doubao-1.5-pro-32k", "豆包 1.5 Pro", Code = "VolcEngine", Thinking = true, Vision = true)]
[AiClientModel("doubao-1.5-lite-32k", "豆包 1.5 Lite", Code = "VolcEngine", FunctionCalling = false)]
[AiClient("Zhipu", "智谱AI", "https://open.bigmodel.cn/api/paas/v4",
    Description = "智谱 AI，支持 GLM-4/CogView 系列模型", ChatPath = "/chat/completions", Order = 5)]
[AiClientModel("glm-4", "GLM-4", Code = "Zhipu", Thinking = true, Vision = true)]
[AiClientModel("glm-4-flash", "GLM-4 Flash", Code = "Zhipu", FunctionCalling = false)]
[AiClientModel("cogview-3", "CogView-3", Code = "Zhipu", ImageGeneration = true, FunctionCalling = false)]
[AiClient("Moonshot", "月之暗面Kimi", "https://api.moonshot.cn",
    Description = "月之暗面 Kimi 系列，支持超长上下文和推理思考", Order = 6)]
[AiClientModel("moonshot-v1-128k", "Kimi 128K", Code = "Moonshot")]
[AiClientModel("kimi-k1.5", "Kimi K1.5", Code = "Moonshot", Thinking = true)]
[AiClient("Hunyuan", "腾讯混元", "https://api.hunyuan.cloud.tencent.com",
    Description = "腾讯混元大模型", Order = 7)]
[AiClientModel("hunyuan-t1", "混元 T1", Code = "Hunyuan", Thinking = true, Vision = true)]
[AiClientModel("hunyuan-pro", "混元 Pro", Code = "Hunyuan", Vision = true)]
[AiClient("Qianfan", "百度文心", "https://qianfan.baidubce.com/v2",
    Description = "百度千帆大模型平台，支持文心一言系列", ChatPath = "/chat/completions", Order = 8)]
[AiClientModel("ernie-4.5-turbo", "ERNIE 4.5 Turbo", Code = "Qianfan", Thinking = true, Vision = true)]
[AiClientModel("ernie-speed", "ERNIE Speed", Code = "Qianfan", FunctionCalling = false)]
[AiClient("Spark", "讯飞星火", "https://spark-api-open.xf-yun.com",
    Description = "讯飞星火认知大模型", Order = 9)]
[AiClientModel("spark-4.0-ultra", "星火 4.0 Ultra", Code = "Spark", Thinking = true)]
[AiClientModel("spark-3.5-max", "星火 3.5 Max", Code = "Spark", FunctionCalling = false)]
[AiClient("MiniMax", "MiniMax", "https://api.minimax.chat",
    Description = "MiniMax 大模型", Order = 10)]
[AiClient("SiliconFlow", "硅基流动", "https://api.siliconflow.cn",
    Description = "硅基流动 AI 模型推理平台", Order = 11)]
[AiClient("MiMo", "小米MiMo", "https://api.xiaomimimo.com",
    Description = "小米 MiMo 大模型", Order = 12)]
[AiClient("Infini", "无问芯穹", "https://cloud.infini-ai.com/maas",
    Description = "无问芯穹 AI 推理平台", Order = 13)]
[AiClient("XiaomaPower", "小马算力", "https://openapi.xmpower.cn",
    Description = "小马算力 GPU 算力平台", Order = 14)]
[AiClient("XAI", "xAI Grok", "https://api.x.ai",
    Description = "xAI Grok 系列大模型", Order = 15)]
[AiClient("GitHubModels", "GitHub Models", "https://models.github.ai/inference",
    Description = "GitHub 模型市场，提供商用 AI 模型体验", Order = 16)]
[AiClient("OpenRouter", "OpenRouter", "https://openrouter.ai/api",
    Description = "OpenRouter 多模型聚合平台", Order = 17)]
[AiClient("Mistral", "Mistral AI", "https://api.mistral.ai",
    Description = "Mistral AI 模型", Order = 18)]
[AiClient("Cohere", "Cohere", "https://api.cohere.com/compatibility",
    Description = "Cohere 语言模型", Order = 19)]
[AiClient("Perplexity", "Perplexity", "https://api.perplexity.ai",
    Description = "Perplexity AI 模型", Order = 20)]
[AiClient("Groq", "Groq", "https://api.groq.com/openai",
    Description = "Groq 高速推理平台", Order = 21)]
[AiClient("Cerebras", "Cerebras", "https://api.cerebras.ai",
    Description = "Cerebras AI 推理平台", Order = 22)]
[AiClient("TogetherAI", "Together AI", "https://api.together.xyz",
    Description = "Together AI 开源模型推理平台", Order = 23)]
[AiClient("Fireworks", "Fireworks AI", "https://api.fireworks.ai/inference",
    Description = "Fireworks AI 生成式模型平台", Order = 24)]
[AiClient("SambaNova", "SambaNova", "https://api.sambanova.ai",
    Description = "SambaNova RDU 架构 AI 推理平台", Order = 25)]
[AiClient("Yi", "零一万物", "https://api.lingyiwanwu.com",
    Description = "零一万物 Yi 系列大模型", Order = 26)]
// ── 本地/私有部署 ────────────────────────────────────────────────────────────────────
[AiClient("LMStudio", "LM Studio", "http://localhost:1234",
    Description = "LM Studio 桌面端本地模型运行工具", Order = 27)]
[AiClient("vLLM", "vLLM", "http://localhost:8000",
    Description = "vLLM 高吞吐量推理引擎，支持自部署", Order = 28)]
[AiClient("OneAPI", "OneAPI", "http://localhost:3000",
    Description = "OneAPI 开源 LLM API 管理和分发系统", Order = 29)]
partial class OpenAIChatClient
{
}
