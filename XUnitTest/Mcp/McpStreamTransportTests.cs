using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO.Pipelines;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using NewLife.AI.Extensions;
using NewLife.AI.ModelContextProtocol;
using Xunit;

namespace XUnitTest.Mcp;

/// <summary>in-memory/Stream 传输交叉验证（对标官方 StreamClientTransport/StreamServerTransport，P0 计划项）
/// 验证：官方客户端通过 Stream 传输进程内直连我们的 StdioMcpServer（注入任意流）；官方客户端对协议错误码的识别</summary>
public class McpStreamTransportTests
{
    /// <summary>测试工具类</summary>
    public class TestTools
    {
        /// <summary>计算两数之和</summary>
        /// <param name="a">第一个数</param>
        /// <param name="b">第二个数</param>
        /// <returns>两数之和</returns>
        public Int32 Add(Int32 a, Int32 b) => a + b;
    }

    #region in-memory Stream 交叉验证
    [Fact]
    [DisplayName("N2-官方 StreamClientTransport 进程内连 StdioMcpServer（in-memory 双流配对）")]
    public async Task N2_OfficialStreamClient_ConnectsToStdioServer()
    {
        // 双管道配对：toServer 客户端写→服务器读；toClient 服务器写→客户端读
        var toServer = new Pipe();
        var toClient = new Pipe();

        var server = new StdioMcpServer
        {
            Input = toServer.Reader.AsStream(),
            Output = toClient.Writer.AsStream(),
        };
        server.AddTool<TestTools>(server);
        var serverTask = Task.Run(() => server.Run());

        // 官方客户端走 Stream 传输（in-memory，无网络）
        var clientTransport = new StreamClientTransport(toServer.Writer.AsStream(), toClient.Reader.AsStream());
        await using var client = await McpClient.CreateAsync(clientTransport);

        // 工具发现
        var tools = await client.ListToolsAsync();
        Assert.Contains(tools, t => t.Name == "add");

        // 工具调用
        var result = await client.CallToolAsync("add", new Dictionary<String, Object?> { ["a"] = 7, ["b"] = 8 });
        Assert.False(result.IsError);
        Assert.Equal("15", result.Content.OfType<TextContentBlock>().First().Text);

        // 关闭流让服务器读取循环退出
        toServer.Writer.Complete();
        toClient.Writer.Complete();
        await serverTask.WaitAsync(TimeSpan.FromSeconds(5));
    }
    #endregion

    #region 官方客户端错误场景（Kestrel 生产路径）
    private static async Task<(WebApplication app, Uri endpoint)> CreateServer()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddMcp<TestTools>();
        var app = builder.Build();
        app.MapMcp("/mcp", s => { s.ResponseFormat = McpResponseFormat.Json; }, typeof(TestTools));
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
            EnableStandaloneGetStream = false,
        }));

    [Fact]
    [DisplayName("N2-官方客户端调用不存在的工具—抛异常且错误码为 InvalidParams(-32602)")]
    public async Task N2_OfficialClient_UnknownTool_Throws()
    {
        var (app, endpoint) = await CreateServer();
        await using var client = await CreateClient(endpoint);

        // 我们的服务端对未知工具返回 InvalidParams error envelope，官方客户端应识别并抛出协议异常
        var ex = await Assert.ThrowsAsync<McpProtocolException>(async () => await client.CallToolAsync("nonexistent", new Dictionary<String, Object?>()));
        Assert.Equal(NewLife.AI.ModelContextProtocol.McpErrorCode.InvalidParams, (Int32)ex.ErrorCode);
    }

    [Fact]
    [DisplayName("N2-官方客户端调用缺参数工具—抛异常（参数校验失败）")]
    public async Task N2_OfficialClient_MissingParams_Throws()
    {
        var (app, endpoint) = await CreateServer();
        await using var client = await CreateClient(endpoint);

        // add 需要 a/b 参数，传空字典触发 InvalidParams
        await Assert.ThrowsAsync<McpProtocolException>(async () => await client.CallToolAsync("add", new Dictionary<String, Object?>()));
    }
    #endregion
}
