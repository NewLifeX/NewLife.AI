#nullable enable
using System;
using System.ComponentModel;
using System.Net.Http;
using System.Threading.Tasks;
using NewLife.AI.Clients;
using NewLife.AI.Clients.OpenAI;
using NewLife.AI.Models;
using Xunit;
using XUnitTest.Helpers;

namespace XUnitTest.Clients;

/// <summary>响应模型回填测试。验证 ParseResponse 不再无条件覆盖服务端真实 model（P2：网关/路由场景计费归属失真）</summary>
public class RespModelPreserveTests
{
    /// <summary>构建指向 stub 地址的 OpenAI 兼容客户端</summary>
    private static OpenAIChatClient CreateClient(StubHttpMessageHandler handler)
    {
        var client = new OpenAIChatClient(new AiClientOptions
        {
            Endpoint = "https://stub.local",
            ApiKey = "test-key",
            Model = "request-model",
        });
        client.HttpClient = new HttpClient(handler);
        return client;
    }

    [Fact]
    [DisplayName("服务端返回model_保留真实模型不被请求覆盖")]
    public async Task ServerModel_Preserved()
    {
        const String json = """{"id":"chatcmpl-1","object":"chat.completion","created":1700000000,"model":"server-real-model","choices":[{"index":0,"message":{"role":"assistant","content":"hi"},"finish_reason":"stop"}],"usage":{"prompt_tokens":1,"completion_tokens":1,"total_tokens":2}}""";
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json(json));
        using var client = CreateClient(handler);

        var response = await client.GetResponseAsync(new ChatRequest
        {
            Model = "request-model",
            Messages = [new ChatMessage { Role = "user", Content = "hi" }],
        });

        // 网关/路由场景：服务端返回的真实模型应保留，用于计费归属
        Assert.Equal("server-real-model", response.Model);
    }

    [Fact]
    [DisplayName("服务端未返回model_回填请求模型")]
    public async Task NoServerModel_BackfilledWithRequestModel()
    {
        const String json = """{"id":"chatcmpl-1","object":"chat.completion","created":1700000000,"choices":[{"index":0,"message":{"role":"assistant","content":"hi"},"finish_reason":"stop"}],"usage":{"prompt_tokens":1,"completion_tokens":1,"total_tokens":2}}""";
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json(json));
        using var client = CreateClient(handler);

        var response = await client.GetResponseAsync(new ChatRequest
        {
            Model = "request-model",
            Messages = [new ChatMessage { Role = "user", Content = "hi" }],
        });

        // 服务端未返回时回填请求模型，保证下游非空
        Assert.Equal("request-model", response.Model);
    }
}
