using System.Runtime.CompilerServices;
using System.Text;
using NewLife.AI.Clients.OpenAI;
using NewLife.Serialization;

namespace NewLife.AI.Clients.Gemini;

/// <summary>Google Gemini 对话客户端。实现 Gemini 原生 API 协议</summary>
/// <remarks>
/// Gemini API 与 OpenAI 的主要差异：
/// <list type="bullet">
/// <item>认证通过 URL 参数 key 传递，不使用 Authorization 请求头</item>
/// <item>请求路径包含模型名称：/v1/models/{model}:generateContent</item>
/// <item>消息结构使用 contents 数组，角色为 user/model（非 assistant）</item>
/// <item>system 指令通过独立的 systemInstruction 顶级字段传入</item>
/// <item>流式接口路径为 :streamGenerateContent?alt=sse</item>
/// </list>
/// </remarks>
/// <remarks>用连接选项初始化 Gemini 客户端</remarks>
[AiClient("Gemini", "谷歌Gemini", "https://generativelanguage.googleapis.com", Protocol = "Gemini", Description = "谷歌 Gemini 系列多模态大模型，支持超长上下文")]
[AiClientModel("gemini-2.5-pro", "Gemini 2.5 Pro", Thinking = true, Vision = true)]
[AiClientModel("gemini-2.5-flash", "Gemini 2.5 Flash", Thinking = true, Vision = true)]
[AiClientModel("imagen-4.0-generate-001", "Imagen 4", ImageGeneration = true, FunctionCalling = false)]
public class GeminiChatClient : AiClientBase, IImageClient, IModelListClient
{
    #region 属性
    /// <inheritdoc/>
    public override String Name { get; set; } = "谷歌Gemini";

    /// <summary>默认Json序列化选项</summary>
    public static JsonOptions DefaultJsonOptions = new()
    {
        PropertyNaming = PropertyNaming.CamelCase,
        IgnoreNullValues = true,
    };
    #endregion

    #region 构造
    /// <param name="options">连接选项（Endpoint、ApiKey、Model 等）</param>
    public GeminiChatClient(AiClientOptions options) : base(options) => JsonOptions = DefaultJsonOptions;

    /// <summary>以 API 密钥和可选模型快速创建 Gemini 客户端</summary>
    /// <param name="apiKey">API 密钥</param>
    /// <param name="model">默认模型编码，为空时由每次请求指定</param>
    /// <param name="endpoint">API 地址覆盖；为空时使用内置默认地址</param>
    public GeminiChatClient(String apiKey, String? model = null, String? endpoint = null)
        : this(new AiClientOptions { ApiKey = apiKey, Model = model, Endpoint = endpoint }) { }
    #endregion

    #region 方法
    /// <summary>流式对话</summary>
    protected override async IAsyncEnumerable<IChatResponse> ChatStreamAsync(IChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var url = BuildUrl(request);
        var body = BuildRequest(request);

        using var httpResponse = await PostStreamAsync(url, body, request, _options, cancellationToken).ConfigureAwait(false);
        using var stream = await httpResponse.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync().ConfigureAwait(false);
            if (line == null) break;

            if (!line.StartsWith("data: ")) continue;

            var data = line.Substring(6).Trim();
            if (data.Length == 0) continue;

            var chunk = ParseChunk(data, request, null);
            if (chunk != null)
                yield return chunk;
        }
    }

    /// <summary>文生图。调用 Imagen 模型的 /v1beta/{model}:predict 端点</summary>
    /// <remarks>
    /// Imagen REST 端点：POST /v1beta/{model}:predict?key={apiKey}
    /// 请求体：{"instances": [{"prompt": "..."}], "parameters": {"sampleCount": N, "aspectRatio": "1:1"}}
    /// 响应体：{"predictions": [{"bytesBase64Encoded": "...", "mimeType": "image/png"}]}
    /// </remarks>
    /// <param name="request">图像生成请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>图像生成响应，失败时返回 null</returns>
    public virtual async Task<ImageGenerationResponse?> TextToImageAsync(ImageGenerationRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var apiKey = _options.ApiKey;
        var endpoint = _options.GetEndpoint(DefaultEndpoint).TrimEnd('/');
        var model = request.Model ?? _options.Model ?? "imagen-4.0-generate-001";
        var url = $"{endpoint}/v1beta/models/{model}:predict?key={apiKey}";

        var count = (request.N ?? 0) > 0 ? request.N!.Value : 1;
        var body = new
        {
            instances = new[] { new { prompt = request.Prompt ?? "" } },
            parameters = new
            {
                sampleCount = count,
                aspectRatio = request.Size ?? "1:1",
            },
        };

        var json = await PostAsync(url, body, null, _options, cancellationToken).ConfigureAwait(false);
        return ParseImagenResponse(json, count);
    }

    /// <summary>图像编辑。Imagen 独立端点不支持图像编辑，始终抛出异常</summary>
    /// <param name="request">图像编辑请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>不支持，始终抛出 NotSupportedException</returns>
    public virtual Task<ImageGenerationResponse?> EditImageAsync(ImageEditsRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Gemini Imagen API 不支持图像编辑（inpainting），请改用 OpenAI 或 DashScope");

    /// <summary>获取 Gemini 账号可用的模型列表</summary>
    /// <remarks>
    /// 端点：GET /v1beta/models?key={apiKey}
    /// 响应：{"models": [{"name": "models/gemini-2.5-flash", "displayName": "...", ...}]}
    /// </remarks>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>模型列表，服务不可用时返回 null</returns>
    public virtual async Task<OpenAiModelListResponse?> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        var apiKey = _options.ApiKey;
        var endpoint = _options.GetEndpoint(DefaultEndpoint).TrimEnd('/');
        var url = $"{endpoint}/v1beta/models?key={apiKey}";

        var json = await TryGetAsync(url, _options, cancellationToken).ConfigureAwait(false);
        if (json == null) return null;

        var dic = JsonParser.Decode(json);
        if (dic == null) return null;

        var response = new OpenAiModelListResponse { Object = "list" };

        if (dic["models"] is IList<Object> modelList)
        {
            var items = new List<OpenAiModelObject>(modelList.Count);
            foreach (var m in modelList)
            {
                if (m is not IDictionary<String, Object> d) continue;

                var name = d["name"] as String ?? "";
                var id = name.StartsWith("models/") ? name.Substring(7) : name;

                items.Add(new OpenAiModelObject
                {
                    Id = id,
                    Name = d["displayName"] as String ?? id,
                    Object = "model",
                    OwnedBy = "google",
                });
            }
            response.Data = [.. items];
        }

        return response;
    }
    #endregion

    #region 辅助
    /// <summary>构建请求地址。子类可重写此方法根据请求参数动态调整路径（如不同模型使用不同端点）</summary>
    protected override String BuildUrl(IChatRequest request)
    {
        var endpoint = _options.GetEndpoint(DefaultEndpoint).TrimEnd('/');
        if (request.Stream)
            return $"{endpoint}/v1/models/{request.Model}:streamGenerateContent?alt=sse&key={_options.ApiKey}";
        else
            return $"{endpoint}/v1/models/{request.Model}:generateContent?key={_options.ApiKey}";
    }

    /// <summary>构建 Gemini 请求体</summary>
    protected override Object BuildRequest(IChatRequest request) => request is GeminiRequest gr ? gr : GeminiRequest.FromChatRequest(request);

    /// <summary>解析 Gemini 非流式响应</summary>
    protected override IChatResponse ParseResponse(String data, IChatRequest request)
    {
        var resp = data.ToJsonEntity<GeminiResponse>(JsonOptions) ?? new GeminiResponse();
        resp.Model = request.Model;
        if (resp is IChatResponse rs && rs.Object.IsNullOrEmpty()) rs.Object = "chat.completion";
        return resp;
    }

    /// <summary>解析 Gemini 流式 SSE 单行 chunk</summary>
    protected override IChatResponse? ParseChunk(String data, IChatRequest request, String? lastEvent)
    {
        var resp = data.ToJsonEntity<GeminiResponse>(JsonOptions);
        resp?.Model = request.Model;
        if (resp is IChatResponse rs && rs.Object.IsNullOrEmpty()) rs.Object = "chat.completion.chunk";
        return resp;
    }

    /// <summary>解析 Imagen :predict 响应体</summary>
    /// <param name="json">响应 JSON</param>
    /// <param name="expectedCount">请求生成的图片数量</param>
    /// <returns>图像生成响应，解析失败时返回 null</returns>
    private static ImageGenerationResponse? ParseImagenResponse(String json, Int32 expectedCount)
    {
        var dic = JsonParser.Decode(json);
        if (dic == null) return null;

        var resp = new ImageGenerationResponse { Created = DateTime.UtcNow };

        if (dic["predictions"] is not IList<Object> preds) return resp;

        var items = new List<ImageData>(preds.Count);
        foreach (var pred in preds)
        {
            if (pred is not IDictionary<String, Object> d) continue;
            var b64 = d["bytesBase64Encoded"] as String;
            var mime = d["mimeType"] as String ?? "image/png";
            items.Add(new ImageData
            {
                B64Json = b64,
                Url = b64 != null ? $"data:{mime};base64,{b64}" : null,
            });
        }
        resp.Data = [.. items];
        return resp;
    }

    #endregion
}
