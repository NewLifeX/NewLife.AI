using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using NewLife.AI.Clients.Anthropic;
using NewLife.AI.Clients.Gemini;
using NewLife.Remoting;
using NewLife.Serialization;

namespace NewLife.AI.Clients.OpenAI;

/// <summary>新生命 AI 对话客户端。新生命团队的统一 AI 网关，兼容 OpenAI / Anthropic / Gemini 协议</summary>
/// <remarks>
/// 星语（StarChat）网关，支持多模型路由、负载均衡和流量控制。
/// 除 /v1/chat/completions 外还提供 /v1/responses、/v1/messages、/v1/gemini、
/// /v1/images/generations、/v1/images/edits 全部端点。
/// 接入地址：https://ai.newlifex.com
/// </remarks>
/// <remarks>用连接选项初始化新生命 AI 客户端</remarks>
/// <param name="options">连接选项（Endpoint、ApiKey、Model 等）</param>
[AiClient("NewLifeAI", "新生命AI", "https://ai.newlifex.com", Description = "新生命团队星语 AI 网关，统一对接多种大模型")]
[AiClientModel("qwen3.5-flash", "Qwen3.5 Flash", Thinking = true)]
public class NewLifeAIChatClient(AiClientOptions options) : OpenAIChatClient(options)
{
    #region 属性
    /// <inheritdoc/>
    public override String Name { get; set; } = "新生命AI";
    #endregion

    #region 构造
    /// <summary>以 API 密钥和可选模型快速创建新生命 AI 客户端</summary>
    /// <param name="apiKey">API 密钥</param>
    /// <param name="model">默认模型编码，为空时由每次请求指定</param>
    /// <param name="endpoint">API 地址覆盖；为空时使用内置默认地址</param>
    public NewLifeAIChatClient(String apiKey, String? model = null, String? endpoint = null)
        : this(new AiClientOptions { ApiKey = apiKey, Model = model, Endpoint = endpoint }) { }
    #endregion

    #region OpenAI Responses API（/v1/responses）
    /// <summary>OpenAI Responses API 非流式。路径 /v1/responses，语义与 Chat Completions 一致</summary>
    /// <param name="request">对话请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>对话响应</returns>
    public virtual Task<IChatResponse> ResponsesAsync(IChatRequest request, CancellationToken cancellationToken = default)
        => ChatViaPathAsync(request, "/v1/responses", cancellationToken);

    /// <summary>OpenAI Responses API 流式。路径 /v1/responses</summary>
    /// <param name="request">对话请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>流式响应块序列</returns>
    public virtual IAsyncEnumerable<IChatResponse> ResponsesStreamAsync(IChatRequest request, CancellationToken cancellationToken = default)
        => ChatStreamViaPathAsync(request, "/v1/responses", cancellationToken);
    #endregion

    #region Anthropic Messages API（/v1/messages）
    /// <summary>Anthropic Messages API 非流式。路径 /v1/messages，兼容 claude 风格客户端</summary>
    /// <param name="request">对话请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>对话响应</returns>
    public virtual async Task<IChatResponse> MessagesAsync(IChatRequest request, CancellationToken cancellationToken = default)
    {
        request.Stream = false;
        var body = request is AnthropicRequest ar ? ar : AnthropicRequest.FromChatRequest(request);
        var url = _options.GetEndpoint(DefaultEndpoint).TrimEnd('/') + "/v1/messages";

        var responseText = await PostAsync(url, body, request, _options, cancellationToken).ConfigureAwait(false);
        var resp = responseText.ToJsonEntity<AnthropicResponse>(JsonOptions)!;
        resp.Model ??= request.Model;
        return resp;
    }

    /// <summary>Anthropic Messages API 流式。路径 /v1/messages</summary>
    /// <param name="request">对话请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>流式响应块序列</returns>
    public virtual async IAsyncEnumerable<IChatResponse> MessagesStreamAsync(IChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        request.Stream = true;
        var body = request is AnthropicRequest ar ? ar : AnthropicRequest.FromChatRequest(request);
        var url = _options.GetEndpoint(DefaultEndpoint).TrimEnd('/') + "/v1/messages";

        using var httpResponse = await PostStreamAsync(url, body, request, _options, cancellationToken).ConfigureAwait(false);
        using var stream = await httpResponse.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync().ConfigureAwait(false);
            if (line == null) break;

            if (!line.StartsWith("data: ")) continue;

            var data = line.Substring(6).Trim();
            if (data == "[DONE]") break;
            if (data.Length == 0) continue;

            IChatResponse? chunk = null;
            try { chunk = data.ToJsonEntity<AnthropicStreamEvent>(JsonOptions)?.ToChunkResponse(request.Model); } catch { }
            if (chunk != null) yield return chunk;
        }
    }
    #endregion

    #region Google Gemini API（/v1/gemini）
    /// <summary>Google Gemini API 非流式。路径 /v1/gemini，兼容 Gemini 风格客户端</summary>
    /// <param name="request">对话请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>对话响应</returns>
    public virtual async Task<IChatResponse> GeminiAsync(IChatRequest request, CancellationToken cancellationToken = default)
    {
        request.Stream = false;
        var body = request is GeminiRequest gr ? gr : GeminiRequest.FromChatRequest(request);
        // Gemini 协议使用 camelCase（如 systemInstruction/generationConfig），必须用 Gemini 专用 JsonOptions 序列化，
        // 不能使用父类的 SnakeCaseLower JsonOptions，否则字段名不匹配导致网关解析失败
        var bodyJson = JsonHost.Write(body, GeminiChatClient.DefaultJsonOptions)!;
        var url = _options.GetEndpoint(DefaultEndpoint).TrimEnd('/') + "/v1/gemini";

        var responseText = await PostAsync(url, bodyJson, request, _options, cancellationToken).ConfigureAwait(false);
        // 同理，Gemini 响应字段（candidates/finishReason/usageMetadata）也是 camelCase，需用 Gemini JsonOptions 反序列化
        var resp = responseText.ToJsonEntity<GeminiResponse>(GeminiChatClient.DefaultJsonOptions)!;
        resp.Model = request.Model;
        return resp;
    }

    /// <summary>Google Gemini API 流式。路径 /v1/gemini</summary>
    /// <param name="request">对话请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>流式响应块序列</returns>
    public virtual async IAsyncEnumerable<IChatResponse> GeminiStreamAsync(IChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        request.Stream = true;
        var body = request is GeminiRequest gr ? gr : GeminiRequest.FromChatRequest(request);
        // Gemini 协议使用 camelCase，必须用 Gemini 专用 JsonOptions 序列化，避免 snake_case 与网关期望格式不匹配
        var bodyJson = JsonHost.Write(body, GeminiChatClient.DefaultJsonOptions)!;
        var url = _options.GetEndpoint(DefaultEndpoint).TrimEnd('/') + "/v1/gemini";

        using var httpResponse = await PostStreamAsync(url, bodyJson, request, _options, cancellationToken).ConfigureAwait(false);
        using var stream = await httpResponse.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync().ConfigureAwait(false);
            if (line == null) break;

            if (!line.StartsWith("data: ")) continue;

            var data = line.Substring(6).Trim();
            if (data == "[DONE]") break;
            if (data.Length == 0) continue;

            IChatResponse? chunk = null;
            try
            {
                // Gemini 流式块字段（candidates/finishReason）为 camelCase，用 Gemini JsonOptions 反序列化
                var resp = data.ToJsonEntity<GeminiResponse>(GeminiChatClient.DefaultJsonOptions);
                if (resp != null) { resp.Model = request.Model; chunk = resp; }
            }
            catch { }
            if (chunk != null) yield return chunk;
        }
    }
    #endregion

    #region 图像生成（/v1/images/generations）
    /// <summary>图像生成。POST /v1/images/generations</summary>
    /// <param name="request">图像生成请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>图像生成响应</returns>
    public virtual Task<ImageGenerationResponse?> ImageGenerationsAsync(ImageGenerationRequest request, CancellationToken cancellationToken = default)
        => TextToImageAsync(request, cancellationToken);

    /// <summary>图像生成（简便重载）。POST /v1/images/generations</summary>
    /// <param name="prompt">图像描述提示词</param>
    /// <param name="model">模型名称，为 null 时使用默认</param>
    /// <param name="size">图像尺寸，如 "1024x1024"，为 null 时使用服务端默认</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>图像生成响应</returns>
    public virtual Task<ImageGenerationResponse?> ImageGenerationsAsync(String prompt, String? model = null, String? size = null, CancellationToken cancellationToken = default)
        => TextToImageAsync(new ImageGenerationRequest { Prompt = prompt, Model = model, Size = size }, cancellationToken);
    #endregion

    #region 图像编辑（/v1/images/edits）
    /// <summary>图像编辑。POST /v1/images/edits，multipart/form-data 格式</summary>
    /// <param name="request">图像编辑请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>图像生成响应</returns>
    public virtual async Task<ImageGenerationResponse?> ImageEditsAsync(ImageEditsRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (String.IsNullOrWhiteSpace(request.Prompt)) throw new ArgumentNullException(nameof(request));

        var url = _options.GetEndpoint(DefaultEndpoint).TrimEnd('/') + "/v1/images/edits";

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(request.Prompt), "prompt");
        if (!String.IsNullOrEmpty(request.Model)) form.Add(new StringContent(request.Model), "model");
        if (!String.IsNullOrEmpty(request.Size)) form.Add(new StringContent(request.Size), "size");

        var imageContent = new StreamContent(request.ImageStream);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(imageContent, "image", request.ImageFileName);

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

        return json.ToJsonEntity<ImageGenerationResponse>(JsonOptions);
    }

    /// <summary>图像编辑（分参数重载）。POST /v1/images/edits，multipart/form-data 格式</summary>
    /// <param name="imageStream">原始图像流（PNG 格式）</param>
    /// <param name="imageFileName">图像文件名</param>
    /// <param name="prompt">编辑提示词</param>
    /// <param name="model">模型名称，为 null 时使用默认</param>
    /// <param name="size">输出尺寸，为 null 时使用服务端默认</param>
    /// <param name="maskStream">蒙版图像流（可选，PNG 格式，透明区域为编辑区域）</param>
    /// <param name="maskFileName">蒙版文件名</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>图像生成响应</returns>
    public virtual Task<ImageGenerationResponse?> ImageEditsAsync(Stream imageStream, String imageFileName, String prompt, String? model, String? size, Stream? maskStream, String? maskFileName, CancellationToken cancellationToken = default)
        => ImageEditsAsync(new ImageEditsRequest { ImageStream = imageStream, ImageFileName = imageFileName, Prompt = prompt, Model = model, Size = size, MaskStream = maskStream, MaskFileName = maskFileName }, cancellationToken);
    #endregion

    #region 辅助
    /// <summary>以指定路径发起非流式对话请求</summary>
    protected async Task<IChatResponse> ChatViaPathAsync(IChatRequest request, String path, CancellationToken cancellationToken)
    {
        request.Stream = false;
        var body = BuildRequest(request);
        var url = _options.GetEndpoint(DefaultEndpoint).TrimEnd('/') + path;

        var responseText = await PostAsync(url, body, request, _options, cancellationToken).ConfigureAwait(false);
        return ParseResponse(responseText, request);
    }

    /// <summary>以指定路径发起流式对话请求</summary>
    protected async IAsyncEnumerable<IChatResponse> ChatStreamViaPathAsync(IChatRequest request, String path, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        request.Stream = true;
        var body = BuildRequest(request);
        var url = _options.GetEndpoint(DefaultEndpoint).TrimEnd('/') + path;

        using var httpResponse = await PostStreamAsync(url, body, request, _options, cancellationToken).ConfigureAwait(false);
        using var stream = await httpResponse.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync().ConfigureAwait(false);
            if (line == null) break;

            if (!line.StartsWith("data: ")) continue;

            var data = line.Substring(6).Trim();
            if (data == "[DONE]") break;
            if (data.Length == 0) continue;

            IChatResponse? chunk = null;
            try { chunk = ParseResponse(data, request); } catch { }
            if (chunk != null) yield return chunk;
        }
    }
    #endregion
}
