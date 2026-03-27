using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using NewLife.AI.Models;
using NewLife.AI.Providers;
using NewLife.Serialization;

namespace NewLife.AI.Clients;

/// <summary>Ollama 对话客户端。使用 Ollama 原生 /api/chat 接口，支持本地开源模型</summary>
/// <remarks>
/// 使用原生接口而非 OpenAI 兼容接口，优势：
/// <list type="bullet">
/// <item>通过 think 参数可靠关闭 qwen3 等模型的思考模式</item>
/// <item>响应格式输出为 NDJSON（非 SSE），更符合 Ollama 原生流式格式</item>
/// <item>原生思考字段为 thinking（区别于兼容模式的 reasoning）</item>
/// </list>
/// 官方文档：https://github.com/ollama/ollama/blob/main/docs/api.md
/// </remarks>
/// <remarks>用连接选项初始化 Ollama 客户端</remarks>
/// <param name="options">连接选项（Endpoint、ApiKey、Model 等）</param>
[AiClient("Ollama", "本地Ollama", "http://localhost:11434", Protocol = "Ollama", Description = "本地运行开源大模型，支持 Llama/Qwen/Gemma 等")]
[AiClientModel("qwen3.5:0.8b", "Qwen 3.5 0.8B", Thinking = true)]
[AiClientModel("llama3.3", "Llama 3.3")]
[AiClientModel("deepseek-r1", "DeepSeek R1", Thinking = true, FunctionCalling = false)]
[AiClientModel("phi4", "Phi-4")]
public class OllamaChatClient(AiClientOptions options) : AiClientBase(), IChatClient
{
    #region 属性
    /// <inheritdoc/>
    public override String Name { get; set; } = "本地Ollama";

    /// <summary>连接选项</summary>
    protected readonly AiClientOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    #endregion

    #region 构造
    /// <summary>以 API 密钥和可选模型快速创建 Ollama 客户端</summary>
    /// <param name="apiKey">API 密钥；本地部署可传 null 或空串</param>
    /// <param name="model">默认模型编码，为空时由每次请求指定</param>
    /// <param name="endpoint">API 地址覆盖；为空时使用默认 http://localhost:11434</param>
    public OllamaChatClient(String? apiKey, String? model = null, String? endpoint = null)
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

        if (request.Messages == null || request.Messages.Count == 0)
            throw new ArgumentException("消息列表不能为空", nameof(request));

        var model = request.Model;
        var startMs = Runtime.TickCount64;
        using var span = Tracer?.NewSpan($"chat:{model}", request.Messages?.FirstOrDefault()?.Content);
        try
        {
            var endpoint = _options.GetEndpoint(DefaultEndpoint).TrimEnd('/');
            var url = endpoint + "/api/chat";
            var body = BuildOllamaBody(request, stream: false);
            var json = await PostAsync(url, body, request, _options, cancellationToken).ConfigureAwait(false);
            var response = ParseOllamaResponse(json);
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
        var model = request.Model ??= _options.Model;

        var startMs = Runtime.TickCount64;
        var endpoint = _options.GetEndpoint(DefaultEndpoint).TrimEnd('/');
        var url = endpoint + "/api/chat";
        var body = BuildOllamaBody(request, stream: true);

        using var span = Tracer?.NewSpan($"chat:streaming:{model}", model);

        UsageDetails? lastUsage = null;
        using var resp = await PostStreamAsync(url, body, request, _options, cancellationToken).ConfigureAwait(false);
        using var stream = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync().ConfigureAwait(false);
            if (String.IsNullOrEmpty(line)) continue;

            var chunk = ParseOllamaChunk(line);
            if (chunk != null)
            {
                if (chunk.Usage != null)
                {
                    lastUsage = chunk.Usage;
                    lastUsage.ElapsedMs = (Int32)(Runtime.TickCount64 - startMs);
                }
                yield return chunk;
            }
        }

        if (lastUsage != null) span?.Value = lastUsage.TotalTokens;
    }

    /// <summary>释放资源</summary>
    public void Dispose() { }
    #endregion

    #region 方法
    /// <summary>获取本地已安装的模型列表</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>模型列表，服务不可用时返回 null</returns>
    public virtual async Task<OllamaTagsResponse?> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        var url = _options.GetEndpoint(DefaultEndpoint).TrimEnd('/') + "/api/tags";
        var json = await TryGetAsync(url, _options, cancellationToken).ConfigureAwait(false);
        return json?.ToJsonEntity<OllamaTagsResponse>();
    }

    /// <summary>获取运行中的模型列表</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>运行中模型列表，服务不可用时返回 null</returns>
    public virtual async Task<OllamaPsResponse?> ListRunningAsync(CancellationToken cancellationToken = default)
    {
        var url = _options.GetEndpoint(DefaultEndpoint).TrimEnd('/') + "/api/ps";
        var json = await TryGetAsync(url, _options, cancellationToken).ConfigureAwait(false);
        return json?.ToJsonEntity<OllamaPsResponse>();
    }

    /// <summary>获取模型详细信息</summary>
    /// <param name="modelName">模型名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>模型详情，服务不可用时返回 null</returns>
    public virtual async Task<OllamaShowResponse?> ShowModelAsync(String modelName, CancellationToken cancellationToken = default)
    {
        if (modelName == null) throw new ArgumentNullException(nameof(modelName));

        var url = _options.GetEndpoint(DefaultEndpoint).TrimEnd('/') + "/api/show";
        var body = new Dictionary<String, Object> { ["model"] = modelName };
        var json = await TryPostAsync(url, body, _options, cancellationToken).ConfigureAwait(false);
        return json?.ToJsonEntity<OllamaShowResponse>();
    }

    /// <summary>获取 Ollama 版本信息</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>版本号字符串，无法连接时返回 null</returns>
    public virtual async Task<String?> GetVersionAsync(CancellationToken cancellationToken = default)
    {
        var url = _options.GetEndpoint(DefaultEndpoint).TrimEnd('/') + "/api/version";
        try
        {
            var json = await GetAsync(url, null, _options, cancellationToken).ConfigureAwait(false);
            var dic = JsonParser.Decode(json);
            return dic?["version"] as String;
        }
        catch { return null; }
    }

    /// <summary>生成嵌入向量</summary>
    /// <param name="request">嵌入请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>嵌入响应，服务不可用时返回 null</returns>
    public virtual async Task<OllamaEmbedResponse?> EmbedAsync(OllamaEmbedRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var url = _options.GetEndpoint(DefaultEndpoint).TrimEnd('/') + "/api/embed";
        var dic = new Dictionary<String, Object>();
        if (request.Model != null) dic["model"] = request.Model;
        if (request.Input != null) dic["input"] = request.Input;
        if (request.Truncate != null) dic["truncate"] = request.Truncate.Value;
        if (request.Dimensions != null) dic["dimensions"] = request.Dimensions.Value;
        if (request.KeepAlive != null) dic["keep_alive"] = request.KeepAlive;

        var json = await PostAsync(url, dic, null, _options, cancellationToken).ConfigureAwait(false);
        return json.ToJsonEntity<OllamaEmbedResponse>();
    }

    /// <summary>拉取（下载）模型。等待完成后返回最终状态</summary>
    /// <param name="modelName">模型名称，如 qwen3.5:0.8b</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>拉取状态，status 为 "success" 表示成功</returns>
    public virtual async Task<OllamaPullStatus?> PullModelAsync(String modelName, CancellationToken cancellationToken = default)
    {
        if (modelName == null) throw new ArgumentNullException(nameof(modelName));

        var url = _options.GetEndpoint(DefaultEndpoint).TrimEnd('/') + "/api/pull";
        var body = new Dictionary<String, Object> { ["model"] = modelName, ["stream"] = false };

        // 拉取模型可能耗时数分钟，使用 30 分钟超时
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMinutes(30));
        var json = await PostAsync(url, body, null, _options, cts.Token).ConfigureAwait(false);
        return json.ToJsonEntity<OllamaPullStatus>();
    }
    #endregion

    #region 辅助
    /// <inheritdoc/>
    protected override void SetHeaders(HttpRequestMessage request, ChatRequest? chatRequest, AiClientOptions options)
    {
        // Ollama 默认不需要 API Key，但如果用户配置了则传递
        if (!String.IsNullOrEmpty(options.ApiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
    }

    /// <summary>构建 Ollama 原生请求体（JSON 字符串）</summary>
    private static String BuildOllamaBody(ChatRequest request, Boolean stream)
    {
        var dic = new Dictionary<String, Object>
        {
            ["model"] = request.Model ?? "",
            ["stream"] = stream,
        };

        // think 参数：显式 true/false 时才传给 Ollama；null（Auto）时不传，由模型自身决定
        // 注意：不能用 ?? false 兜底，否则 Auto 模式会意外关闭思考
        if (request.EnableThinking.HasValue)
            dic["think"] = request.EnableThinking.Value;

        var messages = new List<Object>();
        foreach (var msg in request.Messages)
        {
            var m = new Dictionary<String, Object>
            {
                ["role"] = msg.Role,
                ["content"] = msg.Content ?? "",
            };

            if (msg.ToolCalls != null && msg.ToolCalls.Count > 0)
            {
                var toolCalls = new List<Object>();
                foreach (var tc in msg.ToolCalls)
                {
                    var tcDic = new Dictionary<String, Object> { ["id"] = tc.Id, ["type"] = tc.Type };
                    if (tc.Function != null)
                    {
                        tcDic["function"] = new Dictionary<String, Object?>
                        {
                            ["name"] = tc.Function.Name,
                            ["arguments"] = String.IsNullOrEmpty(tc.Function.Arguments) ? "{}" : tc.Function.Arguments,
                        };
                    }
                    toolCalls.Add(tcDic);
                }
                m["tool_calls"] = toolCalls;
            }

            messages.Add(m);
        }
        dic["messages"] = messages;

        // Ollama 的生成参数放在 options 子对象里
        var opts = new Dictionary<String, Object>();
        if (request.MaxTokens != null) opts["num_predict"] = request.MaxTokens.Value;
        if (request.Temperature != null) opts["temperature"] = request.Temperature.Value;
        if (request.TopP != null) opts["top_p"] = request.TopP.Value;
        if (request.Stop != null && request.Stop.Count > 0) opts["stop"] = request.Stop;
        // 携带工具时限制思考 token 上限，防止 thinking 内容耗尽 context 导致工具调用 JSON 被截断
        if (request.Tools != null && request.Tools.Count > 0 && !opts.ContainsKey("num_predict"))
            opts["num_predict"] = 4096;
        if (opts.Count > 0) dic["options"] = opts;

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

        return dic.ToJson();
    }

    /// <summary>解析 Ollama 非流式响应</summary>
    private static ChatResponse ParseOllamaResponse(String json)
    {
        var dic = JsonParser.Decode(json);
        if (dic == null) throw new InvalidOperationException("无法解析 Ollama 响应");

        var response = new ChatResponse
        {
            Id = dic["created_at"] is Object createdAt ? $"ollama-{createdAt}" : $"ollama-{DateTime.UtcNow.Ticks}",
            Object = "chat.completion",
            Model = dic["model"] as String,
        };

        var msgObj = dic["message"];
        if (msgObj != null)
        {
            var msg = ParseOllamaMessage(msgObj as IDictionary<String, Object>);
            var doneReason = dic["done_reason"] as String;
            response.Messages = [new ChatChoice { Index = 0, Message = msg, FinishReason = doneReason }];
        }

        var promptTokens = dic["prompt_eval_count"].ToInt();
        var completionTokens = dic["eval_count"].ToInt();
        if (promptTokens > 0 || completionTokens > 0)
        {
            response.Usage = new UsageDetails
            {
                InputTokens = promptTokens,
                OutputTokens = completionTokens,
                TotalTokens = promptTokens + completionTokens,
            };
        }

        return response;
    }

    /// <summary>解析 Ollama 流式 NDJSON 单行 chunk</summary>
    private static ChatResponse? ParseOllamaChunk(String json)
    {
        var dic = JsonParser.Decode(json);
        if (dic == null) return null;

        var isDone = dic["done"].ToBoolean();
        var chunk = new ChatResponse
        {
            Id = dic["created_at"] is Object createdAt ? $"ollama-{createdAt}" : $"ollama-{DateTime.UtcNow.Ticks}",
            Object = "chat.completion.chunk",
            Model = dic["model"] as String,
        };

        String? finishReason = null;
        if (isDone) finishReason = dic["done_reason"] as String ?? "stop";

        var msgObj = dic["message"];
        if (msgObj != null)
        {
            var msg = ParseOllamaMessage(msgObj as IDictionary<String, Object>);
            chunk.Messages = [new ChatChoice { Index = 0, Delta = msg, FinishReason = finishReason }];
        }
        else if (isDone)
        {
            chunk.Messages = [new ChatChoice { Index = 0, Delta = new ChatMessage { Role = "assistant" }, FinishReason = finishReason }];
        }

        if (isDone)
        {
            var promptTokens = dic["prompt_eval_count"].ToInt();
            var completionTokens = dic["eval_count"].ToInt();
            if (promptTokens > 0 || completionTokens > 0)
            {
                chunk.Usage = new UsageDetails
                {
                    InputTokens = promptTokens,
                    OutputTokens = completionTokens,
                    TotalTokens = promptTokens + completionTokens,
                };
            }
        }

        return chunk;
    }

    /// <summary>解析 Ollama 原生消息对象</summary>
    private static ChatMessage? ParseOllamaMessage(IDictionary<String, Object>? dic)
    {
        if (dic == null) return null;

        var msg = new ChatMessage
        {
            Role = dic["role"] as String ?? "assistant",
            Content = dic["content"],
            // Ollama 原生思考字段为 thinking（与兼容模式的 reasoning 不同）
            ReasoningContent = dic["thinking"] as String,
        };

        if (dic["tool_calls"] is IList<Object> tcList)
        {
            var toolCalls = new List<ToolCall>();
            foreach (var tcItem in tcList)
            {
                if (tcItem is not IDictionary<String, Object> tcDic) continue;

                var tc = new ToolCall
                {
                    Id = tcDic["id"] as String ?? "",
                    Type = tcDic["type"] as String ?? "function",
                };

                if (tcDic["function"] is IDictionary<String, Object> fnDic)
                {
                    var argsRaw = fnDic["arguments"];
                    tc.Function = new FunctionCall
                    {
                        Name = fnDic["name"] as String ?? "",
                        Arguments = argsRaw is String s ? s : argsRaw?.ToJson(),
                    };
                }

                toolCalls.Add(tc);
            }
            msg.ToolCalls = toolCalls;
        }

        return msg;
    }
    #endregion
}
