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
public class OllamaChatClient(AiClientOptions options) : AiClientBase(options)
{
    #region 属性
    /// <inheritdoc/>
    public override String Name { get; set; } = "本地Ollama";
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
    protected override Object BuildRequest(IChatRequest request) => OllamaChatRequest.FromChatRequest(request);

    /// <summary>解析 Ollama 非流式响应</summary>
    protected override IChatResponse ParseResponse(String json, IChatRequest request) => json.ToJsonEntity<OllamaChatResponse>()!.ToChatResponse();

    /// <summary>解析 Ollama 流式 NDJSON 单行 chunk</summary>
    protected override IChatResponse? ParseChunk(String json, IChatRequest request, String? lastEvent) => json.ToJsonEntity<OllamaChatResponse>()?.ToStreamChunk();
    #endregion
}
