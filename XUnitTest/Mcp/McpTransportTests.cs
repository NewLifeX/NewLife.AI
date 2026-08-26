using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using NewLife.AI.ModelContextProtocol;
using NewLife.Serialization;
using Xunit;

namespace XUnitTest.Mcp;

/// <summary>MCP 传输层测试。覆盖线上格式（camelCase 规范字段）、深度嵌套、stdio 服务端、HttpMcpServer 真实 HTTP 往返</summary>
public class McpTransportTests
{
    #region 线上格式
    [Fact]
    [DisplayName("请求序列化—jsonrpc 字段全小写，params 为空省略")]
    public void Request_Serialize_CamelCase_SpecCompliant()
    {
        var request = new JsonRpcRequest("2.0", "tools/list", null, 1);

        var json = request.ToJson(false, false, true);

        Assert.Contains("\"jsonrpc\":\"2.0\"", json);
        Assert.Contains("\"method\":\"tools/list\"", json);
        Assert.Contains("\"id\":1", json);
        // params 为空应省略（nullValue=false）
        Assert.DoesNotContain("\"params\"", json);
        // 不得出现 PascalCase 或错误驼峰
        Assert.DoesNotContain("jsonRpc", json);
        Assert.DoesNotContain("JsonRpc", json);
    }

    [Fact]
    [DisplayName("响应序列化—jsonrpc 字段全小写，result/error 驼峰")]
    public void Response_Serialize_CamelCase_SpecCompliant()
    {
        var response = new JsonRpcResponse("2.0", new ToolCallResult([new ContentItem("text", "你好")]), null, 1);

        var json = response.ToJson(false, false, true);

        Assert.Contains("\"jsonrpc\":\"2.0\"", json);
        Assert.Contains("\"result\":", json);
        Assert.Contains("\"content\":", json);
        Assert.Contains("\"type\":\"text\"", json);
        Assert.Contains("\"text\":\"你好\"", json);
        Assert.Contains("\"id\":1", json);
        // Error 为空应省略
        Assert.DoesNotContain("\"error\"", json);
    }

    [Fact]
    [DisplayName("响应反序列化—大小写不敏感，兼容官方客户端 camelCase")]
    public void Response_Deserialize_CaseInsensitive()
    {
        var json = "{\"jsonrpc\":\"2.0\",\"result\":{\"tools\":[{\"name\":\"get_time\",\"description\":\"时间\",\"inputSchema\":{\"type\":\"object\"}}]},\"id\":1}";

        var response = json.ToJsonEntity<JsonRpcResponse>();

        Assert.NotNull(response);
        Assert.Equal("2.0", response.JsonRpc);
        Assert.Equal(1, response.Id);
        var result = response.Result?.ToJson().ToJsonEntity<ToolListResult>();
        Assert.NotNull(result);
        Assert.Single(result.Tools);
        Assert.Equal("get_time", result.Tools[0].Name);
    }
    #endregion

    #region 深度嵌套（MaxDepth）
    [Fact]
    [DisplayName("工具列表序列化—嵌套 schema 不被 MaxDepth 截断")]
    public void ToolList_Serialize_DeepSchema_NotTruncated()
    {
        // 模拟工具 schema：properties 内再嵌套子属性，深度超过 JsonWriter 默认 MaxDepth=5
        var schema = new
        {
            type = "object",
            properties = new Dictionary<String, Object>
            {
                ["query"] = new { type = "string", description = "关键词" },
                ["filter"] = new
                {
                    type = "object",
                    properties = new Dictionary<String, Object>
                    {
                        ["category"] = new { type = "string" },
                        ["limit"] = new { type = "integer", minimum = 1 },
                    },
                },
            },
            required = new[] { "query" },
        };
        var result = new ToolListResult([new ToolDefinition("search", "搜索", schema)]);

        var json = result.ToJson(false, false, true);

        // 关键字段必须完整保留（若被 MaxDepth 截断，description/minimum 等会丢失）
        Assert.Contains("\"name\":\"search\"", json);
        Assert.Contains("\"inputSchema\":", json);
        Assert.Contains("\"description\":\"关键词\"", json);
        Assert.Contains("\"minimum\":1", json);
        Assert.Contains("\"required\":", json);
    }
    #endregion

    #region Stdio 服务端
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

    [Fact]
    [DisplayName("Stdio服务端—读取 stdin 逐行 JSON-RPC 并回写响应")]
    public async Task Stdio_Run_ReadsLinesAndWritesResponses()
    {
        var server = new StdioMcpServer();
        server.AddTool<TestTools>(server);

        var input = new MemoryStream();
        var output = new MemoryStream();
        server.Input = input;
        server.Output = output;

        // 两行请求：tools/list + tools/call
        var req1 = new JsonRpcRequest("2.0", "tools/list", null, 1).ToJson(false, false, true);
        var req2 = new JsonRpcRequest("2.0", "tools/call", new ToolCallParams("add", new Dictionary<String, Object?> { ["a"] = 2, ["b"] = 3 }, null), 2).ToJson(false, false, true);
        var text = $"{req1}\n{req2}\n";
        input.Write(Encoding.UTF8.GetBytes(text), 0, text.Length);
        input.Position = 0;

        var task = Task.Run(() => server.Run());
        await task.WaitAsync(TimeSpan.FromSeconds(10));

        var outputText = Encoding.UTF8.GetString(output.ToArray());
        var lines = outputText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);

        var list = lines[0].ToJsonEntity<JsonRpcResponse>();
        Assert.Equal("2.0", list.JsonRpc);
        Assert.Equal(1, list.Id);
        var tools = list.Result?.ToJson().ToJsonEntity<ToolListResult>();
        Assert.NotNull(tools);
        Assert.Contains(tools.Tools, t => t.Name == "get_time" || t.Name == "add");

        var call = lines[1].ToJsonEntity<JsonRpcResponse>();
        Assert.Equal(2, call.Id);
        Assert.Null(call.Error);
        var result = call.Result?.ToJson().ToJsonEntity<ToolCallResult>();
        Assert.NotNull(result);
        Assert.Equal("5", result.Content[0].Text);
    }

    [Fact]
    [DisplayName("Stdio服务端—ping 请求返回空结果")]
    public async Task Stdio_Run_Ping_ReturnsEmpty()
    {
        var server = new StdioMcpServer();

        var input = new MemoryStream();
        var output = new MemoryStream();
        server.Input = input;
        server.Output = output;

        var req = new JsonRpcRequest("2.0", "ping", null, 1).ToJson(false, false, true);
        var text = req + "\n";
        input.Write(Encoding.UTF8.GetBytes(text), 0, text.Length);
        input.Position = 0;

        var task = Task.Run(() => server.Run());
        await task.WaitAsync(TimeSpan.FromSeconds(10));

        var outputText = Encoding.UTF8.GetString(output.ToArray());
        var line = outputText.Split('\n', StringSplitOptions.RemoveEmptyEntries)[0];
        var response = line.ToJsonEntity<JsonRpcResponse>();
        Assert.Equal("2.0", response.JsonRpc);
        Assert.Equal(1, response.Id);
        Assert.NotNull(response.Result);
        Assert.Null(response.Error);
    }
    #endregion

    #region HttpMcpServer 真实 HTTP 往返
    [Fact]
    [DisplayName("HttpMcpServer—JSON 响应模式（Accept 不含 SSE）完整链路")]
    public async Task Http_JsonMode_Initialize_List_Call()
    {
        using var server = new HttpMcpServer { Port = 0, ResponseFormat = McpResponseFormat.Json };
        server.AddTool<TestTools>(server);
        server.Start();

        using var client = new HttpClient();
        var baseUrl = $"http://localhost:{server.Port}/";

        // initialize
        var init = await PostAsync(client, baseUrl, new JsonRpcRequest("2.0", "initialize", new InitializeParams("2025-06-18", new ClientInfo("TestClient", "1.0.0")), 1));
        Assert.Equal("2.0", init.JsonRpc);
        Assert.Null(init.Error);

        // tools/list
        var list = await PostAsync(client, baseUrl, new JsonRpcRequest("2.0", "tools/list", null, 2));
        Assert.Null(list.Error);
        var tools = list.Result?.ToJson().ToJsonEntity<ToolListResult>();
        Assert.NotNull(tools);
        Assert.Contains(tools.Tools, t => t.Name == "get_time");

        // tools/call
        var call = await PostAsync(client, baseUrl, new JsonRpcRequest("2.0", "tools/call", new ToolCallParams("add", new Dictionary<String, Object?> { ["a"] = 10, ["b"] = 32 }, null), 3));
        Assert.Null(call.Error);
        var result = call.Result?.ToJson().ToJsonEntity<ToolCallResult>();
        Assert.NotNull(result);
        Assert.Equal("42", result.Content[0].Text);
    }

    [Fact]
    [DisplayName("HttpMcpServer—SSE 响应模式（Accept 含 text/event-stream）")]
    public async Task Http_SseMode_ReturnsEventStream()
    {
        using var server = new HttpMcpServer { Port = 0, ResponseFormat = McpResponseFormat.Sse };
        server.Start();

        using var client = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Post, $"http://localhost:{server.Port}/")
        {
            Content = new StringContent(new JsonRpcRequest("2.0", "ping", null, 1).ToJson(false, false, true), Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("Accept", "text/event-stream");

        var httpResponse = await client.SendAsync(request);
        httpResponse.EnsureSuccessStatusCode();
        var body = await httpResponse.Content.ReadAsStringAsync();

        Assert.Contains("text/event-stream", httpResponse.Content.Headers.ContentType?.ToString());
        Assert.StartsWith("event: message", body);
        Assert.Contains("data: {", body);
        Assert.Contains("\"jsonrpc\":\"2.0\"", body);
    }

    [Fact]
    [DisplayName("HttpMcpServer—GET 方法返回 405")]
    public async Task Http_GetMethod_Returns405()
    {
        using var server = new HttpMcpServer { Port = 0 };
        server.Start();

        using var client = new HttpClient();
        var response = await client.GetAsync($"http://localhost:{server.Port}/");

        Assert.Equal(System.Net.HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    private static async Task<JsonRpcResponse> PostAsync(HttpClient client, String url, JsonRpcRequest request)
    {
        var httpResponse = await client.PostAsync(url, new StringContent(request.ToJson(false, false, true), Encoding.UTF8, "application/json"));
        httpResponse.EnsureSuccessStatusCode();
        var body = await httpResponse.Content.ReadAsStringAsync();
        var response = body.ToJsonEntity<JsonRpcResponse>();
        Assert.NotNull(response);
        return response;
    }
    #endregion
}
