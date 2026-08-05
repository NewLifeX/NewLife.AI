#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using NewLife.AI.Clients;
using NewLife.AI.Clients.Ollama;
using NewLife.AI.Models;
using NewLife.Remoting;
using Xunit;

namespace XUnitTest.Gateway;

/// <summary>Ollama 客户端 ↔ 网关 闭环集成测试。用项目自身的 OllamaChatClient 出站调用 ChatAI 网关的 Ollama 入站协议，
/// 验证「出站客户端 + 入站网关」协议完全兼容（我→我闭环），无需外部真实 Ollama 服务</summary>
/// <remarks>
/// 通过 ChatAIWebAppFactory 在进程内启动 ChatAI 网关，数据库已配置密钥 sk-NewLifeAI2026 与模型 qwen3.6-flash。
/// OllamaChatClient 注入指向 TestServer 的 HttpClient，请求 http://localhost/api/chat 由网关进程内处理，形成完整闭环。
/// 上游模型（DashScope）暂不可用时网关返回 502，测试跳过该场景。
/// </remarks>
public class OllamaGatewayRoundTripTests : IDisposable, IClassFixture<ChatAIWebAppFactory>
{
    private const String ApiKey = "sk-NewLifeAI2026";
    private const String TestModel = "qwen3.6-flash";

    private readonly HttpClient _testServer;

    public OllamaGatewayRoundTripTests(ChatAIWebAppFactory factory)
    {
        _testServer = factory.CreateDefaultClient();
        _testServer.Timeout = TimeSpan.FromSeconds(60);
    }

    /// <inheritdoc/>
    public void Dispose() => _testServer.Dispose();

    /// <summary>构建指向网关 TestServer 的 Ollama 客户端。注入 HttpClient 实现出站 → 入站闭环</summary>
    /// <param name="apiKey">API 密钥</param>
    /// <returns>Ollama 客户端实例</returns>
    private OllamaChatClient CreateClient(String apiKey = ApiKey)
    {
        var client = new OllamaChatClient(new AiClientOptions
        {
            Endpoint = "http://localhost",
            Model = TestModel,
            ApiKey = apiKey,
        });
        // 注入指向网关 TestServer 的 HttpClient，请求 http://localhost/api/chat 由网关进程内处理
        client.HttpClient = _testServer;
        return client;
    }

    #region 非流式闭环
    [Fact]
    [DisplayName("闭环_非流式_客户端调用网关返回Ollama协议响应")]
    public async Task RoundTrip_NonStream_Returns_OllamaResponse()
    {
        using var client = CreateClient();
        try
        {
            var resp = await client.GetResponseAsync(
                [new ChatMessage { Role = "user", Content = "1+1=" }],
                new ChatOptions { MaxTokens = 32 });

            var ollama = Assert.IsType<OllamaChatResponse>(resp);
            Assert.Equal(TestModel, ollama.Model);
            Assert.True(ollama.Done);
            Assert.False(String.IsNullOrWhiteSpace(ollama.Text), "响应内容不应为空");
        }
        catch (ApiException ex) when (ex.Code == 502)
        {
            // 上游模型服务暂不可用，非网关协议问题，跳过
        }
    }
    #endregion

    #region 流式闭环
    [Fact]
    [DisplayName("闭环_流式_客户端逐帧解析网关NDJSON")]
    public async Task RoundTrip_Stream_Collects_Chunks()
    {
        using var client = CreateClient();
        try
        {
            var chunks = new List<IChatResponse>();
            await foreach (var chunk in client.GetStreamingResponseAsync(
                [new ChatMessage { Role = "user", Content = "1+1=" }],
                new ChatOptions { MaxTokens = 32 }))
            {
                chunks.Add(chunk);
            }

            Assert.True(chunks.Count > 0, "流式应至少返回一个块");
            // 思考模型可能先输出 thinking 帧（Text 为 null），content 帧聚合出最终文本
            var text = String.Concat(chunks.Select(c => c.Text));
            Assert.False(String.IsNullOrWhiteSpace(text), "流式聚合文本不应为空");
            // 末帧为 done 帧（无 message 字段），通过原生属性验证完成状态
            var last = Assert.IsType<OllamaChatResponse>(chunks.Last());
            Assert.True(last.Done, "末帧应标记完成");
            Assert.False(String.IsNullOrWhiteSpace(last.DoneReason), "末帧应携带完成原因");
        }
        catch (ApiException ex) when (ex.Code == 502)
        {
            // 上游模型服务暂不可用，非网关协议问题，跳过
        }
    }
    #endregion

    #region 错误路径
    [Fact]
    [DisplayName("闭环_无效密钥_客户端收到网关401")]
    public async Task RoundTrip_InvalidKey_Throws_401()
    {
        using var client = CreateClient("sk-invalid-key-xyz-000");

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            client.GetResponseAsync([new ChatMessage { Role = "user", Content = "hi" }]));
        Assert.Equal(401, ex.Code);
    }
    #endregion
}
