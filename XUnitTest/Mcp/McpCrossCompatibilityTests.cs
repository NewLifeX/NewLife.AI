using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using NewLife.AI.Extensions;
using NewLife.AI.ModelContextProtocol;
using Xunit;

namespace XUnitTest.Mcp;

/// <summary>MCP 官方 SDK 交叉兼容测试。用 NuGet 官方 ModelContextProtocol 客户端连接我们的 AspNetMcpServer（真实 Kestrel 宿主），验证线上格式与协议互通（N2：你→我）</summary>
public class McpCrossCompatibilityTests
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

    /// <summary>构建带工具/资源/提示词的 Kestrel 宿主 MCP 服务器</summary>
    private static async Task<(WebApplication app, Uri endpoint)> CreateServer()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddMcp<TestTools>();
        var app = builder.Build();
        app.MapMcp("/mcp", s =>
        {
            s.ResponseFormat = McpResponseFormat.Json;
            s.AddResource("knowledge://articles/1", "文章1", "示例文章", "text/plain", _ => "这是文章内容");
            // 提示词名用 ASCII：官方客户端 v2.2.0 会把提示词参数注入 Mcp-Param-* HTTP 头，中文会触发“headers must contain only ASCII”
            s.AddPrompt("knowledge_qa", "基于知识库回答", get: _ => "这是回答内容");
        }, typeof(TestTools));
        await app.StartAsync();

        var feature = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!;
        var endpoint = new Uri(feature.Addresses.First() + "/mcp");
        return (app, endpoint);
    }

    private static async Task<McpClient> CreateClient(Uri endpoint)
        => await McpClient.CreateAsync(new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = endpoint,
            TransportMode = HttpTransportMode.StreamableHttp,
            // 服务器无 server→client 推送，关闭 standalone GET 流（官方文档建议）
            EnableStandaloneGetStream = false,
        }));

    [Fact]
    [DisplayName("N2-官方客户端能连接我们的服务端并发现工具")]
    public async Task N2_OfficialClient_ListTools()
    {
        var (app, endpoint) = await CreateServer();
        await using var client = await CreateClient(endpoint);

        var tools = await client.ListToolsAsync();

        Assert.NotNull(tools);
        Assert.Contains(tools, t => t.Name == "get_time");
        Assert.Contains(tools, t => t.Name == "add");

        await app.StopAsync();
        await app.DisposeAsync();
    }

    [Fact]
    [DisplayName("N2-官方客户端能调用我们的工具并得到正确结果")]
    public async Task N2_OfficialClient_CallTool()
    {
        var (app, endpoint) = await CreateServer();
        await using var client = await CreateClient(endpoint);

        var result = await client.CallToolAsync("add", new Dictionary<String, Object?> { ["a"] = 10, ["b"] = 32 });

        Assert.False(result.IsError);
        var text = result.Content.OfType<TextContentBlock>().First().Text;
        Assert.Equal("42", text);

        await app.StopAsync();
        await app.DisposeAsync();
    }

    [Fact]
    [DisplayName("N2-官方客户端能发现并读取我们的资源")]
    public async Task N2_OfficialClient_ListAndReadResources()
    {
        var (app, endpoint) = await CreateServer();
        await using var client = await CreateClient(endpoint);

        var resources = await client.ListResourcesAsync();
        Assert.Contains(resources, r => r.Name == "文章1");

        var content = await client.ReadResourceAsync("knowledge://articles/1");
        Assert.NotNull(content);
        var text = content.Contents.OfType<TextResourceContents>().First().Text;
        Assert.Equal("这是文章内容", text);

        await app.StopAsync();
        await app.DisposeAsync();
    }

    [Fact]
    [DisplayName("N2-官方客户端能发现并获取我们的提示词")]
    public async Task N2_OfficialClient_ListAndGetPrompts()
    {
        var (app, endpoint) = await CreateServer();
        await using var client = await CreateClient(endpoint);

        var prompts = await client.ListPromptsAsync();
        Assert.Contains(prompts, p => p.Name == "knowledge_qa");

        var result = await client.GetPromptAsync("knowledge_qa");
        Assert.NotNull(result);
        var text = ((TextContentBlock)result.Messages.First().Content).Text;
        Assert.Equal("这是回答内容", text);

        await app.StopAsync();
        await app.DisposeAsync();
    }

    [Fact]
    [DisplayName("N2-官方客户端 ping 我们的服务端")]
    public async Task N2_OfficialClient_Ping()
    {
        var (app, endpoint) = await CreateServer();
        await using var client = await CreateClient(endpoint);

        var result = await client.PingAsync();

        Assert.NotNull(result);

        await app.StopAsync();
        await app.DisposeAsync();
    }
}
