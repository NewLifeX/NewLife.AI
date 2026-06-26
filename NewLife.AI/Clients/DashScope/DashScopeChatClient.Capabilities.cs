using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using NewLife.AI.Clients.OpenAI;
using NewLife.AI.Embedding;
using NewLife.AI.Models;
using NewLife.Remoting;
using NewLife.Serialization;

namespace NewLife.AI.Clients.DashScope;

public partial class DashScopeChatClient
{
    #region 文生图
    /// <summary>文生图。不同万相系列模型使用不同端点和请求格式：
    /// <list type="bullet">
    /// <item>wan2.6-t2i / wan2.*-t2i：原生 DashScope 多模态端点 /services/aigc/multimodal-generation/generation，messages 数组格式，结果在 output.choices[].message.content[].image</item>
    /// <item>wanx3.0-t2i-* 等：兼容模式端点 /compatible-mode/v1/images/generations，DALL·E 格式</item>
    /// </list>
    /// </summary>
    /// <param name="request">图像生成请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>图像生成响应</returns>
    public override async Task<ImageGenerationResponse?> TextToImageAsync(ImageGenerationRequest request, CancellationToken cancellationToken = default)
    {
        var modelId = request.Model ?? _options.Model ?? "";

        // DashScope 原生文生图模型（wan2.x-t2i / qwen-image*）使用原生多模态端点（messages 格式），其余走兼容模式
        if (IsNativeImageGenerationModel(modelId))
            return await TextToImageNativeAsync(request, cancellationToken).ConfigureAwait(false);

        var url = CombineApiUrl(GetCompatibleBaseUrl(), "/v1/images/generations");
        var json = await PostAsync(url, request, null, _options, cancellationToken).ConfigureAwait(false);
        return ParseImageGenerationResponse(json);
    }

    /// <summary>图像编辑。根据模型自动选择 DashScope 原生多模态协议或 OpenAI 兼容 multipart/form-data 协议。</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><term>原生路径</term> qwen-image-2.0* / qwen-image-edit*：走 /api/v1/services/aigc/multimodal-generation/generation，JSON messages 格式，<see cref="ImageEditsRequest.ImageUrl"/> 与 <see cref="ImageEditsRequest.ImageStream"/> 二选一传图。</item>
    /// <item><term>兼容路径</term> 其余模型：走 /compatible-mode/v1/images/edits，multipart/form-data 格式，需提供 <see cref="ImageEditsRequest.ImageStream"/>。</item>
    /// </list>
    /// </remarks>
    /// <param name="request">图像编辑请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>图像生成响应</returns>
    public override async Task<ImageGenerationResponse?> EditImageAsync(ImageEditsRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (String.IsNullOrWhiteSpace(request.Prompt)) throw new ArgumentException("Prompt 不能为空", nameof(request));
        if (request.ImageUrl == null && request.ImageStream == null)
            throw new ArgumentException("ImageUrl 与 ImageStream 不能同时为空", nameof(request));

        var modelId = request.Model ?? _options.Model ?? String.Empty;

        // qwen-image-2.0* / qwen-image-edit* 走原生多模态端点（JSON messages 格式）
        if (IsNativeImageEditModel(modelId))
            return await EditImageNativeAsync(request, cancellationToken).ConfigureAwait(false);

        // 其余模型走 OpenAI 兼容 multipart/form-data 端点
        if (request.ImageStream == null)
            throw new ArgumentException("该模型使用兼容路径，ImageStream 不能为空", nameof(request));

        var url = BuildImageEditUrl();

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(request.Prompt), "prompt");
        if (!String.IsNullOrEmpty(request.Model)) form.Add(new StringContent(request.Model), "model");
        if (!String.IsNullOrEmpty(request.Size)) form.Add(new StringContent(request.Size), "size");

        var imageContent = new StreamContent(request.ImageStream);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(imageContent, "image", request.ImageFileName ?? "image.png");

        if (request.MaskStream != null)
        {
            var maskContent = new StreamContent(request.MaskStream);
            maskContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            form.Add(maskContent, "mask", request.MaskFileName ?? "mask.png");
        }

        using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = form };
        SetHeaders(req, null, _options);

        using var resp = await HttpClient.SendAsync(req, cancellationToken).ConfigureAwait(false);
        var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

        if (!resp.IsSuccessStatusCode)
            throw new ApiException((Int32)resp.StatusCode, json);

        return ParseImageGenerationResponse(json);
    }

    /// <summary>qwen-image-2.0* / qwen-image-edit* 原生多模态图像编辑。端点与多模态对话相同，图片以 URL 或 Base64 传入 messages content 数组</summary>
    /// <remarks>
    /// 请求格式：input.messages[].content = [{image: url_or_base64}, {text: prompt}]<br/>
    /// 优先使用 <see cref="ImageEditsRequest.ImageUrl"/>；无 URL 时从 <see cref="ImageEditsRequest.ImageStream"/> 转 Base64（data:image/png;base64,...）。
    /// </remarks>
    /// <param name="request">图像编辑请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>图像生成响应</returns>
    private async Task<ImageGenerationResponse?> EditImageNativeAsync(ImageEditsRequest request, CancellationToken cancellationToken)
    {
        var url = GetNativeBaseUrl() + "/services/aigc/multimodal-generation/generation";

        // 优先使用 URL，否则将 Stream 转 Base64
        String imageValue;
        if (!String.IsNullOrEmpty(request.ImageUrl))
        {
            imageValue = request.ImageUrl!;
        }
        else
        {
            var ms = new MemoryStream();
            await request.ImageStream!.CopyToAsync(ms).ConfigureAwait(false);
            imageValue = "data:image/png;base64," + Convert.ToBase64String(ms.ToArray());
        }

        var body = new Dictionary<String, Object?>
        {
            ["model"] = request.Model ?? _options.Model,
            ["input"] = new Dictionary<String, Object>
            {
                ["messages"] = new[]
                {
                    new Dictionary<String, Object>
                    {
                        ["role"] = "user",
                        ["content"] = new Object[]
                        {
                            new Dictionary<String, Object> { ["image"] = imageValue },
                            new Dictionary<String, Object> { ["text"]  = request.Prompt },
                        },
                    },
                },
            },
            ["parameters"] = BuildImageEditParameters(request),
        };

        var json = await PostAsync(url, body, null, _options, cancellationToken).ConfigureAwait(false);
        return ParseNativeMultimodalResponse(json);
    }

    /// <summary>wan2.x-t2i 原生多模态文生图。端点与多模态对话相同，请求格式用 messages 数组，图片 URL 在 output.choices[].message.content[].image</summary>
    /// <param name="request">图像生成请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>图像生成响应</returns>
    private async Task<ImageGenerationResponse?> TextToImageNativeAsync(ImageGenerationRequest request, CancellationToken cancellationToken)
    {
        var url = GetNativeBaseUrl() + "/services/aigc/multimodal-generation/generation";

        var body = new Dictionary<String, Object?>
        {
            ["model"] = request.Model ?? _options.Model,
            ["input"] = new Dictionary<String, Object>
            {
                ["messages"] = new[]
                {
                    new Dictionary<String, Object>
                    {
                        ["role"] = "user",
                        ["content"] = new[] { new Dictionary<String, Object> { ["text"] = request.Prompt } },
                    },
                },
            },
            ["parameters"] = BuildT2iParameters(request),
        };

        var json = await PostAsync(url, body, null, _options, cancellationToken).ConfigureAwait(false);
        return ParseNativeMultimodalResponse(json);
    }

    /// <summary>判断是否为使用原生多模态端点的 DashScope 文生图模型</summary>
    /// <remarks>
    /// 包含两类：
    /// 1. wan2.x-t2i 系列（如 wan2.6-t2i、wan2.5-t2i-preview）
    /// 2. qwen-image 系列（如 qwen-image、qwen-image-plus、qwen-image-max、qwen-image-2.0、qwen-image-2.0-pro）
    /// </remarks>
    /// <param name="modelId">模型标识</param>
    /// <returns>是则返回 true</returns>
    private static Boolean IsNativeImageGenerationModel(String modelId)
    {
        if (modelId.IsNullOrEmpty()) return false;

        if (modelId.StartsWith("qwen-image", StringComparison.OrdinalIgnoreCase)) return true;

        return modelId.StartsWith("wan2.", StringComparison.OrdinalIgnoreCase) &&
               modelId.IndexOf("-t2i", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>判断是否为支持原生多模态图像编辑的 DashScope 模型</summary>
    /// <remarks>
    /// 涵盖两类：
    /// 1. qwen-image-2.0 系列（qwen-image-2.0、qwen-image-2.0-pro 等）：同时支持文生图与图像编辑
    /// 2. qwen-image-edit 系列（qwen-image-edit、qwen-image-edit-max、qwen-image-edit-plus 等）：专用图像编辑模型
    /// </remarks>
    /// <param name="modelId">模型标识</param>
    /// <returns>是则返回 true</returns>
    private static Boolean IsNativeImageEditModel(String modelId)
    {
        if (modelId.IsNullOrEmpty()) return false;
        if (modelId.StartsWith("qwen-image-edit", StringComparison.OrdinalIgnoreCase)) return true;

        // qwen-image-2.0 / qwen-image-2.0-pro 等（注意不匹配 qwen-image-plus/max 等纯文生图旧款）
        return modelId.StartsWith("qwen-image-2.", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>构建 wan2.x-t2i 文生图 parameters 字典</summary>
    /// <param name="request">图像生成请求</param>
    /// <returns>parameters 字典；无可用参数时返回 null</returns>
    private static Dictionary<String, Object?>? BuildT2iParameters(ImageGenerationRequest request)
    {
        var p = new Dictionary<String, Object?>();
        if (!String.IsNullOrEmpty(request.NegativePrompt)) p["negative_prompt"] = request.NegativePrompt;
        if (request.N.HasValue) p["n"] = request.N.Value;
        if (!String.IsNullOrEmpty(request.Size)) p["size"] = request.Size;
        return p.Count > 0 ? p : null;
    }

    /// <summary>构建 qwen-image-2.0* / qwen-image-edit* 原生图像编辑 parameters 字典</summary>
    /// <param name="request">图像编辑请求</param>
    /// <returns>parameters 字典；无可用参数时返回 null</returns>
    private static Dictionary<String, Object?>? BuildImageEditParameters(ImageEditsRequest request)
    {
        var p = new Dictionary<String, Object?>();
        if (!String.IsNullOrEmpty(request.NegativePrompt)) p["negative_prompt"] = request.NegativePrompt;
        if (request.N.HasValue) p["n"] = request.N.Value;
        if (!request.Size.IsNullOrEmpty())
        {
            // 统一分隔符：1024x1024 → 1024*1024（DashScope 原生接口要求 * 分隔）
            p["size"] = request.Size.Replace('x', '*').Replace('X', '*');
        }
        return p.Count > 0 ? p : null;
    }

    /// <summary>解析 DashScope 原生多模态响应，提取图片 URL 列表。适用于文生图（wan2/qwen-image）和图像编辑（qwen-image-edit*）两类端点响应</summary>
    /// <param name="json">API 响应 JSON</param>
    /// <returns>图像生成响应；解析失败时返回 null</returns>
    private static ImageGenerationResponse? ParseNativeMultimodalResponse(String json)
    {
        var dic = JsonParser.Decode(json);
        if (dic == null) return null;

        var code = dic["code"] as String;
        if (!String.IsNullOrEmpty(code))
        {
            var message = dic["message"] as String ?? code;
            throw new HttpRequestException($"[DashScope] 文生图错误 {code}: {message}");
        }

        var resp = new ImageGenerationResponse();

        if (dic["output"] is IDictionary<String, Object> output &&
            output["choices"] is IList<Object> choices)
        {
            var items = new List<ImageData>();
            foreach (var choice in choices)
            {
                if (choice is not IDictionary<String, Object> c) continue;
                if (c["message"] is not IDictionary<String, Object> msg) continue;
                if (msg["content"] is not IList<Object> contentList) continue;
                foreach (var item in contentList)
                {
                    if (item is not IDictionary<String, Object> d) continue;
                    var imageUrl = d["image"] as String;
                    if (!String.IsNullOrEmpty(imageUrl))
                        items.Add(new ImageData { Url = imageUrl });
                }
            }
            resp.Data = [.. items];
        }

        return resp;
    }

    private String BuildImageEditUrl()
    {
        var endpoint = _options.Endpoint;
        if (!endpoint.IsNullOrWhiteSpace())
        {
            if (endpoint.EndsWith("/images/edits", StringComparison.OrdinalIgnoreCase))
                return endpoint.TrimEnd('/');

            if (endpoint.IndexOf("compatible-mode", StringComparison.OrdinalIgnoreCase) >= 0)
                return NormalizeOpenAiImagePath(endpoint);
        }

        return NormalizeOpenAiImagePath(GetCompatibleBaseUrl());
    }

    private static String NormalizeOpenAiImagePath(String endpoint)
    {
        endpoint = endpoint.TrimEnd('/');
        if (endpoint.EndsWith("/images/edits", StringComparison.OrdinalIgnoreCase)) return endpoint;
        if (endpoint.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)) return endpoint + "/images/edits";

        return endpoint + "/v1/images/edits";
    }
    #endregion

    #region 重排序（Rerank）
    /// <summary>文档重排序。对 RAG 检索召回的候选文档按语义相关度重新排序</summary>
    /// <param name="request">重排序请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>重排序响应</returns>
    public async Task<RerankResponse> RerankAsync(RerankRequest request, CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<String, Object?>
        {
            ["model"] = !String.IsNullOrEmpty(request.Model) ? request.Model : "gte-rerank-v2",
            ["input"] = new Dictionary<String, Object> { ["query"] = request.Query, ["documents"] = request.Documents },
            ["parameters"] = BuildRerankParameters(request),
        };

        // DashScope 当前重排序走原生端点（非 OpenAI 兼容）；保留历史路径回退
        var nativeBase = GetNativeBaseUrl();
        var compatBase = GetCompatibleBaseUrl();
        var urls = new[]
        {
            nativeBase + "/services/rerank/text-rerank/text-rerank",
            nativeBase + "/services/rerank/text-rerank",
            compatBase + "/v1/reranks",
            compatBase + "/v1/rerank",
        };

        Exception? last = null;
        foreach (var url in urls)
        {
            try
            {
                var json = await PostAsync(url, body, null, _options, cancellationToken).ConfigureAwait(false);
                return ParseRerankResponse(json);
            }
            catch (Exception ex)
            {
                last = ex;
            }
        }

        throw last ?? new InvalidOperationException("重排序调用失败");
    }

    private static Dictionary<String, Object> BuildRerankParameters(RerankRequest request)
    {
        var p = new Dictionary<String, Object> { ["return_documents"] = request.ReturnDocuments };
        if (request.TopN != null) p["top_n"] = request.TopN.Value;
        return p;
    }

    private static RerankResponse ParseRerankResponse(String json)
    {
        var dic = JsonParser.Decode(json);
        if (dic == null) throw new InvalidOperationException("无法解析重排序响应");

        var resp = new RerankResponse { RequestId = dic["request_id"] as String };

        if (dic["output"] is IDictionary<String, Object> output &&
            output["results"] is IList<Object> resultList)
        {
            var results = new List<RerankResult>(resultList.Count);
            foreach (var item in resultList)
            {
                if (item is not IDictionary<String, Object> r) continue;
                var result = new RerankResult
                {
                    Index = r["index"].ToInt(),
                    RelevanceScore = r["relevance_score"].ToDouble(),
                };
                var docVal = r["document"];
                if (docVal != null)
                    result.Document = docVal is IDictionary<String, Object> docDic
                        ? docDic["text"] as String
                        : docVal as String;
                results.Add(result);
            }
            resp.Results = results;
        }

        if (dic["usage"] is IDictionary<String, Object> usage)
            resp.Usage = new RerankUsage { TotalTokens = usage["total_tokens"].ToInt() };

        return resp;
    }
    #endregion

    #region 文生视频
    /// <summary>提交视频生成任务。使用 DashScope 原生异步任务接口</summary>
    /// <param name="request">视频生成请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务提交响应，含 TaskId</returns>
    public override async Task<VideoTaskSubmitResponse> SubmitVideoGenerationAsync(VideoGenerationRequest request, CancellationToken cancellationToken = default)
    {
        var url = GetNativeBaseUrl() + VideoSynthesisPath;

        var body = new Dictionary<String, Object?>
        {
            ["model"] = request.Model ?? _options.Model,
            ["input"] = BuildVideoInput(request),
            ["parameters"] = BuildVideoParameters(request),
        };

        var json = await PostAsync(url, body, null, _options, cancellationToken).ConfigureAwait(false);
        return ParseDashScopeVideoSubmitResponse(json);
    }

    /// <summary>查询视频生成任务状态。使用 DashScope 的 /tasks/{task_id} 接口</summary>
    /// <param name="taskId">任务编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务状态响应</returns>
    public override async Task<VideoTaskStatusResponse> GetVideoTaskAsync(String taskId, CancellationToken cancellationToken = default)
    {
        var url = GetNativeBaseUrl() + $"/tasks/{taskId}";

        var json = await GetAsync(url, null, _options, cancellationToken).ConfigureAwait(false);
        return ParseDashScopeVideoStatusResponse(json);
    }

    /// <summary>构建 DashScope 视频生成 input 字段</summary>
    private static Dictionary<String, Object?> BuildVideoInput(VideoGenerationRequest request)
    {
        var input = new Dictionary<String, Object?> { ["prompt"] = request.Prompt };
        if (!String.IsNullOrEmpty(request.ImageUrl))
        {
            // wan2.7-i2v 新版协议优先使用 media 数组；旧版 i2v 继续兼容 img_url
            if (IsWan27I2vModel(request.Model))
            {
                input["media"] = new[]
                {
                    new Dictionary<String, Object?>
                    {
                        ["type"] = "first_frame",
                        ["url"] = request.ImageUrl,
                    },
                };
            }
            else
            {
                input["img_url"] = request.ImageUrl;
            }
        }

        if (!String.IsNullOrEmpty(request.NegativePrompt))
            input["negative_prompt"] = request.NegativePrompt;
        return input;
    }

    /// <summary>是否为万相 2.7 图生视频模型</summary>
    private static Boolean IsWan27I2vModel(String? model)
    {
        if (model.IsNullOrEmpty()) return false;
        return model.StartsWith("wan2.7-i2v", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>构建 DashScope 视频生成 parameters 字段</summary>
    private static Dictionary<String, Object?>? BuildVideoParameters(VideoGenerationRequest request)
    {
        var param = new Dictionary<String, Object?>();
        if (!String.IsNullOrEmpty(request.Size))
            param["size"] = request.Size;
        if (request.Duration > 0)
            param["duration"] = request.Duration;
        if (request.Fps > 0)
            param["fps"] = request.Fps;
        if (request.Seed.HasValue)
            param["seed"] = request.Seed.Value;
        return param.Count > 0 ? param : null;
    }

    /// <summary>解析 DashScope 视频任务提交响应</summary>
    private VideoTaskSubmitResponse ParseDashScopeVideoSubmitResponse(String json)
    {
        var dic = JsonParser.Decode(json);
        if (dic == null) return new VideoTaskSubmitResponse();

        var output = dic["output"] as IDictionary<String, Object>;
        return new VideoTaskSubmitResponse
        {
            TaskId = output?["task_id"] as String,
            RequestId = dic["request_id"] as String,
            Status = output?["task_status"] as String,
        };
    }

    /// <summary>解析 DashScope 视频任务状态响应</summary>
    private VideoTaskStatusResponse ParseDashScopeVideoStatusResponse(String json)
    {
        var dic = JsonParser.Decode(json);
        if (dic == null) return new VideoTaskStatusResponse();

        var output = dic["output"] as IDictionary<String, Object>;
        var resp = new VideoTaskStatusResponse
        {
            TaskId = output?["task_id"] as String,
            RequestId = dic["request_id"] as String,
            Status = output?["task_status"] as String,
        };

        // 视频URL在 output.video_url 或 output.results[].url
        if (output?["video_url"] is String videoUrl)
        {
            resp.VideoUrls = [videoUrl];
        }
        else if (output?["results"] is IList<Object> results)
        {
            resp.VideoUrls = results
                .OfType<IDictionary<String, Object>>()
                .Select(r => r["url"] as String ?? "")
                .Where(u => u.Length > 0)
                .ToArray();
        }

        resp.ErrorCode = output?["code"] as String ?? dic["code"] as String;
        resp.ErrorMessage = output?["message"] as String ?? dic["message"] as String;

        return resp;
    }
    #endregion

    #region 语音合成（TTS）
    /// <summary>语音合成（TTS）。根据模型前缀自动路由：CosyVoice 系列走 SpeechSynthesizer 端点；Qwen-TTS 系列走 multimodal-generation 端点</summary>
    /// <remarks>
    /// CosyVoice 端点：POST /api/v1/services/audio/tts/SpeechSynthesizer<br/>
    /// 请求格式：{"model":"...","input":{"text":"...","voice":"...","format":"wav","sample_rate":24000}}<br/>
    /// Qwen-TTS 端点：POST /api/v1/services/aigc/multimodal-generation/generation<br/>
    /// 请求格式：{"model":"...","input":{"text":"...","voice":"..."},"parameters":{}}<br/>
    /// 两者响应相同：JSON → output.audio.url → 下载音频字节流<br/>
    /// CosyVoice 文档：https://help.aliyun.com/zh/model-studio/cosyvoice-tts-http-api<br/>
    /// Qwen-TTS 文档：https://help.aliyun.com/zh/model-studio/qwen-tts-api
    /// </remarks>
    /// <param name="request">语音合成请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>音频字节流</returns>
    public override async Task<Byte[]> SpeechAsync(SpeechRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (String.IsNullOrWhiteSpace(request.Input)) throw new ArgumentException("合成文本不能为空", nameof(request));

        var format = request.ResponseFormat switch
        {
            "mp3" => "mp3",
            "opus" => "opus",
            "pcm" => "pcm",
            "wav" or null => "wav",
            var f => f,
        };

        var modelCode = request.Model ?? _options.Model ?? "cosyvoice-v3-flash";

        String voice;
        Dictionary<String, Object> input;
        String ttsUrl;
        Dictionary<String, Object?> body;

        if (IsQwenTtsModel(modelCode))
        {
            // Qwen-TTS 系列：走 multimodal-generation 端点，默认音色 Cherry
            ttsUrl = GetNativeBaseUrl() + "/services/aigc/multimodal-generation/generation";
            voice = request.Voice;
            if (voice.IsNullOrEmpty() || voice.EqualIgnoreCase("alloy", "echo", "fable", "nova", "onyx", "shimmer"))
            {
                if (QwenTtsVoiceList.GetVoices(modelCode).Count > 0)
                    voice = "Cherry";
            }
            if (!QwenTtsVoiceList.IsValidVoice(modelCode, voice))
                throw new ArgumentException($"音色 '{request.Voice}' 不在模型 '{modelCode}' 的合法音色列表中");
            input = BuildQwenTtsInput(request, voice, format, request.SampleRate ?? 24000);
            body = new Dictionary<String, Object?>
            {
                ["model"] = modelCode,
                ["input"] = input,
                ["parameters"] = new Dictionary<String, Object>(),  // Qwen-TTS 必须携带空 parameters
            };
        }
        else
        {
            // CosyVoice 系列：走 SpeechSynthesizer 端点，默认音色 longxiaochun_v3
            ttsUrl = GetNativeBaseUrl() + "/services/audio/tts/SpeechSynthesizer";
            voice = request.Voice;
            if (voice.IsNullOrEmpty() || voice.EqualIgnoreCase("alloy", "echo", "fable", "nova", "onyx", "shimmer"))
            {
                if (CosyVoiceVoiceList.GetVoices(modelCode).Count > 0)
                    voice = "longxiaochun_v3";
            }
            if (!CosyVoiceVoiceList.IsValidVoice(modelCode, voice))
                throw new ArgumentException($"音色 '{request.Voice}' 不在模型 '{modelCode}' 的合法音色列表中。可用音色请参见 GET /api/audio/voices");
            input = BuildCosyVoiceTtsInput(request, voice, format, request.SampleRate ?? 24000);
            body = new Dictionary<String, Object?>
            {
                ["model"] = modelCode,
                ["input"] = input,
            };
        }

        using var span = Tracer?.NewSpan("ai:DashScopeTts", new { model = modelCode, format, voice = request.Voice });
        try
        {
            var json = await PostAsync(ttsUrl, body, null, _options, cancellationToken).ConfigureAwait(false);

            var dic = JsonParser.Decode(json);
            if (dic == null)
                throw new InvalidOperationException("无法解析 DashScope TTS 响应");

            var code = dic["code"] as String;
            if (!String.IsNullOrEmpty(code))
            {
                var message = dic["message"] as String ?? code;
                throw new HttpRequestException($"[DashScope] TTS 错误 {code}: {message}");
            }

            var output = dic["output"] as IDictionary<String, Object>
                ?? throw new InvalidOperationException("DashScope TTS 响应缺少 output 字段");
            var audio = output["audio"] as IDictionary<String, Object>
                ?? throw new InvalidOperationException("DashScope TTS 响应缺少 output.audio 字段");
            var audioUrl = audio["url"] as String;
            if (String.IsNullOrWhiteSpace(audioUrl))
                throw new InvalidOperationException("DashScope TTS 响应缺少 output.audio.url");

            // 解析用量：Qwen-TTS 用 total_tokens，CosyVoice 用 characters，两者兼容
            if (dic["usage"] is IDictionary<String, Object> usage)
            {
                var chars = 0;
                if (chars == 0 && usage.TryGetValue("total_tokens", out var tt)) chars = tt.ToInt();
                if (chars == 0 && usage.TryGetValue("characters", out var ch)) chars = ch.ToInt();
                if (chars == 0 && usage.TryGetValue("input_characters", out var ic)) chars = ic.ToInt();
                request.CharactersUsed = chars;
            }

            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
#if NET5_0_OR_GREATER
            var audioBytes = await httpClient.GetByteArrayAsync(audioUrl, cancellationToken).ConfigureAwait(false);
#else
            var audioBytes = await httpClient.GetByteArrayAsync(audioUrl).ConfigureAwait(false);
#endif
            return audioBytes;
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            throw;
        }
    }

    /// <summary>判断是否为 Qwen-TTS 非实时 HTTP 合成模型（qwen-tts* / qwen3-tts*，不含 -realtime）</summary>
    private static Boolean IsQwenTtsModel(String modelId) =>
        !modelId.IsNullOrEmpty()
        && modelId.StartsWithIgnoreCase("qwen-tts", "qwen3-tts")
        && !modelId.Contains("-realtime", StringComparison.OrdinalIgnoreCase);

    /// <summary>判断是否为 Qwen-TTS-Realtime WebSocket 实时合成模型（含 -realtime 后缀）</summary>
    private static Boolean IsQwenTtsRealtimeModel(String modelId) =>
        !modelId.IsNullOrEmpty()
        && modelId.StartsWithIgnoreCase("qwen-tts", "qwen3-tts")
        && modelId.Contains("-realtime", StringComparison.OrdinalIgnoreCase);

    /// <summary>判断指定模型是否支持流式语音合成</summary>
    /// <remarks>
    /// 支持流式合成的模型类型：
    /// <list type="bullet">
    /// <item>CosyVoice 全系列（cosyvoice-*：v2/v3/v3.5）：通过 run-task WebSocket 实现</item>
    /// <item>Qwen-TTS-Realtime 系列（*-realtime）：通过 session.* WebSocket 实现</item>
    /// </list>
    /// 以上两类均需要所在提供商配置了 Organization（业务空间 ID）才能构建 WebSocket 端点。
    /// </remarks>
    /// <param name="modelId">模型编码，null 时取客户端默认模型</param>
    /// <returns>支持流式合成返回 true</returns>
    public override Boolean SupportsSpeechStreaming(String? modelId)
    {
        var id = modelId ?? _options.Model;
        if (id.IsNullOrEmpty()) return false;

        // CosyVoice 全系列和 Qwen-TTS-Realtime 系列支持 WebSocket 流式合成
        if (!id.StartsWithIgnoreCase("cosyvoice")
            && !id.EndsWith("-realtime", StringComparison.OrdinalIgnoreCase))
            return false;

        // WebSocket 端点需要 Organization（业务空间 ID）
        return !_options.Organization.IsNullOrEmpty();
    }

    /// <summary>构建 CosyVoice TTS HTTP API 的 input 参数字典</summary>
    /// <param name="request">语音合成请求</param>
    /// <param name="voice">已解析的音色</param>
    /// <param name="format">音频格式</param>
    /// <param name="sampleRate">采样率</param>
    /// <returns>input 字典</returns>
    private static Dictionary<String, Object> BuildCosyVoiceTtsInput(SpeechRequest request, String voice, String format, Int32 sampleRate)
    {
        var input = new Dictionary<String, Object>
        {
            ["text"] = request.Input,
            ["voice"] = voice,
            ["format"] = format,
            ["sample_rate"] = sampleRate,
        };

        // 语速倍率。CosyVoice HTTP API 参数名为 rate，默认 1.0（正常语速）
        if (request.Speed is > 0 and not 1.0)
            input["rate"] = request.Speed;

        // 音量。CosyVoice HTTP API 参数名为 volume，默认 50
        if (request.Volume is > 0 and not 50)
            input["volume"] = request.Volume;

        // 音调。CosyVoice HTTP API 参数名为 pitch，默认 1.0
        if (request.Pitch is > 0 and not 1.0)
            input["pitch"] = request.Pitch;

        return input;
    }

    /// <summary>构建 Qwen-TTS HTTP API 的 input 参数字典</summary>
    /// <remarks>
    /// Qwen-TTS 与 CosyVoice 参数不同：Qwen-TTS 仅支持 text / voice / language_type，不支持 format / sample_rate。
    /// 携带 CosyVoice 专属参数会导致 DashScope 路由层误判模型类型，返回 url error。
    /// </remarks>
    /// <param name="request">语音合成请求</param>
    /// <param name="voice">已解析的音色</param>
    /// <param name="format">音频格式（Qwen-TTS 不支持，忽略）</param>
    /// <param name="sampleRate">采样率（Qwen-TTS 不支持，忽略）</param>
    /// <returns>input 字典</returns>
    private static Dictionary<String, Object> BuildQwenTtsInput(SpeechRequest request, String voice, String format, Int32 sampleRate)
    {
        var input = new Dictionary<String, Object>
        {
            ["text"] = request.Input,
            ["voice"] = voice,
        };

        // Qwen-TTS 不支持 format/sample_rate 参数，仅 CosyVoice 支持
        // 这些参数会误导 DashScope 路由层将请求识别为 CosyVoice 类型，导致 url error

        // 语速倍率
        if (request.Speed is > 0 and not 1.0)
            input["rate"] = request.Speed;

        // Qwen-TTS 特有参数：通过 request.Items 字典传入
        var languageType = request["language_type"] as String;
        if (!languageType.IsNullOrEmpty())
            input["language_type"] = languageType!;

        // Qwen3-TTS-Instruct-Flash 指令控制（通过指令自然语言控制语音表现力）
        var instructions = request["instructions"] as String;
        if (!instructions.IsNullOrEmpty())
            input["instructions"] = instructions!;

        return input;
    }

    /// <summary>流式语音合成。根据模型自动路由：CosyVoice 使用 run-task/continue-task WebSocket 协议；Qwen-TTS-Realtime 使用 session.*/input_text_buffer.* 实时协议</summary>
    /// <remarks>
    /// WebSocket 端点：wss://dashscope.aliyuncs.com/api-ws/v1/inference<br/>
    /// 流程：握手 → run-task → task-started → continue-task（分片发文本）→ result-generated + binary frame（音频）→ finish-task → task-finished<br/>
    /// 文本按 ≤500 字符分片发送。详见 Doc/《CosyVoice WebSocket 流式合成.md》。
    /// </remarks>
    /// <param name="request">语音合成请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>逐段返回音频字节分片</returns>
    public override async IAsyncEnumerable<Byte[]> SpeechStreamAsync(SpeechRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (String.IsNullOrWhiteSpace(request.Input)) throw new ArgumentException("合成文本不能为空", nameof(request));

        var format = request.ResponseFormat ?? "mp3";
        format = format switch { "mp3" => "mp3", "wav" => "wav", "opus" => "opus", "pcm" => "pcm", _ => format };

        var modelCode = request.Model ?? _options.Model ?? "cosyvoice-v3-flash";
        var audioChunks = new List<Byte[]>();

        if (IsQwenTtsRealtimeModel(modelCode))
        {
            // Qwen-TTS-Realtime：session.*/input_text_buffer.* 协议，音频在 response.audio.delta JSON 事件内（base64）
            var voice = request.Voice;
            if (voice.IsNullOrEmpty() || voice.EqualIgnoreCase("alloy", "echo", "fable", "nova", "onyx", "shimmer"))
            {
                if (QwenTtsVoiceList.GetVoices(modelCode).Count > 0)
                    voice = "Cherry";
            }
            if (!QwenTtsVoiceList.IsValidVoice(modelCode, voice))
                throw new ArgumentException($"音色 '{request.Voice}' 不在模型 '{modelCode}' 的合法音色列表中");

            using var ws = new ClientWebSocket();
            if (!_options.ApiKey.IsNullOrEmpty())
                ws.Options.SetRequestHeader("Authorization", $"Bearer {_options.ApiKey}");
            ws.Options.SetRequestHeader("user-agent", "NewLife.AI");

            using var span = Tracer?.NewSpan("ai:QwenTtsRealtimeStream", new { model = modelCode, format, voice, textLength = request.Input.Length });
            try
            {
                await RunQwenTtsRealtimeWebSocketAsync(ws, modelCode, voice, format, request, audioChunks, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                span?.SetError(ex, null);
                throw;
            }
            finally
            {
                if (ws.State == WebSocketState.Open)
                {
                    try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None).ConfigureAwait(false); } catch { }
                }
            }
        }
        else
        {
            // CosyVoice：run-task/continue-task 协议，音频以独立 binary frame 传输
            var voice = request.Voice;
            if (voice.IsNullOrEmpty() || voice.EqualIgnoreCase("alloy", "echo", "fable", "nova", "onyx", "shimmer"))
            {
                if (CosyVoiceVoiceList.GetVoices(modelCode).Count > 0)
                    voice = "longxiaochun_v3";
            }
            if (!CosyVoiceVoiceList.IsValidVoice(modelCode, voice))
                throw new ArgumentException($"音色 '{request.Voice}' 不在模型 '{modelCode}' 的合法音色列表中");

            var sampleRate = request.SampleRate ?? 24000;
            var rate = request.Speed ?? 1.0;

            using var ws = new ClientWebSocket();
            if (!_options.ApiKey.IsNullOrEmpty())
                ws.Options.SetRequestHeader("Authorization", $"Bearer {_options.ApiKey}");
            ws.Options.SetRequestHeader("user-agent", "NewLife.AI");

            var taskId = Guid.NewGuid().ToString("D");
            using var span = Tracer?.NewSpan("ai:DashScopeTtsStream", new { model = modelCode, format, voice, textLength = request.Input.Length });
            try
            {
                await RunWebSocketTtsAsync(ws, taskId, modelCode, voice, format, sampleRate, rate, request, audioChunks, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                span?.SetError(ex, null);
                throw;
            }
            finally
            {
                if (ws.State == WebSocketState.Open)
                {
                    try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None).ConfigureAwait(false); } catch { }
                }
            }
        }

        foreach (var chunk in audioChunks)
            yield return chunk;
    }

    #region WebSocket 辅助方法

    /// <summary>构建 CosyVoice WebSocket 地址。仅支持华北2（北京）MaaS 业务空间端点</summary>
    /// <remarks>
    /// 官方文档：CosyVoice WebSocket 实时语音合成仅在北京地域可用，必须使用 MaaS 业务空间端点：
    /// wss://{WorkspaceId}.cn-beijing.maas.aliyuncs.com/api-ws/v1/inference
    /// 标准端点（dashscope.aliyuncs.com）不支持 CosyVoice WebSocket。
    /// </remarks>
    private String BuildWebSocketUrl()
    {
        // CosyVoice WebSocket 仅限北京 MaaS 端点，必须提供 Organization（工作空间 ID）
        if (_options.Organization.IsNullOrEmpty())
            throw new InvalidOperationException("CosyVoice WebSocket 实时语音合成需北京地域 MaaS 业务空间。请在 AiClientOptions.Organization 中设置工作空间 ID（阿里云百炼控制台 → 业务空间 → 复制空间ID）");

        return $"wss://{_options.Organization}.cn-beijing.maas.aliyuncs.com/api-ws/v1/inference";
    }

    /// <summary>构建 Qwen-TTS-Realtime WebSocket 连接地址。模型通过 URL 查询参数指定</summary>
    /// <remarks>
    /// 官方文档：wss://{WorkspaceId}.cn-beijing.maas.aliyuncs.com/api-ws/v1/realtime?model={modelCode}<br/>
    /// 与 CosyVoice 不同，模型通过 URL query string 传递，不在消息体内。
    /// </remarks>
    private String BuildQwenTtsRealtimeWebSocketUrl(String modelCode)
    {
        if (_options.Organization.IsNullOrEmpty())
            throw new InvalidOperationException("Qwen-TTS Realtime WebSocket 实时语音合成需北京地域 MaaS 业务空间。请在 AiClientOptions.Organization 中设置工作空间 ID");

        return $"wss://{_options.Organization}.cn-beijing.maas.aliyuncs.com/api-ws/v1/realtime?model={Uri.EscapeDataString(modelCode)}";
    }

    /// <summary>执行 Qwen-TTS-Realtime WebSocket 全流程</summary>
    /// <remarks>
    /// 流程：连接 → session.created → session.update → session.updated → input_text_buffer.append（分片）
    /// → input_text_buffer.commit → input_text_buffer.committed → response.created → response.audio.delta（base64 音频）
    /// → response.done → session.finish → session.finished
    /// </remarks>
    private async Task RunQwenTtsRealtimeWebSocketAsync(ClientWebSocket ws, String modelCode, String voice, String format, SpeechRequest request, List<Byte[]> audioChunks, CancellationToken cancellationToken)
    {
        var wsUrl = BuildQwenTtsRealtimeWebSocketUrl(modelCode);
        await ws.ConnectAsync(new Uri(wsUrl), cancellationToken).ConfigureAwait(false);

        // 1. 等待 session.created
        var sessionCreated = await ReceiveWebSocketJsonAsync(ws, cancellationToken).ConfigureAwait(false);
        if (GetEventType(sessionCreated) != "session.created")
            throw new InvalidOperationException($"Qwen-TTS Realtime 期望 session.created，实际收到 {GetEventType(sessionCreated) ?? "(null/Close)"}");

        // 2. 发送 session.update 配置音色/格式/模式
        var sampleRate = request.SampleRate ?? 24000;
        var mode = request["mode"] as String ?? "commit";
        var languageType = request["language_type"] as String;
        var instructions = request["instructions"] as String;
        var sessionUpdateId = Guid.NewGuid().ToString("N")[..20];

        var sessionConfig = new Dictionary<String, Object>
        {
            ["voice"] = voice,
            ["mode"] = mode,
            ["response_format"] = format,
            ["sample_rate"] = sampleRate,
        };
        if (!languageType.IsNullOrEmpty()) sessionConfig["language_type"] = languageType!;
        if (!instructions.IsNullOrEmpty()) sessionConfig["instructions"] = instructions!;

        await SendRealtimeEventAsync(ws, new
        {
            event_id = sessionUpdateId,
            type = "session.update",
            session = sessionConfig,
        }, cancellationToken).ConfigureAwait(false);

        // 3. 等待 session.updated
        var sessionUpdated = await ReceiveWebSocketJsonAsync(ws, cancellationToken).ConfigureAwait(false);
        if (GetEventType(sessionUpdated) != "session.updated")
            throw new InvalidOperationException($"Qwen-TTS Realtime 期望 session.updated，实际收到 {GetEventType(sessionUpdated) ?? "(null/Close)"}");

        // 4. 分批发送文本到缓冲区（每片 ≤500 字符）
        var text = request.Input;
        var maxChunkLen = 500;
        var offset = 0;
        while (offset < text.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var chunkLen = Math.Min(maxChunkLen, text.Length - offset);
            if (offset + chunkLen < text.Length && Char.IsHighSurrogate(text[offset + chunkLen - 1]))
                chunkLen--;
            var chunk = text.Substring(offset, chunkLen);
            offset += chunkLen;

            await SendRealtimeEventAsync(ws, new
            {
                event_id = Guid.NewGuid().ToString("N")[..20],
                type = "input_text_buffer.append",
                text = chunk,
            }, cancellationToken).ConfigureAwait(false);
        }

        // 5. 提交文本缓冲区触发合成（commit 模式必须显式提交）
        await SendRealtimeEventAsync(ws, new
        {
            event_id = Guid.NewGuid().ToString("N")[..20],
            type = "input_text_buffer.commit",
        }, cancellationToken).ConfigureAwait(false);

        // 6. 接收音频事件流，直到 response.done
        while (ws.State == WebSocketState.Open)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var ev = await ReceiveWebSocketJsonAsync(ws, cancellationToken).ConfigureAwait(false);
            if (ev == null) break;

            var evType = GetEventType(ev);
            switch (evType)
            {
                case "response.audio.delta":
                    // 音频数据在 JSON 文本帧内以 base64 编码（不同于 CosyVoice 的 binary frame）
                    if (ev.TryGetValue("delta", out var deltaVal) && deltaVal is String base64Audio && base64Audio.Length > 0)
                        audioChunks.Add(Convert.FromBase64String(base64Audio));
                    break;

                case "response.done":
                    // 提取用量（usage.characters 或 usage.total_tokens）
                    if (ev.TryGetValue("response", out var respObj) && respObj is IDictionary<String, Object?> resp
                        && resp.TryGetValue("usage", out var usageObj) && usageObj is IDictionary<String, Object?> usageDic)
                    {
                        var chars = 0;
                        if (chars == 0 && usageDic.TryGetValue("characters", out var uc)) chars = uc.ToInt();
                        if (chars == 0 && usageDic.TryGetValue("total_tokens", out var ut)) chars = ut.ToInt();
                        request.CharactersUsed = chars;
                    }
                    break;

                case "session.finished":
                    return;

                case "error":
                    var errMsg = "未知错误";
                    if (ev.TryGetValue("error", out var errObj) && errObj is IDictionary<String, Object?> errDic
                        && errDic.TryGetValue("message", out var em))
                        errMsg = em as String ?? errMsg;
                    throw new InvalidOperationException($"Qwen-TTS Realtime 错误: {errMsg}");

                default:
                    // input_text_buffer.committed / response.created / response.output_item.added / response.content_part.*  等中间事件，忽略继续
                    break;
            }

            // response.done 收到后发 session.finish，然后等 session.finished
            if (evType == "response.done")
            {
                await SendRealtimeEventAsync(ws, new
                {
                    event_id = Guid.NewGuid().ToString("N")[..20],
                    type = "session.finish",
                }, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>发送 Qwen-TTS Realtime JSON 事件帧</summary>
    private async Task SendRealtimeEventAsync(ClientWebSocket ws, Object eventObj, CancellationToken cancellationToken)
    {
        var json = JsonHost.Write(eventObj, JsonOptions) ?? "{}";
        var bytes = Encoding.UTF8.GetBytes(json);
        await ws.SendAsync(new ArraySegment<Byte>(bytes), WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>从事件字典中提取 type 字段</summary>
    private static String? GetEventType(IDictionary<String, Object?>? dic)
    {
        if (dic == null) return null;
        return dic.TryGetValue("type", out var t) ? t as String : null;
    }

    /// <summary>执行 CosyVoice WebSocket TTS 全流程，将音频分片收集到 audioChunks</summary>
    private async Task RunWebSocketTtsAsync(ClientWebSocket ws, String taskId, String modelCode, String voice, String format, Int32 sampleRate, Double rate, SpeechRequest request, List<Byte[]> audioChunks, CancellationToken cancellationToken)
    {
        var wsUrl = BuildWebSocketUrl();
        await ws.ConnectAsync(new Uri(wsUrl), cancellationToken).ConfigureAwait(false);

        // 发送 run-task
        await SendWebSocketJsonAsync(ws, new
        {
            header = new { task_id = taskId, action = "run-task", streaming = "duplex" },
            payload = new
            {
                model = modelCode,
                task_group = "audio",
                task = "tts",
                function = "SpeechSynthesizer",
                parameters = new
                {
                    text_type = "PlainText",
                    voice,
                    format,
                    sample_rate = sampleRate,
                    rate,
                    volume = request.Volume ?? 50,
                    pitch = request.Pitch ?? 1.0,
                    enable_ssml = false,
                },
                input = new Dictionary<String, Object>(),
            },
        }, cancellationToken).ConfigureAwait(false);

        // 等待 task-started
        var started = await ReceiveWebSocketJsonAsync(ws, cancellationToken).ConfigureAwait(false);
        if (started == null || GetHeaderAction(started) != "task-started")
        {
            var reason = _lastCloseReason.IsNullOrEmpty() ? "" : $"，连接关闭原因: {_lastCloseReason}";
            var extra = modelCode.StartsWith("cosyvoice-v3.5", StringComparison.OrdinalIgnoreCase)
                ? "。v3.5 模型仅限北京地域 MaaS 业务空间（需设置 Organization），且仅支持声音复刻（自定义音色）"
                : "";
            throw new InvalidOperationException($"CosyVoice WebSocket 期望 task-started，实际收到 {GetHeaderAction(started) ?? "(Close/非文本帧)"}{reason}{extra}");
        }

        // 文本分片发送（每片 ≤500 UTF-8 字节，保守取 ≤166 字符）
        var text = request.Input;
        var maxChunkSize = 500;
        var offset = 0;
        while (offset < text.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var chunkLen = Math.Min(maxChunkSize / 3, text.Length - offset);
            if (offset + chunkLen < text.Length && Char.IsHighSurrogate(text[offset + chunkLen - 1]))
                chunkLen--;
            var chunk = text.Substring(offset, chunkLen);
            offset += chunkLen;

            await SendWebSocketJsonAsync(ws, new
            {
                header = new { task_id = taskId, action = "continue-task", streaming = "duplex" },
                payload = new
                {
                    input = new { text = chunk },
                },
            }, cancellationToken).ConfigureAwait(false);
        }

        // 发送 finish-task
        await SendWebSocketJsonAsync(ws, new
        {
            header = new { task_id = taskId, action = "finish-task", streaming = "duplex" },
            payload = new { input = new Dictionary<String, Object>() },
        }, cancellationToken).ConfigureAwait(false);

        // 循环接收 result-generated + binary / task-finished
        while (ws.State == WebSocketState.Open)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var json = await ReceiveWebSocketJsonAsync(ws, cancellationToken).ConfigureAwait(false);
            if (json == null) break;

            var action = GetHeaderAction(json);
            switch (action)
            {
                case "result-generated":
                    var audioBytes = await ReceiveWebSocketBinaryAsync(ws, cancellationToken).ConfigureAwait(false);
                    if (audioBytes != null && audioBytes.Length > 0)
                        audioChunks.Add(audioBytes);
                    break;

                case "task-finished":
                    if (json.TryGetValue("payload", out var fp) && fp is IDictionary<String, Object> fpDic
                        && fpDic.TryGetValue("usage", out var u) && u is IDictionary<String, Object> uDic
                        && uDic.TryGetValue("characters", out var chars))
                        request.CharactersUsed = chars.ToInt();
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "task-finished", cancellationToken).ConfigureAwait(false);
                    return;

                case "task-failed":
                    var errMsg = "未知错误";
                    if (json.TryGetValue("payload", out var ep) && ep is IDictionary<String, Object> epDic
                        && epDic.TryGetValue("message", out var em))
                        errMsg = em as String ?? errMsg;
                    throw new InvalidOperationException($"CosyVoice WebSocket 任务失败: {errMsg}");

                default:
                    break;
            }
        }
    }

    /// <summary>发送 JSON 文本帧</summary>
    private async Task SendWebSocketJsonAsync(ClientWebSocket ws, Object body, CancellationToken cancellationToken)
    {
        var json = JsonHost.Write(body, JsonOptions) ?? "{}";
        var bytes = Encoding.UTF8.GetBytes(json);
        await ws.SendAsync(new ArraySegment<Byte>(bytes), WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>接收一条 JSON 文本帧并解析为字典。非文本帧返回 null（Close 帧通过 out 参数传出原因）</summary>
    private async Task<IDictionary<String, Object?>?> ReceiveWebSocketJsonAsync(ClientWebSocket ws, CancellationToken cancellationToken)
    {
        var buffer = new ArraySegment<Byte>(new Byte[65536]);
        using var ms = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await ws.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                _lastCloseReason = result.CloseStatusDescription ?? result.CloseStatus?.ToString() ?? "未知";
                return null;
            }
            if (result.MessageType != WebSocketMessageType.Text)
                return null;
            ms.Write(buffer.Array!, buffer.Offset, result.Count);
        } while (!result.EndOfMessage);

        var json = Encoding.UTF8.GetString(ms.ToArray());
        return JsonParser.Decode(json);
    }

    /// <summary>最近一次 WebSocket Close 帧的原因描述。由 ReceiveWebSocketJsonAsync 在收到 Close 时设置</summary>
    private String _lastCloseReason = "";

    /// <summary>接收一条二进制帧。非二进制帧返回 null</summary>
    private async Task<Byte[]?> ReceiveWebSocketBinaryAsync(ClientWebSocket ws, CancellationToken cancellationToken)
    {
        var buffer = new ArraySegment<Byte>(new Byte[65536]);
        using var ms = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await ws.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
                return null;
            if (result.MessageType != WebSocketMessageType.Binary)
                return null;
            ms.Write(buffer.Array!, buffer.Offset, result.Count);
        } while (!result.EndOfMessage);

        return ms.ToArray();
    }

    /// <summary>从 WebSocket JSON 事件字典中提取 header.action</summary>
    private String? GetHeaderAction(IDictionary<String, Object?>? dic)
    {
        if (dic == null) return null;
        if (dic.TryGetValue("header", out var headerObj) && headerObj is IDictionary<String, Object?> header
            && header.TryGetValue("action", out var action))
            return action as String;
        return null;
    }

    #endregion
    #endregion

    #region 嵌入向量（IEmbeddingClient 实现）
    /// <summary>生成嵌入向量。始终使用兼容模式端点，与对话原生端点隔离</summary>
    /// <remarks>
    /// DashScope 嵌入 API 仅在兼容模式下可用：POST /compatible-mode/v1/embeddings<br/>
    /// 无论全局端点配置为何，嵌入请求均自动路由到兼容模式端点。
    /// </remarks>
    /// <param name="request">嵌入请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>嵌入响应</returns>
    public override async Task<EmbeddingResponse> GenerateAsync(EmbeddingRequest request, CancellationToken cancellationToken = default)
    {
        // Embedding 始终使用兼容模式端点，临时切换 endpoint 后委托基类
        var saved = _options.Endpoint;
        _options.Endpoint = GetCompatibleBaseUrl();
        try
        {
            return await base.GenerateAsync(request, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _options.Endpoint = saved;
        }
    }
    #endregion
}
