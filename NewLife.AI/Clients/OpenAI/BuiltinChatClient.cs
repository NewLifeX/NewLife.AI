namespace NewLife.AI.Clients.OpenAI;

// ── 国内外兼容 OpenAI 协议的服务商 ────────────────────────────────────────────────────
[AiClient("DeepSeek", "深度求索", "https://api.deepseek.com", Description = "DeepSeek 系列推理和对话模型", Order = 2)]
[AiClientModel("deepseek-chat", "DeepSeek Chat", Code = "DeepSeek", FunctionCalling = true)]
[AiClientModel("deepseek-reasoner", "DeepSeek Reasoner", Code = "DeepSeek", Thinking = true)]
[AiClient("VolcEngine", "字节豆包", "https://ark.cn-beijing.volces.com/api/v3",
    Description = "字节跳动火山方舟平台，支持豆包等大模型", ChatPath = "/chat/completions", Order = 4)]
[AiClientModel("doubao-1.5-pro-32k", "豆包 1.5 Pro", Code = "VolcEngine", Thinking = true, Vision = true)]
[AiClientModel("doubao-1.5-lite-32k", "豆包 1.5 Lite", Code = "VolcEngine", FunctionCalling = false)]
[AiClientModel("doubao-1.5-vision-pro-32k", "豆包 1.5 Vision Pro", Code = "VolcEngine", Vision = true)]
[AiClient("Zhipu", "智谱AI", "https://open.bigmodel.cn/api/paas/v4",
    Description = "智谱 AI，支持 GLM-4/CogView 系列模型", ChatPath = "/chat/completions", Order = 5)]
[AiClientModel("glm-4", "GLM-4", Code = "Zhipu", Thinking = true, Vision = true)]
[AiClientModel("glm-4-flash", "GLM-4 Flash", Code = "Zhipu", FunctionCalling = false)]
[AiClientModel("glm-4v-plus", "GLM-4V Plus", Code = "Zhipu", Vision = true, FunctionCalling = true)]
[AiClientModel("glm-4-alltools", "GLM-4 AllTools", Code = "Zhipu", FunctionCalling = true)]
[AiClientModel("cogview-3", "CogView-3", Code = "Zhipu", ImageGeneration = true, FunctionCalling = false)]
[AiClient("Moonshot", "月之暗面Kimi", "https://api.moonshot.cn",
    Description = "月之暗面 Kimi 系列，支持超长上下文和推理思考", Order = 6)]
[AiClientModel("moonshot-v1-128k", "Kimi 128K", Code = "Moonshot")]
[AiClientModel("kimi-k1.5", "Kimi K1.5", Code = "Moonshot", Thinking = true)]
[AiClient("Hunyuan", "腾讯混元", "https://api.hunyuan.cloud.tencent.com",
    Description = "腾讯混元大模型", Order = 7)]
[AiClientModel("hunyuan-t1", "混元 T1", Code = "Hunyuan", Thinking = true, Vision = true)]
[AiClientModel("hunyuan-pro", "混元 Pro", Code = "Hunyuan", Vision = true)]
[AiClientModel("hunyuan-lite", "混元 Lite", Code = "Hunyuan")]
[AiClient("Qianfan", "百度文心", "https://qianfan.baidubce.com/v2",
    Description = "百度千帆大模型平台，支持文心一言系列", ChatPath = "/chat/completions", Order = 8)]
[AiClientModel("ernie-4.5-turbo", "ERNIE 4.5 Turbo", Code = "Qianfan", Thinking = true, Vision = true)]
[AiClientModel("ernie-4.0-turbo", "ERNIE 4.0 Turbo", Code = "Qianfan", Thinking = true, Vision = true)]
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
[AiClientModel("grok-3", "Grok-3", Code = "XAI", FunctionCalling = true, Vision = true)]
[AiClientModel("grok-3-mini", "Grok-3 Mini", Code = "XAI", FunctionCalling = true, Thinking = true)]
[AiClientModel("grok-2-vision", "Grok-2 Vision", Code = "XAI", Vision = true, FunctionCalling = true)]
[AiClient("GitHubModels", "GitHub Models", "https://models.github.ai/inference",
    Description = "GitHub 模型市场，提供商用 AI 模型体验", Order = 16)]
[AiClient("OpenRouter", "OpenRouter", "https://openrouter.ai/api",
    Description = "OpenRouter 多模型聚合平台", Order = 17)]
[AiClient("Mistral", "Mistral AI", "https://api.mistral.ai",
    Description = "Mistral AI 模型", Order = 18)]
[AiClientModel("mistral-large-latest", "Mistral Large", Code = "Mistral", FunctionCalling = true)]
[AiClientModel("mistral-small-latest", "Mistral Small", Code = "Mistral", FunctionCalling = true)]
[AiClient("Cohere", "Cohere", "https://api.cohere.com/compatibility",
    Description = "Cohere 语言模型", Order = 19)]
[AiClientModel("command-a-03-2025", "Command A", Code = "Cohere", FunctionCalling = true)]
[AiClientModel("command-r-plus", "Command R+", Code = "Cohere", FunctionCalling = true)]
[AiClient("Perplexity", "Perplexity", "https://api.perplexity.ai",
    Description = "Perplexity AI 模型", Order = 20)]
[AiClientModel("sonar-pro", "Sonar Pro", Code = "Perplexity")]
[AiClientModel("sonar", "Sonar", Code = "Perplexity")]
[AiClient("Groq", "Groq", "https://api.groq.com/openai",
    Description = "Groq 高速推理平台", Order = 21)]
[AiClientModel("llama-3.3-70b-versatile", "Llama 3.3 70B", Code = "Groq", FunctionCalling = true)]
[AiClientModel("gemma2-9b-it", "Gemma2 9B", Code = "Groq")]
[AiClientModel("deepseek-r1-distill-llama-70b", "DeepSeek R1 Distill 70B", Code = "Groq", Thinking = true)]
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
// ── 补充国际服务商 ──────────────────────────────────────────────────────────────────
[AiClient("HuggingFace", "Hugging Face", "https://router.huggingface.co",
    Description = "Hugging Face 推理路由，支持数千种开源模型", Order = 30)]
[AiClient("NvidiaNIM", "Nvidia NIM", "https://integrate.api.nvidia.com",
    Description = "Nvidia NIM 推理微服务平台", Order = 31)]
[AiClientModel("meta/llama-3.3-70b-instruct", "Llama 3.3 70B", Code = "NvidiaNIM", FunctionCalling = true)]
[AiClient("DeepInfra", "DeepInfra", "https://api.deepinfra.com",
    Description = "DeepInfra 开源模型推理平台", Order = 32)]
[AiClient("Hyperbolic", "Hyperbolic", "https://api.hyperbolic.xyz",
    Description = "Hyperbolic AI 开源模型推理平台", Order = 33)]
[AiClient("NovitaAI", "Novita AI", "https://api.novita.ai",
    Description = "Novita AI 模型推理平台", Order = 34)]
[AiClient("AI21", "AI21 Labs", "https://api.ai21.com",
    Description = "AI21 Labs Jamba 系列模型", Order = 35)]
[AiClientModel("jamba-1.5-large", "Jamba 1.5 Large", Code = "AI21", FunctionCalling = true)]
// ── 补充国内服务商 ──────────────────────────────────────────────────────────────────
[AiClient("Stepfun", "阶跃星辰", "https://api.stepfun.com",
    Description = "阶跃星辰 Step 系列大模型", Order = 36)]
[AiClientModel("step-2-16k", "Step-2 16K", Code = "Stepfun", FunctionCalling = true)]
[AiClient("Baichuan", "百川智能", "https://api.baichuan-ai.com",
    Description = "百川智能 Baichuan 系列大模型", Order = 37)]
[AiClientModel("Baichuan4-Turbo", "百川4 Turbo", Code = "Baichuan", FunctionCalling = true)]
[AiClient("SenseNova", "商汤日日新", "https://api.sensenova.cn/compatible-mode",
    Description = "商汤科技日日新大模型平台", ChatPath = "/chat/completions", Order = 38)]
[AiClient("Doubao", "抖音豆包", "https://api.doubao.com",
    Description = "字节跳动豆包大模型消费端入口", Order = 39)]
[AiClient("CloudflareAI", "Cloudflare AI", "https://api.cloudflare.com/client/v4/accounts/{account_id}/ai",
    Description = "Cloudflare Workers AI 平台，需替换 {account_id}", Order = 40)]
partial class OpenAIChatClient
{
}
