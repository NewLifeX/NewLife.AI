using System.Runtime.CompilerServices;
using System.Text;
using NewLife.AI.Models;
using NewLife.AI.Providers;
using NewLife.Serialization;

namespace NewLife.AI.Clients;

/// <summary>Google Gemini 对话客户端。实现 Gemini 原生 API 协议</summary>
/// <remarks>
/// Gemini API 与 OpenAI 的主要差异：
/// <list type="bullet">
/// <item>认证通过 URL 参数 key 传递，不使用 Authorization 请求头</item>
/// <item>请求路径包含模型名称：/v1/models/{model}:generateContent</item>
/// <item>消息结构使用 contents 数组，角色为 user/model（非 assistant）</item>
/// <item>system 指令通过独立的 systemInstruction 顶级字段传入</item>
/// <item>流式接口路径为 :streamGenerateContent?alt=sse</item>
/// </list>
/// </remarks>
[AiClient("Gemini", "谷歌Gemini", "https://generativelanguage.googleapis.com", Protocol = "Gemini", Description = "谷歌 Gemini 系列多模态大模型，支持超长上下文")]
[AiClientModel("gemini-2.5-pro", "Gemini 2.5 Pro", Thinking = true, Vision = true)]
[AiClientModel("gemini-2.5-flash", "Gemini 2.5 Flash", Thinking = true, Vision = true)]
[AiClientModel("imagen-3.0-generate-001", "Imagen 3", ImageGeneration = true, FunctionCalling = false)]
public class GeminiChatClient : AiClientBase, IChatClient
{
    #region 属性
    /// <inheritdoc/>
    protected override String ClientName => "谷歌Gemini";

    /// <summary>默认 API 地址</summary>
    public virtual String DefaultEndpoint => "https://generativelanguage.googleapis.com";

    /// <summary>主流模型列表</summary>
    public virtual AiModelInfo[] DefaultModels { get; } =
    [
        new("gemini-2.5-pro",          "Gemini 2.5 Pro",   new(true,  true, false, true)),
        new("gemini-2.5-flash",        "Gemini 2.5 Flash", new(true,  true, false, true)),
        new("imagen-3.0-generate-001", "Imagen 3",         new(false, false, true, false)),
    ];

    /// <summary>连接选项</summary>
    protected readonly AiClientOptions _options;
    #endregion

    #region 构造
    /// <summary>用连接选项初始化 Gemini 客户端</summary>
    /// <param name="options">连接选项（Endpoint、ApiKey、Model 等）</param>
    /// <param name="httpClient">外部管理的 HttpClient，传 null 时自动创建</param>
    public GeminiChatClient(AiClientOptions options, HttpClient? httpClient = null)
        : base(httpClient)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }
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
            Log.Error("[{0}] GetResponseAsync error! {1}", ClientName, ex.Message);
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
    public void Dispose() { }
    #endregion

    #region 方法
    /// <summary>非流式对话</summary>
    private async Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken)
    {
        request.Stream = false;
        var body = BuildGeminiRequest(request);

        var model = request.Model ?? "gemini-2.5-flash";
        var endpoint = _options.GetEndpoint(DefaultEndpoint).TrimEnd('/');
        var url = $"{endpoint}/v1/models/{model}:generateContent?key={_options.ApiKey}";

        var responseText = await PostAsync(url, body, _options, cancellationToken).ConfigureAwait(false);
        return ParseGeminiResponse(responseText, model);
    }

    /// <summary>流式对话</summary>
    private async IAsyncEnumerable<ChatResponse> ChatStreamAsync(ChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        request.Stream = true;
        var body = BuildGeminiRequest(request);

        var model = request.Model ?? "gemini-2.5-flash";
        var endpoint = _options.GetEndpoint(DefaultEndpoint).TrimEnd('/');
        var url = $"{endpoint}/v1/models/{model}:streamGenerateContent?alt=sse&key={_options.ApiKey}";

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
            if (data.Length == 0) continue;

            var chunk = ParseGeminiStreamChunk(data, model);
            if (chunk != null)
                yield return chunk;
        }
    }
    #endregion

    #region 辅助
    /// <summary>构建 Gemini 请求体</summary>
    private static Object BuildGeminiRequest(ChatRequest request)
    {
        var dic = new Dictionary<String, Object>();

        var contents = new List<Object>();
        String? systemInstruction = null;

        foreach (var msg in request.Messages)
        {
            if (msg.Role.Equals("system", StringComparison.OrdinalIgnoreCase))
            {
                systemInstruction = msg.Content?.ToString();
                continue;
            }

            // Gemini 角色为 user/model，不使用 assistant
            var role = msg.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase) ? "model" : "user";
            var parts = new List<Object>();

            if (msg.Content != null)
                parts.Add(new Dictionary<String, Object> { ["text"] = msg.Content.ToString() ?? "" });

            contents.Add(new Dictionary<String, Object> { ["role"] = role, ["parts"] = parts });
        }
        dic["contents"] = contents;

        if (!String.IsNullOrEmpty(systemInstruction))
        {
            dic["systemInstruction"] = new Dictionary<String, Object>
            {
                ["parts"] = new List<Object> { new Dictionary<String, Object> { ["text"] = systemInstruction } }
            };
        }

        var genConfig = new Dictionary<String, Object>();
        if (request.Temperature != null) genConfig["temperature"] = request.Temperature.Value;
        if (request.TopP != null) genConfig["topP"] = request.TopP.Value;
        if (request.MaxTokens != null) genConfig["maxOutputTokens"] = request.MaxTokens.Value;
        if (request.Stop != null && request.Stop.Count > 0) genConfig["stopSequences"] = request.Stop;
        if (request.TopK != null) genConfig["topK"] = request.TopK.Value;
        if (genConfig.Count > 0)
            dic["generationConfig"] = genConfig;

        if (request.Tools != null && request.Tools.Count > 0)
        {
            var functionDeclarations = new List<Object>();
            foreach (var tool in request.Tools)
            {
                if (tool.Function == null) continue;
                var fn = new Dictionary<String, Object?> { ["name"] = tool.Function.Name };
                if (tool.Function.Description != null) fn["description"] = tool.Function.Description;
                if (tool.Function.Parameters != null) fn["parameters"] = tool.Function.Parameters;
                functionDeclarations.Add(fn);
            }
            dic["tools"] = new List<Object>
            {
                new Dictionary<String, Object> { ["functionDeclarations"] = functionDeclarations }
            };
        }

        return dic;
    }

    /// <summary>解析 Gemini 非流式响应</summary>
    private static ChatResponse ParseGeminiResponse(String json, String model)
    {
        var dic = JsonParser.Decode(json);
        if (dic == null) throw new InvalidOperationException("无法解析 Gemini 响应");

        var response = new ChatResponse
        {
            Model = model,
            Object = "chat.completion",
        };

        if (dic["candidates"] is IList<Object> candidates)
        {
            foreach (var item in candidates)
            {
                if (item is not IDictionary<String, Object> candidate) continue;
                var contentText = ExtractGeminiContent(candidate);
                var finishReason = MapGeminiFinishReason(candidate["finishReason"] as String);
                response.Add(contentText, null, finishReason);
            }
        }

        if (dic["usageMetadata"] is IDictionary<String, Object> usageDic)
        {
            response.Usage = new UsageDetails
            {
                InputTokens = usageDic["promptTokenCount"].ToInt(),
                OutputTokens = usageDic["candidatesTokenCount"].ToInt(),
                TotalTokens = usageDic["totalTokenCount"].ToInt(),
            };
        }

        return response;
    }

    /// <summary>解析 Gemini 流式数据块</summary>
    private static ChatResponse? ParseGeminiStreamChunk(String data, String model)
    {
        var dic = JsonParser.Decode(data);
        if (dic == null) return null;

        var response = new ChatResponse
        {
            Model = model,
            Object = "chat.completion.chunk",
        };

        if (dic["candidates"] is IList<Object> candidates && candidates.Count > 0)
        {
            foreach (var item in candidates)
            {
                if (item is not IDictionary<String, Object> candidate) continue;
                var deltaText = ExtractGeminiContent(candidate);
                var finishReason = MapGeminiFinishReason(candidate["finishReason"] as String);
                response.AddDelta(deltaText, null, finishReason);
            }
        }

        if (dic["usageMetadata"] is IDictionary<String, Object> usageDic)
        {
            response.Usage = new UsageDetails
            {
                InputTokens = usageDic["promptTokenCount"].ToInt(),
                OutputTokens = usageDic["candidatesTokenCount"].ToInt(),
                TotalTokens = usageDic["totalTokenCount"].ToInt(),
            };
        }

        return response;
    }

    /// <summary>提取 Gemini candidate 中的文本内容</summary>
    private static String ExtractGeminiContent(IDictionary<String, Object> candidate)
    {
        if (candidate["content"] is not IDictionary<String, Object> contentDic) return "";
        if (contentDic["parts"] is not IList<Object> parts) return "";

        var sb = new StringBuilder();
        foreach (var part in parts)
        {
            if (part is IDictionary<String, Object> partDic)
                sb.Append(partDic["text"]);
        }
        return sb.ToString();
    }

    /// <summary>映射 Gemini finishReason 到标准格式</summary>
    private static String? MapGeminiFinishReason(String? finishReason) => finishReason switch
    {
        "STOP" => "stop",
        "MAX_TOKENS" => "length",
        "SAFETY" or "RECITATION" => "content_filter",
        null => null,
        _ => finishReason.ToLower(),
    };
    #endregion
}
