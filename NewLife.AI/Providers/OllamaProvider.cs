using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using NewLife.AI.Models;
using NewLife.Serialization;

namespace NewLife.AI.Providers;

/// <summary>Ollama 服务商。本地部署和运行开源大模型</summary>
/// <remarks>
/// Ollama 提供原生 /api/chat 和 OpenAI 兼容两套 API。
/// 此实现使用原生 /api/chat 接口，可通过 think:false 可靠地关闭 qwen3 等模型的思考模式，
/// 避免思考 token 占满 max_tokens 导致正文内容为空的问题。
/// 官方文档：https://github.com/ollama/ollama/blob/main/docs/api.md
/// </remarks>
public class OllamaProvider : OpenAiProvider
{
    #region 属性
    /// <summary>服务商编码</summary>
    public override String Code => "Ollama";

    /// <summary>服务商名称</summary>
    public override String Name => "本地Ollama";

    /// <summary>服务商描述</summary>
    public override String? Description => "本地运行开源大模型，支持 Llama/Qwen/Gemma 等";

    /// <summary>默认 API 地址</summary>
    public override String DefaultEndpoint => "http://localhost:11434";

    /// <summary>主流模型列表。Ollama 本地常用开源模型（能力取决于实际加载的模型）</summary>
    public override AiModelInfo[] Models { get; } =
    [
        new("qwen3.5:0.8b", "Qwen 3.5 0.8B", new(true,  false, false, true)),
        new("llama3.3",     "Llama 3.3",    new(false, false, false, true)),
        new("deepseek-r1",  "DeepSeek R1",  new(true,  false, false, false)),
        new("phi4",         "Phi-4",        new(false, false, false, true)),
    ];
    #endregion

    #region 方法
    /// <summary>非流式对话。使用 Ollama 原生 /api/chat 接口</summary>
    /// <param name="request">对话请求</param>
    /// <param name="options">连接选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public override async Task<ChatResponse> ChatAsync(ChatCompletionRequest request, AiProviderOptions options, CancellationToken cancellationToken = default)
    {
        if (request.Messages == null || request.Messages.Count == 0)
            throw new ArgumentException("消息列表不能为空", nameof(request));

        var url = (String.IsNullOrEmpty(options.Endpoint) ? DefaultEndpoint : options.Endpoint.TrimEnd('/')) + "/api/chat";
        var body = BuildOllamaBody(request, stream: false);
        var json = await PostAsync(url, body, options, cancellationToken).ConfigureAwait(false);
        return ParseOllamaResponse(json);
    }

    /// <summary>流式对话。使用 Ollama 原生 /api/chat 接口（NDJSON 格式）</summary>
    /// <param name="request">对话请求</param>
    /// <param name="options">连接选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public override async IAsyncEnumerable<ChatResponse> ChatStreamAsync(ChatCompletionRequest request, AiProviderOptions options, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var url = (String.IsNullOrEmpty(options.Endpoint) ? DefaultEndpoint : options.Endpoint.TrimEnd('/')) + "/api/chat";
        var body = BuildOllamaBody(request, stream: true);
        using var resp = await PostStreamAsync(url, body, options, cancellationToken).ConfigureAwait(false);
        using var stream = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync().ConfigureAwait(false);
            if (String.IsNullOrEmpty(line)) continue;

            var chunk = ParseOllamaChunk(line);
            if (chunk != null)
                yield return chunk;
        }
    }

    /// <summary>设置请求头。Ollama 默认不需要认证</summary>
    /// <param name="request">HTTP 请求</param>
    /// <param name="options">选项</param>
    protected override void SetHeaders(HttpRequestMessage request, AiProviderOptions options)
    {
        // Ollama 默认不需要 API Key，但如果用户配置了则传递
        if (!String.IsNullOrEmpty(options.ApiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
    }

    /// <summary>获取本地已安装的模型列表</summary>
    /// <param name="options">连接选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>模型列表</returns>
    public new virtual async Task<OllamaTagsResponse?> ListModelsAsync(AiProviderOptions options, CancellationToken cancellationToken = default)
    {
        var url = (String.IsNullOrEmpty(options.Endpoint) ? DefaultEndpoint : options.Endpoint.TrimEnd('/')) + "/api/tags";
        var json = await TryGetAsync(url, options, cancellationToken).ConfigureAwait(false);
        return json?.ToJsonEntity<OllamaTagsResponse>();
    }

    /// <summary>获取运行中的模型列表</summary>
    /// <param name="options">连接选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>运行中模型列表</returns>
    public virtual async Task<OllamaPsResponse?> ListRunningAsync(AiProviderOptions options, CancellationToken cancellationToken = default)
    {
        var url = (String.IsNullOrEmpty(options.Endpoint) ? DefaultEndpoint : options.Endpoint.TrimEnd('/')) + "/api/ps";
        var json = await TryGetAsync(url, options, cancellationToken).ConfigureAwait(false);
        return json?.ToJsonEntity<OllamaPsResponse>();
    }

    /// <summary>获取模型详细信息</summary>
    /// <param name="modelName">模型名称</param>
    /// <param name="options">连接选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>模型详情</returns>
    public virtual async Task<OllamaShowResponse?> ShowModelAsync(String modelName, AiProviderOptions options, CancellationToken cancellationToken = default)
    {
        if (modelName == null) throw new ArgumentNullException(nameof(modelName));

        var url = (String.IsNullOrEmpty(options.Endpoint) ? DefaultEndpoint : options.Endpoint.TrimEnd('/')) + "/api/show";
        var body = new Dictionary<String, Object> { ["model"] = modelName };
        var json = await TryPostAsync(url, body, options, cancellationToken).ConfigureAwait(false);
        return json?.ToJsonEntity<OllamaShowResponse>();
    }

    /// <summary>获取 Ollama 版本信息</summary>
    /// <param name="options">连接选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>版本号字符串，无法连接时返回 null</returns>
    public virtual async Task<String?> GetVersionAsync(AiProviderOptions options, CancellationToken cancellationToken = default)
    {
        var url = (String.IsNullOrEmpty(options.Endpoint) ? DefaultEndpoint : options.Endpoint.TrimEnd('/')) + "/api/version";
        try
        {
            var json = await GetAsync(url, options, cancellationToken).ConfigureAwait(false);
            var dic = JsonParser.Decode(json);
            return dic?["version"] as String;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>生成嵌入向量</summary>
    /// <param name="request">嵌入请求</param>
    /// <param name="options">连接选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>嵌入响应</returns>
    public virtual async Task<OllamaEmbedResponse?> EmbedAsync(OllamaEmbedRequest request, AiProviderOptions options, CancellationToken cancellationToken = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var url = (options.Endpoint.IsNullOrEmpty() ? DefaultEndpoint : options.Endpoint.TrimEnd('/')) + "/api/embed";

        var dic = new Dictionary<String, Object>();
        if (request.Model != null) dic["model"] = request.Model;
        if (request.Input != null) dic["input"] = request.Input;
        if (request.Truncate != null) dic["truncate"] = request.Truncate.Value;
        if (request.Dimensions != null) dic["dimensions"] = request.Dimensions.Value;
        if (request.KeepAlive != null) dic["keep_alive"] = request.KeepAlive;

        var json = await PostAsync(url, dic, options, cancellationToken).ConfigureAwait(false);
        return json.ToJsonEntity<OllamaEmbedResponse>();
    }

    /// <summary>拉取（下载）模型。等待完成后返回最终状态</summary>
    /// <param name="modelName">模型名称，如 qwen3.5:0.8b</param>
    /// <param name="options">连接选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>拉取状态，status 为 "success" 表示成功</returns>
    public virtual async Task<OllamaPullStatus?> PullModelAsync(String modelName, AiProviderOptions options, CancellationToken cancellationToken = default)
    {
        if (modelName == null) throw new ArgumentNullException(nameof(modelName));

        var url = (options.Endpoint.IsNullOrEmpty() ? DefaultEndpoint : options.Endpoint.TrimEnd('/')) + "/api/pull";
        // stream:false 让 Ollama 等待完成后返回单条 JSON，避免处理 NDJSON 流
        var body = new Dictionary<String, Object> { ["model"] = modelName, ["stream"] = false };
        // 拉取模型可能耗时数分钟，使用 30 分钟超时
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMinutes(30));

        var json = await PostAsync(url, body, options, cts.Token).ConfigureAwait(false);
        return json.ToJsonEntity<OllamaPullStatus>();
    }
    #endregion

    #region 辅助
    /// <summary>构建 Ollama 原生 /api/chat 请求体</summary>
    /// <param name="request">对话请求</param>
    /// <param name="stream">是否流式</param>
    /// <returns>JSON 字符串</returns>
    private static String BuildOllamaBody(ChatCompletionRequest request, Boolean stream)
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

        // 构建消息列表
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

        // 工具定义
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

    /// <summary>解析 Ollama 原生 /api/chat 非流式响应</summary>
    /// <param name="json">JSON 字符串</param>
    /// <returns></returns>
    private static ChatResponse ParseOllamaResponse(String json)
    {
        var dic = JsonParser.Decode(json);
        if (dic == null) throw new InvalidOperationException("无法解析 Ollama 响应");

        var response = new ChatResponse
        {
            // Ollama 原生响应无 id 字段，用 created_at 生成
            Id = dic["created_at"] is Object createdAt ? $"ollama-{createdAt}" : $"ollama-{DateTime.UtcNow.Ticks}",
            Object = "chat.completion",
            Model = dic["model"] as String,
        };

        // 解析消息并构造 choices
        var msgObj = dic["message"];
        if (msgObj != null)
        {
            var msg = ParseOllamaMessage(msgObj as IDictionary<String, Object>);
            var doneReason = dic["done_reason"] as String;
            response.Messages = [new ChatChoice { Index = 0, Message = msg, FinishReason = doneReason, }];
        }

        // 解析 usage：prompt_eval_count = 输入 token，eval_count = 输出 token
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
    /// <param name="json">单行 JSON</param>
    /// <returns>解析出的响应块，无效行返回 null</returns>
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
        if (isDone)
            finishReason = dic["done_reason"] as String ?? "stop";

        // 每个 chunk 都有 message 字段（含增量内容）
        var msgObj = dic["message"];
        if (msgObj != null)
        {
            var msg = ParseOllamaMessage(msgObj as IDictionary<String, Object>);
            chunk.Messages = [new ChatChoice { Index = 0, Delta = msg, FinishReason = finishReason, }];
        }
        else if (isDone)
        {
            chunk.Messages =
            [
                new ChatChoice { Index = 0, Delta = new ChatMessage { Role = "assistant" }, FinishReason = finishReason }
            ];
        }

        // 最终 done chunk 包含 usage 统计
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
    /// <param name="dic">消息字典</param>
    /// <returns></returns>
    private static ChatMessage? ParseOllamaMessage(IDictionary<String, Object>? dic)
    {
        if (dic == null) return null;

        var msg = new ChatMessage
        {
            Role = dic["role"] as String ?? "assistant",
            Content = dic["content"],

            // Ollama 原生思考字段为 thinking（与 OpenAI 兼容模式的 reasoning 不同）
            ReasoningContent = dic["thinking"] as String
        };

        // 工具调用
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
                    tc.Function = new FunctionCall
                    {
                        Name = fnDic["name"] as String ?? "",
                        Arguments = fnDic["arguments"]?.ToJson(),
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
