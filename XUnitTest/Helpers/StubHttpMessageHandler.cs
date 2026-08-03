#nullable enable
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace XUnitTest.Helpers;

/// <summary>Stub HttpMessageHandler。按请求返回预制响应（JSON / SSE / NDJSON），用于无真实 API Key 的协议级测试</summary>
/// <remarks>
/// 通过注入到 <see cref="NewLife.AI.Clients.AiClientBase.HttpClient"/>（可写属性）模拟服务商响应，无需真实网络：
/// <code>
/// var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json("{...}"));
/// var client = new OpenAIChatClient(new AiClientOptions { Endpoint = "https://stub.local", ApiKey = "test" });
/// client.HttpClient = new HttpClient(handler);
/// </code>
/// </remarks>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    /// <summary>最近一次收到的请求地址。用于断言 URL 构建</summary>
    public String? LastRequestUrl { get; private set; }

    /// <summary>最近一次收到的请求体。用于断言请求序列化（如 think 字段映射）</summary>
    public String? LastRequestBody { get; private set; }

    /// <summary>最近一次收到的请求头。用于断言认证头（如 Bedrock SigV4）</summary>
    public IDictionary<String, String> LastRequestHeaders { get; } = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);

    /// <summary>创建按请求返回响应的 stub</summary>
    /// <param name="responder">请求 → 响应工厂</param>
    public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        _responder = responder ?? throw new ArgumentNullException(nameof(responder));
    }

    /// <summary>创建固定响应的 stub</summary>
    /// <param name="response">固定响应</param>
    public StubHttpMessageHandler(HttpResponseMessage response)
        : this(_ => response) { }

    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequestUrl = request.RequestUri?.ToString();
        LastRequestHeaders.Clear();
        foreach (var header in request.Headers)
            LastRequestHeaders[header.Key] = String.Join(",", header.Value);

        if (request.Content != null)
        {
            LastRequestBody = request.Content.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();
            foreach (var header in request.Content.Headers)
                LastRequestHeaders[header.Key] = String.Join(",", header.Value);
        }

        return Task.FromResult(_responder(request));
    }

    /// <summary>创建 JSON 响应</summary>
    /// <param name="json">响应体</param>
    /// <param name="status">HTTP 状态码</param>
    /// <returns>响应对象</returns>
    public static HttpResponseMessage Json(String json, HttpStatusCode status = HttpStatusCode.OK)
        => new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    /// <summary>创建 SSE 流响应</summary>
    /// <param name="body">SSE 文本（含 data: 行与空行）</param>
    /// <param name="status">HTTP 状态码</param>
    /// <returns>响应对象</returns>
    public static HttpResponseMessage Sse(String body, HttpStatusCode status = HttpStatusCode.OK)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "text/event-stream") };

    /// <summary>创建 NDJSON 流响应（Ollama 逐行 JSON）</summary>
    /// <param name="body">NDJSON 文本（每行一个 JSON）</param>
    /// <param name="status">HTTP 状态码</param>
    /// <returns>响应对象</returns>
    public static HttpResponseMessage NdJson(String body, HttpStatusCode status = HttpStatusCode.OK)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/x-ndjson") };
}
