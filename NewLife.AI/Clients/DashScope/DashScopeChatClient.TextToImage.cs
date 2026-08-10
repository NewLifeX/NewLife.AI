using System.Net.Http.Headers;
using NewLife.AI.Clients.OpenAI;
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
            // 统一分隔符：1024*1024 → 1024*1024（DashScope 原生接口要求 * 分隔）
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
}
