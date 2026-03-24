using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using NewLife.AI.Models;
using NewLife.Serialization;

namespace NewLife.AI.Providers;

/// <summary>新生命 AI 服务商。新生命团队的统一 AI 网关，兼容 OpenAI / Anthropic / Gemini 协议</summary>
/// <remarks>
/// 星语（StarChat）网关，支持多模型路由、负载均衡和流量控制。
/// 提供 /v1/chat/completions、/v1/responses、/v1/messages、/v1/gemini、
/// /v1/images/generations、/v1/images/edits 全部端点。
/// 接入地址：https://ai.newlifex.com
/// </remarks>
public class NewLifeAiProvider : OpenAiProvider
{
    #region 属性
    /// <summary>服务商编码</summary>
    public override String Code => "NewLifeAI";

    /// <summary>服务商名称</summary>
    public override String Name => "新生命AI";

    /// <summary>服务商描述</summary>
    public override String? Description => "新生命团队星语 AI 网关，统一对接多种大模型";

    /// <summary>默认 API 地址</summary>
    public override String DefaultEndpoint => "https://ai.newlifex.com";

    /// <summary>主流模型列表。通过网关路由到后端各服务商模型</summary>
    public override AiModelInfo[] Models { get; } =
    [
        new("qwen3.5", "Qwen3.5-0.8b", new(true, false, false, true)),
    ];
    #endregion

    #region OpenAI Responses API（/v1/responses）
    /// <summary>OpenAI Responses API 非流式。路径 /v1/responses，语义与 Chat Completions 一致</summary>
    /// <param name="request">对话请求</param>
    /// <param name="options">连接选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>对话响应</returns>
    public virtual Task<ChatResponse> ResponsesAsync(ChatCompletionRequest request, AiProviderOptions options, CancellationToken cancellationToken = default)
        => ChatViaPathAsync(request, options, "/v1/responses", cancellationToken);

    /// <summary>OpenAI Responses API 流式。路径 /v1/responses</summary>
    /// <param name="request">对话请求</param>
    /// <param name="options">连接选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>流式响应块序列</returns>
    public virtual IAsyncEnumerable<ChatResponse> ResponsesStreamAsync(ChatCompletionRequest request, AiProviderOptions options, CancellationToken cancellationToken = default)
        => ChatStreamViaPathAsync(request, options, "/v1/responses", cancellationToken);
    #endregion

    #region Anthropic Messages API（/v1/messages）
    /// <summary>Anthropic Messages API 非流式。路径 /v1/messages，兼容 claude 风格客户端</summary>
    /// <param name="request">对话请求</param>
    /// <param name="options">连接选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>对话响应</returns>
    public virtual Task<ChatResponse> MessagesAsync(ChatCompletionRequest request, AiProviderOptions options, CancellationToken cancellationToken = default)
        => ChatViaPathAsync(request, options, "/v1/messages", cancellationToken);

    /// <summary>Anthropic Messages API 流式。路径 /v1/messages</summary>
    /// <param name="request">对话请求</param>
    /// <param name="options">连接选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>流式响应块序列</returns>
    public virtual IAsyncEnumerable<ChatResponse> MessagesStreamAsync(ChatCompletionRequest request, AiProviderOptions options, CancellationToken cancellationToken = default)
        => ChatStreamViaPathAsync(request, options, "/v1/messages", cancellationToken);
    #endregion

    #region Google Gemini API（/v1/gemini）
    /// <summary>Google Gemini API 非流式。路径 /v1/gemini，兼容 Gemini 风格客户端</summary>
    /// <param name="request">对话请求</param>
    /// <param name="options">连接选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>对话响应</returns>
    public virtual Task<ChatResponse> GeminiAsync(ChatCompletionRequest request, AiProviderOptions options, CancellationToken cancellationToken = default)
        => ChatViaPathAsync(request, options, "/v1/gemini", cancellationToken);

    /// <summary>Google Gemini API 流式。路径 /v1/gemini</summary>
    /// <param name="request">对话请求</param>
    /// <param name="options">连接选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>流式响应块序列</returns>
    public virtual IAsyncEnumerable<ChatResponse> GeminiStreamAsync(ChatCompletionRequest request, AiProviderOptions options, CancellationToken cancellationToken = default)
        => ChatStreamViaPathAsync(request, options, "/v1/gemini", cancellationToken);
    #endregion

    #region 图像生成（/v1/images/generations）
    /// <summary>图像生成。POST /v1/images/generations</summary>
    /// <param name="prompt">图像描述提示词</param>
    /// <param name="model">模型名称，为 null 时使用默认模型</param>
    /// <param name="size">图像尺寸，如 "1024x1024"，为 null 时使用服务端默认</param>
    /// <param name="options">连接选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>图像生成响应</returns>
    public virtual async Task<ImageGenerationResponse?> ImageGenerationsAsync(String prompt, String? model, String? size, AiProviderOptions options, CancellationToken cancellationToken = default)
    {
        if (String.IsNullOrWhiteSpace(prompt)) throw new ArgumentNullException(nameof(prompt));

        var endpoint = options.GetEndpoint(DefaultEndpoint).TrimEnd('/');
        var url = $"{endpoint}/v1/images/generations";

        var dic = new Dictionary<String, Object> { ["prompt"] = prompt };
        if (!String.IsNullOrEmpty(model)) dic["model"] = model;
        if (!String.IsNullOrEmpty(size)) dic["size"] = size;

        var json = await PostAsync(url, dic, options, cancellationToken).ConfigureAwait(false);
        return json.ToJsonEntity<ImageGenerationResponse>();
    }
    #endregion

    #region 图像编辑（/v1/images/edits）
    /// <summary>图像编辑。POST /v1/images/edits，multipart/form-data 格式</summary>
    /// <param name="imageStream">原始图像流（PNG 格式）</param>
    /// <param name="imageFileName">图像文件名</param>
    /// <param name="prompt">编辑提示词</param>
    /// <param name="model">模型名称，为 null 时使用默认模型</param>
    /// <param name="size">输出尺寸，为 null 时使用服务端默认</param>
    /// <param name="maskStream">蒙版图像流（可选，PNG 格式，透明区域为编辑区域）</param>
    /// <param name="maskFileName">蒙版文件名</param>
    /// <param name="options">连接选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>图像生成响应</returns>
    public virtual async Task<ImageGenerationResponse?> ImageEditsAsync(Stream imageStream, String imageFileName, String prompt, String? model, String? size, Stream? maskStream, String? maskFileName, AiProviderOptions options, CancellationToken cancellationToken = default)
    {
        if (imageStream == null) throw new ArgumentNullException(nameof(imageStream));
        if (String.IsNullOrWhiteSpace(prompt)) throw new ArgumentNullException(nameof(prompt));

        var endpoint = options.GetEndpoint(DefaultEndpoint).TrimEnd('/');
        var url = $"{endpoint}/v1/images/edits";

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
        SetHeaders(req, options);

        using var resp = await HttpClient.SendAsync(req, cancellationToken).ConfigureAwait(false);
        var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException($"NewLifeAI 图像编辑失败 [{(Int32)resp.StatusCode}]: {json}");

        return json.ToJsonEntity<ImageGenerationResponse>();
    }
    #endregion

    #region 辅助
    /// <summary>以指定路径发起非流式对话请求。供 ResponsesAsync / MessagesAsync / GeminiAsync 复用</summary>
    /// <param name="request">对话请求</param>
    /// <param name="options">连接选项</param>
    /// <param name="path">API 路径，如 /v1/responses</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>对话响应</returns>
    protected async Task<ChatResponse> ChatViaPathAsync(ChatCompletionRequest request, AiProviderOptions options, String path, CancellationToken cancellationToken)
    {
        request.Stream = false;
        var body = BuildRequestBody(request);
        var url = options.GetEndpoint(DefaultEndpoint).TrimEnd('/') + path;

        var responseText = await PostAsync(url, body, options, cancellationToken).ConfigureAwait(false);
        return ParseResponse(responseText);
    }

    /// <summary>以指定路径发起流式对话请求。供 ResponsesStreamAsync / MessagesStreamAsync / GeminiStreamAsync 复用</summary>
    /// <param name="request">对话请求</param>
    /// <param name="options">连接选项</param>
    /// <param name="path">API 路径，如 /v1/responses</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>流式响应块序列</returns>
    protected async IAsyncEnumerable<ChatResponse> ChatStreamViaPathAsync(ChatCompletionRequest request, AiProviderOptions options, String path, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        request.Stream = true;
        var body = BuildRequestBody(request);
        var url = options.GetEndpoint(DefaultEndpoint).TrimEnd('/') + path;

        using var httpResponse = await PostStreamAsync(url, body, options, cancellationToken).ConfigureAwait(false);

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

            ChatResponse? chunk = null;
            try { chunk = ParseResponse(data); } catch { }
            if (chunk != null) yield return chunk;
        }
    }
    #endregion
}
