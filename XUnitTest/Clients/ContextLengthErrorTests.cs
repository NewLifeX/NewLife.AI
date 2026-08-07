using System;
using System.ComponentModel;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using NewLife.AI.Clients;
using NewLife.AI.Clients.OpenAI;
using NewLife.AI.Models;
using NewLife.Remoting;
using Xunit;
using XUnitTest.Helpers;

namespace XUnitTest.Clients;

/// <summary>上下文超限错误识别测试。验证 HTTP 错误与流式错误中的上下文超限文案被升级为类型化异常</summary>
[DisplayName("上下文超限错误识别测试")]
public class ContextLengthErrorTests
{
    /// <summary>构建指向 stub 地址的 OpenAI 客户端</summary>
    private static OpenAIChatClient CreateClient(StubHttpMessageHandler handler, String model = "gpt-4o")
    {
        var client = new OpenAIChatClient(new AiClientOptions
        {
            Endpoint = "https://stub.local",
            ApiKey = "test-key",
            Model = model,
        });
        client.HttpClient = new HttpClient(handler);
        return client;
    }

    /// <summary>构建简单用户请求</summary>
    private static ChatRequest CreateRequest(String model = "gpt-4o", String prompt = "你好")
        => new()
        {
            Model = model,
            Messages = [new ChatMessage { Role = "user", Content = prompt }],
        };

    [Fact]
    [DisplayName("非流式_HTTP400上下文超限_抛类型化异常")]
    public async Task NonStream_ContextLengthError_ThrowsTypedException()
    {
        const String errBody = """{"error":{"message":"litellm.BadRequestError: OpenAIException - <400> InternalError.Algo.InvalidParameter: Range of input length should be [1, 983616]","type":"invalid_request_error","code":"400"}}""";
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json(errBody, HttpStatusCode.BadRequest));
        using var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<ContextLengthExceededException>(() => client.GetResponseAsync(CreateRequest()));

        Assert.Equal(400, ex.Code);
        Assert.Equal(983616, ex.ContextLength);
    }

    [Fact]
    [DisplayName("非流式_HTTP400非上下文错误_仍抛普通ApiException")]
    public async Task NonStream_OtherError_ThrowsPlainApiException()
    {
        const String errBody = """{"error":{"message":"模型不存在","type":"invalid_request_error"}}""";
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json(errBody, HttpStatusCode.BadRequest));
        using var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.GetResponseAsync(CreateRequest()));

        Assert.IsNotType<ContextLengthExceededException>(ex);
        Assert.Equal(400, ex.Code);
    }

    [Fact]
    [DisplayName("流式_SSE上下文超限error对象_抛类型化异常")]
    public async Task Stream_ContextLengthError_ThrowsTypedException()
    {
        var sse = String.Join("\n",
        [
            """data: {"error":{"message":"This model's maximum context length is 32768 tokens. However, you requested 40000 tokens.","type":"invalid_request_error","code":"context_length_exceeded"}}""",
            "data: [DONE]",
        ]) + "\n";

        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Sse(sse));
        using var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<ContextLengthExceededException>(async () =>
        {
            await foreach (var _ in client.GetStreamingResponseAsync(CreateRequest())) { }
        });

        Assert.Equal(32768, ex.ContextLength);
    }

    [Fact]
    [DisplayName("流式_SSE非上下文错误_仍抛普通HttpRequestException")]
    public async Task Stream_OtherError_ThrowsPlainHttpException()
    {
        var sse = String.Join("\n",
        [
            """data: {"error":{"message":"模型已下线","type":"invalid_request_error","code":"model_not_found"}}""",
            "data: [DONE]",
        ]) + "\n";

        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Sse(sse));
        using var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await foreach (var _ in client.GetStreamingResponseAsync(CreateRequest())) { }
        });

        Assert.IsNotType<ContextLengthExceededException>(ex);
    }
}
