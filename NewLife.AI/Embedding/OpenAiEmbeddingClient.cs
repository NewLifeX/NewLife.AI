using System.Net.Http.Headers;
using System.Text;
using NewLife.AI.Providers;
using NewLife.Serialization;

namespace NewLife.AI.Embedding;

/// <summary>OpenAI 协议嵌入向量客户端。兼容所有支持 OpenAI Embeddings API 的服务商</summary>
/// <remarks>
/// 通过 <see cref="IEmbeddingProvider.CreateEmbeddingClient"/> 创建，
/// 也可直接实例化用于支持 OpenAI Embeddings API 的服务商（阿里百炼、DeepSeek 等）。
/// </remarks>
public class OpenAiEmbeddingClient : IEmbeddingClient
{
    #region 属性

    private readonly AiProviderOptions _options;
    private readonly String _defaultEndpoint;

    /// <summary>嵌入路径</summary>
    protected virtual String EmbeddingPath => "/v1/embeddings";

    /// <summary>客户端元数据</summary>
    public EmbeddingClientMetadata Metadata { get; }

    /// <summary>HTTP 请求超时时间。默认 2 分钟</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(2);

    private HttpClient? _httpClient;

    /// <summary>获取 HttpClient 实例</summary>
    protected HttpClient HttpClient => _httpClient ??= CreateHttpClient();

    #endregion

    #region 构造

    /// <summary>初始化 OpenAI 嵌入客户端</summary>
    /// <param name="providerName">服务商名称（用于元数据展示）</param>
    /// <param name="defaultEndpoint">服务商默认 API 地址</param>
    /// <param name="options">连接选项（Endpoint、ApiKey 等）</param>
    public OpenAiEmbeddingClient(String providerName, String defaultEndpoint, AiProviderOptions options)
    {
        if (providerName == null) throw new ArgumentNullException(nameof(providerName));
        if (defaultEndpoint == null) throw new ArgumentNullException(nameof(defaultEndpoint));
        if (options == null) throw new ArgumentNullException(nameof(options));

        _defaultEndpoint = defaultEndpoint;
        _options = options;
        Metadata = new EmbeddingClientMetadata
        {
            ProviderName = providerName,
            Endpoint = options.GetEndpoint(defaultEndpoint),
        };
    }

    /// <summary>从 IAiProvider 创建嵌入客户端（便捷重载）</summary>
    /// <param name="provider">AI 服务商</param>
    /// <param name="options">连接选项</param>
    public OpenAiEmbeddingClient(IAiProvider provider, AiProviderOptions options)
        : this(provider.Name, provider.DefaultEndpoint, options) { }

    private HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
        };
        return new HttpClient(handler) { Timeout = Timeout };
    }

    #endregion

    #region 方法

    /// <summary>生成嵌入向量</summary>
    /// <param name="request">嵌入请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>嵌入响应</returns>
    public virtual async Task<EmbeddingResponse> GenerateAsync(EmbeddingRequest request, CancellationToken cancellationToken = default)
    {
        var dic = new Dictionary<String, Object>();

        // 单条输入直接传字符串，多条传数组（节省序列化开销）
        if (request.Input.Count == 1)
            dic["input"] = request.Input[0];
        else
            dic["input"] = request.Input;

        if (request.Model != null) dic["model"] = request.Model;
        if (request.Dimensions != null) dic["dimensions"] = request.Dimensions.Value;
        if (request.EncodingFormat != null) dic["encoding_format"] = request.EncodingFormat;
        if (request.User != null) dic["user"] = request.User;

        var body = dic.ToJson();
        var endpoint = _options.GetEndpoint(_defaultEndpoint).TrimEnd('/');
        var url = endpoint + EmbeddingPath;

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
        if (!String.IsNullOrEmpty(_options.ApiKey))
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        httpRequest.Content = new StringContent(body, Encoding.UTF8, "application/json");

        using var httpResponse = await HttpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        var responseText = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);

        if (!httpResponse.IsSuccessStatusCode)
            throw new HttpRequestException($"Embedding API 返回错误 {(Int32)httpResponse.StatusCode}: {responseText}");

        return ParseResponse(responseText);
    }

    #endregion

    #region 辅助

    /// <summary>解析嵌入响应 JSON</summary>
    /// <param name="json">JSON 字符串</param>
    /// <returns>嵌入响应</returns>
    protected virtual EmbeddingResponse ParseResponse(String json)
    {
        var dic = JsonParser.Decode(json);
        if (dic == null) throw new InvalidOperationException("无法解析 Embedding API 响应");

        var response = new EmbeddingResponse
        {
            Model = dic.TryGetValue("model", out var model) ? model as String : null,
        };

        // 解析 data 数组
        if (dic.TryGetValue("data", out var dataObj) && dataObj is IList<Object> dataList)
        {
            var items = new List<EmbeddingItem>(dataList.Count);
            foreach (var item in dataList)
            {
                if (item is not IDictionary<String, Object> itemDic) continue;

                var ei = new EmbeddingItem
                {
                    Index = itemDic.TryGetValue("index", out var idx) ? idx.ToInt() : 0,
                };

                if (itemDic.TryGetValue("embedding", out var embObj) && embObj is IList<Object> embList)
                {
                    var arr = new Single[embList.Count];
                    for (var i = 0; i < embList.Count; i++)
                        arr[i] = (Single)embList[i].ToDouble();
                    ei.Embedding = arr;
                }

                items.Add(ei);
            }
            response.Data = items;
        }

        // 解析 usage
        if (dic.TryGetValue("usage", out var usageObj) && usageObj is IDictionary<String, Object> usageDic)
        {
            response.Usage = new EmbeddingUsage
            {
                PromptTokens = usageDic.TryGetValue("prompt_tokens", out var pt) ? pt.ToInt() : 0,
                TotalTokens = usageDic.TryGetValue("total_tokens", out var tt) ? tt.ToInt() : 0,
            };
        }

        return response;
    }

    #endregion

    #region 释放

    /// <summary>释放资源（HttpClient 为静态共享，不随实例释放）</summary>
    public void Dispose() { }

    #endregion
}
