using System.Text.RegularExpressions;
using NewLife.AI.Models;
using NewLife.Serialization;

namespace NewLife.AI.Clients.DashScope;

// ===== 对话模型 =====
[AiClientModel("qwen3-max", "Qwen3 Max", Thinking = true)]
[AiClientModel("qwq-plus", "QwQ Plus", Thinking = true)]
[AiClientModel("qwen-vl-max", "Qwen VL Max", Vision = true)]
[AiClientModel("qwen-image-2.0-pro", "Qwen Image 2.0 Pro", ImageGeneration = true, FunctionCalling = false)]
[AiClientModel("qwen-image-edit", "Qwen Image Edit", ImageGeneration = true, FunctionCalling = false)]
[AiClientModel("qwen3-coder-next", "Qwen3 Coder")]
[AiClientModel("qwen3.5-omni-plus", "Qwen3.5 Omni Plus", Vision = true, Audio = true, FunctionCalling = false)]
[AiClientModel("qwen3.5-omni-flash", "Qwen3.5 Omni Flash", Vision = true, Audio = true, FunctionCalling = false)]
[AiClientModel("qwen3-omni-flash", "Qwen3 Omni Flash", Vision = true, Audio = true, Thinking = true, FunctionCalling = false)]
[AiClientModel("qwen-omni-turbo", "Qwen Omni Turbo", Vision = true, Audio = true, FunctionCalling = false)]
[AiClientModel("wan2.6-t2i", "文生图（万相2.6）", ImageGeneration = true, FunctionCalling = false)]
[AiClientModel("wan2.7-t2v", "文生视频（万相2.7）", VideoGeneration = true, FunctionCalling = false)]
[AiClientModel("wan2.7-i2v", "图生视频（万相2.7）", Vision = true, VideoGeneration = true, FunctionCalling = false)]
// ===== TTS 语音合成模型 =====
[AiClientModel("cosyvoice-v3-flash", "CosyVoice V3 Flash", Audio = true, FunctionCalling = false)]
[AiClientModel("cosyvoice-v3-plus", "CosyVoice V3 Plus", Audio = true, FunctionCalling = false)]
// Qwen-TTS 非实时 HTTP 合成（稳定版别名）
[AiClientModel("qwen-tts", "千问 TTS", Audio = true, FunctionCalling = false)]
[AiClientModel("qwen3-tts-flash", "千问3 TTS Flash", Audio = true, FunctionCalling = false)]
[AiClientModel("qwen3-tts-instruct-flash", "千问3 TTS Instruct Flash", Audio = true, FunctionCalling = false)]
[AiClientModel("qwen3-tts-vd", "千问3 TTS VD", Audio = true, FunctionCalling = false)]
[AiClientModel("qwen3-tts-vc", "千问3 TTS VC", Audio = true, FunctionCalling = false)]
// Qwen-TTS-Realtime WebSocket 实时合成（稳定版别名）
[AiClientModel("qwen-tts-realtime", "千问 TTS Realtime", Audio = true, FunctionCalling = false)]
[AiClientModel("qwen3-tts-flash-realtime", "千问3 TTS Flash Realtime", Audio = true, FunctionCalling = false)]
[AiClientModel("qwen3-tts-instruct-flash-realtime", "千问3 TTS Instruct Flash Realtime", Audio = true, FunctionCalling = false)]
[AiClientModel("qwen3-tts-vd-realtime", "千问3 TTS VD Realtime", Audio = true, FunctionCalling = false)]
[AiClientModel("qwen3-tts-vc-realtime", "千问3 TTS VC Realtime", Audio = true, FunctionCalling = false)]
// ===== 主力对话模型（2026-Q2 qwen3.6 系列）=====
// -max：纯文本旗舰，不支持视觉；-plus/-flash：支持文本+视觉
[AiClientModel("qwen3.6-max", "Qwen3.6 Max", Thinking = true)]
[AiClientModel("qwen3.6-plus", "Qwen3.6 Plus", Thinking = true, Vision = true)]
[AiClientModel("qwen3.6-flash", "Qwen3.6 Flash", Thinking = true, Vision = true)]
[AiClientModel("deepseek-v4-pro", "DeepSeek V4 Pro", Thinking = true)]
[AiClientModel("deepseek-v4-flash", "DeepSeek V4 Flash", Thinking = true)]
[AiClientModel("glm-5.1", "GLM 5.1", Thinking = true)]
[AiClientModel("kimi-k2.6", "Kimi K2.6", Thinking = true)]
[AiClientModel("MiniMax-M2.5", "MiniMax M2.5", Thinking = true)]
// ===== 嵌入与重排序模型 =====
[AiClientModel("text-embedding-v4", "Text Embedding V4", FunctionCalling = false)]
[AiClientModel("qwen3-vl-embedding", "Qwen3 VL Embedding", Vision = true, FunctionCalling = false)]
[AiClientModel("qwen3-rerank", "Qwen3 Rerank", FunctionCalling = false)]
[AiClientModel("qwen3-vl-rerank", "Qwen3 VL Rerank", Vision = true, FunctionCalling = false)]
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
    /// <summary>根据千问模型 ID 命名规律推断模型能力</summary>
    /// <remarks>
    /// 阿里百炼模型命名规律（基于 2026-Q2 官方文档）：
    /// <list type="bullet">
    /// <item>qwen -plus/-flash/-turbo：支持文本 + 视觉（Vision = true），通过 OpenAI 兼容模式传入图片</item>
    /// <item>qwen -max：纯文本旗舰，不支持视觉</item>
    /// <item>qwen*-vl* / qvq-*：视觉语言系列，走 multimodal-generation 专属端点</item>
    /// <item>qwq-* / qvq-*：专用推理模型，始终具备思考能力</item>
    /// <item>qwen3*（除 coder 和 -instruct 后缀）：qwen3 时代全系列支持思考模式</item>
    /// <item>qwen-max/plus/flash/turbo（稳定版别名）：当前均指向 qwen3 时代，支持思考</item>
    /// <item>qwen-long / qwen2* / qwen1*：不支持思考模式</item>
    /// <item>qwen*-omni*：全模态模型，视觉+音频，使用专用 API</item>
    /// <item>wanx* / wan2* / flux* / qwen-image* / z-image*：文生图/视频生成</item>
    /// <item>embed* / rerank* / paraformer* / cosyvoice* / sambert* 等：非对话模型</item>
    /// <item>farui* / qwen-mt*：专用模型，不支持函数调用</item>
    /// <item>deepseek-v4* / kimi-k2* / glm-5* / MiniMax-M2*：百炼托管第三方推理模型，支持思考</item>
    /// </list>
    /// 注意：-max/-plus 本身不是思考能力的可靠信号，早期 qwen-max（qwen2 时代）不支持思考
    /// </remarks>
    /// <param name="modelId">模型标识</param>
    /// <returns>推断出的能力信息，无法推断时返回 null</returns>
    public override AiProviderCapabilities? InferModelCapabilities(String? modelId)
    {
        if (modelId.IsNullOrEmpty()) return null;

        // 非对话模型：嵌入、重排序、语音识别等
        if (modelId.StartsWith("text-embedding", StringComparison.OrdinalIgnoreCase) ||
            modelId.Contains("embed", StringComparison.OrdinalIgnoreCase) ||
            modelId.Contains("rerank", StringComparison.OrdinalIgnoreCase) ||
            modelId.StartsWith("paraformer", StringComparison.OrdinalIgnoreCase) ||
            modelId.StartsWith("sambert", StringComparison.OrdinalIgnoreCase) ||
            modelId.StartsWith("fun-asr", StringComparison.OrdinalIgnoreCase) ||
            modelId.StartsWith("sensevoice", StringComparison.OrdinalIgnoreCase) ||
            modelId.StartsWith("qwen-audio", StringComparison.OrdinalIgnoreCase) ||
            modelId.StartsWithIgnoreCase("qwen3-asr", "qwen-voice"))
            return new AiProviderCapabilities(false, false, false, false);

        // Qwen-TTS 语音合成模型：包含非实时和 Realtime WebSocket 变体
        if (modelId.StartsWithIgnoreCase("qwen-tts", "qwen3-tts"))
            return new AiProviderCapabilities(false, false, false, true);

        // TTS 语音合成模型：cosyvoice* 支持音频能力
        if (modelId.StartsWith("cosyvoice", StringComparison.OrdinalIgnoreCase))
            return new AiProviderCapabilities(false, false, false, true);

        var thinking = false;
        var vision = false;
        var imageGen = false;
        var funcCall = true;
        var audio = false;
        var videoGen = false;
        var contextLength = 32_768;

        // 文生图：wanx / flux / stable-diffusion / qwen-image / z-image
        if (modelId.StartsWith("wanx", StringComparison.OrdinalIgnoreCase) ||
            modelId.StartsWith("flux", StringComparison.OrdinalIgnoreCase) ||
            modelId.StartsWith("stable-diffusion", StringComparison.OrdinalIgnoreCase) ||
            modelId.StartsWith("qwen-image", StringComparison.OrdinalIgnoreCase) ||
            modelId.StartsWith("z-image", StringComparison.OrdinalIgnoreCase))
            return new AiProviderCapabilities(false, false, false, false, true, false, false, 0);
        if (modelId.StartsWith("wan2", StringComparison.OrdinalIgnoreCase) &&
            (modelId.Contains("-t2v", StringComparison.OrdinalIgnoreCase) ||
             modelId.Contains("-i2v", StringComparison.OrdinalIgnoreCase)))
            return new AiProviderCapabilities(false, false, false, false, false, true, false, 0);

        // 文生图：wan2 其他系列（如 wan2*-t2i*）
        if (modelId.StartsWith("wan2", StringComparison.OrdinalIgnoreCase))
            return new AiProviderCapabilities(false, false, false, false, true, false, false, 0);

        // === 全模态 Omni 模型 ===
        // qwen3.5-omni-* 系列：视觉+音频，支持联网搜索，不支持思考和函数调用
        if (modelId.StartsWith("qwen3.5-omni", StringComparison.OrdinalIgnoreCase))
            return new AiProviderCapabilities(false, false, true, true, false, false, false, 131_072);
        if (modelId.StartsWith("qwen3-omni", StringComparison.OrdinalIgnoreCase))
            return new AiProviderCapabilities(true, false, true, true, false, false, false, 131_072);
        // 旧版 Omni 模型（如 qwen-omni-turbo）：视觉+音频，不支持思考和函数调用
        if (modelId.Contains("-omni", StringComparison.OrdinalIgnoreCase))
            return new AiProviderCapabilities(false, false, true, true, false, false, false, 32_768);

        // === 视觉能力 ===
        // -vl 标识符（视觉语言/OCR）或 qvq- 前缀（视觉推理）
        if (modelId.Contains("-vl", StringComparison.OrdinalIgnoreCase) ||
            modelId.Contains("-ocr", StringComparison.OrdinalIgnoreCase) ||
            modelId.StartsWith("qvq-", StringComparison.OrdinalIgnoreCase))
            vision = true;

        // qwen -plus/-flash/-turbo 支持文本+视觉；-max 为纯文本旗舰无视觉
        if (Regex.IsMatch(modelId, @"^qwen\d+\.\d+-", RegexOptions.IgnoreCase) &&
            (modelId.Contains("-plus", StringComparison.OrdinalIgnoreCase) ||
             modelId.Contains("-flash", StringComparison.OrdinalIgnoreCase) ||
             modelId.Contains("-turbo", StringComparison.OrdinalIgnoreCase)))
            vision = true;

        // === 思考/推理能力 ===
        // 按模型家族精确匹配，-max/-plus 本身不是思考能力的可靠信号

        // 专用推理模型：qwq 纯文本推理，qvq 视觉推理
        if (modelId.StartsWith("qwq-", StringComparison.OrdinalIgnoreCase) ||
            modelId.StartsWith("qvq-", StringComparison.OrdinalIgnoreCase))
            thinking = true;

        // qwen3 全系列支持思考模式（qwen3-max/qwen3.5-plus/qwen3.5-flash 等）
        // 排除：coder（instruct-only）、-instruct 后缀（显式非思考版本）
        if (modelId.StartsWith("qwen3", StringComparison.OrdinalIgnoreCase) &&
            !modelId.Contains("-coder", StringComparison.OrdinalIgnoreCase) &&
            !modelId.Contains("-instruct", StringComparison.OrdinalIgnoreCase))
            thinking = true;

        // 稳定版别名当前均指向 qwen3 时代，支持思考模式
        // qwen-max → qwen3-max, qwen-plus → qwen3.6-plus, qwen-flash → qwen3.5-flash
        if (modelId.StartsWithIgnoreCase("qwen-max", "qwen-plus", "qwen-flash", "qwen-turbo"))
            thinking = true;

        // 明确不支持思考的模型
        if (modelId.StartsWithIgnoreCase("qwen-long", "qwen2", "qwen1"))
            thinking = false;

        // 阿里云百炼第三方推理模型：deepseek-v4、kimi-k2、glm-5、MiniMax-M2 支持思考模式
        if (modelId.StartsWithIgnoreCase("deepseek-v4-", "kimi-k2.", "glm-5.", "MiniMax-M2."))
            thinking = true;

        // === 函数调用 ===
        // 专用模型不支持函数调用
        if (modelId.StartsWith("farui", StringComparison.OrdinalIgnoreCase) ||
            modelId.StartsWith("qwen-mt", StringComparison.OrdinalIgnoreCase))
            funcCall = false;

        // === 上下文长度 ===
        // qwen-long 专为长文档设计，支持 1M tokens
        if (modelId.StartsWithIgnoreCase("qwen-long"))
            contextLength = 1_000_000;
        // qwen3.6-max-preview 256K，其他 qwen3.6-* 均为 1M（须在通用 qwen3 分支之前匹配）
        else if (modelId.StartsWithIgnoreCase("qwen3.6-max-preview"))
            contextLength = 262_144;
        else if (modelId.StartsWithIgnoreCase("qwen3.6-"))
            contextLength = 1_048_576;
        // qwen3/qwen3.5 全系列、稳定版别名（qwen-max/plus/flash/turbo）、推理模型（qwq/qvq）、qwen2.5 系列
        else if (modelId.StartsWithIgnoreCase("qwen3", "qwen-max", "qwen-plus", "qwen-flash", "qwen-turbo",
            "qwq-", "qvq-", "qwen2.5"))
            contextLength = 131_072;
        // deepseek-v4 系列：1M 上下文（须在通用 deepseek 分支之前匹配）
        else if (modelId.StartsWithIgnoreCase("deepseek-v4-"))
            contextLength = 1_048_576;
        // deepseek 其他系列（v3、r1 等）
        else if (modelId.StartsWith("deepseek", StringComparison.OrdinalIgnoreCase))
            contextLength = 65_536;
        // 第三方推理模型
        else if (modelId.StartsWithIgnoreCase("kimi-k2."))
            contextLength = 262_144;
        else if (modelId.StartsWithIgnoreCase("glm-5."))
            contextLength = 200_704;
        else if (modelId.StartsWithIgnoreCase("MiniMax-M2."))
            contextLength = 196_608;
        // 其余对话模型默认 32K（已在变量初始化时设置）

        return new AiProviderCapabilities(thinking, funcCall, vision, audio, imageGen, videoGen, false, contextLength);
    }
    #endregion
}
