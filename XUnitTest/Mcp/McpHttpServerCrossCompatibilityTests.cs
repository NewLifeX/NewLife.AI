using System;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using NewLife.AI.ModelContextProtocol;
using NewLife.Serialization;
using Xunit;
using Xunit.Abstractions;

namespace XUnitTest.Mcp;

/// <summary>HttpMcpServer（NewLife.Http.HttpServer 宿主）与官方客户端交叉验证。
/// 验证 keep-alive 规避（强制 Connection: close）后，官方客户端多请求链路可正常互通（N2：你→我）</summary>
[DisplayName("HttpMcpServer 官方客户端交叉测试")]
public class McpHttpServerCrossCompatibilityTests
{
    private readonly ITestOutputHelper _output;

    public McpHttpServerCrossCompatibilityTests(ITestOutputHelper output) => _output = output;

    /// <summary>测试工具类</summary>
    public class TestTools
    {
        /// <summary>计算两数之和</summary>
        /// <param name="a">第一个数</param>
        /// <param name="b">第二个数</param>
        /// <returns>两数之和</returns>
        public Int32 Add(Int32 a, Int32 b) => a + b;
    }

    private static HttpMcpServer CreateServer()
    {
        var server = new HttpMcpServer { Port = 0, ResponseFormat = McpResponseFormat.Json };
        server.AddTool<TestTools>(server);
        server.Start();
        return server;
    }

    private static HttpClientTransport CreateTransport(HttpMcpServer server)
        => new(new HttpClientTransportOptions
        {
            Endpoint = new Uri($"http://localhost:{server.Port}/"),
            TransportMode = HttpTransportMode.StreamableHttp,
            EnableStandaloneGetStream = false,
        });

    [Fact]
    [DisplayName("N2-官方客户端能连 HttpMcpServer：发现并调用工具（多请求链路）")]
    public async Task N2_OfficialClient_ListAndCall()
    {
        using var server = CreateServer();
        await using var client = await McpClient.CreateAsync(CreateTransport(server));

        // 官方客户端内部会发起 initialize → notifications/initialized → tools/list → tools/call 多条请求
        var tools = await client.ListToolsAsync();
        Assert.Contains(tools, t => t.Name == "add");

        var result = await client.CallToolAsync("add", new System.Collections.Generic.Dictionary<String, Object?> { ["a"] = 7, ["b"] = 8 });
        Assert.False(result.IsError);
        Assert.Equal("15", result.Content.OfType<TextContentBlock>().First().Text);
    }

    [Fact]
    [DisplayName("N2-官方客户端 ping HttpMcpServer")]
    public async Task N2_OfficialClient_Ping()
    {
        using var server = CreateServer();
        await using var client = await McpClient.CreateAsync(CreateTransport(server));

        var result = await client.PingAsync();

        Assert.NotNull(result);
    }

    [Fact]
    [DisplayName("HttpClient 连续多请求（复用连接池）访问 HttpMcpServer 正常")]
    public async Task HttpClient_MultipleRequests_WithReusedClient()
    {
        using var server = CreateServer();
        using var client = new HttpClient();

        for (var i = 0; i < 3; i++)
        {
            var response = await client.PostAsync($"http://localhost:{server.Port}/",
                new StringContent(new NewLife.AI.ModelContextProtocol.JsonRpcRequest("2.0", "tools/list", null, i + 1).ToJson(false, false, true), Encoding.UTF8, "application/json"));
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync();
            var rpc = body.ToJsonEntity<NewLife.AI.ModelContextProtocol.JsonRpcResponse>();
            Assert.NotNull(rpc);
            Assert.Null(rpc.Error);
            Assert.Contains("add", body);
        }
    }

    [Fact]
    [DisplayName("诊断-JsonContent(chunked 编码)请求体访问 HttpMcpServer")]
    public async Task Diagnostic_JsonContent_ChunkedBody()
    {
        using var server = CreateServer();
        using var client = new HttpClient();

        // 官方客户端用 JsonContent.Create 构造请求体（内部走 Transfer-Encoding: chunked），
        // 复现其请求体编码方式
        var response = await client.PostAsync($"http://localhost:{server.Port}/",
            JsonContent.Create(new NewLife.AI.ModelContextProtocol.JsonRpcRequest("2.0", "tools/list", null, 1)));
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("add", body);
    }

    [Fact]
    [DisplayName("诊断-原始 chunked 请求抓包：返回 200 且工具列表正确")]
    public async Task Diagnostic_RawChunked_WireCapture()
    {
        using var server = CreateServer();
        using var tcp = new TcpClient();
        await tcp.ConnectAsync("localhost", server.Port);
        var stream = tcp.GetStream();

        var json = "{\"jsonrpc\":\"2.0\",\"method\":\"tools/list\",\"id\":1}";
        var hex = json.Length.ToString("x");
        var raw = $"POST / HTTP/1.1\r\nHost: localhost:{server.Port}\r\nTransfer-Encoding: chunked\r\nContent-Type: application/json\r\nAccept: application/json, text/event-stream\r\n\r\n{hex}\r\n{json}\r\n0\r\n\r\n";
        var bytes = Encoding.UTF8.GetBytes(raw);
        await stream.WriteAsync(bytes);

        var buf = new Byte[8192];
        var n = await stream.ReadAsync(buf);
        var resp = Encoding.UTF8.GetString(buf, 0, n);
        _output.WriteLine("=== 服务器响应原始字节 ===");
        _output.WriteLine(resp.Replace("\r", "\\r").Replace("\n", "\\n"));
        // chunked 解码生效后应返回 200 与工具列表，而非 500/400
        Assert.StartsWith("HTTP/1.1 200", resp);
        Assert.Contains("\"add\"", resp);
    }
}
