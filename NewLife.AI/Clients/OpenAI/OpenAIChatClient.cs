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
/// <param name="options">连接选项（Endpoint、ApiKey、Model 等）</param>
// ── OpenAI 原生 ──────────────────────────────────────────────────────────────────────
[AiClient("OpenAI", "OpenAI", "https://api.openai.com", Description = "OpenAI GPT 系列模型", Order = 1)]
[AiClientModel("gpt-4.1", "GPT-4.1", Code = "OpenAI", Vision = true, FunctionCalling = true)]
[AiClientModel("gpt-4o", "GPT-4o", Code = "OpenAI", Vision = true, FunctionCalling = true)]
[AiClientModel("gpt-5-mini", "GPT-5 Mini", Code = "OpenAI", Vision = true, FunctionCalling = true)]
public partial class OpenAIChatClient(AiClientOptions options) : AiClientBase(options)
{
    #region 属性
    /// <inheritdoc/>
    public override String Name { get; set; } = "OpenAI";

    /// <summary>对话完成路径。默认 /v1/chat/completions，部分服务商需要调整</summary>
    public override String ChatPath { get; set; } = "/v1/chat/completions";
    #endregion

    #region 构造
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

        var dic = new Dictionary<String, Object?>();
        if (!String.IsNullOrEmpty(request.Model)) dic["model"] = request.Model;
        dic["prompt"] = request.Prompt;
        if (request.N != null) dic["n"] = request.N.Value;
        if (!String.IsNullOrEmpty(request.Size)) dic["size"] = request.Size;
        if (!String.IsNullOrEmpty(request.Quality)) dic["quality"] = request.Quality;
        if (!String.IsNullOrEmpty(request.Style)) dic["style"] = request.Style;
        if (!String.IsNullOrEmpty(request.ResponseFormat)) dic["response_format"] = request.ResponseFormat;
        if (!String.IsNullOrEmpty(request.User)) dic["user"] = request.User;
        if (!String.IsNullOrEmpty(request.NegativePrompt)) dic["negative_prompt"] = request.NegativePrompt;

        var json = await PostAsync(url, dic, null, _options, cancellationToken).ConfigureAwait(false);
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
            Created = dic["created"].ToLong(),
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
    /// <param name="input">要合成的文本内容</param>
    /// <param name="voice">音色名称。如 longxiaochun、alloy</param>
    /// <param name="model">TTS 模型编码。如 cosyvoice-v2、tts-1</param>
    /// <param name="responseFormat">音频格式。mp3（默认）/ wav / opus / flac / pcm</param>
    /// <param name="speed">语速倍率。0.25~4.0，默认 1.0</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>音频字节流（格式由 responseFormat 决定，默认 mp3）</returns>
    public virtual async Task<Byte[]> SpeechAsync(String input, String voice, String? model = null, String? responseFormat = null, Double? speed = null, CancellationToken cancellationToken = default)
    {
        var endpoint = _options.GetEndpoint(DefaultEndpoint).TrimEnd('/');
        var url = endpoint + "/v1/audio/speech";

        var dic = new Dictionary<String, Object?>
        {
            ["model"] = model ?? "tts-1",
            ["input"] = input,
            ["voice"] = voice
        };
        if (!String.IsNullOrEmpty(responseFormat)) dic["response_format"] = responseFormat;
        if (speed != null) dic["speed"] = speed.Value;

        return await PostBinaryAsync(url, dic, null, _options, cancellationToken).ConfigureAwait(false);
    }
    #endregion

    #region 辅助
    /// <summary>构建请求地址。子类可重写此方法根据请求参数动态调整路径（如不同模型使用不同端点）</summary>
    protected override String BuildUrl(IChatRequest request) => _options.GetEndpoint(DefaultEndpoint).TrimEnd('/') + ChatPath;

    /// <summary>构建请求体。返回符合 OpenAI 格式的协议请求对象</summary>
    /// <param name="request">请求对象</param>
    /// <returns>ChatCompletionRequest 实例，由 PostAsync 调用 ToJson 序列化</returns>
    protected override Object BuildRequest(IChatRequest request) => ChatCompletionRequest.FromChatRequest(request);

    /// <summary>解析响应 JSON</summary>
    /// <param name="json">JSON 字符串</param>
    /// <param name="request">请求对象</param>
    /// <returns></returns>
    protected override IChatResponse ParseResponse(String json, IChatRequest request) => json.ToJsonEntity<ChatCompletionResponse>()!.ToChatResponse();

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
                    Index = tcDic["index"] is Object idxVal ? (Int32?)idxVal.ToInt() : null,
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
    #endregion
}
