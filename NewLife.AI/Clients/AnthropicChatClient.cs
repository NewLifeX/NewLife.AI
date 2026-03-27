using System.Runtime.CompilerServices;
using System.Text;
using NewLife.AI.Models;
using NewLife.AI.Providers;
using NewLife.Serialization;

namespace NewLife.AI.Clients;

/// <summary>Anthropic Claude 对话客户端。实现 Anthropic Messages API 原生协议</summary>
/// <remarks>
/// Anthropic API 与 OpenAI 的主要差异：
/// <list type="bullet">
/// <item>认证通过 x-api-key 头传递，需附加 anthropic-version 头</item>
/// <item>system 消息为顶级独立字段，不在 messages 数组中</item>
/// <item>响应中的内容为 content 数组（text_delta / thinking_delta）</item>
/// <item>流式响应使用 event/data 格式而非 OpenAI 的 data-only 格式</item>
/// </list>
/// </remarks>
/// <remarks>用连接选项初始化 Anthropic 客户端</remarks>
/// <param name="options">连接选项（Endpoint、ApiKey、Model 等）</param>
[AiClient("Anthropic", "Anthropic", "https://api.anthropic.com", Protocol = "AnthropicMessages", Description = "Anthropic Claude 系列模型")]
[AiClientModel("claude-opus-4-6", "Claude Opus 4.6", Thinking = true, Vision = true)]
[AiClientModel("claude-sonnet-4-6", "Claude Sonnet 4.6", Thinking = true, Vision = true)]
[AiClientModel("claude-haiku-4-5", "Claude Haiku 4.5", Thinking = true, Vision = true)]
public class AnthropicChatClient(AiClientOptions options) : AiClientBase(), IChatClient
{
    #region 属性
    /// <inheritdoc/>
    public override String Name { get; set; } = "Anthropic";

    /// <summary>Anthropic API 版本</summary>
    protected virtual String ApiVersion => "2023-06-01";

    /// <summary>连接选项</summary>
    protected readonly AiClientOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    #endregion

    #region 构造
    /// <summary>以 API 密钥和可选模型快速创建 Anthropic 客户端</summary>
    /// <param name="apiKey">API 密钥</param>
    /// <param name="model">默认模型编码，为空时由每次请求指定</param>
    /// <param name="endpoint">API 地址覆盖；为空时使用内置默认地址</param>
    public AnthropicChatClient(String apiKey, String? model = null, String? endpoint = null)
        : this(new AiClientOptions { ApiKey = apiKey, Model = model, Endpoint = endpoint }) { }
    #endregion

    #region IChatClient
    /// <summary>非流式对话完成</summary>
    /// <param name="request">对话请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>完整的对话响应</returns>
    public async Task<ChatResponse> GetResponseAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        request.Model ??= _options.Model;

        var model = request.Model;
        var startMs = Runtime.TickCount64;
        using var span = Tracer?.NewSpan($"chat:{model}", request.Messages?.FirstOrDefault()?.Content);
        try
        {
            var response = await ChatAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.Usage != null)
            {
                response.Usage.ElapsedMs = (Int32)(Runtime.TickCount64 - startMs);
                span?.Value = response.Usage.TotalTokens;
            }
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
    public async IAsyncEnumerable<ChatResponse> GetStreamingResponseAsync(ChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        request.Model ??= _options.Model;

        var model = request.Model;
        var startMs = Runtime.TickCount64;
        using var span = Tracer?.NewSpan($"chat:streaming:{model}", model);

        UsageDetails? lastUsage = null;
        await foreach (var chunk in ChatStreamAsync(request, cancellationToken).ConfigureAwait(false))
        {
            if (chunk.Usage != null)
            {
                lastUsage = chunk.Usage;
                lastUsage.ElapsedMs = (Int32)(Runtime.TickCount64 - startMs);
            }
            yield return chunk;
        }

        if (lastUsage != null) span?.Value = lastUsage.TotalTokens;
    }

    /// <summary>释放资源</summary>
    public void Dispose() { }
    #endregion

    #region 方法
    /// <summary>非流式对话</summary>
    private async Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken)
    {
        request.Stream = false;
        var body = BuildAnthropicRequest(request);

        var model = request.Model ?? "";
        var endpoint = _options.GetEndpoint(DefaultEndpoint).TrimEnd('/');
        var url = $"{endpoint}/v1/messages";

        var responseText = await PostAsync(url, body, request, _options, cancellationToken).ConfigureAwait(false);
        return ParseAnthropicResponse(responseText, model);
    }

    /// <summary>流式对话</summary>
    private async IAsyncEnumerable<ChatResponse> ChatStreamAsync(ChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        request.Stream = true;
        var body = BuildAnthropicRequest(request);

        var model = request.Model ?? "";
        var endpoint = _options.GetEndpoint(DefaultEndpoint).TrimEnd('/');
        var url = $"{endpoint}/v1/messages";

        using var httpResponse = await PostStreamAsync(url, body, request, _options, cancellationToken).ConfigureAwait(false);
        using var stream = await httpResponse.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        var lastEvent = "";
        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync().ConfigureAwait(false);
            if (line == null) break;

            if (line.StartsWith("event:"))
            {
                lastEvent = line.Substring(6).Trim();
                continue;
            }

            if (!line.StartsWith("data:")) continue;

            var data = line.Substring(5).Trim();
            if (data.Length == 0) continue;

            var chunk = ParseAnthropicStreamChunk(data, model, lastEvent);
            if (chunk != null)
                yield return chunk;
        }
    }
    #endregion

    #region 辅助
    /// <summary>构建 Anthropic 请求体</summary>
    /// <param name="request">请求</param>
    private static Object BuildAnthropicRequest(ChatRequest request)
    {
        var dic = new Dictionary<String, Object?>();
        dic["model"] = request.Model ?? "";

        // system 作为顶级字段，不放入 messages 数组
        String? systemContent = null;
        var messages = new List<Object>();
        foreach (var msg in request.Messages)
        {
            if (msg.Role.Equals("system", StringComparison.OrdinalIgnoreCase))
            {
                systemContent = msg.Content?.ToString();
                continue;
            }

            var role = msg.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase) ? "assistant" : "user";
            var m = new Dictionary<String, Object?> { ["role"] = role };

            // 工具结果消息
            if (msg.ToolCallId != null)
            {
                m["role"] = "user";
                m["content"] = new List<Object>
                {
                    new Dictionary<String, Object?>
                    {
                        ["type"] = "tool_result",
                        ["tool_use_id"] = msg.ToolCallId,
                        ["content"] = msg.Content?.ToString() ?? "",
                    }
                };
            }
            else if (msg.ToolCalls != null && msg.ToolCalls.Count > 0)
            {
                // assistant 带工具调用
                var contentBlocks = new List<Object>();
                if (msg.Content != null)
                    contentBlocks.Add(new Dictionary<String, Object> { ["type"] = "text", ["text"] = msg.Content.ToString()! });
                foreach (var tc in msg.ToolCalls)
                {
                    Object input = tc.Function?.Arguments != null
                        ? (JsonParser.Decode(tc.Function.Arguments) ?? new Dictionary<String, Object?>())
                        : new Dictionary<String, Object?>();
                    contentBlocks.Add(new Dictionary<String, Object?>
                    {
                        ["type"] = "tool_use",
                        ["id"] = tc.Id,
                        ["name"] = tc.Function?.Name ?? "",
                        ["input"] = input,
                    });
                }
                m["content"] = contentBlocks;
            }
            else
            {
                m["content"] = msg.Content;
            }

            messages.Add(m);
        }

        if (!String.IsNullOrEmpty(systemContent))
            dic["system"] = systemContent;
        dic["messages"] = messages;

        // max_tokens 在 Anthropic 中是必填项，默认 4096
        dic["max_tokens"] = request.MaxTokens ?? 4096;

        if (request.Temperature != null) dic["temperature"] = request.Temperature.Value;
        if (request.TopP != null) dic["top_p"] = request.TopP.Value;
        if (request.Stop != null && request.Stop.Count > 0) dic["stop_sequences"] = request.Stop;
        if (request.Stream) dic["stream"] = true;

        if (request.Tools != null && request.Tools.Count > 0)
        {
            var tools = new List<Object>();
            foreach (var tool in request.Tools)
            {
                if (tool.Function == null) continue;
                var fn = new Dictionary<String, Object?>
                {
                    ["name"] = tool.Function.Name,
                    ["description"] = tool.Function.Description,
                    ["input_schema"] = tool.Function.Parameters ?? (Object)new Dictionary<String, Object> { ["type"] = "object" },
                };
                tools.Add(fn);
            }
            dic["tools"] = tools;
        }

        return dic;
    }

    /// <summary>解析 Anthropic 非流式响应</summary>
    private static ChatResponse ParseAnthropicResponse(String json, String model)
    {
        var dic = JsonParser.Decode(json);
        if (dic == null) throw new InvalidOperationException("无法解析 Anthropic 响应");

        var response = new ChatResponse
        {
            Id = dic["id"] as String,
            Object = "chat.completion",
            Model = dic["model"] as String ?? model,
        };

        String? contentText = null;
        String? reasoningText = null;
        String? finishReason = null;
        List<ToolCall>? toolCalls = null;

        if (dic["content"] is IList<Object> contentList)
        {
            var textParts = new List<String>();
            var reasoningParts = new List<String>();

            foreach (var block in contentList)
            {
                if (block is not IDictionary<String, Object> blockDic) continue;
                var blockType = blockDic["type"] as String;
                if (blockType == "text")
                    textParts.Add(blockDic["text"] as String ?? "");
                else if (blockType == "thinking")
                    reasoningParts.Add(blockDic["thinking"] as String ?? "");
                else if (blockType == "tool_use")
                {
                    // 解析工具调用块 → ToolCall 对象（Anthropic input 字段为 object，需序列化为 JSON string）
                    toolCalls ??= [];
                    var inputRaw = blockDic["input"];
                    toolCalls.Add(new ToolCall
                    {
                        Id = blockDic["id"] as String ?? "",
                        Type = "function",
                        Function = new FunctionCall
                        {
                            Name = blockDic["name"] as String ?? "",
                            Arguments = inputRaw is IDictionary<String, Object> inputDic
                                ? inputDic.ToJson()
                                : inputRaw as String ?? "{}",
                        },
                    });
                }
            }

            contentText = textParts.Count > 0 ? String.Join("", textParts) : null;
            reasoningText = reasoningParts.Count > 0 ? String.Join("", reasoningParts) : null;
        }

        var stopReason = dic["stop_reason"] as String;
        finishReason = MapStopReason(stopReason);
        var choice = response.Add(contentText, reasoningText, finishReason);

        // 将工具调用挂载到 Message
        if (toolCalls != null && toolCalls.Count > 0)
        {
            choice.Message ??= new ChatMessage { Role = "assistant" };
            choice.Message.ToolCalls = toolCalls;
        }

        if (dic["usage"] is IDictionary<String, Object> usageDic)
        {
            response.Usage = new UsageDetails
            {
                InputTokens = usageDic["input_tokens"].ToInt(),
                OutputTokens = usageDic["output_tokens"].ToInt(),
                TotalTokens = usageDic["input_tokens"].ToInt() + usageDic["output_tokens"].ToInt(),
            };
        }

        return response;
    }

    /// <summary>解析 Anthropic 流式 chunk</summary>
    private static ChatResponse? ParseAnthropicStreamChunk(String data, String model, String lastEvent)
    {
        var dic = JsonParser.Decode(data);
        if (dic == null) return null;

        var response = new ChatResponse
        {
            Model = model,
            Object = "chat.completion.chunk",
        };

        switch (lastEvent)
        {
            case "message_start":
                // 初始事件，包含 usage 的 input_tokens
                if (dic["message"] is IDictionary<String, Object> msgDic &&
                    msgDic["usage"] is IDictionary<String, Object> usageDic)
                {
                    response.Usage = new UsageDetails { InputTokens = usageDic["input_tokens"].ToInt() };
                }
                response.AddDelta(null, null, null);
                return response;

            case "content_block_delta":
                if (dic["delta"] is IDictionary<String, Object> deltaDic)
                {
                    var deltaType = deltaDic["type"] as String;
                    if (deltaType == "text_delta")
                    {
                        response.AddDelta(deltaDic["text"] as String, null, null);
                        return response;
                    }
                    if (deltaType == "thinking_delta")
                    {
                        response.AddDelta(null, deltaDic["thinking"] as String, null);
                        return response;
                    }
                }
                return null;

            case "message_delta":
                if (dic["delta"] is IDictionary<String, Object> msgDeltaDic)
                {
                    var stopReason = msgDeltaDic["stop_reason"] as String;
                    var finishReason = MapStopReason(stopReason);
                    response.AddDelta(null, null, finishReason);
                }
                if (dic["usage"] is IDictionary<String, Object> deltaUsageDic)
                {
                    response.Usage = new UsageDetails { OutputTokens = deltaUsageDic["output_tokens"].ToInt() };
                }
                return response;

            case "message_stop":
                return null;

            default:
                return null;
        }
    }

    /// <summary>映射 Anthropic stop_reason 到标准格式</summary>
    private static String? MapStopReason(String? stopReason) => stopReason switch
    {
        "end_turn" => "stop",
        "max_tokens" => "length",
        "tool_use" => "tool_calls",
        null => null,
        _ => stopReason,
    };

    /// <summary>设置 Anthropic 认证请求头</summary>
    protected override void SetHeaders(HttpRequestMessage request, ChatRequest? chatRequest, AiClientOptions options)
    {
        if (!String.IsNullOrEmpty(options.ApiKey))
            request.Headers.Add("x-api-key", options.ApiKey);
        request.Headers.Add("anthropic-version", ApiVersion);
    }
    #endregion
}
