using System;
using System.Collections.Generic;
using NewLife.AI.ModelContextProtocol;
using NewLife.Log;
using NewLife.Remoting;
using Xunit;

namespace XUnitTest.Mcp;

/// <summary>McpServer 能力测试。覆盖 ping / resources / prompts / 取消通知（对标官方 MCP SDK P0+P1 能力）</summary>
public class McpServerCapabilitiesTests
{
    #region 测试辅助
    /// <summary>Mock服务提供者</summary>
    private class MockServiceProvider : IServiceProvider
    {
        private readonly Dictionary<Type, Object> _services = new();

        public void AddService<T>(T service) where T : class => _services[typeof(T)] = service;

        public Object? GetService(Type serviceType) => _services.TryGetValue(serviceType, out var service) ? service : null;
    }

    /// <summary>创建测试上下文</summary>
    private static McpContext CreateTestContext(IServiceProvider? serviceProvider = null)
        => new()
        {
            Services = serviceProvider ?? new MockServiceProvider(),
            GetRequest = key => key == "Mcp-Session-Id" ? "test-session-123" : null,
            SetResponse = (key, value) => { },
        };
    #endregion

    #region Ping
    [Fact]
    public void Process_WithPing_ShouldReturnEmptyResult()
    {
        var server = new McpServer();
        var context = CreateTestContext();
        var request = new JsonRpcRequest("2.0", "ping", null, 1);

        var response = server.Process(request, context);

        Assert.Equal("2.0", response.JsonRpc);
        Assert.NotNull(response.Result);
        Assert.Null(response.Error);
        Assert.Equal(1, response.Id);
    }
    #endregion

    #region Resources
    [Fact]
    public void Process_WithResourceListEmpty_ShouldReturnEmptyList()
    {
        var server = new McpServer();
        var context = CreateTestContext();
        var request = new JsonRpcRequest("2.0", "resources/list", null, 1);

        var response = server.Process(request, context);

        var result = response.Result as ResourceListResult;
        Assert.NotNull(result);
        Assert.NotNull(result.Resources);
        Assert.Empty(result.Resources);
    }

    [Fact]
    public void Process_WithResourceList_ShouldReturnRegisteredResources()
    {
        var server = new McpServer();
        server.AddResource("knowledge://articles/1", "文章1", "测试文章", "text/plain", uri => "文章内容");
        server.AddResource("knowledge://articles/2", "文章2");

        var context = CreateTestContext();
        var request = new JsonRpcRequest("2.0", "resources/list", null, 1);

        var response = server.Process(request, context);

        var result = response.Result as ResourceListResult;
        Assert.NotNull(result);
        Assert.Equal(2, result.Resources.Count);
        Assert.Contains(result.Resources, r => r.Uri == "knowledge://articles/1" && r.Name == "文章1" && r.MimeType == "text/plain");
        Assert.Contains(result.Resources, r => r.Uri == "knowledge://articles/2" && r.MimeType == "text/plain");
    }

    [Fact]
    public void Process_WithResourceRead_ShouldReturnContent()
    {
        var server = new McpServer();
        server.AddResource("knowledge://articles/1", "文章1", "测试文章", "text/plain", uri => "这是文章内容");

        var context = CreateTestContext();
        var ps = new ResourceReadParams("knowledge://articles/1");
        var request = new JsonRpcRequest("2.0", "resources/read", ps, 1);

        var response = server.Process(request, context);

        Assert.Null(response.Error);
        var result = response.Result as ReadResourceResult;
        Assert.NotNull(result);
        Assert.Single(result.Contents);
        Assert.Equal("knowledge://articles/1", result.Contents[0].Uri);
        Assert.Equal("这是文章内容", result.Contents[0].Text);
    }

    [Fact]
    public void Process_WithResourceReadNotFound_ShouldReturnError()
    {
        var server = new McpServer();
        var context = CreateTestContext();
        var ps = new ResourceReadParams("knowledge://articles/missing");
        var request = new JsonRpcRequest("2.0", "resources/read", ps, 1);

        var response = server.Process(request, context);

        Assert.Null(response.Result);
        var error = response.Error as JsonRpcError;
        Assert.NotNull(error);
        // MCP 2025-06-18 协议：未知资源 URI 返回 ResourceNotFound（-32002）
        Assert.Equal(McpErrorCode.ResourceNotFound, error.Code);
        Assert.Contains("not found", error.Message);
    }

    [Fact]
    public void Process_WithResourceReadNullParams_ShouldReturnError()
    {
        var server = new McpServer();
        var context = CreateTestContext();
        var request = new JsonRpcRequest("2.0", "resources/read", null, 1);

        var response = server.Process(request, context);

        Assert.Null(response.Result);
        var error = response.Error as JsonRpcError;
        Assert.NotNull(error);
        // 参数缺失属于协议级无效参数（-32602）
        Assert.Equal(McpErrorCode.InvalidParams, error.Code);
    }
    #endregion

    #region Prompts
    [Fact]
    public void Process_WithPromptListEmpty_ShouldReturnEmptyList()
    {
        var server = new McpServer();
        var context = CreateTestContext();
        var request = new JsonRpcRequest("2.0", "prompts/list", null, 1);

        var response = server.Process(request, context);

        var result = response.Result as PromptListResult;
        Assert.NotNull(result);
        Assert.NotNull(result.Prompts);
        Assert.Empty(result.Prompts);
    }

    [Fact]
    public void Process_WithPromptList_ShouldReturnRegisteredPrompts()
    {
        var server = new McpServer();
        server.AddPrompt("知识问答", "基于知识库回答", arguments: [new PromptArgument("question", "问题", true)], get: _ => "回答");

        var context = CreateTestContext();
        var request = new JsonRpcRequest("2.0", "prompts/list", null, 1);

        var response = server.Process(request, context);

        var result = response.Result as PromptListResult;
        Assert.NotNull(result);
        Assert.Single(result.Prompts);
        Assert.Equal("知识问答", result.Prompts[0].Name);
        Assert.NotNull(result.Prompts[0].Arguments);
        Assert.Single(result.Prompts[0].Arguments);
        Assert.True(result.Prompts[0].Arguments![0].Required);
    }

    [Fact]
    public void Process_WithPromptGet_ShouldReturnUserMessage()
    {
        var server = new McpServer();
        server.AddPrompt("知识问答", "基于知识库回答", get: _ => "这是回答内容");

        var context = CreateTestContext();
        var ps = new PromptGetParams("知识问答");
        var request = new JsonRpcRequest("2.0", "prompts/get", ps, 1);

        var response = server.Process(request, context);

        Assert.Null(response.Error);
        var result = response.Result as GetPromptResult;
        Assert.NotNull(result);
        Assert.Single(result.Messages);
        Assert.Equal("user", result.Messages[0].Role);
        Assert.Equal("text", result.Messages[0].Content.Type);
        Assert.Equal("这是回答内容", result.Messages[0].Content.Text);
    }

    [Fact]
    public void Process_WithPromptGetMessageList_ShouldReturnAsIs()
    {
        var server = new McpServer();
        server.AddPrompt("多轮问答", get: _ => (IList<PromptMessage>)
        [
            new PromptMessage("user", new ContentItem("text", "问题")),
            new PromptMessage("assistant", new ContentItem("text", "回答")),
        ]);

        var context = CreateTestContext();
        var request = new JsonRpcRequest("2.0", "prompts/get", new PromptGetParams("多轮问答"), 1);

        var response = server.Process(request, context);

        Assert.Null(response.Error);
        var result = response.Result as GetPromptResult;
        Assert.NotNull(result);
        Assert.Equal(2, result.Messages.Count);
        Assert.Equal("assistant", result.Messages[1].Role);
    }

    [Fact]
    public void Process_WithPromptGetNotFound_ShouldReturnError()
    {
        var server = new McpServer();
        var context = CreateTestContext();
        var request = new JsonRpcRequest("2.0", "prompts/get", new PromptGetParams("不存在"), 1);

        var response = server.Process(request, context);

        Assert.Null(response.Result);
        var error = response.Error as JsonRpcError;
        Assert.NotNull(error);
        // 官方 SDK：未知提示词名属于协议级无效参数（-32602）
        Assert.Equal(McpErrorCode.InvalidParams, error.Code);
    }
    #endregion

    #region 取消与能力协商
    [Fact]
    public void Process_WithCancelledNotification_ShouldReturnNull()
    {
        // JSON-RPC notification 无 Id，不得响应
        var server = new McpServer();
        var context = CreateTestContext();
        var request = new JsonRpcRequest("2.0", "notifications/cancelled", null, null);

        var response = server.Process(request, context);

        Assert.Null(response);
    }

    [Fact]
    public void Process_WithInitialize_ShouldDeclareFullCapabilities()
    {
        var server = new McpServer();
        var context = CreateTestContext();
        var request = new JsonRpcRequest("2.0", "initialize", null, 1);

        var response = server.Process(request, context);

        var result = response.Result as InitializeResult;
        Assert.NotNull(result);
        Assert.NotNull(result.Capabilities);
        Assert.NotNull(result.Capabilities.Tools);
        Assert.NotNull(result.Capabilities.Resources);
        Assert.NotNull(result.Capabilities.Prompts);
    }
    #endregion
}
