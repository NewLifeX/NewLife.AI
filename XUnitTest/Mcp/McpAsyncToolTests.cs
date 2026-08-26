using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using NewLife.AI.ModelContextProtocol;
using Xunit;

namespace XUnitTest.Mcp;

/// <summary>MCP 工具异步调用与参数转换测试（覆盖 McpTool 自包含调用的能力）</summary>
[DisplayName("MCP 异步工具与参数转换测试")]
public class McpAsyncToolTests
{
    /// <summary>异步与类型转换工具类</summary>
    public class AsyncTools
    {
        /// <summary>异步回显</summary>
        /// <param name="message">消息</param>
        /// <returns>原样返回</returns>
        public async Task<String> EchoAsync(String message) { await Task.Delay(1); return message; }

        /// <summary>异步求和</summary>
        /// <param name="a">第一个数</param>
        /// <param name="b">第二个数</param>
        /// <returns>和</returns>
        public async Task<Int32> SumAsync(Int32 a, Int32 b) { await Task.Delay(1); return a + b; }

        /// <summary>异步无返回值</summary>
        /// <param name="message">消息</param>
        public async Task DoAsync(String message) { await Task.Delay(1); }

        /// <summary>字符串转数字</summary>
        /// <param name="value">数字字符串</param>
        /// <returns>数字</returns>
        public Int32 Parse(String value) => Int32.Parse(value);

        /// <summary>处理字节数组参数（Base64 解码绑定）</summary>
        /// <param name="data">Base64 编码的字节数组</param>
        /// <returns>字节长度</returns>
        public String ProcessBytes(Byte[] data) => data.Length.ToString();
    }

    private static McpServer CreateServer()
    {
        var server = new McpServer();
        server.AddTool<AsyncTools>(server);
        return server;
    }

    private static McpContext CreateContext(McpServer server)
        => new()
        {
            Services = server,
            GetRequest = key => key == "Mcp-Session-Id" ? "test-session" : null,
            SetResponse = (key, value) => { },
        };

    private static JsonRpcResponse Call(McpServer server, String tool, IDictionary<String, Object?> args)
        => server.Process(new JsonRpcRequest("2.0", "tools/call", new ToolCallParams(tool, new Dictionary<String, Object?>(args), null), 1), CreateContext(server));

    private static String CallText(McpServer server, String tool, IDictionary<String, Object?> args)
    {
        var response = Call(server, tool, args);
        Assert.Null(response.Error);
        var result = response.Result as ToolCallResult;
        Assert.NotNull(result);
        return result.Content[0].Text;
    }

    [Fact]
    [DisplayName("异步 Task&lt;String&gt; 工具—返回结果正确")]
    public void Async_TaskString_ReturnsResult()
    {
        var server = CreateServer();
        Assert.Equal("你好世界", CallText(server, "echo", new Dictionary<String, Object?> { ["message"] = "你好世界" }));
    }

    [Fact]
    [DisplayName("异步 Task&lt;Int32&gt; 工具—数值结果转文本")]
    public void Async_TaskInt32_ReturnsNumber()
    {
        var server = CreateServer();
        Assert.Equal("7", CallText(server, "sum", new Dictionary<String, Object?> { ["a"] = 3, ["b"] = 4 }));
    }

    [Fact]
    [DisplayName("异步 Task 无返回值工具—调用成功返回空")]
    public void Async_Task_NoReturn_Succeeds()
    {
        var server = CreateServer();
        Assert.Equal("", CallText(server, "do", new Dictionary<String, Object?> { ["message"] = "x" }));
    }

    [Fact]
    [DisplayName("参数转换—字符串数字正确转为 Int32")]
    public void Conversion_StringToInt32_Works()
    {
        var server = CreateServer();
        Assert.Equal("42", CallText(server, "parse", new Dictionary<String, Object?> { ["value"] = "42" }));
    }

    [Fact]
    [DisplayName("参数转换—非法数字返回 InvalidParams")]
    public void Conversion_InvalidNumber_ReturnsInvalidParams()
    {
        var server = CreateServer();
        var response = Call(server, "parse", new Dictionary<String, Object?> { ["value"] = "abc" });

        var error = response.Error as JsonRpcError;
        Assert.NotNull(error);
        Assert.Equal(McpErrorCode.InvalidParams, error.Code);
    }

    [Fact]
    [DisplayName("byte[] 参数—Base64 字符串解码后绑定")]
    public void ByteArrayParam_Base64Decoded()
    {
        var server = CreateServer();
        var base64 = Convert.ToBase64String(new Byte[] { 1, 2, 3, 4, 5 });
        Assert.Equal("5", CallText(server, "process_bytes", new Dictionary<String, Object?> { ["data"] = base64 }));
    }
}
