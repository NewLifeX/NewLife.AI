using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NewLife.AI.Extensions;
using NewLife.AI.ModelContextProtocol;
using NewLife.Serialization;
using Xunit;

namespace XUnitTest.Mcp;

/// <summary>AspNetMcpServer 集成测试。覆盖 MapMcp 注册、Streamable HTTP（JSON/SSE 协商）、认证、legacy SSE、多类型注册（对标官方 ModelContextProtocol.AspNetCore）</summary>
public class AspNetMcpServerTests
{
    /// <summary>测试工具类</summary>
    public class TestTools
    {
        /// <summary>获取当前时间</summary>
        public String GetTime() => "2026-01-01 08:00:00";

        /// <summary>计算两数之和</summary>
        /// <param name="a">第一个数</param>
        /// <param name="b">第二个数</param>
        /// <returns>两数之和</returns>
        public Int32 Add(Int32 a, Int32 b) => a + b;
    }

    /// <summary>第二工具类（多类型注册测试）</summary>
    public class MoreTools
    {
        /// <summary>字符串拼接</summary>
        /// <param name="text1">第一个字符串</param>
        /// <param name="text2">第二个字符串</param>
        /// <returns>拼接结果</returns>
        public String Concat(String text1, String text2 = "World") => $"{text1} {text2}";
    }

    /// <summary>构建并启动测试主机</summary>
    private static async Task<WebApplication> CreateHost(Action<WebApplicationBuilder>? configure = null, Action<AspNetMcpServer>? configureServer = null, params Type[] toolTypes)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddMcp<TestTools>();
        configure?.Invoke(builder);

        var app = builder.Build();
        var types = toolTypes.Length > 0 ? toolTypes : [typeof(TestTools)];
        app.MapMcp("/mcp", configureServer, types);
        await app.StartAsync();
        return app;
    }

    private static async Task<JsonRpcResponse> PostAsync(HttpClient client, String url, JsonRpcRequest request)
    {
        var httpResponse = await client.PostAsync(url, new StringContent(request.ToJson(false, true, true), Encoding.UTF8, "application/json"));
        var body = await httpResponse.Content.ReadAsStringAsync();
        var response = body.ToJsonEntity<JsonRpcResponse>();
        Assert.NotNull(response);
        return response;
    }

    #region Streamable HTTP 主链路
    [Fact]
    [DisplayName("MapMcp—initialize/tools/list/tools/call 全链路（JSON 模式）")]
    public async Task MapMcp_JsonMode_FullFlow()
    {
        using var app = await CreateHost();
        var client = app.GetTestClient();

        // initialize
        var init = await PostAsync(client, "/mcp", new JsonRpcRequest("2.0", "initialize", new InitializeParams("2025-06-18", new ClientInfo("TestClient", "1.0.0")), 1));
        Assert.Equal("2.0", init.JsonRpc);
        Assert.Null(init.Error);
        Assert.NotNull(init.Result);

        // tools/list
        var list = await PostAsync(client, "/mcp", new JsonRpcRequest("2.0", "tools/list", null, 2));
        Assert.Null(list.Error);
        var tools = list.Result?.ToJson().ToJsonEntity<ToolListResult>();
        Assert.NotNull(tools);
        Assert.Contains(tools.Tools, t => t.Name == "get_time");
        Assert.Contains(tools.Tools, t => t.Name == "add");

        // tools/call
        var call = await PostAsync(client, "/mcp", new JsonRpcRequest("2.0", "tools/call", new ToolCallParams("add", new Dictionary<String, Object?> { ["a"] = 5, ["b"] = 7 }, null), 3));
        Assert.Null(call.Error);
        var result = call.Result?.ToJson().ToJsonEntity<ToolCallResult>();
        Assert.NotNull(result);
        Assert.Equal("12", result.Content[0].Text);
    }

    [Fact]
    [DisplayName("MapMcp—tools/call 传空字符串参数绑定正常")]
    public async Task MapMcp_EmptyStringParam_Binds()
    {
        using var app = await CreateHost(configureServer: null, toolTypes: [typeof(TestTools), typeof(MoreTools)]);
        var client = app.GetTestClient();

        // text1 必填传空字符串，text2 用默认值——验证空字符串不被误判为缺参
        var call = await PostAsync(client, "/mcp", new JsonRpcRequest("2.0", "tools/call",
            new ToolCallParams("concat", new Dictionary<String, Object?> { ["text1"] = "" }, null), 3));

        Assert.Null(call.Error);
        var result = call.Result?.ToJson().ToJsonEntity<ToolCallResult>();
        Assert.NotNull(result);
        Assert.Equal(" World", result.Content[0].Text);
    }

    [Fact]
    [DisplayName("MapMcp—JSON 响应 Content-Type 为 application/json")]
    public async Task MapMcp_JsonResponse_ContentType()
    {
        using var app = await CreateHost();
        var client = app.GetTestClient();

        var httpResponse = await client.PostAsync("/mcp", new StringContent(new JsonRpcRequest("2.0", "ping", null, 1).ToJson(false, true, true), Encoding.UTF8, "application/json"));

        Assert.Equal("application/json", httpResponse.Content.Headers.ContentType?.MediaType);
        var body = await httpResponse.Content.ReadAsStringAsync();
        Assert.Contains("\"jsonrpc\":\"2.0\"", body);
    }

    [Fact]
    [DisplayName("MapMcp—Accept 含 text/event-stream 时返回 SSE")]
    public async Task MapMcp_SseResponse_WhenAcceptSse()
    {
        using var app = await CreateHost();
        var client = app.GetTestClient();

        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(new JsonRpcRequest("2.0", "ping", null, 1).ToJson(false, true, true), Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("Accept", "text/event-stream");

        var httpResponse = await client.SendAsync(request);
        var body = await httpResponse.Content.ReadAsStringAsync();

        Assert.Equal("text/event-stream", httpResponse.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("event: message", body);
        Assert.Contains("\"jsonrpc\":\"2.0\"", body);
    }

    [Fact]
    [DisplayName("MapMcp—多类型注册暴露全部工具")]
    public async Task MapMcp_MultiTypes_ExposesAllTools()
    {
        using var app = await CreateHost(null, null, typeof(TestTools), typeof(MoreTools));
        var client = app.GetTestClient();

        var list = await PostAsync(client, "/mcp", new JsonRpcRequest("2.0", "tools/list", null, 1));
        var tools = list.Result?.ToJson().ToJsonEntity<ToolListResult>();
        Assert.NotNull(tools);
        Assert.Contains(tools.Tools, t => t.Name == "add");
        Assert.Contains(tools.Tools, t => t.Name == "concat");
    }
    #endregion

    #region 认证
    [Fact]
    [DisplayName("认证—配置 AuthToken 后无令牌返回 401")]
    public async Task Auth_WithTokenConfigured_NoToken_Returns401()
    {
        using var app = await CreateHost(null, s => s.AuthToken = "secret-token");
        var client = app.GetTestClient();

        var httpResponse = await client.PostAsync("/mcp", new StringContent(new JsonRpcRequest("2.0", "tools/list", null, 1).ToJson(false, true, true), Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Unauthorized, httpResponse.StatusCode);
    }

    [Fact]
    [DisplayName("认证—正确 Bearer 令牌放行")]
    public async Task Auth_WithCorrectToken_Passes()
    {
        using var app = await CreateHost(null, s => s.AuthToken = "secret-token");
        var client = app.GetTestClient();

        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(new JsonRpcRequest("2.0", "tools/list", null, 1).ToJson(false, true, true), Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer secret-token");

        var httpResponse = await client.SendAsync(request);
        httpResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    [DisplayName("认证—从配置节 Mcp:AuthToken 自动读取")]
    public async Task Auth_FromConfiguration_ReadsToken()
    {
        using var app = await CreateHost(builder =>
        {
            builder.Configuration.AddInMemoryCollection(new Dictionary<String, String?> { ["Mcp:AuthToken"] = "cfg-token" });
        });
        var client = app.GetTestClient();

        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(new JsonRpcRequest("2.0", "tools/list", null, 1).ToJson(false, true, true), Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer cfg-token");

        var httpResponse = await client.SendAsync(request);
        httpResponse.EnsureSuccessStatusCode();
    }
    #endregion

    #region legacy SSE
    [Fact]
    [DisplayName("legacy SSE—GET /sse + POST /message 往返")]
    public async Task LegacySse_EndpointAndMessage_Roundtrip()
    {
        using var app = await CreateHost(null, s => s.EnableLegacySse = true);
        var client = app.GetTestClient();
        var sessionId = "sess-001";

        // 打开 SSE 事件流
        var sseRequest = new HttpRequestMessage(HttpMethod.Get, $"/mcp/sse?sessionId={sessionId}");
        sseRequest.Headers.TryAddWithoutValidation("Accept", "text/event-stream");
        var sseResponse = await client.SendAsync(sseRequest, HttpCompletionOption.ResponseHeadersRead);
        sseResponse.EnsureSuccessStatusCode();
        var stream = await sseResponse.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream, Encoding.UTF8);

        // 读取 endpoint 事件
        var endpointEvent = await ReadSseEventAsync(reader, CancellationToken.None);
        Assert.NotNull(endpointEvent);
        Assert.Contains("event: endpoint", endpointEvent);
        Assert.Contains("/message", endpointEvent);

        // POST 消息（202 无响应体，响应经事件流推送）
        var msgResponse = await client.PostAsync($"/mcp/message?sessionId={sessionId}",
            new StringContent(new JsonRpcRequest("2.0", "tools/list", null, 5).ToJson(false, true, true), Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Accepted, msgResponse.StatusCode);

        // 从事件流读取响应事件
        var msgEvent = await ReadSseEventAsync(reader, CancellationToken.None);
        Assert.NotNull(msgEvent);
        Assert.Contains("event: message", msgEvent);
        Assert.Contains("\"jsonrpc\":\"2.0\"", msgEvent);
        Assert.Contains("get_time", msgEvent);
    }

    /// <summary>读取一条 SSE 事件（以空行结束）</summary>
    private static async Task<String?> ReadSseEventAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line == null) return sb.Length > 0 ? sb.ToString() : null;
            if (line.Length == 0) return sb.ToString();
            sb.AppendLine(line);
        }
        return null;
    }
    #endregion

    #region 请求体限制
    [Fact]
    [DisplayName("请求体—超过 1MB 返回 413")]
    public async Task Body_OverLimit_Returns413()
    {
        using var app = await CreateHost();
        var client = app.GetTestClient();

        var big = new String('x', 1024 * 1024 + 10);
        var httpResponse = await client.PostAsync("/mcp", new StringContent(big, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, httpResponse.StatusCode);
    }
    #endregion
}
