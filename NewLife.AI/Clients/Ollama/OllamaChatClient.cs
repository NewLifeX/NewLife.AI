using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using NewLife.Serialization;

namespace NewLife.AI.Clients.Ollama;

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
[AiClient("Ollama", "本地Ollama", "http://localhost:11434", Protocol = "Ollama", Description = "本地运行开源大模型，支持 Llama/Qwen/Gemma 等")]
[AiClientModel("qwen3.5:0.8b", "Qwen 3.5 0.8B", Thinking = true)]
[AiClientModel("qwen3:8b", "Qwen3 8B", Thinking = true)]
[AiClientModel("llama3.3", "Llama 3.3")]
[AiClientModel("deepseek-r1", "DeepSeek R1", Thinking = true, FunctionCalling = false)]
[AiClientModel("phi4", "Phi-4")]
[AiClientModel("llava", "LLaVA", Vision = true, FunctionCalling = false)]
[AiClientModel("gemma3", "Gemma 3", Vision = true)]
public class OllamaChatClient : AiClientBase
{
    #region 属性
    /// <inheritdoc/>
    public override String Name { get; set; } = "本地Ollama";

    /// <summary>默认Json序列化选项</summary>
    public static JsonOptions DefaultJsonOptions = new()
    {
        PropertyNaming = PropertyNaming.SnakeCaseLower,
        IgnoreNullValues = false,
    };
    #endregion

    #region 构造
    /// <summary>以连接选项初始化 Ollama 客户端</summary>
    /// <param name="options">连接选项（Endpoint、ApiKey、Model 等）</param>
    public OllamaChatClient(AiClientOptions options) : base(options) => JsonOptions = DefaultJsonOptions;

    /// <summary>以 API 密钥和可选模型快速创建 Ollama 客户端</summary>
    /// <param name="apiKey">API 密钥；本地部署可传 null 或空串</param>
    /// <param name="model">默认模型编码，为空时由每次请求指定</param>
    /// <param name="endpoint">API 地址覆盖；为空时使用默认 http://localhost:11434</param>
    public OllamaChatClient(String? apiKey, String? model = null, String? endpoint = null)
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
            if (String.IsNullOrEmpty(line)) continue;

            var chunk = ParseChunk(line, request, null);
            if (chunk != null)
                yield return chunk;
        }
    }
    #endregion

    #region 方法
    /// <summary>获取本地已安装的模型列表</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>模型列表，服务不可用时返回 null</returns>
    public virtual async Task<OllamaTagsResponse?> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        var url = _options.GetEndpoint(DefaultEndpoint).TrimEnd('/') + "/api/tags";
        var json = await TryGetAsync(url, _options, cancellationToken).ConfigureAwait(false);
        return json?.ToJsonEntity<OllamaTagsResponse>(JsonOptions);
    }

    /// <summary>获取运行中的模型列表</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>运行中模型列表，服务不可用时返回 null</returns>
    public virtual async Task<OllamaPsResponse?> ListRunningAsync(CancellationToken cancellationToken = default)
    {
        var url = _options.GetEndpoint(DefaultEndpoint).TrimEnd('/') + "/api/ps";
        var json = await TryGetAsync(url, _options, cancellationToken).ConfigureAwait(false);
        return json?.ToJsonEntity<OllamaPsResponse>(JsonOptions);
    }

    /// <summary>获取模型详细信息</summary>
    /// <param name="model">模型名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>模型详情，服务不可用时返回 null</returns>
    public virtual async Task<OllamaShowResponse?> ShowModelAsync(String model, CancellationToken cancellationToken = default)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));

        var url = _options.GetEndpoint(DefaultEndpoint).TrimEnd('/') + "/api/show";
        var json = await TryPostAsync(url, new { model }, _options, cancellationToken).ConfigureAwait(false);
        return json?.ToJsonEntity<OllamaShowResponse>(JsonOptions);
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
        var json = await PostAsync(url, request, null, _options, cancellationToken).ConfigureAwait(false);
        return json.ToJsonEntity<OllamaEmbedResponse>(JsonOptions);
    }

    /// <summary>拉取（下载）模型。等待完成后返回最终状态</summary>
    /// <param name="model">模型名称，如 qwen3.5:0.8b</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>拉取状态，status 为 "success" 表示成功</returns>
    public virtual async Task<OllamaPullStatus?> PullModelAsync(String model, CancellationToken cancellationToken = default)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));

        var url = _options.GetEndpoint(DefaultEndpoint).TrimEnd('/') + "/api/pull";

        // 拉取模型可能耗时数分钟，使用 30 分钟超时
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMinutes(30));
        var json = await PostAsync(url, new { model, stream = false }, null, _options, cts.Token).ConfigureAwait(false);
        return json.ToJsonEntity<OllamaPullStatus>(JsonOptions);
    }
    #endregion

    #region 辅助
    /// <summary>构建请求地址。子类可重写此方法根据请求参数动态调整路径（如不同模型使用不同端点）</summary>
    protected override String BuildUrl(IChatRequest request)
    {
        var endpoint = _options.GetEndpoint(DefaultEndpoint).TrimEnd('/');
        return endpoint + "/api/chat";
    }

    /// <inheritdoc/>
    protected override void SetHeaders(HttpRequestMessage request, IChatRequest? chatRequest, AiClientOptions options)
    {
        // Ollama 默认不需要 API Key，但如果用户配置了则传递
        if (!String.IsNullOrEmpty(options.ApiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
    }

    /// <summary>构建 Ollama 原生请求体</summary>
    protected override Object BuildRequest(IChatRequest request) => request is OllamaChatRequest or ? or : OllamaChatRequest.FromChatRequest(request);

    /// <summary>解析 Ollama 非流式响应</summary>
    protected override IChatResponse ParseResponse(String json, IChatRequest request)
    {
        var resp = json.ToJsonEntity<OllamaChatResponse>(JsonOptions)!;
        ((IChatResponse)resp).Object = "chat.completion";
        return resp;
    }

    /// <summary>解析 Ollama 流式 NDJSON 单行 chunk，OllamaChatResponse 适配器同时设置 Message/Delta</summary>
    protected override IChatResponse? ParseChunk(String json, IChatRequest request, String? lastEvent)
    {
        var resp = json.ToJsonEntity<OllamaChatResponse>(JsonOptions);
        if (resp != null) ((IChatResponse)resp).Object = "chat.completion.chunk";
        return resp;
    }

    /// <summary>根据 Ollama 模型 ID 和详情推断模型能力</summary>
    /// <remarks>
    /// 推断规则：
    /// <list type="bullet">
    /// <item>details.Families 含 clip / mllama → 视觉能力（视觉编码器）</item>
    /// <item>模型名含 -vl / vision / llava → 视觉能力</item>
    /// <item>模型名含 deepseek-r1 / qwq / qvq → 思考能力</item>
    /// <item>qwen3 系列 → 支持函数调用和思考</item>
    /// <item>gemma3 → 支持视觉</item>
    /// <item>Ollama 大部分模型默认不支持函数调用</item>
    /// </list>
    /// </remarks>
    /// <param name="modelId">模型标识</param>
    /// <param name="details">Ollama 模型详情（含 Family/Families 等），可为 null</param>
    /// <returns>推断出的能力信息</returns>
    public AiProviderCapabilities InferModelCapabilities(String? modelId, OllamaModelDetails? details)
    {
        if (String.IsNullOrEmpty(modelId))
            return new AiProviderCapabilities(false, false, false, false);

        var thinking = false;
        var vision = false;
        var funcCall = false;

        // 从 Families 探测视觉编码器
        if (details?.Families != null)
        {
            foreach (var f in details.Families)
            {
                if (f != null && (f.Contains("clip", StringComparison.OrdinalIgnoreCase) ||
                    f.Contains("mllama", StringComparison.OrdinalIgnoreCase)))
                {
                    vision = true;
                    break;
                }
            }
        }

        // 模型名模式匹配 — 视觉
        if (modelId.Contains("-vl", StringComparison.OrdinalIgnoreCase) ||
            modelId.Contains("vision", StringComparison.OrdinalIgnoreCase) ||
            modelId.StartsWith("llava", StringComparison.OrdinalIgnoreCase) ||
            modelId.StartsWith("gemma3", StringComparison.OrdinalIgnoreCase) ||
            modelId.StartsWith("qvq", StringComparison.OrdinalIgnoreCase))
            vision = true;

        // 模型名模式匹配 — 思考/推理
        if (modelId.Contains("deepseek-r1", StringComparison.OrdinalIgnoreCase) ||
            modelId.StartsWith("qwq", StringComparison.OrdinalIgnoreCase) ||
            modelId.StartsWith("qvq", StringComparison.OrdinalIgnoreCase))
            thinking = true;

        // qwen3 系列支持函数调用和思考
        if (modelId.StartsWith("qwen3", StringComparison.OrdinalIgnoreCase))
        {
            funcCall = true;
            thinking = true;
        }

        return new AiProviderCapabilities(thinking, vision, false, funcCall);
    }
    #endregion
}
