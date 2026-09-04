using NewLife.AI.Clients.OpenAI;
using NewLife.AI.Models;
using NewLife.Serialization;

namespace NewLife.AI.Clients.DashScope;

// ===== 主力对话模型（2026-Q3 代表性：最新旗舰 + 无新版替代的同档型号；历史型号由模型元数据表承载） =====
// qwen3.8-max 多模态旗舰；qwen3.7-plus/flash 保留（3.8 无对应档位）
[AiClientModel("qwen3.8-max", "Qwen3.8 Max", Thinking = true, Vision = true, InputPrice = 12, OutputPrice = 36, CachedInputPrice = 1.5, CacheCreationPrice = 15)]
[AiClientModel("qwen3.7-plus", "Qwen3.7 Plus", Thinking = true, Vision = true, InputPrice = 2, OutputPrice = 8, CachedInputPrice = 0.4, CacheCreationPrice = 2.5)]
[AiClientModel("qwen3.7-flash", "Qwen3.7 Flash", Thinking = true, Vision = true, InputPrice = 0.2, OutputPrice = 0.8, CachedInputPrice = 0.04, CacheCreationPrice = 0.25)]
// Omni 全模态（最新 3.5 系列）
[AiClientModel("qwen3.5-omni-plus", "Qwen3.5 Omni Plus", Vision = true, Audio = true, Speech = true, FunctionCalling = false, InputPrice = 3.5, OutputPrice = 14, CachedInputPrice = 0.35)]
[AiClientModel("qwen3.5-omni-flash", "Qwen3.5 Omni Flash", Vision = true, Audio = true, Speech = true, FunctionCalling = false, InputPrice = 1.5, OutputPrice = 6, CachedInputPrice = 0.15)]
// TTS 主力 + 图像编辑（无新版替代）
[AiClientModel("qwen3-tts-flash", "千问3 TTS Flash", Speech = true, FunctionCalling = false, InputPrice = 0.2)]
[AiClientModel("qwen-image-edit", "Qwen Image Edit", ImageGeneration = true, FunctionCalling = false, InputPrice = 0.2)]
// 百炼托管第三方（价格与官方渠道不同，必须精确注册；历史版本由模型元数据表承载）
[AiClientModel("deepseek-v4-pro", "DeepSeek V4 Pro", Thinking = true, InputPrice = 12, OutputPrice = 24, CachedInputPrice = 1)]
[AiClientModel("deepseek-v4-flash", "DeepSeek V4 Flash", Thinking = true, InputPrice = 1, OutputPrice = 2, CachedInputPrice = 0.2)]
[AiClientModel("glm-5.2", "GLM 5.2", Thinking = true, InputPrice = 8, OutputPrice = 28, CachedInputPrice = 2)]
[AiClientModel("kimi-k3", "Kimi K3", Thinking = true, Vision = true, InputPrice = 20, OutputPrice = 100, CachedInputPrice = 2)]
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
    /// <para>qwen/qwq/qvq/deepseek/glm/kimi/minimax 等家族命名规律已抽为 <see cref="ModelFamily"/> 家族档案（版本通配，新版本自动覆盖），
    /// 由 <see cref="OpenAIClientBase.InferModelCapabilities"/> 基类匹配。此处仅保留百炼平台专属模型：</para>
    /// <list type="bullet">
    /// <item>embed / rerank：嵌入与重排序模型</item>
    /// <item>paraformer / sensevoice / fun-asr / sambert：语音识别（ASR）</item>
    /// <item>cosyvoice：语音合成（TTS）</item>
    /// <item>wanx / wan2 / flux / stable-diffusion / z-image：文生图/视频生成</item>
    /// <item>farui：专用模型，不支持函数调用</item>
    /// </list>
    /// <para>百炼托管第三方模型（kimi/glm/MiniMax）能力由对应家族接管，价格差异走 [AiClientModel] 精确注册。</para>
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

        // 其余模型（qwen/qwq/qvq/deepseek/glm/kimi/minimax 等家族及未知模型）交给基类：先按家族规则匹配，未命中走通用兜底
        return base.InferModelCapabilities(modelId);
    }
    #endregion
}
