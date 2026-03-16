using System.Collections.Generic;
using System.IO;
using System.Net.Http;
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
    public override String Name => "Ollama";

    /// <summary>服务商描述</summary>
    public override String? Description => "本地运行开源大模型，支持 Llama/Qwen/Gemma 等";

    /// <summary>默认 API 地址</summary>
    public override String DefaultEndpoint => "http://localhost:11434";

    /// <summary>主流模型列表。Ollama 本地常用开源模型（能力取决于实际加载的模型）</summary>
    public override AiModelInfo[] Models { get; } =
    [
        new("llama3.3",     "Llama 3.3",    new(false, false, false, true)),
        new("qwen2.5",      "Qwen 2.5",     new(false, false, false, true)),
        new("deepseek-r1",  "DeepSeek R1",  new(true,  false, false, false)),
        new("phi4",         "Phi-4",        new(false, false, false, true)),
    ];

    private static readonly HttpClient _httpClient = CreateHttpClient();
    #endregion

    #region 方法
    /// <summary>非流式对话。使用 Ollama 原生 /api/chat 接口</summary>
    /// <param name="request">对话请求</param>
    /// <param name="options">连接选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public override async Task<ChatCompletionResponse> ChatAsync(ChatCompletionRequest request, AiProviderOptions options, CancellationToken cancellationToken = default)
    {
        if (request.Messages == null || request.Messages.Count == 0)
            throw new ArgumentException("消息列表不能为空", nameof(request));

        var endpoint = String.IsNullOrEmpty(options.Endpoint) ? DefaultEndpoint : options.Endpoint.TrimEnd('/');
        var url = $"{endpoint}/api/chat";

        var body = BuildOllamaBody(request, stream: false);
        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        SetHeaders(req, options);

        var resp = await _httpClient.SendAsync(req, cancellationToken).ConfigureAwait(false);
        var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException($"Ollama 请求失败 [{(Int32)resp.StatusCode}]: {json}");

        return ParseOllamaResponse(json);
    }

    /// <summary>流式对话。使用 Ollama 原生 /api/chat 接口（NDJSON 格式）</summary>
    /// <param name="request">对话请求</param>
    /// <param name="options">连接选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public override async IAsyncEnumerable<ChatCompletionResponse> ChatStreamAsync(ChatCompletionRequest request, AiProviderOptions options, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var endpoint = String.IsNullOrEmpty(options.Endpoint) ? DefaultEndpoint : options.Endpoint.TrimEnd('/');
        var url = $"{endpoint}/api/chat";

        var body = BuildOllamaBody(request, stream: true);
        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        SetHeaders(req, options);

        var resp = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

        if (!resp.IsSuccessStatusCode)
        {
            var errBody = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            throw new HttpRequestException($"Ollama 请求失败 [{(Int32)resp.StatusCode}]: {errBody}");
        }

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
            // think:false 默认关闭思考模式，防止 qwen3 等模型的思考 token 占满 max_tokens
            ["think"] = request.EnableThinking ?? false,
        };

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

        // Ollama 的生成参数放在 options 子对象里
        var opts = new Dictionary<String, Object>();
        if (request.MaxTokens != null) opts["num_predict"] = request.MaxTokens.Value;
        if (request.Temperature != null) opts["temperature"] = request.Temperature.Value;
        if (request.TopP != null) opts["top_p"] = request.TopP.Value;
        if (request.Stop != null && request.Stop.Count > 0) opts["stop"] = request.Stop;
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
    private static ChatCompletionResponse ParseOllamaResponse(String json)
    {
        var dic = JsonParser.Decode(json);
        if (dic == null) throw new InvalidOperationException("无法解析 Ollama 响应");

        var response = new ChatCompletionResponse
        {
            // Ollama 原生响应无 id 字段，用 created_at 生成
            Id = dic.TryGetValue("created_at", out var createdAt) ? $"ollama-{createdAt}" : $"ollama-{DateTime.UtcNow.Ticks}",
            Object = "chat.completion",
            Model = dic.TryGetValue("model", out var model) ? model as String : null,
        };

        // 解析消息并构造 choices
        if (dic.TryGetValue("message", out var msgObj))
        {
            var msg = ParseOllamaMessage(msgObj as IDictionary<String, Object>);
            var doneReason = dic.TryGetValue("done_reason", out var dr) ? dr as String : null;
            response.Choices =
            [
                new ChatChoice
                {
                    Index = 0,
                    Message = msg,
                    FinishReason = doneReason,
                }
            ];
        }

        // 解析 usage：prompt_eval_count = 输入 token，eval_count = 输出 token
        var promptTokens = dic.TryGetValue("prompt_eval_count", out var pec) ? pec.ToInt() : 0;
        var completionTokens = dic.TryGetValue("eval_count", out var ec) ? ec.ToInt() : 0;
        if (promptTokens > 0 || completionTokens > 0)
        {
            response.Usage = new ChatUsage
            {
                PromptTokens = promptTokens,
                CompletionTokens = completionTokens,
                TotalTokens = promptTokens + completionTokens,
            };
        }

        return response;
    }

    /// <summary>解析 Ollama 流式 NDJSON 单行 chunk</summary>
    /// <param name="json">单行 JSON</param>
    /// <returns>解析出的响应块，无效行返回 null</returns>
    private static ChatCompletionResponse? ParseOllamaChunk(String json)
    {
        var dic = JsonParser.Decode(json);
        if (dic == null) return null;

        var isDone = dic.TryGetValue("done", out var doneObj) && doneObj.ToBoolean();
        var chunk = new ChatCompletionResponse
        {
            Id = dic.TryGetValue("created_at", out var createdAt) ? $"ollama-{createdAt}" : $"ollama-{DateTime.UtcNow.Ticks}",
            Object = "chat.completion.chunk",
            Model = dic.TryGetValue("model", out var model) ? model as String : null,
        };

        String? finishReason = null;
        if (isDone)
            finishReason = dic.TryGetValue("done_reason", out var dr) ? dr as String : "stop";

        // 每个 chunk 都有 message 字段（含增量内容）
        if (dic.TryGetValue("message", out var msgObj))
        {
            var msg = ParseOllamaMessage(msgObj as IDictionary<String, Object>);
            chunk.Choices =
            [
                new ChatChoice
                {
                    Index = 0,
                    Delta = msg,
                    FinishReason = finishReason,
                }
            ];
        }
        else if (isDone)
        {
            chunk.Choices =
            [
                new ChatChoice { Index = 0, Delta = new ChatMessage { Role = "assistant" }, FinishReason = finishReason }
            ];
        }

        // 最终 done chunk 包含 usage 统计
        if (isDone)
        {
            var promptTokens = dic.TryGetValue("prompt_eval_count", out var pec) ? pec.ToInt() : 0;
            var completionTokens = dic.TryGetValue("eval_count", out var ec) ? ec.ToInt() : 0;
            if (promptTokens > 0 || completionTokens > 0)
            {
                chunk.Usage = new ChatUsage
                {
                    PromptTokens = promptTokens,
                    CompletionTokens = completionTokens,
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
            Role = dic.TryGetValue("role", out var role) ? role as String ?? "assistant" : "assistant",
        };

        if (dic.TryGetValue("content", out var content))
            msg.Content = content;

        // Ollama 原生思考字段为 thinking（与 OpenAI 兼容模式的 reasoning 不同）
        if (dic.TryGetValue("thinking", out var thinking))
            msg.ReasoningContent = thinking as String;

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
                        Arguments = fnDic.TryGetValue("arguments", out var fnArgs) ? fnArgs?.ToJson() : null,
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
