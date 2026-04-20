using System.Net.Http.Headers;
using NewLife.AI.Embedding;
using NewLife.AI.Models;
using NewLife.Remoting;
using NewLife.Serialization;

namespace NewLife.AI.Clients.OpenAI;

/// <summary>OpenAIChatClient 多模态能力扩展（partial）。
/// 提供图像编辑、语音识别（Whisper STT）、嵌入向量等 OpenAI 协议的多模态能力实现。</summary>
public partial class OpenAIChatClient
{
    #region 图像编辑（/v1/images/edits）
    /// <summary>图像编辑（含 Inpainting）。POST /v1/images/edits，multipart/form-data 格式</summary>
    /// <param name="request">图像编辑请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>图像生成响应</returns>
    public virtual async Task<ImageGenerationResponse?> EditImageAsync(ImageEditsRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (request.ImageStream == null) throw new ArgumentException("ImageStream 不能为空", nameof(request));
        if (String.IsNullOrWhiteSpace(request.Prompt)) throw new ArgumentException("Prompt 不能为空", nameof(request));

        var url = BuildApiUrl("/v1/images/edits");

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(request.Prompt), "prompt");
        if (!String.IsNullOrEmpty(request.Model)) form.Add(new StringContent(request.Model!), "model");
        if (!String.IsNullOrEmpty(request.Size)) form.Add(new StringContent(request.Size!), "size");

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
    #endregion

    #region 语音识别（Whisper STT，/v1/audio/transcriptions）
    /// <summary>语音识别（STT）。POST /v1/audio/transcriptions，multipart/form-data 格式</summary>
    /// <param name="request">语音识别请求。File + FileName 必填（OpenAI 不支持远程 URL）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>识别响应</returns>
    public virtual async Task<TranscriptionResponse> TranscribeAsync(TranscriptionRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (request.File == null)
            throw new ArgumentException("OpenAI Whisper 仅支持文件流上传，请通过 File 字段提供音频内容", nameof(request));

        var url = BuildApiUrl("/v1/audio/transcriptions");

        using var form = new MultipartFormDataContent();
        var fileContent = new StreamContent(request.File);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(fileContent, "file", request.FileName ?? "audio.mp3");

        var model = request.Model ?? _options.Model ?? "whisper-1";
        form.Add(new StringContent(model), "model");
        if (!String.IsNullOrEmpty(request.Language)) form.Add(new StringContent(request.Language!), "language");
        if (!String.IsNullOrEmpty(request.Prompt)) form.Add(new StringContent(request.Prompt!), "prompt");
        if (!String.IsNullOrEmpty(request.ResponseFormat)) form.Add(new StringContent(request.ResponseFormat!), "response_format");
        if (request.Temperature.HasValue) form.Add(new StringContent(request.Temperature.Value.ToString("0.##")), "temperature");
        if (request.TimestampGranularities != null)
        {
            foreach (var g in request.TimestampGranularities)
                form.Add(new StringContent(g), "timestamp_granularities[]");
        }

        using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = form };
        SetHeaders(req, null, _options);

        using var resp = await HttpClient.SendAsync(req, cancellationToken).ConfigureAwait(false);
        var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
            throw new ApiException((Int32)resp.StatusCode, body);

        return ParseTranscriptionResponse(body, request.ResponseFormat);
    }

    /// <summary>解析语音识别响应。json/verbose_json 走 JSON 解析；text/srt/vtt 直接放入 Text</summary>
    /// <param name="body">响应体</param>
    /// <param name="responseFormat">请求中指定的响应格式</param>
    /// <returns>识别响应</returns>
    protected virtual TranscriptionResponse ParseTranscriptionResponse(String body, String? responseFormat)
    {
        // 文本格式：直接当作 Text 返回
        if (responseFormat is "text" or "srt" or "vtt")
            return new TranscriptionResponse { Text = body };

        var dic = JsonParser.Decode(body);
        if (dic == null) return new TranscriptionResponse { Text = body };

        var resp = new TranscriptionResponse
        {
            Text = dic["text"] as String,
            Language = dic["language"] as String,
            Duration = dic.TryGetValue("duration", out var d) ? d.ToDouble() : null,
        };

        if (dic.TryGetValue("segments", out var segs) && segs is IList<Object> segList)
        {
            var items = new List<TranscriptionSegment>(segList.Count);
            foreach (var s in segList)
            {
                if (s is not IDictionary<String, Object> sd) continue;
                items.Add(new TranscriptionSegment
                {
                    Id = sd.TryGetValue("id", out var id) ? id.ToInt() : 0,
                    Start = sd.TryGetValue("start", out var st) ? st.ToDouble() : 0,
                    End = sd.TryGetValue("end", out var en) ? en.ToDouble() : 0,
                    Text = sd.TryGetValue("text", out var tx) ? tx as String : null,
                });
            }
            resp.Segments = items;
        }

        if (dic.TryGetValue("words", out var wds) && wds is IList<Object> wdList)
        {
            var items = new List<TranscriptionWord>(wdList.Count);
            foreach (var w in wdList)
            {
                if (w is not IDictionary<String, Object> wd) continue;
                items.Add(new TranscriptionWord
                {
                    Start = wd.TryGetValue("start", out var st) ? st.ToDouble() : 0,
                    End = wd.TryGetValue("end", out var en) ? en.ToDouble() : 0,
                    Word = wd.TryGetValue("word", out var w2) ? w2 as String : null,
                });
            }
            resp.Words = items;
        }

        return resp;
    }
    #endregion

    #region 嵌入向量（IEmbeddingClient 实现）
    private EmbeddingClientMetadata? _embeddingMetadata;

    /// <summary>嵌入客户端元数据。实现 <see cref="IEmbeddingClient"/></summary>
    public EmbeddingClientMetadata Metadata => _embeddingMetadata ??= new EmbeddingClientMetadata
    {
        ProviderName = Name,
        Endpoint = _options.GetEndpoint(DefaultEndpoint),
        DefaultModelId = _options.Model,
    };

    private OpenAiEmbeddingClient? _embeddingClient;
    /// <summary>生成嵌入向量。委托到内部 <see cref="OpenAiEmbeddingClient"/>，与现有 OpenAI 协议保持一致</summary>
    /// <param name="request">嵌入请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>嵌入响应</returns>
    public virtual Task<EmbeddingResponse> GenerateAsync(EmbeddingRequest request, CancellationToken cancellationToken = default)
    {
        _embeddingClient ??= new OpenAiEmbeddingClient(Name, DefaultEndpoint, _options) { JsonHost = JsonHost, Timeout = Timeout };
        return _embeddingClient.GenerateAsync(request, cancellationToken);
    }
    #endregion
}
