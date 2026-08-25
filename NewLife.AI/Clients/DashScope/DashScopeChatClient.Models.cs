using NewLife.AI.Clients.OpenAI;
using NewLife.AI.Models;
using NewLife.Serialization;

namespace NewLife.AI.Clients.DashScope;

// ===== 对话模型 =====
[AiClientModel("qwen3-max", "Qwen3 Max", Thinking = true, InputPrice = 2.4, OutputPrice = 14.4, CachedInputPrice = 0.24)]
[AiClientModel("qwq-plus", "QwQ Plus", Thinking = true, InputPrice = 2, OutputPrice = 12, CachedInputPrice = 0.2)]
[AiClientModel("qwen-vl-max", "Qwen VL Max", Vision = true, InputPrice = 3, OutputPrice = 18, CachedInputPrice = 0.3)]
[AiClientModel("qwen-image-2.0-pro", "Qwen Image 2.0 Pro", ImageGeneration = true, FunctionCalling = false, InputPrice = 0.2)]
[AiClientModel("qwen-image-edit", "Qwen Image Edit", ImageGeneration = true, FunctionCalling = false, InputPrice = 0.2)]
[AiClientModel("qwen3-coder-next", "Qwen3 Coder", InputPrice = 2.4, OutputPrice = 14.4, CachedInputPrice = 0.24)]
// Omni 系列：视觉输入 + 语音识别输入 + 语音合成输出
[AiClientModel("qwen3.5-omni-plus", "Qwen3.5 Omni Plus", Vision = true, Audio = true, Speech = true, FunctionCalling = false, InputPrice = 3.5, OutputPrice = 14, CachedInputPrice = 0.35)]
[AiClientModel("qwen3.5-omni-flash", "Qwen3.5 Omni Flash", Vision = true, Audio = true, Speech = true, FunctionCalling = false, InputPrice = 1.5, OutputPrice = 6, CachedInputPrice = 0.15)]
[AiClientModel("qwen3-omni-flash", "Qwen3 Omni Flash", Vision = true, Audio = true, Speech = true, Thinking = true, FunctionCalling = false, InputPrice = 1.5, OutputPrice = 6, CachedInputPrice = 0.15)]
[AiClientModel("qwen-omni-turbo", "Qwen Omni Turbo", Vision = true, Audio = true, Speech = true, FunctionCalling = false, InputPrice = 2, OutputPrice = 8, CachedInputPrice = 0.2)]
[AiClientModel("wan2.6-t2i", "文生图（万相2.6）", ImageGeneration = true, FunctionCalling = false, InputPrice = 0.2)]
[AiClientModel("wan2.7-t2v", "文生视频（万相2.7）", VideoGeneration = true, FunctionCalling = false, InputPrice = 0.6)]
[AiClientModel("wan2.7-i2v", "图生视频（万相2.7）", Vision = true, VideoGeneration = true, FunctionCalling = false, InputPrice = 0.6)]
// ===== TTS 语音合成模型（Speech=true 表示音频输出） =====
[AiClientModel("cosyvoice-v3-flash", "CosyVoice V3 Flash", Speech = true, FunctionCalling = false, InputPrice = 0.2)]
[AiClientModel("cosyvoice-v3-plus", "CosyVoice V3 Plus", Speech = true, FunctionCalling = false, InputPrice = 0.2)]
// Qwen3-TTS 主力：非实时 HTTP 合成 + WebSocket 实时合成
[AiClientModel("qwen3-tts-flash", "千问3 TTS Flash", Speech = true, FunctionCalling = false, InputPrice = 0.2)]
[AiClientModel("qwen3-tts-flash-realtime", "千问3 TTS Flash Realtime", Speech = true, FunctionCalling = false, InputPrice = 0.2)]
// ===== 主力对话模型（2026-Q3 qwen3.8/3.7 系列）=====
// -max：qwen3.8 起为多模态旗舰（文本+图像+视频理解），qwen3.7-max 为纯文本旗舰；-plus/-flash：支持文本+视觉
[AiClientModel("qwen3.8-max", "Qwen3.8 Max", Thinking = true, Vision = true, InputPrice = 12, OutputPrice = 36, CachedInputPrice = 1.5, CacheCreationPrice = 15)]
[AiClientModel("qwen3.7-max", "Qwen3.7 Max", Thinking = true, InputPrice = 12, OutputPrice = 36, CachedInputPrice = 2.4, CacheCreationPrice = 15)]
[AiClientModel("qwen3.7-plus", "Qwen3.7 Plus", Thinking = true, Vision = true, InputPrice = 2, OutputPrice = 8, CachedInputPrice = 0.4, CacheCreationPrice = 2.5)]
[AiClientModel("qwen3.7-flash", "Qwen3.7 Flash", Thinking = true, Vision = true, InputPrice = 0.2, OutputPrice = 0.8, CachedInputPrice = 0.04, CacheCreationPrice = 0.25)]
[AiClientModel("qwen3.6-max", "Qwen3.6 Max", Thinking = true, InputPrice = 2, OutputPrice = 12, CachedInputPrice = 0.2)]
[AiClientModel("qwen3.6-plus", "Qwen3.6 Plus", Thinking = true, Vision = true, InputPrice = 1.4, OutputPrice = 5.6, CachedInputPrice = 0.14)]
[AiClientModel("qwen3.6-flash", "Qwen3.6 Flash", Thinking = true, Vision = true, InputPrice = 0.7, OutputPrice = 2.8, CachedInputPrice = 0.07)]
[AiClientModel("deepseek-v4-pro", "DeepSeek V4 Pro", Thinking = true, InputPrice = 12, OutputPrice = 24, CachedInputPrice = 1)]
[AiClientModel("deepseek-v4-flash", "DeepSeek V4 Flash", Thinking = true, InputPrice = 1, OutputPrice = 2, CachedInputPrice = 0.2)]
[AiClientModel("glm-5.1", "GLM 5.1", Thinking = true, InputPrice = 1.5, OutputPrice = 6, CachedInputPrice = 0.15)]
[AiClientModel("glm-5.2", "GLM 5.2", Thinking = true, InputPrice = 8, OutputPrice = 28, CachedInputPrice = 2)]
[AiClientModel("kimi-k2.6", "Kimi K2.6", Thinking = true, InputPrice = 1, OutputPrice = 4, CachedInputPrice = 0.1)]
[AiClientModel("kimi-k3", "Kimi K3", Thinking = true, Vision = true, InputPrice = 20, OutputPrice = 100, CachedInputPrice = 2)]
[AiClientModel("MiniMax-M2.5", "MiniMax M2.5", Thinking = true, InputPrice = 2, OutputPrice = 8, CachedInputPrice = 0.2)]
[AiClientModel("MiniMax/MiniMax-M3", "MiniMax M3", Thinking = true, Vision = true, InputPrice = 4.2, OutputPrice = 16.8, CachedInputPrice = 0.84)]
[AiClientModel("xiaomi/mimo-v2.5-pro", "MiMo V2.5 Pro", FunctionCalling = true, InputPrice = 7, OutputPrice = 21, CachedInputPrice = 1.4)]
// ===== 嵌入与重排序模型 =====
[AiClientModel("text-embedding-v4", "Text Embedding V4", Embedding = true, FunctionCalling = false, InputPrice = 0.5)]
[AiClientModel("qwen3-vl-embedding", "Qwen3 VL Embedding", Vision = true, Embedding = true, FunctionCalling = false, InputPrice = 0.5)]
[AiClientModel("qwen3-rerank", "Qwen3 Rerank", Rerank = true, FunctionCalling = false, InputPrice = 1)]
[AiClientModel("qwen3-vl-rerank", "Qwen3 VL Rerank", Vision = true, Rerank = true, FunctionCalling = false, InputPrice = 1)]
public partial class DashScopeChatClient
{
    #region 模型列表
    /// <summary>获取可用模型列表。使用兼容模式端点以保证返回完整模型目录</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>模型列表，服务不可用时返回 null</returns>
    public override async Task<ModelListResponse?> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        var url = CombineApiUrl(GetCompatibleBaseUrl(), "/v1/models");
        var json = await TryGetAsync(url, _options, cancellationToken).ConfigureAwait(false);
        if (json == null) return null;

        var dic = JsonParser.Decode(json);
        if (dic == null) return null;

        var response = new ModelListResponse { Object = dic["object"] as String };

        if (dic["data"] is IList<Object> dataList)
        {
            var items = new List<ModelInfo>(dataList.Count);
            foreach (var item in dataList)
            {
                if (item is not IDictionary<String, Object> d) continue;
                items.Add(new ModelInfo
                {
                    Id = d["id"] as String,
                    Object = d["object"] as String,
                    OwnedBy = d["owned_by"] as String,
                    Created = d["created"].ToLong().ToDateTime(),
                });
            }
            response.Data = [.. items];
        }
        return response;
    }
    #endregion

    #region 模型能力推断
    /// <summary>根据千问模型 ID 命名规律推断模型能力。qwen/deepseek 家族规则由基类家族匹配统一接管，此处仅保留百炼专属模型预检</summary>
    /// <remarks>
    /// <para>qwen/qwq/qvq/deepseek 家族命名规律已抽为 <see cref="ModelFamily"/> 家族档案（版本通配，新版本自动覆盖），
    /// 由 <see cref="OpenAIClientBase.InferModelCapabilities"/> 基类匹配。此处仅保留百炼平台专属模型：</para>
    /// <list type="bullet">
    /// <item>embed / rerank：嵌入与重排序模型</item>
    /// <item>paraformer / sensevoice / fun-asr / sambert：语音识别（ASR）</item>
    /// <item>cosyvoice：语音合成（TTS）</item>
    /// <item>wanx / wan2 / flux / stable-diffusion / z-image：文生图/视频生成</item>
    /// <item>farui：专用模型，不支持函数调用</item>
    /// <item>kimi-k2 / glm-5 / MiniMax-M2：百炼托管第三方推理模型</item>
    /// </list>
    /// </remarks>
    /// <param name="modelId">模型标识</param>
    /// <returns>推断出的能力信息，无法推断时返回 null</returns>
    public override AiProviderCapabilities? InferModelCapabilities(String? modelId)
    {
        if (modelId.IsNullOrEmpty()) return null;

        // 嵌入向量模型
        if (modelId.StartsWith("text-embedding", StringComparison.OrdinalIgnoreCase) ||
            modelId.Contains("embed", StringComparison.OrdinalIgnoreCase))
            return new AiProviderCapabilities(SupportEmbedding: true, SupportFunction: false,
                Pricing: new AiModelPricing(InputPrice: 0.5m));

        // 重排序模型
        if (modelId.Contains("rerank", StringComparison.OrdinalIgnoreCase))
            return new AiProviderCapabilities(SupportRerank: true, SupportFunction: false,
                Pricing: new AiModelPricing(InputPrice: 1m));

        // 语音识别（ASR）模型：paraformer / sensevoice / fun-asr / sambert（qwen 系 ASR 由 qwen-media 家族接管）
        if (modelId.StartsWithIgnoreCase("paraformer", "sambert", "fun-asr", "sensevoice"))
            return new AiProviderCapabilities(SupportAudio: true, SupportFunction: false,
                Pricing: new AiModelPricing(InputPrice: 0.2m));

        // TTS 语音合成模型：cosyvoice（qwen-tts 由 qwen-media 家族接管）
        if (modelId.StartsWith("cosyvoice", StringComparison.OrdinalIgnoreCase))
            return new AiProviderCapabilities(SupportSpeech: true, SupportFunction: false,
                Pricing: new AiModelPricing(InputPrice: 0.2m));

        // 文生图 / 文生视频：wanx / flux / stable-diffusion / z-image / wan2
        if (modelId.StartsWithIgnoreCase("wanx", "flux", "stable-diffusion", "z-image"))
            return new AiProviderCapabilities(SupportImage: true, SupportFunction: false);
        if (modelId.StartsWith("wan2", StringComparison.OrdinalIgnoreCase) &&
            (modelId.Contains("-t2v", StringComparison.OrdinalIgnoreCase) ||
             modelId.Contains("-i2v", StringComparison.OrdinalIgnoreCase)))
            return new AiProviderCapabilities(SupportVideo: true, SupportFunction: false);

        // 文生图：wan2 其他系列（如 wan2*-t2i*）
        if (modelId.StartsWith("wan2", StringComparison.OrdinalIgnoreCase))
            return new AiProviderCapabilities(SupportImage: true, SupportFunction: false);

        // 专用模型不支持函数调用：farui
        if (modelId.StartsWith("farui", StringComparison.OrdinalIgnoreCase))
            return new AiProviderCapabilities(SupportFunction: false);

        // 百炼托管第三方推理模型：kimi / glm / MiniMax（v1 未抽家族，暂保留服务商预检）
        if (modelId.StartsWithIgnoreCase("kimi-k2.", "glm-5.", "MiniMax-M2."))
        {
            var contextLength = modelId.StartsWithIgnoreCase("kimi-k2.") ? 262_144 :
                modelId.StartsWithIgnoreCase("glm-5.") ? 200_704 : 196_608;
            var pricing = modelId.StartsWithIgnoreCase("kimi-k2.") ? new AiModelPricing(1m, 4m, 0.1m) :
                modelId.StartsWithIgnoreCase("glm-5.") ? new AiModelPricing(1.5m, 6m, 0.15m) :
                new AiModelPricing(2m, 8m, 0.2m);
            return new AiProviderCapabilities(SupportThinking: true, SupportFunction: true, ContextLength: contextLength, Pricing: pricing);
        }

        // 其余模型（qwen/qwq/qvq/deepseek 家族及未知模型）交给基类：先按家族规则匹配，未命中走通用兜底
        return base.InferModelCapabilities(modelId);
    }
    #endregion
}
