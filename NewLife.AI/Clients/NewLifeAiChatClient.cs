using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using NewLife.AI.Models;
using NewLife.AI.Providers;
using NewLife.Remoting;
using NewLife.Serialization;

namespace NewLife.AI.Clients;

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
    public virtual Task<IChatResponse> MessagesAsync(IChatRequest request, CancellationToken cancellationToken = default)
        => ChatViaPathAsync(request, "/v1/messages", cancellationToken);

    /// <summary>Anthropic Messages API 流式。路径 /v1/messages</summary>
    /// <param name="request">对话请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>流式响应块序列</returns>
    public virtual IAsyncEnumerable<IChatResponse> MessagesStreamAsync(IChatRequest request, CancellationToken cancellationToken = default)
        => ChatStreamViaPathAsync(request, "/v1/messages", cancellationToken);
    #endregion

    #region Google Gemini API（/v1/gemini）
    /// <summary>Google Gemini API 非流式。路径 /v1/gemini，兼容 Gemini 风格客户端</summary>
    /// <param name="request">对话请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>对话响应</returns>
    public virtual Task<IChatResponse> GeminiAsync(IChatRequest request, CancellationToken cancellationToken = default)
        => ChatViaPathAsync(request, "/v1/gemini", cancellationToken);

    /// <summary>Google Gemini API 流式。路径 /v1/gemini</summary>
    /// <param name="request">对话请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>流式响应块序列</returns>
    public virtual IAsyncEnumerable<IChatResponse> GeminiStreamAsync(IChatRequest request, CancellationToken cancellationToken = default)
        => ChatStreamViaPathAsync(request, "/v1/gemini", cancellationToken);
    #endregion

    #region 图像生成（/v1/images/generations）
    /// <summary>图像生成。POST /v1/images/generations</summary>
    /// <param name="prompt">图像描述提示词</param>
    /// <param name="model">模型名称，为 null 时使用默认</param>
    /// <param name="size">图像尺寸，如 "1024x1024"，为 null 时使用服务端默认</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>图像生成响应</returns>
    public virtual async Task<ImageGenerationResponse?> ImageGenerationsAsync(String prompt, String? model, String? size, CancellationToken cancellationToken = default)
    {
        if (String.IsNullOrWhiteSpace(prompt)) throw new ArgumentNullException(nameof(prompt));

        var url = _options.GetEndpoint(DefaultEndpoint).TrimEnd('/') + "/v1/images/generations";
        var dic = new Dictionary<String, Object> { ["prompt"] = prompt };
        if (!String.IsNullOrEmpty(model)) dic["model"] = model;
        if (!String.IsNullOrEmpty(size)) dic["size"] = size;

        var json = await PostAsync(url, dic, null, _options, cancellationToken).ConfigureAwait(false);
        return json.ToJsonEntity<ImageGenerationResponse>();
    }
    #endregion

    #region 图像编辑（/v1/images/edits）
    /// <summary>图像编辑。POST /v1/images/edits，multipart/form-data 格式</summary>
    /// <param name="imageStream">原始图像流（PNG 格式）</param>
    /// <param name="imageFileName">图像文件名</param>
    /// <param name="prompt">编辑提示词</param>
    /// <param name="model">模型名称，为 null 时使用默认</param>
    /// <param name="size">输出尺寸，为 null 时使用服务端默认</param>
    /// <param name="maskStream">蒙版图像流（可选，PNG 格式，透明区域为编辑区域）</param>
    /// <param name="maskFileName">蒙版文件名</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>图像生成响应</returns>
    public virtual async Task<ImageGenerationResponse?> ImageEditsAsync(Stream imageStream, String imageFileName, String prompt, String? model, String? size, Stream? maskStream, String? maskFileName, CancellationToken cancellationToken = default)
    {
        if (imageStream == null) throw new ArgumentNullException(nameof(imageStream));
        if (String.IsNullOrWhiteSpace(prompt)) throw new ArgumentNullException(nameof(prompt));

        var url = _options.GetEndpoint(DefaultEndpoint).TrimEnd('/') + "/v1/images/edits";

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(prompt), "prompt");
        if (!String.IsNullOrEmpty(model)) form.Add(new StringContent(model), "model");
        if (!String.IsNullOrEmpty(size)) form.Add(new StringContent(size), "size");

        var imageContent = new StreamContent(imageStream);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(imageContent, "image", imageFileName ?? "image.png");

        if (maskStream != null)
        {
            var maskContent = new StreamContent(maskStream);
            maskContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            form.Add(maskContent, "mask", maskFileName ?? "mask.png");
        }

        using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = form };
        SetHeaders(req, null, _options);

        using var resp = await HttpClient.SendAsync(req, cancellationToken).ConfigureAwait(false);
        var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

        if (!resp.IsSuccessStatusCode)
            throw new ApiException((Int32)resp.StatusCode, json);
        //throw new HttpRequestException($"[{Name}] 图像编辑失败 [{(Int32)resp.StatusCode}]: {json}");

        return json.ToJsonEntity<ImageGenerationResponse>();
    }
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
