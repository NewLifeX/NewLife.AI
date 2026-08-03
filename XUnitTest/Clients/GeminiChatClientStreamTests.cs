#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using NewLife.AI.Clients;
using NewLife.AI.Clients.Gemini;
using NewLife.AI.Models;
using Xunit;
using XUnitTest.Helpers;

namespace XUnitTest.Clients;

/// <summary>Gemini 协议级测试。通过 StubHttpMessageHandler 模拟服务商响应，验证 chat/stream/think 协议解析，无需真实 API Key</summary>
public class GeminiChatClientStreamTests
{
    /// <summary>构建指向 stub 地址的 Gemini 客户端</summary>
    private static GeminiChatClient CreateClient(StubHttpMessageHandler handler)
    {
        var client = new GeminiChatClient(new AiClientOptions
        {
            Endpoint = "https://stub.local",
            ApiKey = "test-key",
            Model = "gemini-2.5-flash",
        });
        client.HttpClient = new HttpClient(handler);
        return client;
    }

    /// <summary>构建简单用户请求</summary>
    private static ChatRequest CreateRequest()
        => new()
        {
            Model = "gemini-2.5-flash",
            Messages = [new ChatMessage { Role = "user", Content = "你好" }],
        };

    #region 非流式

    [Fact]
    [DisplayName("非流式_camelCase响应_解析文本与用量")]
    public async Task NonStream_CamelCaseResponse_ParsesTextAndUsage()
    {
        const String json = """{"candidates":[{"content":{"role":"model","parts":[{"text":"你好，我是 Gemini"}]},"finishReason":"STOP"}],"usageMetadata":{"promptTokenCount":10,"candidatesTokenCount":5,"totalTokenCount":15}}""";
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json(json));
        using var client = CreateClient(handler);

        var response = await client.GetResponseAsync(CreateRequest());

        Assert.Equal("你好，我是 Gemini", response.Text);
        Assert.Equal(10, response.Usage!.InputTokens);
        Assert.Equal(5, response.Usage.OutputTokens);
        Assert.Equal(15, response.Usage.TotalTokens);
        Assert.Contains(":generateContent", handler.LastRequestUrl!);
    }

    [Fact]
    [DisplayName("非流式_error对象_抛出异常而非空响应")]
    public async Task NonStream_ErrorObject_Throws()
    {
        var json = """{"error":{"code":400,"message":"blocked","status":"INVALID_ARGUMENT"}}""";
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json(json));
        using var client = CreateClient(handler);

        await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await client.GetResponseAsync(CreateRequest());
        });
    }

    #endregion

    #region 流式

    [Fact]
    [DisplayName("流式_thought=true_提取为思考内容")]
    public async Task Stream_ThoughtPart_ExtractedAsReasoning()
    {
        var sse = String.Join("\n\n",
        [
            """data: {"candidates":[{"content":{"role":"model","parts":[{"text":"先分析用户意图","thought":true}]}}]}""",
            """data: {"candidates":[{"content":{"role":"model","parts":[{"text":"你好"}]},"finishReason":"STOP"}],"usageMetadata":{"promptTokenCount":10,"candidatesTokenCount":5,"totalTokenCount":15}}""",
        ]) + "\n\n";

        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Sse(sse));
        using var client = CreateClient(handler);

        var chunks = new List<IChatResponse>();
        await foreach (var chunk in client.GetStreamingResponseAsync(CreateRequest()))
            chunks.Add(chunk);

        Assert.Equal(2, chunks.Count);
        Assert.Equal("你好", String.Concat(chunks.Select(c => c.Text)));

        var reasoning = String.Concat(chunks.Select(c => c.Messages?.FirstOrDefault()?.Delta?.ReasoningContent ?? ""));
        Assert.Equal("先分析用户意图", reasoning);

        // 最后一个 chunk 含完整 usageMetadata
        var usage = chunks.LastOrDefault(c => c.Usage != null)?.Usage;
        Assert.NotNull(usage);
        Assert.Equal(10, usage!.InputTokens);
        Assert.Equal(5, usage.OutputTokens);
        Assert.Equal(15, usage.TotalTokens);
    }

    [Fact]
    [DisplayName("流式_error对象_抛出异常而非静默吞掉")]
    public async Task Stream_ErrorObject_Throws()
    {
        var sse = """data: {"error":{"code":400,"message":"content blocked","status":"INVALID_ARGUMENT"}}""" + "\n\n";
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Sse(sse));
        using var client = CreateClient(handler);

        await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await foreach (var _ in client.GetStreamingResponseAsync(CreateRequest())) { }
        });
    }

    [Fact]
    [DisplayName("流式_畸形块_跳过不中断")]
    public async Task Stream_MalformedChunk_SkipsWithoutBreaking()
    {
        var sse = String.Join("\n\n",
        [
            """data: {"candidates":[{"content":{"role":"model","parts":[{"text":"你好"}]}}]}""",
            "data: {not-valid-json",
            """data: {"candidates":[{"content":{"role":"model","parts":[{"text":"世界"}]},"finishReason":"STOP"}],"usageMetadata":{"promptTokenCount":1,"candidatesTokenCount":1,"totalTokenCount":2}}""",
        ]) + "\n\n";

        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Sse(sse));
        using var client = CreateClient(handler);

        var chunks = new List<IChatResponse>();
        await foreach (var chunk in client.GetStreamingResponseAsync(CreateRequest()))
            chunks.Add(chunk);

        Assert.Equal(2, chunks.Count);
        Assert.Equal("你好世界", String.Concat(chunks.Select(c => c.Text)));
    }

    #endregion

    #region 思考请求序列化

    [Fact]
    [DisplayName("请求体_EnableThinking_序列化thinkingConfig")]
    public async Task RequestBody_EnableThinking_SerializesThinkingConfig()
    {
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json(
            """{"candidates":[{"content":{"role":"model","parts":[{"text":"ok"}]},"finishReason":"STOP"}],"usageMetadata":{"promptTokenCount":1,"candidatesTokenCount":1,"totalTokenCount":2}}"""));
        using var client = CreateClient(handler);

        var request = CreateRequest();
        request.EnableThinking = true;
        await client.GetResponseAsync(request);

        Assert.Contains("thinkingConfig", handler.LastRequestBody!);
        Assert.Contains("thinkingBudget", handler.LastRequestBody!);
        // 默认思考预算 1024
        Assert.Contains("1024", handler.LastRequestBody!);
    }

    [Fact]
    [DisplayName("请求体_EnableThinking=false_序列化thinkingBudget=0")]
    public async Task RequestBody_EnableThinkingFalse_SerializesZeroBudget()
    {
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json(
            """{"candidates":[{"content":{"role":"model","parts":[{"text":"ok"}]},"finishReason":"STOP"}],"usageMetadata":{"promptTokenCount":1,"candidatesTokenCount":1,"totalTokenCount":2}}"""));
        using var client = CreateClient(handler);

        var request = CreateRequest();
        request.EnableThinking = false;
        await client.GetResponseAsync(request);

        Assert.Contains("thinkingConfig", handler.LastRequestBody!);
        Assert.Contains("\"thinkingBudget\":0", handler.LastRequestBody!);
    }

    [Fact]
    [DisplayName("请求体_ThinkingBudget_透传自定义预算")]
    public async Task RequestBody_ThinkingBudget_CustomValue()
    {
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json(
            """{"candidates":[{"content":{"role":"model","parts":[{"text":"ok"}]},"finishReason":"STOP"}],"usageMetadata":{"promptTokenCount":1,"candidatesTokenCount":1,"totalTokenCount":2}}"""));
        using var client = CreateClient(handler);

        var request = CreateRequest();
        request.EnableThinking = true;
        request["ThinkingBudget"] = 2048;
        await client.GetResponseAsync(request);

        Assert.Contains("\"thinkingBudget\":2048", handler.LastRequestBody!);
    }

    #endregion
}
