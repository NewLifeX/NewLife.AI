using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using NewLife.AI.Embedding;
using NewLife.AI.Models;
using NewLife.Log;
using NewLife.Serialization;

namespace NewLife.AI.Providers;

/// <summary>OpenAI 协议基类。兼容所有支持 OpenAI Chat Completions API 的服务商</summary>
/// <remarks>
/// 大部分国内外服务商均兼容 OpenAI Chat Completions 协议，
/// 只需继承此类并设置 Name 和 DefaultEndpoint 即可完成适配。
/// 同时实现 <see cref="IEmbeddingProvider"/> ，支持创建嵌入向量客户端。
/// </remarks>
public class OpenAiProvider : IAiProvider, IAiChatProtocol, IEmbeddingProvider
{
    #region 属性
    /// <summary>服务商编码</summary>
    public virtual String Code => "OpenAI";

    /// <summary>服务商名称</summary>
    public virtual String Name => "OpenAI";

    /// <summary>服务商描述</summary>
    public virtual String? Description => "OpenAI 官方 API，支持 GPT 系列模型";

    /// <summary>API 协议类型</summary>
    public virtual String ApiProtocol => "ChatCompletions";

    /// <summary>默认 API 地址</summary>
    public virtual String DefaultEndpoint => "https://api.openai.com";

    /// <summary>主流模型列表。OpenAI 各主力模型及其能力</summary>
    public virtual AiModelInfo[] Models { get; } =
    [
        new("gpt-4.1",       "GPT-4.1",       new(false, true,  false, true)),
        new("gpt-4o",        "GPT-4o",         new(false, true,  false, true)),
        new("gpt-4o-mini",   "GPT-4o Mini",    new(false, true,  false, true)),
        new("o3",            "o3",             new(true,  false, false, true)),
        new("o4-mini",       "o4-mini",        new(true,  false, false, true)),
        new("dall-e-3",      "DALL·E 3",       new(false, false, true,  false)),
    ];

    /// <summary>对话完成路径</summary>
    protected virtual String ChatPath => "/v1/chat/completions";

    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>追踪器</summary>
    public ITracer? Tracer { get; set; }

    private static readonly HttpClient _httpClient = CreateHttpClient();
    #endregion

    #region 构造
    /// <summary>创建 HttpClient 实例。子类可通过此方法以相同配置创建独立客户端</summary>
    protected static HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
        };
        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMinutes(5),
        };
        client.DefaultRequestHeaders.Add("Accept", "application/json");

        return client;
    }
    #endregion

    #region 方法
    /// <summary>非流式对话</summary>
    /// <param name="request">对话请求</param>
    /// <param name="options">连接选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public virtual async Task<ChatCompletionResponse> ChatAsync(ChatCompletionRequest request, AiProviderOptions options, CancellationToken cancellationToken = default)
    {
        request.Stream = false;
        var body = BuildRequestBody(request);

        var endpoint = options.GetEndpoint(DefaultEndpoint).TrimEnd('/');
        var url = endpoint + ChatPath;

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
        SetHeaders(httpRequest, options);
        httpRequest.Content = new StringContent(body, Encoding.UTF8, "application/json");

        using var httpResponse = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        var responseText = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);

        if (!httpResponse.IsSuccessStatusCode)
            throw new HttpRequestException($"AI 服务商 {Name} 返回错误 {(Int32)httpResponse.StatusCode}: {responseText}");

        return ParseResponse(responseText);
    }

    /// <summary>创建已绑定连接参数的对话客户端（MEAI 兼容入口）</summary>
    /// <param name="options">连接选项（Endpoint、ApiKey 等）</param>
    /// <returns>已配置的 IChatClient 实例</returns>
    public virtual IChatClient CreateClient(AiProviderOptions options) => new OpenAiChatClient(this, options);

    /// <summary>创建已绑定连接参数的嵌入向量客户端</summary>
    /// <param name="options">连接选项（Endpoint、ApiKey 等）</param>
    /// <returns>已配置的 IEmbeddingClient 实例</returns>
    public virtual IEmbeddingClient CreateEmbeddingClient(AiProviderOptions options) => new OpenAiEmbeddingClient(this, options);

    /// <summary>流式对话</summary>
    /// <param name="request">对话请求</param>
    /// <param name="options">连接选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public virtual async IAsyncEnumerable<ChatCompletionResponse> ChatStreamAsync(ChatCompletionRequest request, AiProviderOptions options, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        request.Stream = true;
        var body = BuildRequestBody(request);

        var endpoint = options.GetEndpoint(DefaultEndpoint).TrimEnd('/');
        var url = endpoint + ChatPath;

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
        SetHeaders(httpRequest, options);
        httpRequest.Content = new StringContent(body, Encoding.UTF8, "application/json");

        using var httpResponse = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

        if (!httpResponse.IsSuccessStatusCode)
        {
            var errorText = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
            throw new HttpRequestException($"AI 服务商 {Name} 返回错误 {(Int32)httpResponse.StatusCode}: {errorText}");
        }

        using var stream = await httpResponse.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync().ConfigureAwait(false);
            if (line == null) break;

            // SSE 格式：data: {json}
            if (!line.StartsWith("data: ")) continue;

            var data = line.Substring(6).Trim();
            if (data == "[DONE]") break;
            if (data.Length == 0) continue;

            ChatCompletionResponse? chunk = null;
            try
            {
                chunk = ParseResponse(data);
            }
            catch
            {
                // 跳过无法解析的行
            }

            if (chunk != null)
                yield return chunk;
        }
    }
    #endregion

    #region 辅助
    /// <summary>构建请求体 JSON</summary>
    /// <param name="request">请求对象</param>
    /// <returns>JSON 字符串</returns>
    protected virtual String BuildRequestBody(ChatCompletionRequest request)
    {
        // 构建符合 OpenAI 格式的请求体
        var dic = new Dictionary<String, Object>();

        if (!String.IsNullOrEmpty(request.Model))
            dic["model"] = request.Model;

        // 构建消息列表
        var messages = new List<Object>();
        foreach (var msg in request.Messages)
        {
            var m = new Dictionary<String, Object> { ["role"] = msg.Role };

            // 类型化内容（Contents）优先于原始 Content 字段
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
                        tcDic["function"] = new Dictionary<String, Object?>
                        {
                            ["name"] = tc.Function.Name,
                            ["arguments"] = tc.Function.Arguments ?? "",
                        };
                    }
                    toolCalls.Add(tcDic);
                }
                m["tool_calls"] = toolCalls;
            }

            messages.Add(m);
        }
        dic["messages"] = messages;

        if (request.Stream) dic["stream"] = true;
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

        return dic.ToJson();
    }

    /// <summary>解析响应 JSON</summary>
    /// <param name="json">JSON 字符串</param>
    /// <returns></returns>
    protected virtual ChatCompletionResponse ParseResponse(String json)
    {
        var dic = JsonParser.Decode(json);
        if (dic == null) throw new InvalidOperationException("无法解析 AI 服务商响应");

        var response = new ChatCompletionResponse
        {
            Id = dic["id"] as String,
            Object = dic.TryGetValue("object", out var obj) ? obj as String : null,
            Created = dic.TryGetValue("created", out var created) ? created.ToLong() : 0,
            Model = dic.TryGetValue("model", out var model) ? model as String : null,
        };

        // 解析 choices
        if (dic.TryGetValue("choices", out var choicesObj) && choicesObj is IList<Object> choicesList)
        {
            var choices = new List<ChatChoice>();
            foreach (var item in choicesList)
            {
                if (item is not IDictionary<String, Object> choiceDic) continue;

                var choice = new ChatChoice
                {
                    Index = choiceDic.TryGetValue("index", out var idx) ? idx.ToInt() : 0,
                    FinishReason = choiceDic.TryGetValue("finish_reason", out var fr) ? fr as String : null,
                };

                // 非流式：message
                if (choiceDic.TryGetValue("message", out var msgObj))
                    choice.Message = ParseChatMessage(msgObj as IDictionary<String, Object>);

                // 流式：delta
                if (choiceDic.TryGetValue("delta", out var deltaObj))
                    choice.Delta = ParseChatMessage(deltaObj as IDictionary<String, Object>);

                choices.Add(choice);
            }
            response.Choices = choices;
        }

        // 解析 usage
        if (dic.TryGetValue("usage", out var usageObj) && usageObj is IDictionary<String, Object> usageDic)
        {
            response.Usage = new ChatUsage
            {
                PromptTokens = usageDic.TryGetValue("prompt_tokens", out var pt) ? pt.ToInt() : 0,
                CompletionTokens = usageDic.TryGetValue("completion_tokens", out var ct) ? ct.ToInt() : 0,
                TotalTokens = usageDic.TryGetValue("total_tokens", out var tt) ? tt.ToInt() : 0,
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
            Role = dic.TryGetValue("role", out var role) ? role as String ?? "" : "",
        };

        // content 可能是字符串或数组
        if (dic.TryGetValue("content", out var content))
            msg.Content = content;

        // 思考内容（DeepSeek/Moonshot/MiMo 等模型使用 reasoning_content；Ollama OpenAI 兼容模式使用 reasoning）
        if (dic.TryGetValue("reasoning_content", out var reasoning))
            msg.ReasoningContent = reasoning as String;
        else if (dic.TryGetValue("reasoning", out var ollamaReasoning))
            msg.ReasoningContent = ollamaReasoning as String;

        // 工具调用
        if (dic.TryGetValue("tool_calls", out var tcObj) && tcObj is IList<Object> tcList)
        {
            var toolCalls = new List<ToolCall>();
            foreach (var tcItem in tcList)
            {
                if (tcItem is not IDictionary<String, Object> tcDic) continue;

                var tc = new ToolCall
                {
                    Id = tcDic.TryGetValue("id", out var tcId) ? tcId as String ?? "" : "",
                    Type = tcDic.TryGetValue("type", out var tcType) ? tcType as String ?? "function" : "function",
                };

                if (tcDic.TryGetValue("function", out var fnObj) && fnObj is IDictionary<String, Object> fnDic)
                {
                    tc.Function = new FunctionCall
                    {
                        Name = fnDic.TryGetValue("name", out var fnName) ? fnName as String ?? "" : "",
                        Arguments = fnDic.TryGetValue("arguments", out var fnArgs) ? fnArgs as String : null,
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

    /// <summary>设置请求头</summary>
    /// <param name="request">HTTP 请求</param>
    /// <param name="options">选项</param>
    protected virtual void SetHeaders(HttpRequestMessage request, AiProviderOptions options)
    {
        if (!String.IsNullOrEmpty(options.ApiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);

        if (!String.IsNullOrEmpty(options.Organization))
            request.Headers.Add("OpenAI-Organization", options.Organization);
    }

    /// <summary>将 AIContent 集合转换为 OpenAI 格式的 content 字段将</summary>
    /// <param name="contents">AIContent 列表</param>
    /// <returns>字符串（单一文本）或内容数组（多模态）</returns>
    private static Object BuildContent(IList<AIContent> contents)
    {
        // 单纯文本优化：单个 TextContent 直接返回字符串，减少层套
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
