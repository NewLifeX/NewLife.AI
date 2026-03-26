using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using NewLife.AI.Models;
using NewLife.AI.Providers;
using NewLife.Log;
using NewLife.Serialization;

namespace NewLife.AI.Clients;

/// <summary>OpenAI 协议对话客户端。兼容所有支持 OpenAI Chat Completions API 的服务商</summary>
/// <remarks>
/// 大部分国内外服务商均兼容 OpenAI Chat Completions 协议。
/// 通过设置 <see cref="ChatPath"/> 可适配不同路径的服务商（默认 /v1/chat/completions）。
/// 类上标注的多个 <see cref="AiClientAttribute"/> 由 <see cref="AiClientRegistry"/> 反射扫描自动注册。
/// </remarks>
/// <remarks>用连接选项初始化 OpenAI 客户端</remarks>
/// <param name="options">连接选项（Endpoint、ApiKey、Model 等）</param>
/// <param name="httpClient">外部管理的 HttpClient，传 null 时自动创建</param>
// ── OpenAI 原生 ──────────────────────────────────────────────────────────────────────
[AiClient("OpenAI", "OpenAI", "https://api.openai.com", Description = "OpenAI GPT 系列模型", Order = 1)]
[AiClientModel("gpt-4.1", "GPT-4.1", Code = "OpenAI", Vision = true, FunctionCalling = true)]
[AiClientModel("gpt-4o", "GPT-4o", Code = "OpenAI", Vision = true, FunctionCalling = true)]
[AiClientModel("gpt-5-mini", "GPT-5 Mini", Code = "OpenAI", Vision = true, FunctionCalling = true)]
public partial class OpenAiChatClient(AiClientOptions options, HttpClient? httpClient = null) : AiClientBase(httpClient), IChatClient, ILogFeature, ITracerFeature
{
    #region 属性
    /// <inheritdoc/>
    public override String Name { get; set; } = "OpenAI";

    /// <summary>对话完成路径。默认 /v1/chat/completions，部分服务商需要调整</summary>
    public override String ChatPath { get; set; } = "/v1/chat/completions";

    /// <summary>连接选项</summary>
    protected readonly AiClientOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    #endregion

    #region IChatClient
    /// <summary>非流式对话完成</summary>
    /// <param name="request">对话请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>完整的对话响应</returns>
    public virtual async Task<ChatResponse> GetResponseAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        request.Model ??= _options.Model;

        var model = request.Model;
        using var span = Tracer?.NewSpan($"chat:{model}", request.Messages?.FirstOrDefault()?.Content);
        try
        {
            var response = await ChatAsync(request, cancellationToken).ConfigureAwait(false);
            if (span != null && response.Usage != null)
                span.Value = response.Usage.TotalTokens;
            return response;
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            Log.Error("[{0}] GetResponseAsync error! {1}", Name, ex.Message);
            throw;
        }
    }

    /// <summary>流式对话完成</summary>
    /// <param name="request">对话请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>流式响应块的异步枚举</returns>
    public virtual async IAsyncEnumerable<ChatResponse> GetStreamingResponseAsync(ChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        request.Model ??= _options.Model;

        var model = request.Model;
        using var span = Tracer?.NewSpan($"chat:streaming:{model}", model);

        UsageDetails? lastUsage = null;
        await foreach (var chunk in ChatStreamAsync(request, cancellationToken).ConfigureAwait(false))
        {
            if (chunk.Usage != null) lastUsage = chunk.Usage;
            yield return chunk;
        }

        if (span != null && lastUsage != null)
            span.Value = lastUsage.TotalTokens;
    }

    /// <summary>释放资源</summary>
    public virtual void Dispose() { }
    #endregion

    #region 方法
    /// <summary>非流式对话</summary>
    /// <param name="request">对话请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    protected virtual async Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        request.Stream = false;
        var body = BuildRequestBody(request);

        var endpoint = _options.GetEndpoint(DefaultEndpoint).TrimEnd('/');
        var url = endpoint + ChatPath;

        var responseText = await PostAsync(url, body, _options, cancellationToken).ConfigureAwait(false);
        return ParseResponse(responseText);
    }

    /// <summary>流式对话</summary>
    /// <param name="request">对话请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    protected virtual async IAsyncEnumerable<ChatResponse> ChatStreamAsync(ChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        request.Stream = true;
        var body = BuildRequestBody(request);

        var endpoint = _options.GetEndpoint(DefaultEndpoint).TrimEnd('/');
        var url = endpoint + ChatPath;

        using var httpResponse = await PostStreamAsync(url, body, _options, cancellationToken).ConfigureAwait(false);
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

            ChatResponse? chunk = null;
            try { chunk = ParseResponse(data); } catch { }
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

        var json = await PostAsync(url, dic, _options, cancellationToken).ConfigureAwait(false);
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

        return await PostBinaryAsync(url, dic, _options, cancellationToken).ConfigureAwait(false);
    }
    #endregion

    #region 辅助
    /// <summary>构建请求体。返回符合 OpenAI 格式的字典</summary>
    /// <param name="request">请求对象</param>
    /// <returns>请求体字典</returns>
    protected virtual Object BuildRequestBody(ChatRequest request)
    {
        var dic = new Dictionary<String, Object>();

        if (!String.IsNullOrEmpty(request.Model))
            dic["model"] = request.Model;

        var messages = new List<Object>();
        foreach (var msg in request.Messages)
        {
            var m = new Dictionary<String, Object> { ["role"] = msg.Role };

            if (msg.Contents != null && msg.Contents.Count > 0)
                m["content"] = BuildContent(msg.Contents);
            else if (msg.Content != null)
                m["content"] = msg.Content;

            if (msg.Name != null) m["name"] = msg.Name;
            if (msg.ToolCallId != null) m["tool_call_id"] = msg.ToolCallId;

            if (msg.ToolCalls != null && msg.ToolCalls.Count > 0)
            {
                var toolCalls = new List<Object>();
                foreach (var tc in msg.ToolCalls)
                {
                    var tcDic = new Dictionary<String, Object>
                    {
                        ["id"] = tc.Id,
                        ["type"] = tc.Type,
                    };
                    if (tc.Function != null)
                    {
                        var args = String.IsNullOrEmpty(tc.Function.Arguments) ? "{}" : tc.Function.Arguments;
                        tcDic["function"] = new Dictionary<String, Object?>
                        {
                            ["name"] = tc.Function.Name,
                            ["arguments"] = args,
                        };
                    }
                    toolCalls.Add(tcDic);
                }
                m["tool_calls"] = toolCalls;
            }

            messages.Add(m);
        }
        dic["messages"] = messages;

        if (request.Stream)
        {
            dic["stream"] = true;
            dic["stream_options"] = new Dictionary<String, Object> { ["include_usage"] = true };
        }
        if (request.Temperature != null) dic["temperature"] = request.Temperature.Value;
        if (request.TopP != null) dic["top_p"] = request.TopP.Value;
        if (request.MaxTokens != null) dic["max_tokens"] = request.MaxTokens.Value;
        if (request.Stop != null && request.Stop.Count > 0) dic["stop"] = request.Stop;
        if (request.PresencePenalty != null) dic["presence_penalty"] = request.PresencePenalty.Value;
        if (request.FrequencyPenalty != null) dic["frequency_penalty"] = request.FrequencyPenalty.Value;
        if (request.User != null) dic["user"] = request.User;

        if (request.Tools != null && request.Tools.Count > 0)
        {
            var tools = new List<Object>();
            foreach (var tool in request.Tools)
            {
                var t = new Dictionary<String, Object> { ["type"] = tool.Type };
                if (tool.Function != null)
                {
                    var fn = new Dictionary<String, Object?> { ["name"] = tool.Function.Name };
                    if (tool.Function.Description != null) fn["description"] = tool.Function.Description;
                    if (tool.Function.Parameters != null) fn["parameters"] = tool.Function.Parameters;
                    t["function"] = fn;
                }
                tools.Add(t);
            }
            dic["tools"] = tools;
        }
        if (request.ToolChoice != null) dic["tool_choice"] = request.ToolChoice;
        if (request.EnableThinking != null) dic["enable_thinking"] = request.EnableThinking.Value;

        return dic;
    }

    /// <summary>解析响应 JSON</summary>
    /// <param name="json">JSON 字符串</param>
    /// <returns></returns>
    protected virtual ChatResponse ParseResponse(String json)
    {
        var dic = JsonParser.Decode(json);
        if (dic == null) throw new InvalidOperationException("无法解析 AI 服务商响应");

        var response = new ChatResponse
        {
            Id = dic["id"] as String,
            Object = dic["object"] as String,
            Created = dic["created"].ToLong().ToDateTimeOffset(),
            Model = dic["model"] as String,
        };

        if (dic["choices"] is IList<Object> choicesList)
        {
            var choices = new List<ChatChoice>();
            foreach (var item in choicesList)
            {
                if (item is not IDictionary<String, Object> choiceDic) continue;

                var choice = new ChatChoice
                {
                    Index = choiceDic["index"].ToInt(),
                    FinishReason = choiceDic["finish_reason"] as String,
                    Message = ParseChatMessage(choiceDic["message"] as IDictionary<String, Object>),
                    Delta = ParseChatMessage(choiceDic["delta"] as IDictionary<String, Object>),
                };

                choices.Add(choice);
            }
            response.Messages = choices;
        }

        if (dic["usage"] is IDictionary<String, Object> usageDic)
        {
            response.Usage = new UsageDetails
            {
                InputTokens = usageDic["prompt_tokens"].ToInt(),
                OutputTokens = usageDic["completion_tokens"].ToInt(),
                TotalTokens = usageDic["total_tokens"].ToInt(),
            };
        }

        return response;
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
    /// <param name="options">连接选项</param>
    protected override void SetHeaders(HttpRequestMessage request, AiClientOptions options)
    {
        if (!String.IsNullOrEmpty(options.ApiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);

        if (!String.IsNullOrEmpty(options.Organization))
            request.Headers.Add("OpenAI-Organization", options.Organization);
    }

    /// <summary>将 AIContent 集合转换为 OpenAI 格式的 content 字段</summary>
    /// <param name="contents">AIContent 列表</param>
    /// <returns>字符串（单一文本）或内容数组（多模态）</returns>
    protected static Object BuildContent(IList<AIContent> contents)
    {
        if (contents.Count == 1 && contents[0] is TextContent singleText)
            return singleText.Text;

        var parts = new List<Object>(contents.Count);
        foreach (var item in contents)
        {
            if (item is TextContent text)
            {
                parts.Add(new Dictionary<String, Object> { ["type"] = "text", ["text"] = text.Text });
            }
            else if (item is ImageContent img)
            {
                String url;
                if (img.Data != null && img.Data.Length > 0)
                    url = $"data:{img.MediaType ?? "image/jpeg"};base64,{Convert.ToBase64String(img.Data)}";
                else
                    url = img.Uri ?? "";

                var imgDic = new Dictionary<String, Object> { ["url"] = url };
                if (img.Detail != null) imgDic["detail"] = img.Detail;
                parts.Add(new Dictionary<String, Object> { ["type"] = "image_url", ["image_url"] = imgDic });
            }
        }
        return parts;
    }
    #endregion
}
