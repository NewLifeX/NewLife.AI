using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using NewLife.AI.Clients.OpenAI;
using NewLife.AI.Models;
using NewLife.Collections;
using NewLife.Remoting;
using NewLife.Serialization;

namespace NewLife.AI.Clients.DashScope;

/// <summary>阿里百炼（DashScope）对话客户端。支持 DashScope 原生协议与 OpenAI 兼容协议双模式</summary>
/// <remarks>
/// 通过 <see cref="AiClientOptions.Protocol"/> 控制协议模式：
/// <list type="bullet">
/// <item>"DashScope"（默认/空）：使用阿里云 DashScope 原生协议，走 /api/v1 端点</item>
/// <item>"ChatCompletions"：使用 OpenAI 兼容协议，走 /compatible-mode 端点，复用基类逻辑</item>
/// </list>
/// 官方文档：https://help.aliyun.com/zh/model-studio/qwen-api-via-dashscope
/// </remarks>
/// <remarks>用连接选项初始化 DashScope 客户端</remarks>
[AiClient("DashScope", "阿里百炼", "https://dashscope.aliyuncs.com/api/v1", Protocol = "DashScope", Description = "阿里云百炼大模型平台，支持 Qwen/通义千问全系列商业版模型")]
[AiClientModel("qwen3-max", "Qwen3 Max", Thinking = true)]
[AiClientModel("qwen3.5-plus", "Qwen3.5 Plus", Thinking = true, Vision = true)]
[AiClientModel("qwen3.5-flash", "Qwen3.5 Flash", Thinking = true, Vision = true)]
[AiClientModel("qwq-plus", "QwQ Plus", Thinking = true)]
[AiClientModel("qwen-vl-max", "Qwen VL Max", Vision = true)]
[AiClientModel("qwen-image", "Qwen Image", ImageGeneration = true, FunctionCalling = false)]
[AiClientModel("qwen-image-plus", "Qwen Image Plus", ImageGeneration = true, FunctionCalling = false)]
[AiClientModel("qwen-image-max", "Qwen Image Max", ImageGeneration = true, FunctionCalling = false)]
[AiClientModel("qwen-image-2.0", "Qwen Image 2.0", ImageGeneration = true, FunctionCalling = false)]
[AiClientModel("qwen-image-2.0-pro",   "Qwen Image 2.0 Pro",     ImageGeneration = true, FunctionCalling = false)]
[AiClientModel("qwen-image-edit-max", "Qwen Image Edit Max",    ImageGeneration = true, FunctionCalling = false)]
[AiClientModel("qwen-image-edit-plus","Qwen Image Edit Plus",   ImageGeneration = true, FunctionCalling = false)]
[AiClientModel("qwen-image-edit",     "Qwen Image Edit",        ImageGeneration = true, FunctionCalling = false)]
[AiClientModel("qwen3-coder-next", "Qwen3 Coder")]
[AiClientModel("wan2.6-t2i", "Wan 文生图（万相2.6）", ImageGeneration = true, FunctionCalling = false)]
[AiClientModel("wan2.7-t2v", "Wanx 文生视频", VideoGeneration = true, FunctionCalling = false)]
[AiClientModel("wan2.7-i2v", "Wanx 图生视频", Vision = true, VideoGeneration = true, FunctionCalling = false)]
public class DashScopeChatClient : OpenAIChatClient, IRerankClient
{
    #region 属性
    /// <inheritdoc/>
    public override String Name { get; set; } = "阿里百炼";

    /// <summary>原生 DashScope API 基础地址（/api/v1）</summary>
    protected virtual String NativeEndpoint => "https://dashscope.aliyuncs.com/api/v1";

    /// <summary>兼容模式基础地址。Embedding、重排序等沿用此端点</summary>
    protected virtual String CompatibleEndpoint => "https://dashscope.aliyuncs.com/compatible-mode";

    /// <inheritdoc/>
    public override String DefaultEndpoint
    {
        get => IsNativeProtocol ? NativeEndpoint : CompatibleEndpoint;
        set => base.DefaultEndpoint = value;
    }

    /// <summary>是否使用 DashScope 原生协议。Protocol 为空或 "DashScope" 时为原生模式</summary>
    protected Boolean IsNativeProtocol => _options.Protocol.IsNullOrEmpty() || _options.Protocol == "DashScope";

    /// <summary>默认Json序列化选项</summary>
    public static JsonOptions DashScopeDefaultJsonOptions = new()
    {
        PropertyNaming = PropertyNaming.SnakeCaseLower,
        IgnoreNullValues = true,
    };
    #endregion

    #region 构造
    /// <param name="options">连接选项（Endpoint、ApiKey、Model、Protocol 等）</param>
    public DashScopeChatClient(AiClientOptions options) : base(options) => JsonOptions = DashScopeDefaultJsonOptions;

    /// <summary>以 API 密钥和可选模型快速创建阿里百炼客户端</summary>
    /// <param name="apiKey">阿里云 API Key</param>
    /// <param name="model">默认模型编码，为空时由每次请求指定</param>
    /// <param name="endpoint">API 地址覆盖；为空时使用内置默认地址</param>
    public DashScopeChatClient(String apiKey, String? model = null, String? endpoint = null)
        : this(new AiClientOptions { ApiKey = apiKey, Model = model, Endpoint = endpoint }) { }
    #endregion

    #region 对话（重写）
    /// <summary>非流式对话。原生协议走 DashScope 格式，兼容模式委托基类</summary>
    protected override async Task<IChatResponse> ChatAsync(IChatRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsNativeProtocol)
            return await base.ChatAsync(request, cancellationToken).ConfigureAwait(false);

        var model = request.Model ?? _options.Model;
        var url = BuildUrl(request);
        var body = DashScopeRequest.FromChatRequest(request, IsMultimodalModel(request.Model));
        var json = await PostAsync(url, body, request, _options, cancellationToken).ConfigureAwait(false);
        var dashResp = json.ToJsonEntity<DashScopeResponse>(JsonOptions)!;
        if (!dashResp.Code.IsNullOrEmpty())
            throw new HttpRequestException($"[DashScope] 错误 {dashResp.Code}: {dashResp.Message}");

        // 原生响应无顶层 model 字段，从请求回填
        dashResp.Model = model;
        if (dashResp is IChatResponse rs && rs.Object.IsNullOrEmpty()) rs.Object = "chat.completion";

        return dashResp;
    }

    /// <summary>流式对话。原生协议走 DashScope SSE 格式，兼容模式委托基类</summary>
    protected override async IAsyncEnumerable<IChatResponse> ChatStreamAsync(IChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!IsNativeProtocol)
        {
            await foreach (var chunk in base.ChatStreamAsync(request, cancellationToken).ConfigureAwait(false))
                yield return chunk;
            yield break;
        }

        var url = BuildUrl(request);
        var body = DashScopeRequest.FromChatRequest(request, IsMultimodalModel(request.Model));

        using var httpResponse = await PostStreamAsync(url, body, request, _options, cancellationToken).ConfigureAwait(false);
        using var stream = await httpResponse.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        var lastEvent = "";
        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync().ConfigureAwait(false);
            if (line == null) break;

            if (line.StartsWith("id:")) continue;

            if (line.StartsWith("event:"))
            {
                lastEvent = line.Substring(6).Trim();
                continue;
            }

            if (!line.StartsWith("data:")) continue;

            var data = line.Substring(5).Trim();
            if (data.Length == 0) continue;

            if (lastEvent == "error")
            {
                var errDic = JsonParser.Decode(data);
                var code = errDic?["code"] as String ?? "error";
                var message = errDic?["message"] as String ?? data;
                throw new HttpRequestException($"[{Name}] 流式错误 {code}: {message}");
            }

            IChatResponse? chunk = null;
            try { chunk = ParseChunk(data, request, null); } catch { }

            if (chunk != null)
            {
                chunk.Model ??= request.Model;
                yield return chunk;
            }
        }
    }
    #endregion

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

    #region 模型列表
    /// <summary>获取可用模型列表。使用兼容模式端点以保证返回完整模型目录</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>模型列表，服务不可用时返回 null</returns>
    public override async Task<ModelListResponse?> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        var url = CombineApiUrl(CompatibleEndpoint, "/v1/models");
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

    #region 辅助
    // 原生对话路径（纯文本）
    private const String ChatGenerationPath = "/services/aigc/text-generation/generation";

    // 原生对话路径（多模态：含视觉/音频/视频输入）
    private const String MultimodalGenerationPath = "/services/aigc/multimodal-generation/generation";

    // 原生视频生成路径（Wan2.x 文生视频/图生视频）
    private const String VideoSynthesisPath = "/services/aigc/video-generation/video-synthesis";

    /// <summary>构建请求地址。子类可重写此方法根据请求参数动态调整路径（如不同模型使用不同端点）</summary>
    protected override String BuildUrl(IChatRequest request)
    {
        var path = IsMultimodalModel(request.Model) ? MultimodalGenerationPath : ChatGenerationPath;

        // 原生协议只能对接 /api/v1 端点；若用户配置了兼容模式地址则忽略并回退到原生端点
        var endpoint = _options.Endpoint;
        if (endpoint.IsNullOrWhiteSpace() ||
            endpoint.IndexOf("compatible-mode", StringComparison.OrdinalIgnoreCase) >= 0)
            endpoint = NativeEndpoint;
        return endpoint.TrimEnd('/') + path;
    }

    /// <summary>判断指定模型是否为多模态模型（需走 multimodal-generation 端点）</summary>
    /// <remarks>
    /// 命名规律：
    /// <list type="bullet">
    /// <item>含 -vl：Vision-Language 系列</item>
    /// <item>qvq- 前缀：视觉推理系列（区别于纯文本推理 qwq-）</item>
    /// <item>qwen3.X- 前缀（如 qwen3.5-/qwen3.6-）：内置多模态能力，仅支持 multimodal-generation 端点</item>
    /// </list>
    /// </remarks>
    private static Boolean IsMultimodalModel(String? model)
    {
        if (String.IsNullOrEmpty(model)) return false;
        if (model.IndexOf("-vl", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        if (model.StartsWith("qvq-", StringComparison.OrdinalIgnoreCase)) return true;
        if (model.StartsWithIgnoreCase("qwen3.5-", "qwen3.")) return true;
        // 音频理解模型（qwen-audio-chat、qwen-audio-turbo、qwen2-audio-instruct 等）使用多模态端点
        if (model.StartsWith("qwen-audio", StringComparison.OrdinalIgnoreCase)) return true;
        if (model.StartsWith("qwen2-audio", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    /// <summary>解析 DashScope 原生流式 SSE chunk，DashScopeResponse 适配器同时设置 Delta</summary>
    protected override IChatResponse? ParseChunk(String data, IChatRequest request, String? lastEvent)
    {
        var chunk = data.ToJsonEntity<DashScopeResponse>(JsonOptions);
        chunk?.Model = request.Model;
        if (chunk is IChatResponse rs && rs.Object.IsNullOrEmpty()) rs.Object = "chat.completion.chunk";
        return chunk;
    }

    /// <inheritdoc/>
    /// <remarks>多模态响应中 content 为数组格式（[{"text":"..."}]），归一化为字符串</remarks>
    protected override void OnParseChatMessage(ChatMessage msg, IDictionary<String, Object> dic)
    {
        if (msg.Content is not IList<Object> contentList) return;

        var sb = Pool.StringBuilder.Get();
        foreach (var item in contentList)
        {
            if (item is IDictionary<String, Object> d && d["text"] is String t)
                sb.Append(t);
        }
        msg.Content = sb.Return(true);
    }

    /// <inheritdoc/>
    protected override void SetHeaders(HttpRequestMessage request, IChatRequest? chatRequest, AiClientOptions options)
    {
        if (!String.IsNullOrEmpty(options.ApiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);

        var path = request.RequestUri?.AbsolutePath;
        if (String.IsNullOrEmpty(path)) return;

        // 视频生成接口仅支持异步调用，必须携带该请求头
        if (path.EndsWith(VideoSynthesisPath, StringComparison.OrdinalIgnoreCase))
            request.Headers.TryAddWithoutValidation("X-DashScope-Async", "enable");

        if (!IsNativeProtocol) return;
        if (chatRequest == null || !chatRequest.Stream) return;

        if (!path.EndsWith(ChatGenerationPath, StringComparison.OrdinalIgnoreCase) &&
            !path.EndsWith(MultimodalGenerationPath, StringComparison.OrdinalIgnoreCase)) return;

        // qwen-plus 不能识别为多模态，得使用文本完成地址，但是accept需要text/event-stream
        //var model = chatRequest?.Model ?? options.Model;
        //if (IsMultimodalModel(model) || model.EndsWithIgnoreCase("-max", "-plus", "-turbo"))
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        //else
        request.Headers.TryAddWithoutValidation("X-DashScope-SSE", "enable");
    }

    /// <summary>根据千问模型 ID 命名规律推断模型能力</summary>
    /// <remarks>
    /// 阿里百炼模型命名规律（基于 2026-04 官方文档）：
    /// <list type="bullet">
    /// <item>qwen*-vl* / qvq-*：视觉能力</item>
    /// <item>qwen3.X-*（如 qwen3.5-/qwen3.6-）中 Plus 和开源版：内置多模态（视觉），Flash/Max/Coder 纯文本</item>
    /// <item>qwq-* / qvq-*：专用推理模型，始终具备思考能力</item>
    /// <item>qwen3*（除 coder 和 -instruct 后缀）：qwen3 时代全系列支持思考模式</item>
    /// <item>qwen-max/plus/flash/turbo（稳定版别名）：当前均指向 qwen3 时代，支持思考</item>
    /// <item>qwen-long / qwen2* / qwen1*：不支持思考模式</item>
    /// <item>qwen*-omni*：全模态模型，视觉+音频，使用专用 API</item>
    /// <item>wanx* / wan2* / flux* / qwen-image* / z-image*：文生图/视频生成</item>
    /// <item>embed* / rerank* / paraformer* / cosyvoice* / sambert* 等：非对话模型</item>
    /// <item>farui* / qwen-mt*：专用模型，不支持函数调用</item>
    /// </list>
    /// 注意：-max/-plus 本身不是思考能力的可靠信号，早期 qwen-max（qwen2 时代）不支持思考
    /// </remarks>
    /// <param name="modelId">模型标识</param>
    /// <returns>推断出的能力信息，无法推断时返回 null</returns>
    public override AiProviderCapabilities? InferModelCapabilities(String? modelId)
    {
        if (String.IsNullOrEmpty(modelId)) return null;

        // 非对话模型：嵌入、重排序、语音识别/合成等
        if (modelId.StartsWith("text-embedding", StringComparison.OrdinalIgnoreCase) ||
            modelId.Contains("embed", StringComparison.OrdinalIgnoreCase) ||
            modelId.Contains("rerank", StringComparison.OrdinalIgnoreCase) ||
            modelId.StartsWith("paraformer", StringComparison.OrdinalIgnoreCase) ||
            modelId.StartsWith("cosyvoice", StringComparison.OrdinalIgnoreCase) ||
            modelId.StartsWith("sambert", StringComparison.OrdinalIgnoreCase) ||
            modelId.StartsWith("fun-asr", StringComparison.OrdinalIgnoreCase) ||
            modelId.StartsWith("sensevoice", StringComparison.OrdinalIgnoreCase) ||
            modelId.StartsWith("qwen-audio", StringComparison.OrdinalIgnoreCase) ||
            modelId.StartsWithIgnoreCase("qwen3-asr", "qwen3-tts", "qwen-tts", "qwen-voice"))
            return new AiProviderCapabilities(false, false, false, false);

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
            return new AiProviderCapabilities(false, false, false, false, true, false, 0);

        // 文生视频 / 图生视频：wan2*-t2v* / wan2*-i2v*
        if (modelId.StartsWith("wan2", StringComparison.OrdinalIgnoreCase) &&
            (modelId.Contains("-t2v", StringComparison.OrdinalIgnoreCase) ||
             modelId.Contains("-i2v", StringComparison.OrdinalIgnoreCase)))
            return new AiProviderCapabilities(false, false, false, false, false, true, 0);

        // 文生图：wan2 其他系列（如 wan2*-t2i*）
        if (modelId.StartsWith("wan2", StringComparison.OrdinalIgnoreCase))
            return new AiProviderCapabilities(false, false, false, false, true, false, 0);

        // 全模态模型 omni：视觉+音频输入输出，使用专用 API，不支持标准函数调用
        if (modelId.Contains("-omni", StringComparison.OrdinalIgnoreCase))
            return new AiProviderCapabilities(false, false, true, true, false, false, 32_768);

        // === 视觉能力 ===
        // VL 系列和 QVQ 视觉推理模型
        if (modelId.Contains("-vl", StringComparison.OrdinalIgnoreCase) ||
            modelId.StartsWith("qvq-", StringComparison.OrdinalIgnoreCase))
            vision = true;

        // qwen3.X-*（如 qwen3.5-/qwen3.6-）中 Plus 和开源模型支持多模态（文本+图像+视频输入）
        // Flash/Max/Turbo/Coder 子系列为纯文本，"qwen3." 不匹配 "qwen3-max" 等
        if (modelId.StartsWithIgnoreCase("qwen3.") &&
            !modelId.Contains("-flash", StringComparison.OrdinalIgnoreCase) &&
            !modelId.Contains("-max", StringComparison.OrdinalIgnoreCase) &&
            !modelId.Contains("-turbo", StringComparison.OrdinalIgnoreCase) &&
            !modelId.Contains("-coder", StringComparison.OrdinalIgnoreCase))
            vision = true;

        // === 思考/推理能力 ===
        // 按模型家族精确匹配，-max/-plus 本身不是思考能力的可靠信号
        // 例如早期 qwen-max（qwen2 时代）不支持思考，仅 qwen3 时代才全面支持

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

        // === 函数调用 ===
        // 专用模型不支持函数调用
        if (modelId.StartsWith("farui", StringComparison.OrdinalIgnoreCase) ||
            modelId.StartsWith("qwen-mt", StringComparison.OrdinalIgnoreCase))
            funcCall = false;

        // === 上下文长度 ===
        // qwen-long 专为长文档设计，支持 1M tokens
        if (modelId.StartsWithIgnoreCase("qwen-long"))
            contextLength = 1_000_000;
        // qwen3/qwen3.5 全系列、稳定版别名（qwen-max/plus/flash/turbo）、推理模型（qwq/qvq）、qwen2.5 系列
        else if (modelId.StartsWithIgnoreCase("qwen3", "qwen-max", "qwen-plus", "qwen-flash", "qwen-turbo",
            "qwq-", "qvq-", "qwen2.5"))
            contextLength = 131_072;
        // deepseek 系列
        else if (modelId.StartsWith("deepseek", StringComparison.OrdinalIgnoreCase))
            contextLength = 65_536;
        // 其余对话模型默认 32K（已在变量初始化时设置）

        return new AiProviderCapabilities(thinking, funcCall, vision, audio, imageGen, videoGen, contextLength);
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
