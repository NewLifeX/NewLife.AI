using System.Net.Http.Headers;
using NewLife.AI.Clients.OpenAI;
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

        var url = CombineApiUrl(CompatibleEndpoint, "/v1/images/generations");
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
        var url = NativeEndpoint.TrimEnd('/') + "/services/aigc/multimodal-generation/generation";

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
        var url = NativeEndpoint.TrimEnd('/') + "/services/aigc/multimodal-generation/generation";

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
        if (!String.IsNullOrEmpty(request.Size))
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

        return NormalizeOpenAiImagePath(CompatibleEndpoint);
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
        var nativeBase = NativeEndpoint.TrimEnd('/');
        var compatBase = CompatibleEndpoint.TrimEnd('/');
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
        var endpoint = NativeEndpoint.TrimEnd('/');
        var url = endpoint + VideoSynthesisPath;

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
        var endpoint = NativeEndpoint.TrimEnd('/');
        var url = endpoint + $"/tasks/{taskId}";

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
        if (String.IsNullOrEmpty(model)) return false;
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
}
