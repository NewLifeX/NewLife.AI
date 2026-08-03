#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NewLife;
using NewLife.AI.Clients;
using NewLife.AI.Clients.OpenAI;
using NewLife.AI.Models;
using NewLife.Remoting;
using Xunit;
using XUnitTest.Helpers;

namespace XUnitTest.Clients;

/// <summary>SDK 层重试机制测试。验证 AiClientBase 对 429/5xx/网络异常的可配置指数退避重试，4xx 不重试</summary>
public class AiClientRetryTests
{
    /// <summary>计数 Handler。按调用序号返回预制响应，用于验证重试次数与行为</summary>
    private sealed class CountingHandler : HttpMessageHandler
    {
        /// <summary>已调用次数</summary>
        public Int32 Count;

        /// <summary>按调用序号（0 起）返回响应</summary>
        public Func<Int32, HttpResponseMessage> Responder { get; }

        public CountingHandler(Func<Int32, HttpResponseMessage> responder) => Responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var i = Count++;
            return Task.FromResult(Responder(i));
        }
    }

    /// <summary>构建开启重试的 OpenAI 客户端（小退避间隔，测试不等待）</summary>
    private static OpenAIChatClient CreateClient(Int32 retryCount)
    {
        var client = new OpenAIChatClient(new AiClientOptions
        {
            Endpoint = "https://stub.local",
            ApiKey = "test-key",
            Model = "gpt-4o",
            RetryCount = retryCount,
            RetryIntervalMs = 1,
        });
        return client;
    }

    private static ChatRequest CreateRequest(String prompt = "你好")
        => new()
        {
            Model = "gpt-4o",
            Messages = [new ChatMessage { Role = "user", Content = prompt }],
        };

    private const String OkJson = """{"id":"1","object":"chat.completion","created":1700000000,"model":"gpt-4o","choices":[{"index":0,"message":{"role":"assistant","content":"重试成功"},"finish_reason":"stop"}],"usage":{"prompt_tokens":1,"completion_tokens":1,"total_tokens":2}}""";

    private const String StreamSse = """
data: {"id":"1","object":"chat.completion.chunk","choices":[{"index":0,"delta":{"content":"流式成功"},"finish_reason":null}]}

data: [DONE]

""";

    #region 非流式重试

    [Fact]
    [DisplayName("非流式_429后重试_成功返回")]
    public async Task NonStream_429ThenSuccess_RetriesAndSucceeds()
    {
        var handler = new CountingHandler(i => i == 0
            ? StubHttpMessageHandler.Json("""{"error":{"message":"限流"}}""", HttpStatusCode.TooManyRequests)
            : StubHttpMessageHandler.Json(OkJson));
        using var client = CreateClient(2);
        client.HttpClient = new HttpClient(handler);

        var response = await client.GetResponseAsync(CreateRequest());

        Assert.Equal("重试成功", response.Text);
        Assert.Equal(2, handler.Count);
    }

    [Fact]
    [DisplayName("非流式_500后重试_成功返回")]
    public async Task NonStream_500ThenSuccess_RetriesAndSucceeds()
    {
        var handler = new CountingHandler(i => i == 0
            ? StubHttpMessageHandler.Json("""{"error":{"message":"服务端错误"}}""", HttpStatusCode.InternalServerError)
            : StubHttpMessageHandler.Json(OkJson));
        using var client = CreateClient(2);
        client.HttpClient = new HttpClient(handler);

        var response = await client.GetResponseAsync(CreateRequest());

        Assert.Equal("重试成功", response.Text);
        Assert.Equal(2, handler.Count);
    }

    [Fact]
    [DisplayName("非流式_400客户端错误_不重试立即抛异常")]
    public async Task NonStream_400_NoRetryThrowsImmediately()
    {
        var handler = new CountingHandler(_ => StubHttpMessageHandler.Json("""{"error":{"message":"参数错误"}}""", HttpStatusCode.BadRequest));
        using var client = CreateClient(3);
        client.HttpClient = new HttpClient(handler);

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.GetResponseAsync(CreateRequest()));

        Assert.Equal(400, ex.Code);
        Assert.Equal(1, handler.Count);
    }

    [Fact]
    [DisplayName("非流式_重试次数默认0_不重试")]
    public async Task NonStream_DefaultNoRetry_ThrowsImmediately()
    {
        var handler = new CountingHandler(_ => StubHttpMessageHandler.Json("""{"error":{"message":"服务端错误"}}""", HttpStatusCode.InternalServerError));
        // RetryCount 默认 0
        var client = new OpenAIChatClient(new AiClientOptions { Endpoint = "https://stub.local", ApiKey = "test", Model = "gpt-4o" });
        client.HttpClient = new HttpClient(handler);

        await Assert.ThrowsAsync<ApiException>(() => client.GetResponseAsync(CreateRequest()));

        Assert.Equal(1, handler.Count);
    }

    [Fact]
    [DisplayName("非流式_重试耗尽_抛异常")]
    public async Task NonStream_RetriesExhausted_Throws()
    {
        var handler = new CountingHandler(_ => StubHttpMessageHandler.Json("""{"error":{"message":"持续失败"}}""", HttpStatusCode.ServiceUnavailable));
        using var client = CreateClient(2);
        client.HttpClient = new HttpClient(handler);

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.GetResponseAsync(CreateRequest()));

        Assert.Equal(503, ex.Code);
        Assert.Equal(3, handler.Count); // 1 次原始 + 2 次重试
    }

    #endregion

    #region 流式重试

    [Fact]
    [DisplayName("流式_429后重试_成功输出")]
    public async Task Stream_429ThenSuccess_RetriesAndStreams()
    {
        var handler = new CountingHandler(i => i == 0
            ? StubHttpMessageHandler.Json("""{"error":{"message":"限流"}}""", HttpStatusCode.TooManyRequests)
            : StubHttpMessageHandler.Sse(StreamSse));
        using var client = CreateClient(2);
        client.HttpClient = new HttpClient(handler);

        var list = new List<String>();
        await foreach (var chunk in client.GetStreamingResponseAsync(CreateRequest()))
        {
            if (!chunk.Text.IsNullOrEmpty()) list.Add(chunk.Text);
        }

        Assert.Equal("流式成功", String.Join("", list));
        Assert.Equal(2, handler.Count);
    }

    #endregion
}
