using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using NewLife.AI.Models;
using NewLife.Serialization;

namespace NewLife.AI.Providers;

/// <summary>Anthropic 服务商。支持 Claude 系列模型的 Messages API 协议</summary>
/// <remarks>
/// Anthropic Messages API 与 OpenAI Chat Completions 有以下差异：
/// <list type="bullet">
/// <item>认证使用 x-api-key 头而非 Bearer Token</item>
/// <item>请求结构不同：system 作为独立字段，不放在 messages 数组中</item>
/// <item>响应结构不同：content 为数组，支持多个内容块</item>
/// <item>流式协议不同：使用 event/data 组合的 SSE 格式</item>
/// <item>支持交错思考（extended thinking）</item>
/// </list>
/// </remarks>
public class AnthropicProvider : IAiProvider
{
    #region 属性
    /// <summary>服务商编码</summary>
    public virtual String Code => "Anthropic";

    /// <summary>服务商名称</summary>
    public virtual String Name => "Anthropic";

    /// <summary>服务商描述</summary>
    public virtual String? Description => "Anthropic Claude 系列模型，擅长安全对齐和长文本理解";

    /// <summary>API 协议类型</summary>
    public virtual String ApiProtocol => "AnthropicMessages";

    /// <summary>默认 API 地址</summary>
    public virtual String DefaultEndpoint => "https://api.anthropic.com";

    /// <summary>默认能力信息。Anthropic 支持思考/视觉/函数调用，不支持图像生成</summary>
    public virtual AiProviderCapabilities DefaultCapabilities { get; } = new(true, true, false, true);

    /// <summary>API 版本</summary>
    protected virtual String ApiVersion => "2023-06-01";

    private static readonly HttpClient _httpClient = new(new HttpClientHandler
    {
        AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
    })
    {
        Timeout = TimeSpan.FromMinutes(5),
    };
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
        var body = BuildAnthropicRequest(request);

        var endpoint = options.GetEndpoint(DefaultEndpoint).TrimEnd('/');
        var url = endpoint + "/v1/messages";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
        SetHeaders(httpRequest, options);
        httpRequest.Content = new StringContent(body, Encoding.UTF8, "application/json");

        using var httpResponse = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        var responseText = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);

        if (!httpResponse.IsSuccessStatusCode)
            throw new HttpRequestException($"AI 服务商 {Name} 返回错误 {(Int32)httpResponse.StatusCode}: {responseText}");

        return ParseAnthropicResponse(responseText);
    }

    /// <summary>流式对话</summary>
    /// <param name="request">对话请求</param>
    /// <param name="options">连接选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public virtual async IAsyncEnumerable<ChatCompletionResponse> ChatStreamAsync(ChatCompletionRequest request, AiProviderOptions options, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        request.Stream = true;
        var body = BuildAnthropicRequest(request);

        var endpoint = options.GetEndpoint(DefaultEndpoint).TrimEnd('/');
        var url = endpoint + "/v1/messages";

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

        var eventType = "";
        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync().ConfigureAwait(false);
            if (line == null) break;

            // Anthropic SSE：event: xxx\ndata: {json}
            if (line.StartsWith("event: "))
            {
                eventType = line.Substring(7).Trim();
                continue;
            }

            if (!line.StartsWith("data: ")) continue;
            var data = line.Substring(6).Trim();
            if (data.Length == 0) continue;

            var chunk = ParseAnthropicStreamChunk(eventType, data);
            if (chunk != null)
                yield return chunk;
        }
    }
    #endregion

    #region 辅助
    /// <summary>构建 Anthropic 请求体</summary>
    /// <param name="request">请求对象</param>
    /// <returns></returns>
    private String BuildAnthropicRequest(ChatCompletionRequest request)
    {
        var dic = new Dictionary<String, Object>();

        if (!String.IsNullOrEmpty(request.Model))
            dic["model"] = request.Model;

        // Anthropic 的 system 作为独立字段
        var messages = new List<Object>();
        foreach (var msg in request.Messages)
        {
            if (msg.Role.Equals("system", StringComparison.OrdinalIgnoreCase))
            {
                dic["system"] = msg.Content?.ToString() ?? "";
                continue;
            }

            var m = new Dictionary<String, Object> { ["role"] = msg.Role };
            if (msg.Content != null) m["content"] = msg.Content;
            messages.Add(m);
        }
        dic["messages"] = messages;

        dic["max_tokens"] = request.MaxTokens ?? 4096;
        if (request.Stream) dic["stream"] = true;
        if (request.Temperature != null) dic["temperature"] = request.Temperature.Value;
        if (request.TopP != null) dic["top_p"] = request.TopP.Value;
        if (request.Stop != null && request.Stop.Count > 0) dic["stop_sequences"] = request.Stop;

        // 工具列表转换为 Anthropic 格式
        if (request.Tools != null && request.Tools.Count > 0)
        {
            var tools = new List<Object>();
            foreach (var tool in request.Tools)
            {
                if (tool.Function == null) continue;
                var t = new Dictionary<String, Object?> { ["name"] = tool.Function.Name };
                if (tool.Function.Description != null) t["description"] = tool.Function.Description;
                if (tool.Function.Parameters != null) t["input_schema"] = tool.Function.Parameters;
                tools.Add(t);
            }
            dic["tools"] = tools;
        }

        return dic.ToJson();
    }

    /// <summary>解析 Anthropic 非流式响应</summary>
    /// <param name="json">JSON 字符串</param>
    /// <returns></returns>
    private ChatCompletionResponse ParseAnthropicResponse(String json)
    {
        var dic = JsonParser.Decode(json);
        if (dic == null) throw new InvalidOperationException("无法解析 Anthropic 响应");

        var response = new ChatCompletionResponse
        {
            Id = dic.TryGetValue("id", out var id) ? id as String : null,
            Model = dic.TryGetValue("model", out var model) ? model as String : null,
            Object = "chat.completion",
        };

        // 将 Anthropic content 数组转换为 OpenAI 格式
        var contentText = "";
        var reasoningText = "";
        if (dic.TryGetValue("content", out var contentObj) && contentObj is IList<Object> contentList)
        {
            foreach (var block in contentList)
            {
                if (block is not IDictionary<String, Object> blockDic) continue;
                var blockType = blockDic.TryGetValue("type", out var bt) ? bt as String : null;

                if (blockType == "text" && blockDic.TryGetValue("text", out var text))
                    contentText += text;
                else if (blockType == "thinking" && blockDic.TryGetValue("thinking", out var thinking))
                    reasoningText += thinking;
            }
        }

        var msg = new ChatMessage { Role = "assistant", Content = contentText };
        if (!String.IsNullOrEmpty(reasoningText))
            msg.ReasoningContent = reasoningText;

        var finishReason = dic.TryGetValue("stop_reason", out var sr) ? sr as String : null;
        response.Choices = [new ChatChoice { Index = 0, Message = msg, FinishReason = MapStopReason(finishReason) }];

        if (dic.TryGetValue("usage", out var usageObj) && usageObj is IDictionary<String, Object> usageDic)
        {
            response.Usage = new ChatUsage
            {
                PromptTokens = usageDic.TryGetValue("input_tokens", out var it) ? it.ToInt() : 0,
                CompletionTokens = usageDic.TryGetValue("output_tokens", out var ot) ? ot.ToInt() : 0,
            };
            response.Usage.TotalTokens = response.Usage.PromptTokens + response.Usage.CompletionTokens;
        }

        return response;
    }

    /// <summary>解析 Anthropic 流式数据块</summary>
    /// <param name="eventType">SSE 事件类型</param>
    /// <param name="data">JSON 数据</param>
    /// <returns></returns>
    private ChatCompletionResponse? ParseAnthropicStreamChunk(String eventType, String data)
    {
        var dic = JsonParser.Decode(data);
        if (dic == null) return null;

        var response = new ChatCompletionResponse { Object = "chat.completion.chunk" };

        switch (eventType)
        {
            case "message_start":
                if (dic.TryGetValue("message", out var msgObj) && msgObj is IDictionary<String, Object> msgDic)
                {
                    response.Id = msgDic.TryGetValue("id", out var id) ? id as String : null;
                    response.Model = msgDic.TryGetValue("model", out var model) ? model as String : null;
                }
                response.Choices = [new ChatChoice { Index = 0, Delta = new ChatMessage { Role = "assistant" } }];
                return response;

            case "content_block_delta":
                if (dic.TryGetValue("delta", out var deltaObj) && deltaObj is IDictionary<String, Object> deltaDic)
                {
                    var deltaType = deltaDic.TryGetValue("type", out var dt) ? dt as String : null;

                    if (deltaType == "text_delta" && deltaDic.TryGetValue("text", out var text))
                    {
                        response.Choices = [new ChatChoice { Index = 0, Delta = new ChatMessage { Content = text } }];
                        return response;
                    }

                    if (deltaType == "thinking_delta" && deltaDic.TryGetValue("thinking", out var thinking))
                    {
                        response.Choices = [new ChatChoice { Index = 0, Delta = new ChatMessage { ReasoningContent = thinking as String } }];
                        return response;
                    }
                }
                return null;

            case "message_delta":
                if (dic.TryGetValue("delta", out var mdObj) && mdObj is IDictionary<String, Object> mdDic)
                {
                    var finishReason = mdDic.TryGetValue("stop_reason", out var sr) ? sr as String : null;
                    response.Choices = [new ChatChoice { Index = 0, FinishReason = MapStopReason(finishReason) }];
                }
                if (dic.TryGetValue("usage", out var usageObj) && usageObj is IDictionary<String, Object> usageDic)
                {
                    response.Usage = new ChatUsage
                    {
                        CompletionTokens = usageDic.TryGetValue("output_tokens", out var ot) ? ot.ToInt() : 0,
                    };
                }
                return response;

            case "message_stop":
                return null;

            default:
                return null;
        }
    }

    /// <summary>映射 Anthropic 的 stop_reason 到 OpenAI 的 finish_reason</summary>
    /// <param name="stopReason">Anthropic 停止原因</param>
    /// <returns></returns>
    private static String? MapStopReason(String? stopReason) => stopReason switch
    {
        "end_turn" => "stop",
        "max_tokens" => "length",
        "tool_use" => "tool_calls",
        _ => stopReason,
    };

    /// <summary>设置请求头</summary>
    /// <param name="request">HTTP 请求</param>
    /// <param name="options">选项</param>
    private void SetHeaders(HttpRequestMessage request, AiProviderOptions options)
    {
        if (!String.IsNullOrEmpty(options.ApiKey))
            request.Headers.Add("x-api-key", options.ApiKey);

        request.Headers.Add("anthropic-version", ApiVersion);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }
    #endregion
}
