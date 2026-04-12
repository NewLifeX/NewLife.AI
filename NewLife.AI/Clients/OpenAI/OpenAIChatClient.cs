using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using NewLife.AI.Models;
using NewLife.Serialization;

namespace NewLife.AI.Clients.OpenAI;

/// <summary>OpenAI 协议对话客户端。兼容所有支持 OpenAI Chat Completions API 的服务商</summary>
/// <remarks>
/// 大部分国内外服务商均兼容 OpenAI Chat Completions 协议。
/// 通过设置 <see cref="ChatPath"/> 可适配不同路径的服务商（默认 /v1/chat/completions）。
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
public partial class OpenAIChatClient : AiClientBase
{
    #region 属性
    /// <inheritdoc/>
    public override String Name { get; set; } = "OpenAI";

    /// <summary>对话完成路径。默认 /v1/chat/completions，部分服务商需要调整</summary>
    public override String ChatPath { get; set; } = "/v1/chat/completions";

    /// <summary>默认Json序列化选项</summary>
    public static JsonOptions DefaultJsonOptions = new()
    {
        PropertyNaming = PropertyNaming.SnakeCaseLower,
        IgnoreNullValues = true,
    };
    #endregion

    #region 构造
    /// <param name="options">连接选项（Endpoint、ApiKey、Model 等）</param>
    public OpenAIChatClient(AiClientOptions options) : base(options) => JsonOptions = DefaultJsonOptions;

    /// <summary>以 API 密钥和可选模型快速创建 OpenAI 兼容客户端</summary>
    /// <param name="apiKey">API 密钥</param>
    /// <param name="model">默认模型编码，为空时由每次请求指定</param>
    /// <param name="endpoint">API 地址覆盖；为空时使用内置默认地址</param>
    public OpenAIChatClient(String apiKey, String? model = null, String? endpoint = null)
        : this(new AiClientOptions { ApiKey = apiKey, Model = model, Endpoint = endpoint }) { }
    #endregion

    #region IChatClient
    /// <summary>流式对话</summary>
    /// <param name="request">对话请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    protected override async IAsyncEnumerable<IChatResponse> ChatStreamAsync(IChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
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
            if (data == "[DONE]") break;
            if (data.Length == 0) continue;

            IChatResponse? chunk = null;
            try { chunk = ParseChunk(data, request, null); } catch { }
            if (chunk != null)
                yield return chunk;
        }
    }
    #endregion

    #region 模型列表
    /// <summary>获取该服务商当前可用的模型列表</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>模型列表，服务不可用时返回 null</returns>
    public virtual async Task<OpenAiModelListResponse?> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        var endpoint = _options.GetEndpoint(DefaultEndpoint).TrimEnd('/');
        var url = endpoint + "/v1/models";

        var json = await TryGetAsync(url, _options, cancellationToken).ConfigureAwait(false);
        if (json == null) return null;

        var dic = JsonParser.Decode(json);
        if (dic == null) return null;

        var response = new OpenAiModelListResponse
        {
            Object = dic["object"] as String,
        };

        if (dic["data"] is IList<Object> dataList)
        {
            var items = new List<OpenAiModelObject>();
            foreach (var item in dataList)
            {
                if (item is not IDictionary<String, Object> d) continue;
                items.Add(new OpenAiModelObject
                {
                    Id = d["id"] as String,
                    Name = d["name"] as String,
                    Object = d["object"] as String,
                    Created = d["created"].ToLong().ToDateTime(),
                    OwnedBy = d["owned_by"] as String,
                    ContextLength = d.TryGetValue("context_length", out var cl) ? cl.ToInt() : 0,
                    SupportThinking = d.TryGetValue("support_thinking", out var st) && st.ToBoolean(),
                    SupportFunctionCalling = d.TryGetValue("support_function_calling", out var sfc) && sfc.ToBoolean(),
                    SupportVision = d.TryGetValue("support_vision", out var sv) && sv.ToBoolean(),
                    SupportAudio = d.TryGetValue("support_audio", out var sa) && sa.ToBoolean(),
                    SupportImageGeneration = d.TryGetValue("support_image_generation", out var sig) && sig.ToBoolean(),
                    SupportVideoGeneration = d.TryGetValue("support_video_generation", out var svg) && svg.ToBoolean(),
                });
            }
            response.Data = [.. items];
        }

        return response;
    }
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

    #region 辅助
    /// <summary>构建请求地址。子类可重写此方法根据请求参数动态调整路径（如不同模型使用不同端点）</summary>
    protected override String BuildUrl(IChatRequest request) => _options.GetEndpoint(DefaultEndpoint).TrimEnd('/') + ChatPath;

    ///// <summary>构建请求体。返回符合 OpenAI 格式的字典，仅包含非空字段，避免部分模型拒绝 null 值</summary>
    ///// <param name="request">请求对象</param>
    ///// <returns>过滤 null 后的字典，由 PostAsync 序列化为 JSON</returns>
    //protected override Object BuildRequest(IChatRequest request) => ChatCompletionRequest.BuildBody(request);
    /// <summary>构建请求体。返回符合 OpenAI 格式的协议请求对象</summary>
    /// <param name="request">请求对象</param>
    /// <returns>ChatCompletionRequest 实例，由 PostAsync 调用 ToJson 序列化</returns>
    protected override Object BuildRequest(IChatRequest request) => request is ChatCompletionRequest cr ? cr : ChatCompletionRequest.FromChatRequest(request);

    /// <summary>解析响应 JSON</summary>
    /// <param name="json">JSON 字符串</param>
    /// <param name="request">请求对象</param>
    /// <returns></returns>
    protected override IChatResponse ParseResponse(String json, IChatRequest request)
    {
        var resp = json.ToJsonEntity<ChatCompletionResponse>(JsonOptions) ?? new ChatCompletionResponse();
        resp.Model = request.Model;
        if (resp is IChatResponse rs && rs.Object.IsNullOrEmpty()) rs.Object = "chat.completion";
        return resp;
    }

    /// <summary>解析消息对象</summary>
    /// <param name="dic">字典</param>
    /// <returns></returns>
    protected virtual ChatMessage? ParseChatMessage(IDictionary<String, Object>? dic)
    {
        if (dic == null) return null;

        var msg = new ChatMessage
        {
            Role = dic["role"] as String ?? "",
            Content = dic["content"],
            ReasoningContent = dic["reasoning_content"] as String ?? dic["reasoning"] as String,
        };

        if (dic["tool_calls"] is IList<Object> tcList)
        {
            var toolCalls = new List<ToolCall>();
            foreach (var tcItem in tcList)
            {
                if (tcItem is not IDictionary<String, Object> tcDic) continue;

                var tc = new ToolCall
                {
                    Index = tcDic["index"] is Object idxVal ? idxVal.ToInt() : null,
                    Id = tcDic["id"] as String ?? "",
                    Type = tcDic["type"] as String ?? "function",
                };

                if (tcDic["function"] is IDictionary<String, Object> fnDic)
                {
                    tc.Function = new FunctionCall
                    {
                        Name = fnDic["name"] as String ?? "",
                        Arguments = fnDic["arguments"] as String,
                    };
                }

                toolCalls.Add(tc);
            }
            msg.ToolCalls = toolCalls;
        }

        OnParseChatMessage(msg, dic);
        return msg;
    }

    /// <summary>消息解析扩展点。子类可重写此方法处理自定义响应字段</summary>
    /// <param name="msg">已完成基础解析的消息对象</param>
    /// <param name="dic">原始 JSON 字典，可读取额外字段</param>
    protected virtual void OnParseChatMessage(ChatMessage msg, IDictionary<String, Object> dic) { }

    /// <summary>设置请求头。Bearer Token + OpenAI-Organization</summary>
    /// <param name="request">HTTP 请求</param>
    /// <param name="chatRequest">对话请求，可为 null</param>
    /// <param name="options">连接选项</param>
    protected override void SetHeaders(HttpRequestMessage request, IChatRequest? chatRequest, AiClientOptions options)
    {
        if (!String.IsNullOrEmpty(options.ApiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);

        if (!String.IsNullOrEmpty(options.Organization))
            request.Headers.Add("OpenAI-Organization", options.Organization);
    }

    /// <summary>将 AIContent 集合转换为 OpenAI 格式的 content 字段</summary>
    /// <param name="contents">AIContent 列表</param>
    /// <returns>字符串（单一文本）或内容数组（多模态）</returns>
    protected static Object BuildContent(IList<AIContent> contents) => ChatCompletionRequest.BuildContent(contents);

    /// <summary>根据模型 ID 命名规律推断模型能力。子类可重写以实现服务商特定的推断逻辑</summary>
    /// <param name="modelId">模型标识</param>
    /// <returns>推断出的能力信息，无法推断时返回 null</returns>
    public virtual AiProviderCapabilities? InferModelCapabilities(String? modelId)
    {
        if (String.IsNullOrEmpty(modelId)) return null;

        // 非对话模型：嵌入、语音合成、语音识别等
        if (modelId.Contains("embed", StringComparison.OrdinalIgnoreCase) ||
            modelId.StartsWith("tts", StringComparison.OrdinalIgnoreCase) ||
            modelId.Contains("whisper", StringComparison.OrdinalIgnoreCase) ||
            modelId.Contains("rerank", StringComparison.OrdinalIgnoreCase))
            return new AiProviderCapabilities(false, false, false, false);

        var thinking = false;
        var funcCall = true;
        var vision = false;
        var audio = false;
        var imageGen = false;
        var videoGen = false;
        var contextLength = 0;

        // 视觉能力：含 -vl / -vision / 含 vision
        if (modelId.Contains("-vl", StringComparison.OrdinalIgnoreCase) ||
            modelId.Contains("vision", StringComparison.OrdinalIgnoreCase))
            vision = true;

        // 思考/推理能力
        if (modelId.Contains("-reasoner", StringComparison.OrdinalIgnoreCase) ||
            modelId.Contains("-thinking", StringComparison.OrdinalIgnoreCase))
            thinking = true;

        // OpenAI o 系列推理模型
        if (modelId.StartsWith("o1", StringComparison.OrdinalIgnoreCase) ||
            modelId.StartsWith("o3", StringComparison.OrdinalIgnoreCase) ||
            modelId.StartsWith("o4", StringComparison.OrdinalIgnoreCase))
            thinking = true;

        // 高端系列（max/plus）通常支持思考
        if (modelId.Contains("-max", StringComparison.OrdinalIgnoreCase) ||
            modelId.Contains("-plus", StringComparison.OrdinalIgnoreCase))
            thinking = true;

        // 文生图
        if (modelId.StartsWith("dall-e", StringComparison.OrdinalIgnoreCase) ||
            modelId.Contains("image-gen", StringComparison.OrdinalIgnoreCase))
        {
            imageGen = true;
            funcCall = false;
        }

        // 音频能力：gpt-4o-audio 系列
        if (modelId.Contains("-audio", StringComparison.OrdinalIgnoreCase))
            audio = true;

        // 文生视频：Sora 系列
        if (modelId.StartsWith("sora", StringComparison.OrdinalIgnoreCase))
        {
            videoGen = true;
            funcCall = false;
        }

        // === 上下文长度 ===
        // OpenAI o 系列推理模型：200K
        if (modelId.StartsWith("o1", StringComparison.OrdinalIgnoreCase) ||
            modelId.StartsWith("o3", StringComparison.OrdinalIgnoreCase) ||
            modelId.StartsWith("o4", StringComparison.OrdinalIgnoreCase))
            contextLength = 200_000;
        // GPT-4o 系列：128K
        else if (modelId.StartsWith("gpt-4o", StringComparison.OrdinalIgnoreCase))
            contextLength = 128_000;
        // GPT-4 Turbo：128K
        else if (modelId.StartsWith("gpt-4-turbo", StringComparison.OrdinalIgnoreCase))
            contextLength = 128_000;
        // GPT-4 经典：8K
        else if (modelId.StartsWith("gpt-4", StringComparison.OrdinalIgnoreCase))
            contextLength = 8_192;
        // GPT-3.5 Turbo：16K
        else if (modelId.StartsWith("gpt-3.5", StringComparison.OrdinalIgnoreCase))
            contextLength = 16_385;
        // Claude 系列：200K
        else if (modelId.StartsWith("claude", StringComparison.OrdinalIgnoreCase))
            contextLength = 200_000;
        // DeepSeek 系列：64K
        else if (modelId.StartsWith("deepseek", StringComparison.OrdinalIgnoreCase))
            contextLength = 65_536;

        return new AiProviderCapabilities(thinking, funcCall, vision, audio, imageGen, videoGen, contextLength);
    }
    #endregion
}
