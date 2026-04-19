using NewLife.AI.Embedding;
using NewLife.Serialization;

namespace NewLife.AI.Clients.OpenAI;

/// <summary>OpenAI 原生多模态对话客户端。在 <see cref="OpenAIClientBase"/> 聊天与模型列表能力之上，扩展图像/视频/语音/嵌入等多模态能力</summary>
/// <remarks>
/// 大部分国内外服务商均兼容 OpenAI Chat Completions 协议，但多模态能力各有差异。
/// 仅在确有多模态实现时才应继承此类；若仅需聊天与模型列表，请直接继承 <see cref="OpenAIClientBase"/>。
/// 类上标注的多个 <see cref="AiClientAttribute"/> 由 <see cref="AiClientRegistry"/> 反射扫描自动注册。
/// </remarks>
/// <remarks>用连接选项初始化 OpenAI 客户端</remarks>
// ── OpenAI 原生 ──────────────────────────────────────────────────────────────────────
[AiClient("OpenAI", "OpenAI", "https://api.openai.com", Description = "OpenAI GPT 系列模型", Order = 1)]
[AiClientModel("gpt-4.1", "GPT-4.1", Code = "OpenAI", Vision = true, FunctionCalling = true)]
[AiClientModel("gpt-4o", "GPT-4o", Code = "OpenAI", Vision = true, FunctionCalling = true)]
[AiClientModel("gpt-4o-mini", "GPT-4o Mini", Code = "OpenAI", Vision = true, FunctionCalling = true)]
[AiClientModel("gpt-5-mini", "GPT-5 Mini", Code = "OpenAI", Vision = true, FunctionCalling = true)]
[AiClientModel("o3-mini", "o3 Mini", Code = "OpenAI", Thinking = true, FunctionCalling = true)]
[AiClientModel("o4-mini", "o4 Mini", Code = "OpenAI", Thinking = true, Vision = true, FunctionCalling = true)]
[AiClientModel("dall-e-3", "DALL·E 3", Code = "OpenAI", ImageGeneration = true, FunctionCalling = false)]
public partial class OpenAIChatClient : OpenAIClientBase,
    IImageClient, IVideoClient, ISpeechClient, ITranscriptionClient, IEmbeddingClient
{
    #region 属性
    /// <inheritdoc/>
    public override String Name { get; set; } = "OpenAI";
    #endregion

    #region 构造
    /// <param name="options">连接选项（Endpoint、ApiKey、Model 等）</param>
    public OpenAIChatClient(AiClientOptions options) : base(options) { }

    /// <summary>以 API 密钥和可选模型快速创建 OpenAI 兼容客户端</summary>
    /// <param name="apiKey">API 密钥</param>
    /// <param name="model">默认模型编码，为空时由每次请求指定</param>
    /// <param name="endpoint">API 地址覆盖；为空时使用内置默认地址</param>
    public OpenAIChatClient(String apiKey, String? model = null, String? endpoint = null)
        : this(new AiClientOptions { ApiKey = apiKey, Model = model, Endpoint = endpoint }) { }
    #endregion

    #region 文生图
    /// <summary>文生图。按 DALL·E 3 / OpenAI 兼容格式调用 /v1/images/generations 生成图像</summary>
    /// <param name="request">图像生成请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>图像生成响应，失败时返回 null</returns>
    public virtual async Task<ImageGenerationResponse?> TextToImageAsync(ImageGenerationRequest request, CancellationToken cancellationToken = default)
    {
        var endpoint = _options.GetEndpoint(DefaultEndpoint).TrimEnd('/');
        var url = endpoint + "/v1/images/generations";

        var json = await PostAsync(url, request, null, _options, cancellationToken).ConfigureAwait(false);
        return ParseImageGenerationResponse(json);
    }

    /// <summary>解析图像生成响应 JSON</summary>
    /// <param name="json">响应 JSON 字符串</param>
    /// <returns>解析后的响应对象，解析失败时返回 null</returns>
    protected virtual ImageGenerationResponse? ParseImageGenerationResponse(String json)
    {
        var dic = JsonParser.Decode(json);
        if (dic == null) return null;

        var resp = new ImageGenerationResponse
        {
            Created = dic["created"].ToLong().ToDateTime(),
        };

        if (dic["data"] is IList<Object> dataList)
        {
            var items = new List<ImageData>(dataList.Count);
            foreach (var item in dataList)
            {
                if (item is not IDictionary<String, Object> d) continue;
                items.Add(new ImageData
                {
                    Url = d["url"] as String,
                    B64Json = d["b64_json"] as String,
                    RevisedPrompt = d["revised_prompt"] as String,
                });
            }
            resp.Data = [.. items];
        }

        return resp;
    }
    #endregion

    #region 语音合成（TTS）
    /// <summary>语音合成（TTS）。兼容 OpenAI /v1/audio/speech 接口，返回音频字节流</summary>
    /// <param name="request">语音合成请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>音频字节流（格式由 request.ResponseFormat 决定，默认 mp3）</returns>
    public virtual async Task<Byte[]> SpeechAsync(SpeechRequest request, CancellationToken cancellationToken = default)
    {
        var endpoint = _options.GetEndpoint(DefaultEndpoint).TrimEnd('/');
        var url = endpoint + "/v1/audio/speech";

        return await PostBinaryAsync(url, request, null, _options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>语音合成（TTS）。兼容 OpenAI /v1/audio/speech 接口，返回音频字节流</summary>
    /// <param name="input">要合成的文本内容</param>
    /// <param name="voice">音色名称。如 longxiaochun、alloy</param>
    /// <param name="model">TTS 模型编码。如 cosyvoice-v2、tts-1</param>
    /// <param name="responseFormat">音频格式。mp3（默认）/ wav / opus / flac / pcm</param>
    /// <param name="speed">语速倍率。0.25~4.0，默认 1.0</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>音频字节流（格式由 responseFormat 决定，默认 mp3）</returns>
    public virtual Task<Byte[]> SpeechAsync(String input, String voice, String? model = null, String? responseFormat = null, Double? speed = null, CancellationToken cancellationToken = default)
        => SpeechAsync(new SpeechRequest { Input = input, Voice = voice, Model = model ?? "tts-1", ResponseFormat = responseFormat, Speed = speed }, cancellationToken);
    #endregion

    #region 文生视频
    /// <summary>提交视频生成任务。返回任务编号用于后续轮询</summary>
    /// <param name="request">视频生成请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务提交响应，含 TaskId</returns>
    public virtual async Task<VideoTaskSubmitResponse> SubmitVideoGenerationAsync(VideoGenerationRequest request, CancellationToken cancellationToken = default)
    {
        var endpoint = _options.GetEndpoint(DefaultEndpoint).TrimEnd('/');
        var url = endpoint + "/v1/video/generations";

        var json = await PostAsync(url, request, null, _options, cancellationToken).ConfigureAwait(false);
        return ParseVideoTaskSubmitResponse(json);
    }

    /// <summary>查询视频生成任务状态</summary>
    /// <param name="taskId">任务编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务状态响应</returns>
    public virtual async Task<VideoTaskStatusResponse> GetVideoTaskAsync(String taskId, CancellationToken cancellationToken = default)
    {
        var endpoint = _options.GetEndpoint(DefaultEndpoint).TrimEnd('/');
        var url = endpoint + $"/v1/video/generations/{taskId}";

        var json = await GetAsync(url, null, _options, cancellationToken).ConfigureAwait(false);
        return ParseVideoTaskStatusResponse(json);
    }

    /// <summary>解析视频任务提交响应</summary>
    /// <param name="json">响应 JSON</param>
    /// <returns>解析后的提交响应</returns>
    protected virtual VideoTaskSubmitResponse ParseVideoTaskSubmitResponse(String json)
    {
        var dic = JsonParser.Decode(json);
        if (dic == null) return new VideoTaskSubmitResponse();

        return new VideoTaskSubmitResponse
        {
            TaskId = dic["id"] as String ?? dic["task_id"] as String,
            RequestId = dic["request_id"] as String,
            Status = dic["status"] as String,
        };
    }

    /// <summary>解析视频任务状态响应</summary>
    /// <param name="json">响应 JSON</param>
    /// <returns>解析后的状态响应</returns>
    protected virtual VideoTaskStatusResponse ParseVideoTaskStatusResponse(String json)
    {
        var dic = JsonParser.Decode(json);
        if (dic == null) return new VideoTaskStatusResponse();

        var resp = new VideoTaskStatusResponse
        {
            TaskId = dic["id"] as String ?? dic["task_id"] as String,
            RequestId = dic["request_id"] as String,
            Status = dic["status"] as String,
        };

        // OpenAI 格式：generation.url
        if (dic["generation"] is IDictionary<String, Object> gen && gen["url"] is String genUrl)
            resp.VideoUrls = [genUrl];

        // 通用格式：video_url / video_urls
        if (dic["video_url"] is String videoUrl)
            resp.VideoUrls = [videoUrl];
        else if (dic["video_urls"] is IList<Object> urls)
            resp.VideoUrls = urls.Select(u => u?.ToString() ?? "").Where(u => u.Length > 0).ToArray();

        resp.ErrorCode = dic["error_code"] as String ?? dic["code"] as String;
        resp.ErrorMessage = dic["error_message"] as String ?? dic["message"] as String;

        return resp;
    }
    #endregion

}
