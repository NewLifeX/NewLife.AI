using System.Net;
using System.Text;
using NewLife.AI.Models;
using NewLife.Log;
using NewLife.Serialization;

namespace NewLife.AI.Providers;

/// <summary>AI 服务商统一接口。描述服务商身份与能力，并作为创建对话客户端的工厂</summary>
/// <remarks>
/// 接口设计原则：
/// <list type="bullet">
/// <item>纯工厂 + 描述者职责：仅暴露服务商元数据与 <see cref="CreateClient"/> 工厂方法</item>
/// <item>对话执行委托给 <see cref="IChatClient"/>，对标 MEAI 的 IChatClient 设计</item>
/// <item>使用服务商名称（字符串）而非枚举标识，方便扩展自定义服务商</item>
/// <item>接口不依赖数据库，可在任意 .NET 项目中独立使用</item>
/// </list>
/// </remarks>
public interface IAiProvider
{
    /// <summary>服务商编码。唯一标识，如 OpenAI、DashScope、DeepSeek 等</summary>
    String Code { get; }

    /// <summary>服务商名称。用于界面显示的友好名称，如"OpenAI"、"阿里百炼"、"深度求索"等</summary>
    String Name { get; }

    /// <summary>服务商描述。详细说明服务商特点、支持的模型系列等</summary>
    String? Description { get; }

    /// <summary>API 协议类型。ChatCompletions / AnthropicMessages / Gemini</summary>
    String ApiProtocol { get; }

    /// <summary>默认 API 地址</summary>
    String DefaultEndpoint { get; }

    /// <summary>主流模型列表。该服务商下各主流模型及其能力描述，供用户选择配置时参考</summary>
    AiModelInfo[] Models { get; }

    /// <summary>创建已绑定连接参数的对话客户端（MEAI 兼容入口）</summary>
    /// <remarks>
    /// 返回的 <see cref="IChatClient"/> 已将 Endpoint 和 ApiKey 绑定，无需每次调用传入 options。
    /// 可与 <see cref="ChatClientBuilder"/> 组合，通过中间件管道添加日志、追踪、用量统计等横切行为。
    /// </remarks>
    /// <param name="options">连接选项（Endpoint、ApiKey 等）</param>
    /// <returns>已配置的 IChatClient 实例</returns>
    IChatClient CreateClient(AiProviderOptions options);
}

/// <summary>支持列出可用模型的 AI 服务商接口。对应 OpenAI GET /v1/models 端点</summary>
public interface IModelListProvider
{
    /// <summary>获取该服务商当前可用的模型列表</summary>
    /// <param name="options">连接选项（Endpoint、ApiKey 等）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>模型列表，服务不可用时返回 null</returns>
    Task<OpenAiModelListResponse?> ListModelsAsync(AiProviderOptions options, CancellationToken cancellationToken = default);
}

/// <summary>AI 服务商默认能力信息。表示该服务商主力模型的典型能力</summary>
/// <remarks>这些是服务商级别的默认值，用户创建具体模型配置时可按实际模型覆盖</remarks>
/// <param name="SupportThinking">是否支持思考模式。如 DeepSeek-R1、Claude 的 extended thinking</param>
/// <param name="SupportVision">是否支持图片输入（视觉）。如 GPT-4V、Claude Vision、Qwen-VL</param>
/// <param name="SupportImageGeneration">是否支持文生图。如 DALL·E、Qwen 的图像生成</param>
/// <param name="SupportFunctionCalling">是否支持 Function Calling / Tool Use</param>
public record AiProviderCapabilities(
    Boolean SupportThinking = false,
    Boolean SupportVision = false,
    Boolean SupportImageGeneration = false,
    Boolean SupportFunctionCalling = false);

/// <summary>AI 模型信息。描述服务商旗下某具体模型的标识与能力</summary>
/// <param name="Model">模型标识，即 API 请求中 model 字段的值，如 "gpt-4o"</param>
/// <param name="DisplayName">模型显示名称，用于界面展示，如 "GPT-4o"</param>
/// <param name="Capabilities">该模型支持的能力</param>
public record AiModelInfo(String Model, String DisplayName, AiProviderCapabilities Capabilities);

/// <summary>AI 服务商连接选项</summary>
public class AiProviderOptions
{
    /// <summary>API 地址。为空时使用服务商默认地址</summary>
    public String? Endpoint { get; set; }

    /// <summary>API 密钥</summary>
    public String? ApiKey { get; set; }

    /// <summary>组织编号。部分服务商需要（如 OpenAI）</summary>
    public String? Organization { get; set; }

    /// <summary>默认模型编码。客户端每次调用时若未指定模型则使用此值</summary>
    public String? Model { get; set; }

    /// <summary>获取实际使用的 API 地址</summary>
    /// <param name="defaultEndpoint">默认地址</param>
    /// <returns></returns>
    public String GetEndpoint(String defaultEndpoint) =>
        String.IsNullOrWhiteSpace(Endpoint) ? defaultEndpoint : Endpoint;
}

/// <summary>IAiProvider 扩展方法。提供更简洁的客户端创建入口</summary>
public static class AiProviderExtensions
{
    /// <summary>用指定密钥和模型快速创建对话客户端，无需手动构造 AiProviderOptions</summary>
    /// <param name="provider">AI 服务商</param>
    /// <param name="apiKey">API 密钥</param>
    /// <param name="model">默认模型编码，为空时使用服务商默认值</param>
    /// <param name="endpoint">自定义接入点地址，为空时使用服务商默认地址</param>
    /// <returns>已绑定连接参数的 IChatClient 实例</returns>
    public static IChatClient CreateClient(this IAiProvider provider, String apiKey, String? model = null, String? endpoint = null)
    {
        if (provider == null) throw new ArgumentNullException(nameof(provider));

        var options = new AiProviderOptions { ApiKey = apiKey, Model = model, Endpoint = endpoint };
        return provider.CreateClient(options);
    }
}

/// <summary>AI 服务商抽象基类。统一封装 HttpClient 管理与 HTTP 请求辅助方法</summary>
/// <remarks>
/// 子类可实现 <see cref="IAiProvider"/> 接口以提供服务商标识，并通过重写 <see cref="SetHeaders"/> 注入认证头。
/// 通过重写 <see cref="CreateHttpClient"/> 定制 HttpClient 行为。
/// </remarks>
public abstract class AiProviderBase
{
    #region 属性
    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>追踪器</summary>
    public ITracer? Tracer { get; set; }

    /// <summary>HTTP 请求超时时间。默认 5 分钟</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);

    private HttpClient? _httpClient;

    /// <summary>获取 HttpClient 实例。首次访问时通过 CreateHttpClient 创建</summary>
    protected HttpClient HttpClient => _httpClient ??= CreateHttpClient();
    #endregion

    #region 构造
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
    protected virtual void SetHeaders(HttpRequestMessage request, AiProviderOptions options) { }

    /// <summary>发送 GET 请求并返回响应字符串。非 2xx 时抛出 HttpRequestException</summary>
    /// <param name="url">请求地址</param>
    /// <param name="options">连接选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>响应字符串</returns>
    protected async Task<String> GetAsync(String url, AiProviderOptions options, CancellationToken cancellationToken = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        SetHeaders(req, options);
        var resp = await HttpClient.SendAsync(req, cancellationToken).ConfigureAwait(false);
        var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException($"AI 服务商[{(this as IAiProvider)?.Name}]返回错误 {(Int32)resp.StatusCode}: {json}");
        return json;
    }

    /// <summary>发送 GET 请求，非 2xx 时返回 null 而非抛出异常</summary>
    /// <param name="url">请求地址</param>
    /// <param name="options">连接选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>响应字符串，服务不可用时返回 null</returns>
    protected async Task<String?> TryGetAsync(String url, AiProviderOptions options, CancellationToken cancellationToken = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        SetHeaders(req, options);
        var resp = await HttpClient.SendAsync(req, cancellationToken).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
    }

    /// <summary>发送 POST 请求并返回响应字符串。非 2xx 时抛出 HttpRequestException</summary>
    /// <param name="url">请求地址</param>
    /// <param name="body">请求体，字符串直接使用，其它对象序列化为 JSON</param>
    /// <param name="options">连接选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>响应字符串</returns>
    protected async Task<String> PostAsync(String url, Object? body, AiProviderOptions options, CancellationToken cancellationToken = default)
    {
        var bodyStr = body is String s ? s : body?.ToJson() ?? "";
        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(bodyStr, Encoding.UTF8, "application/json"),
        };
        SetHeaders(req, options);
        var resp = await HttpClient.SendAsync(req, cancellationToken).ConfigureAwait(false);
        var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException($"AI 服务商[{(this as IAiProvider)?.Name}]返回错误 {(Int32)resp.StatusCode}: {json}");
        return json;
    }

    /// <summary>发送 POST 请求，非 2xx 时返回 null 而非抛出异常</summary>
    /// <param name="url">请求地址</param>
    /// <param name="body">请求体，字符串直接使用，其它对象序列化为 JSON</param>
    /// <param name="options">连接选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>响应字符串，服务不可用时返回 null</returns>
    protected async Task<String?> TryPostAsync(String url, Object? body, AiProviderOptions options, CancellationToken cancellationToken = default)
    {
        var bodyStr = body is String s ? s : body?.ToJson() ?? "";
        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(bodyStr, Encoding.UTF8, "application/json"),
        };
        SetHeaders(req, options);
        var resp = await HttpClient.SendAsync(req, cancellationToken).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
    }

    /// <summary>发送 POST 流式请求，返回已通过状态检查的 HttpResponseMessage。非 2xx 时抛出 HttpRequestException</summary>
    /// <param name="url">请求地址</param>
    /// <param name="body">请求体，字符串直接使用，其它对象序列化为 JSON</param>
    /// <param name="options">连接选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>HttpResponseMessage，调用方负责 Dispose</returns>
    protected async Task<HttpResponseMessage> PostStreamAsync(String url, Object? body, AiProviderOptions options, CancellationToken cancellationToken = default)
    {
        var bodyStr = body is String s ? s : body?.ToJson() ?? "";
        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(bodyStr, Encoding.UTF8, "application/json"),
        };
        SetHeaders(req, options);
        var resp = await HttpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            var errBody = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            resp.Dispose();
            throw new HttpRequestException($"AI 服务商[{(this as IAiProvider)?.Name}]返回错误 {(Int32)resp.StatusCode}: {errBody}");
        }
        return resp;
    }
    #endregion
}
