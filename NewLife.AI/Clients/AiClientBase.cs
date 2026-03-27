using System.Net;
using System.Text;
using NewLife.AI.Models;
using NewLife.AI.Providers;
using NewLife.Log;
using NewLife.Serialization;

namespace NewLife.AI.Clients;

/// <summary>AI 客户端抽象基类。统一封装 HttpClient 管理与 HTTP 请求辅助方法</summary>
/// <remarks>
/// 子类需提供 <see cref="Name"/> 用于错误日志，并可通过重写 <see cref="SetHeaders"/> 注入认证头。
/// 通过重写 <see cref="CreateHttpClient"/> 定制 HttpClient 行为。
/// </remarks>
public abstract class AiClientBase
{
    #region 属性
    /// <summary>客户端名称。用于日志标识和默认端点查找；可外部设置（如注册表按服务商编码覆盖）</summary>
    public virtual String Name { get; set; } = null!;

    private String? _defaultEndpoint;
    /// <summary>默认 API 地址。可读写；首次读取为空时自动从注册表按 Name 查找（先匹配 Code，再匹配 DisplayName）</summary>
    public virtual String DefaultEndpoint
    {
        get
        {
            if (_defaultEndpoint != null) return _defaultEndpoint;
            var d = AiClientRegistry.Default.GetDescriptor(Name);
            d ??= AiClientRegistry.Default.Descriptors.Values.FirstOrDefault(x => x.DisplayName == Name);
            return _defaultEndpoint = d?.DefaultEndpoint ?? "";
        }
        set => _defaultEndpoint = value;
    }

    /// <summary>HTTP 请求超时时间。默认 30 秒</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>对话完成路径。为空时子类使用自身默认值；平台注册时可由注册表覆盖（如将 /v1/chat/completions 改为 /chat/completions）</summary>
    public virtual String ChatPath { get; set; } = "";

    private HttpClient? _httpClient;

    /// <summary>HTTP 客户端。首次访问时自动创建；可替换为代理、自定义管道或测试用 Mock</summary>
    public HttpClient HttpClient
    {
        get => _httpClient ??= CreateHttpClient();
        set => _httpClient = value;
    }
    #endregion

    #region 构造
    /// <summary>默认构造</summary>
    protected AiClientBase() => Name = GetType().Name.TrimEnd("ChatClient", "Client");

    /// <summary>创建 HttpClient 实例。子类可重写此方法自定义 HttpClient 行为</summary>
    /// <returns>新的 HttpClient 实例</returns>
    protected virtual HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        };
        var client = new HttpClient(handler)
        {
            Timeout = Timeout,
        };
        client.DefaultRequestHeaders.Add("Accept", "application/json");
        return client;
    }
    #endregion

    #region 辅助
    /// <summary>设置请求头。子类可重写此方法注入认证信息</summary>
    /// <param name="request">HTTP 请求</param>
    /// <param name="options">连接选项</param>
    /// <param name="chatRequest">对话请求，可为 null。子类可据此读取运行时参数（如 Model）覆盖 options 中的默认值</param>
    protected virtual void SetHeaders(HttpRequestMessage request, ChatRequest? chatRequest, AiClientOptions options) { }

    /// <summary>发送 GET 请求并返回响应字符串。非 2xx 时抛出 HttpRequestException</summary>
    /// <param name="url">请求地址</param>
    /// <param name="chatRequest">对话请求，可为 null，传递给 SetHeaders 以支持运行时参数覆盖</param>
    /// <param name="options">连接选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>响应字符串</returns>
    protected async Task<String> GetAsync(String url, ChatRequest? chatRequest, AiClientOptions options, CancellationToken cancellationToken = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        SetHeaders(req, chatRequest, options);
        var resp = await HttpClient.SendAsync(req, cancellationToken).ConfigureAwait(false);
        var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException($"AI 服务商[{Name}]返回错误 {(Int32)resp.StatusCode}: {json}");
        return json;
    }

    /// <summary>发送 GET 请求，非 2xx 时返回 null 而非抛出异常</summary>
    /// <param name="url">请求地址</param>
    /// <param name="options">连接选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>响应字符串，服务不可用时返回 null</returns>
    protected async Task<String?> TryGetAsync(String url, AiClientOptions options, CancellationToken cancellationToken = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        SetHeaders(req, null, options);
        var resp = await HttpClient.SendAsync(req, cancellationToken).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
    }

    /// <summary>发送 POST 请求并返回响应字符串。非 2xx 时抛出 HttpRequestException</summary>
    /// <param name="url">请求地址</param>
    /// <param name="body">请求体，字符串直接使用，其它对象序列化为 JSON</param>
    /// <param name="chatRequest">对话请求，可为 null，传递给 SetHeaders 以支持运行时参数覆盖</param>
    /// <param name="options">连接选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>响应字符串</returns>
    protected async Task<String> PostAsync(String url, Object? body, ChatRequest? chatRequest, AiClientOptions options, CancellationToken cancellationToken = default)
    {
        var bodyStr = body is String s ? s : body?.ToJson() ?? "";
        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(bodyStr, Encoding.UTF8, "application/json"),
        };
        SetHeaders(req, chatRequest, options);
        var resp = await HttpClient.SendAsync(req, cancellationToken).ConfigureAwait(false);
        var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException($"AI 服务商[{Name}]返回错误 {(Int32)resp.StatusCode}: {json}");
        return json;
    }

    /// <summary>发送 POST 请求，非 2xx 时返回 null 而非抛出异常</summary>
    /// <param name="url">请求地址</param>
    /// <param name="body">请求体，字符串直接使用，其它对象序列化为 JSON</param>
    /// <param name="options">连接选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>响应字符串，服务不可用时返回 null</returns>
    protected async Task<String?> TryPostAsync(String url, Object? body, AiClientOptions options, CancellationToken cancellationToken = default)
    {
        var bodyStr = body is String s ? s : body?.ToJson() ?? "";
        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(bodyStr, Encoding.UTF8, "application/json"),
        };
        SetHeaders(req, null, options);
        var resp = await HttpClient.SendAsync(req, cancellationToken).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
    }

    /// <summary>发送 POST 流式请求，返回已通过状态检查的 HttpResponseMessage。非 2xx 时抛出 HttpRequestException</summary>
    /// <param name="url">请求地址</param>
    /// <param name="body">请求体，字符串直接使用，其它对象序列化为 JSON</param>
    /// <param name="options">连接选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <param name="chatRequest">对话请求，可为 null，传递给 SetHeaders / SetStreamingHeaders 以支持运行时参数覆盖</param>
    /// <returns>HttpResponseMessage，调用方负责 Dispose</returns>
    protected async Task<HttpResponseMessage> PostStreamAsync(String url, Object? body, ChatRequest? chatRequest, AiClientOptions options, CancellationToken cancellationToken = default)
    {
        var bodyStr = body is String s ? s : body?.ToJson() ?? "";
        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(bodyStr, Encoding.UTF8, "application/json"),
        };
        SetHeaders(req, chatRequest, options);
        SetStreamingHeaders(req, chatRequest, options);
        var resp = await HttpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            var errBody = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            resp.Dispose();
            throw new HttpRequestException($"AI 服务商[{Name}]返回错误 {(Int32)resp.StatusCode}: {errBody}");
        }
        return resp;
    }

    /// <summary>设置流式请求专属请求头。仅在 PostStreamAsync 中调用，子类可重写以注入流式特定头（如 X-DashScope-SSE: enable）</summary>
    /// <param name="request">HTTP 请求</param>
    /// <param name="chatRequest">对话请求，可为 null。子类可据此读取运行时参数（如 Model）以做更精确的判断</param>
    /// <param name="options">连接选项</param>
    protected virtual void SetStreamingHeaders(HttpRequestMessage request, ChatRequest? chatRequest, AiClientOptions options) { }

    /// <summary>发送 POST 请求并返回二进制响应。用于音频合成等返回字节流的接口</summary>
    /// <param name="url">请求地址</param>
    /// <param name="body">请求体，字符串直接使用，其它对象序列化为 JSON</param>
    /// <param name="options">连接选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <param name="chatRequest">对话请求，可为 null，传递给 SetHeaders 以支持运行时参数覆盖</param>
    /// <returns>响应字节数组</returns>
    protected async Task<Byte[]> PostBinaryAsync(String url, Object? body, ChatRequest? chatRequest, AiClientOptions options, CancellationToken cancellationToken = default)
    {
        var bodyStr = body is String s ? s : body?.ToJson() ?? "";
        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(bodyStr, Encoding.UTF8, "application/json"),
        };
        SetHeaders(req, chatRequest, options);
        var resp = await HttpClient.SendAsync(req, cancellationToken).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            var errBody = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            throw new HttpRequestException($"AI 服务商[{Name}]返回错误 {(Int32)resp.StatusCode}: {errBody}");
        }
        return await resp.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
    }
    #endregion

    #region 日志
    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>追踪器</summary>
    public ITracer? Tracer { get; set; }
    #endregion
}
