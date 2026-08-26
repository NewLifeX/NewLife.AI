using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using NewLife.AI.ModelContextProtocol;
using NewLife.Serialization;
using Xunit;

namespace XUnitTest.Mcp;

/// <summary>MCP 错误码对齐测试（对标官方 ModelContextProtocol SDK 的 McpErrorCode）
/// 验证协议级错误返回 JSON-RPC 规范负值错误码，而非 HTTP 风格状态码</summary>
public class McpErrorCodeTests
{
    /// <summary>测试工具类。含 IProgress 参数与抛异常方法</summary>
    public class TestTools
    {
        /// <summary>回显消息</summary>
        /// <param name="message">消息</param>
        /// <returns>原样返回</returns>
        public String Echo(String message) => message;

        /// <summary>报告进度。验证 IProgress 参数注入非空，工具内调用不崩</summary>
        /// <param name="progress">进度（框架注入）</param>
        /// <param name="message">消息</param>
        /// <returns>结果</returns>
        public String Report(IProgress<ProgressValue> progress, String message)
        {
            progress.Report(new ProgressValue { Message = message, Progress = 50, Total = 100 });
            return "ok:" + message;
        }

        /// <summary>抛出业务异常。验证映射为内部错误</summary>
        public String ThrowError() => throw new InvalidOperationException("业务异常");
    }

    #region 辅助
    private static McpServer CreateServer()
    {
        var server = new McpServer();
        server.AddTool<TestTools>(server);
        return server;
    }

    private static McpContext CreateContext(McpServer server)
        => new()
        {
            Services = server,
            GetRequest = key => key == "Mcp-Session-Id" ? "test-session" : null,
            SetResponse = (key, value) => { },
        };
    #endregion

    #region 错误码对齐
    [Fact]
    [DisplayName("未知方法—返回 MethodNotFound(-32601)")]
    public void UnknownMethod_ReturnsMethodNotFound()
    {
        var server = CreateServer();
        var response = server.Process(new JsonRpcRequest("2.0", "no_such_method", null, 1), CreateContext(server));

        var error = response.Error as JsonRpcError;
        Assert.NotNull(error);
        Assert.Equal(McpErrorCode.MethodNotFound, error.Code);
    }

    [Fact]
    [DisplayName("tools/call 未知工具—映射底层 404 为 MethodNotFound(-32601)")]
    public void ToolCall_UnknownTool_ReturnsMethodNotFound()
    {
        var server = CreateServer();
        var request = new JsonRpcRequest("2.0", "tools/call", new ToolCallParams("nonexistent", new Dictionary<String, Object?>(), null), 1);

        var response = server.Process(request, CreateContext(server));

        var error = response.Error as JsonRpcError;
        Assert.NotNull(error);
        Assert.Equal(McpErrorCode.MethodNotFound, error.Code);
        Assert.Contains("nonexistent", error.Message);
    }

    [Fact]
    [DisplayName("tools/call 缺参数—返回 InvalidParams(-32602)")]
    public void ToolCall_NullParams_ReturnsInvalidParams()
    {
        var server = CreateServer();
        var response = server.Process(new JsonRpcRequest("2.0", "tools/call", null, 1), CreateContext(server));

        var error = response.Error as JsonRpcError;
        Assert.NotNull(error);
        Assert.Equal(McpErrorCode.InvalidParams, error.Code);
    }

    [Fact]
    [DisplayName("tools/call 参数类型错误—返回 InvalidParams(-32602)")]
    public void ToolCall_InvalidParams_ReturnsInvalidParams()
    {
        var server = CreateServer();
        // Echo 需要 message 参数，传非字典对象触发转换失败
        var response = server.Process(new JsonRpcRequest("2.0", "tools/call", "bad-params", 1), CreateContext(server));

        var error = response.Error as JsonRpcError;
        Assert.NotNull(error);
        Assert.Equal(McpErrorCode.InvalidParams, error.Code);
    }

    [Fact]
    [DisplayName("resources/read 未找到—返回 ResourceNotFound(-32002)（2025-06-18 协议）")]
    public void ResourceRead_NotFound_ReturnsResourceNotFound()
    {
        var server = CreateServer();
        var response = server.Process(new JsonRpcRequest("2.0", "resources/read", new ResourceReadParams("knowledge://missing"), 1), CreateContext(server));

        var error = response.Error as JsonRpcError;
        Assert.NotNull(error);
        Assert.Equal(McpErrorCode.ResourceNotFound, error.Code);
    }

    [Fact]
    [DisplayName("prompts/get 未找到—返回 InvalidParams(-32602)")]
    public void PromptGet_NotFound_ReturnsInvalidParams()
    {
        var server = CreateServer();
        var response = server.Process(new JsonRpcRequest("2.0", "prompts/get", new PromptGetParams("不存在"), 1), CreateContext(server));

        var error = response.Error as JsonRpcError;
        Assert.NotNull(error);
        Assert.Equal(McpErrorCode.InvalidParams, error.Code);
    }

    [Fact]
    [DisplayName("工具抛业务异常—返回 InternalError(-32603)")]
    public void ToolCall_Throws_ReturnsInternalError()
    {
        var server = CreateServer();
        var response = server.Process(new JsonRpcRequest("2.0", "tools/call", new ToolCallParams("throw_error", new Dictionary<String, Object?>(), null), 1), CreateContext(server));

        var error = response.Error as JsonRpcError;
        Assert.NotNull(error);
        Assert.Equal(McpErrorCode.InternalError, error.Code);
        Assert.Contains("业务异常", error.Message);
    }

    [Fact]
    [DisplayName("JSON-RPC 版本错误—返回 InvalidRequest(-32600)")]
    public void WrongJsonRpcVersion_ReturnsInvalidRequest()
    {
        var server = CreateServer();
        var response = server.Process(new JsonRpcRequest("1.0", "ping", null, 1), CreateContext(server));

        var error = response.Error as JsonRpcError;
        Assert.NotNull(error);
        Assert.Equal(McpErrorCode.InvalidRequest, error.Code);
    }
    #endregion

    #region IProgress 注入
    [Fact]
    [DisplayName("工具带 IProgress 参数—调用成功不崩溃，progress 非空")]
    public void ToolCall_WithProgressParam_Succeeds()
    {
        var server = CreateServer();
        var request = new JsonRpcRequest("2.0", "tools/call",
            new ToolCallParams("report", new Dictionary<String, Object?> { ["message"] = "hello" }, null), 1);

        var response = server.Process(request, CreateContext(server));

        Assert.Null(response.Error);
        var result = response.Result as ToolCallResult;
        Assert.NotNull(result);
        Assert.Equal("ok:hello", result.Content[0].Text);
    }

    [Fact]
    [DisplayName("tools/list 跳过 IProgress 参数—schema 不含 progress 字段")]
    public void ToolList_SkipsProgressParam()
    {
        var server = CreateServer();
        var response = server.Process(new JsonRpcRequest("2.0", "tools/list", null, 1), CreateContext(server));

        var result = response.Result as ToolListResult;
        Assert.NotNull(result);
        var report = result.Tools.FirstOrDefault(t => t.Name == "report");
        Assert.NotNull(report);
        var schema = report.InputSchema?.ToJson();
        Assert.DoesNotContain("progress", schema);
        Assert.Contains("message", schema);
    }

    [Fact]
    [DisplayName("工具传空字符串参数—正常绑定不报缺失")]
    public void ToolCall_EmptyStringValue_Binds()
    {
        var server = CreateServer();
        var request = new JsonRpcRequest("2.0", "tools/call",
            new ToolCallParams("echo", new Dictionary<String, Object?> { ["message"] = "" }, null), 1);

        var response = server.Process(request, CreateContext(server));

        Assert.Null(response.Error);
        var result = response.Result as ToolCallResult;
        Assert.NotNull(result);
        Assert.Equal("", result.Content[0].Text);
    }

    [Fact]
    [DisplayName("诊断-NewLife 反序列化 JSON 空字符串值到参数字典")]
    public void Diagnostic_JsonDeserialize_EmptyStringValue()
    {
        // 复现 Kestrel 路径：System.Text.Json 得到 JsonElement，ConvertParams 走 ToJsonEntity
        var json = "{\"name\":\"search_knowledge\",\"arguments\":{\"query\":\"\"}}";
        var ps = json.ToJsonEntity<ToolCallParams>();

        Assert.NotNull(ps);
        Assert.True(ps.Arguments != null, "Arguments 不应为 null");
        Assert.True(ps.Arguments.ContainsKey("query"), "空字符串值不应丢 key");
        Assert.Equal("", ps.Arguments["query"]);
    }

    [Fact]
    [DisplayName("诊断-concat 场景反序列化参数字典")]
    public void Diagnostic_Concat_Arguments()
    {
        // AspNetMcpServerTests.MapMcp_EmptyStringParam_Binds 失败复现：
        // concat(text1, text2="World") 传 text1="" 报 Missing
        var json = "{\"name\":\"concat\",\"arguments\":{\"text1\":\"\"}}";
        var ps = json.ToJsonEntity<ToolCallParams>();

        Assert.NotNull(ps);
        Assert.True(ps.Arguments != null, "Arguments 不应为 null");
        Assert.True(ps.Arguments.ContainsKey("text1"), "text1 不应丢");
        Assert.Equal("", ps.Arguments["text1"]);
    }

    [Fact]
    [DisplayName("诊断-ToJson 序列化含空字符串值的参数字典")]
    public void Diagnostic_ToJson_EmptyStringValue()
    {
        // AspNetMcpServerTests.PostAsync 用 ToJson 序列化请求；nullValue 必须为 true 保留空字符串参数
        var json = new ToolCallParams("concat", new Dictionary<String, Object?> { ["text1"] = "" }, null).ToJson(false, true, true);

        Assert.Contains("text1", json);
    }

    [Fact]
    [DisplayName("诊断-System.Text.Json 反序列化后 Process 空字符串参数")]
    public void Diagnostic_SystemTextJsonRoundtrip_EmptyString()
    {
        // 精确复现 AspNetMcpServer：System.Text.Json 反序列化 JsonRpcRequest → Params 是 JsonElement
        var json = "{\"jsonrpc\":\"2.0\",\"method\":\"tools/call\",\"params\":{\"name\":\"echo\",\"arguments\":{\"message\":\"\"}},\"id\":1}";
        var request = System.Text.Json.JsonSerializer.Deserialize<JsonRpcRequest>(json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var server = CreateServer();
        var response = server.Process(request!, CreateContext(server));

        Assert.Null(response.Error);
        var result = response.Result as ToolCallResult;
        Assert.NotNull(result);
        Assert.Equal("", result.Content[0].Text);
    }
    #endregion
}
