#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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

/// <summary>OpenAI 协议级测试。通过 StubHttpMessageHandler 模拟服务商响应，验证 chat/stream/think 协议解析，无需真实 API Key</summary>
public class OpenAiChatClientStreamTests
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

    #region 非流式

    [Fact]
    [DisplayName("非流式_标准响应_解析文本与用量")]
    public async Task NonStream_StandardResponse_ParsesTextAndUsage()
    {
        const String json = """{"id":"chatcmpl-1","object":"chat.completion","created":1700000000,"model":"gpt-4o","choices":[{"index":0,"message":{"role":"assistant","content":"你好，我是助手"},"finish_reason":"stop"}],"usage":{"prompt_tokens":10,"completion_tokens":5,"total_tokens":15}}""";
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json(json));
        using var client = CreateClient(handler);

        var response = await client.GetResponseAsync(CreateRequest());

        Assert.Equal("chatcmpl-1", response.Id);
        Assert.Equal("你好，我是助手", response.Text);
        Assert.Equal(10, response.Usage!.InputTokens);
        Assert.Equal(5, response.Usage.OutputTokens);
        Assert.Equal(15, response.Usage.TotalTokens);
        Assert.True(response.Usage.ElapsedMs >= 0, "ElapsedMs 应已设置");
        Assert.Contains("/v1/chat/completions", handler.LastRequestUrl!);
    }

    [Fact]
    [DisplayName("非流式_HTTP错误_抛ApiException")]
    public async Task NonStream_HttpError_ThrowsApiException()
    {
        const String errBody = """{"error":{"message":"模型不存在","type":"invalid_request_error"}}""";
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json(errBody, HttpStatusCode.BadRequest));
        using var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.GetResponseAsync(CreateRequest()));

        Assert.Equal(400, ex.Code);
        Assert.Contains("模型不存在", ex.Message);
    }

    #endregion

    #region 流式

    [Fact]
    [DisplayName("流式_SSE标准格式_逐块解析并拼接文本")]
    public async Task Stream_StandardSse_CollectsChunks()
    {
        var sse = String.Join("\n\n",
        [
            """data: {"id":"1","object":"chat.completion.chunk","created":1700000000,"model":"gpt-4o","choices":[{"index":0,"delta":{"role":"assistant","content":"你"},"finish_reason":null}]}""",
            """data: {"id":"1","object":"chat.completion.chunk","created":1700000000,"model":"gpt-4o","choices":[{"index":0,"delta":{"content":"好"},"finish_reason":null}]}""",
            """data: {"id":"1","object":"chat.completion.chunk","created":1700000000,"model":"gpt-4o","choices":[{"index":0,"delta":{"content":""},"finish_reason":"stop"}]}""",
            """data: {"id":"1","object":"chat.completion.chunk","created":1700000000,"model":"gpt-4o","choices":[],"usage":{"prompt_tokens":10,"completion_tokens":5,"total_tokens":15}}""",
            "data: [DONE]",
        ]) + "\n\n";

        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Sse(sse));
        using var client = CreateClient(handler);

        var chunks = new List<IChatResponse>();
        await foreach (var chunk in client.GetStreamingResponseAsync(CreateRequest()))
            chunks.Add(chunk);

        Assert.Equal(4, chunks.Count);
        var text = String.Concat(chunks.Select(c => c.Text));
        Assert.Equal("你好", text);

        // 最后一个带 usage 的 chunk 应包含完整用量
        var lastUsage = chunks.LastOrDefault(c => c.Usage != null)?.Usage;
        Assert.NotNull(lastUsage);
        Assert.Equal(10, lastUsage!.InputTokens);
        Assert.Equal(5, lastUsage.OutputTokens);
        Assert.Equal(15, lastUsage.TotalTokens);
    }

    [Fact]
    [DisplayName("流式_data无空格_兼容解析")]
    public async Task Stream_SseWithoutSpace_Compatible()
    {
        // 部分服务商返回 data:{...}（无空格），应兼容
        var sse = String.Join("\n",
        [
            """data:{"id":"1","object":"chat.completion.chunk","created":1700000000,"model":"gpt-4o","choices":[{"index":0,"delta":{"role":"assistant","content":"好"},"finish_reason":null}]}""",
            """data:{"id":"1","object":"chat.completion.chunk","created":1700000000,"model":"gpt-4o","choices":[{"index":0,"delta":{"content":""},"finish_reason":"stop"}]}""",
            "data:[DONE]",
        ]) + "\n";

        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Sse(sse));
        using var client = CreateClient(handler);

        var text = "";
        await foreach (var chunk in client.GetStreamingResponseAsync(CreateRequest()))
            text += chunk.Text;

        Assert.Equal("好", text);
    }

    [Fact]
    [DisplayName("流式_思考内容_reasoning_content提取到Delta")]
    public async Task Stream_ThinkingReasoning_ExtractedToDelta()
    {
        // DeepSeek 风格：delta.reasoning_content 携带思考内容
        var sse = String.Join("\n\n",
        [
            """data: {"id":"1","object":"chat.completion.chunk","created":1700000000,"model":"deepseek-chat","choices":[{"index":0,"delta":{"reasoning_content":"先分析问题","content":""},"finish_reason":null}]}""",
            """data: {"id":"1","object":"chat.completion.chunk","created":1700000000,"model":"deepseek-chat","choices":[{"index":0,"delta":{"content":"答案是2"},"finish_reason":"stop"}]}""",
            "data: [DONE]",
        ]) + "\n\n";

        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Sse(sse));
        using var client = CreateClient(handler, "deepseek-chat");

        String? reasoning = null;
        var text = "";
        await foreach (var chunk in client.GetStreamingResponseAsync(CreateRequest("deepseek-chat")))
        {
            var delta = chunk.Messages?.FirstOrDefault()?.Delta;
            if (delta?.ReasoningContent != null) reasoning += delta.ReasoningContent;
            if (chunk.Text != null) text += chunk.Text;
        }

        Assert.Equal("先分析问题", reasoning);
        Assert.Equal("答案是2", text);
    }

    [Fact]
    [DisplayName("流式_服务商返回error对象_抛HttpRequestException")]
    public async Task Stream_ErrorObject_Throws()
    {
        // 服务商在流中返回 error 对象而非正常 chunk，应抛出而非静默吞掉
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

        Assert.Contains("模型已下线", ex.Message);
    }

    #endregion

    #region 请求序列化

    [Fact]
    [DisplayName("请求体_EnableThinking_序列化enable_thinking")]
    public async Task RequestBody_EnableThinking_Serialized()
    {
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json(
            """{"id":"1","object":"chat.completion","created":1700000000,"model":"qwen-plus","choices":[{"index":0,"message":{"role":"assistant","content":"ok"},"finish_reason":"stop"}],"usage":{"prompt_tokens":1,"completion_tokens":1,"total_tokens":2}}"""));
        using var client = CreateClient(handler, "qwen-plus");

        var request = CreateRequest("qwen-plus");
        request.EnableThinking = true;
        await client.GetResponseAsync(request);

        Assert.Contains("\"enable_thinking\":true", handler.LastRequestBody!);
    }

    [Fact]
    [DisplayName("请求体_ReasoningEffort_序列化reasoning_effort")]
    public async Task RequestBody_ReasoningEffort_Serialized()
    {
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json(
            """{"id":"1","object":"chat.completion","created":1700000000,"model":"o3-mini","choices":[{"index":0,"message":{"role":"assistant","content":"ok"},"finish_reason":"stop"}],"usage":{"prompt_tokens":1,"completion_tokens":1,"total_tokens":2}}"""));
        using var client = CreateClient(handler, "o3-mini");

        var request = CreateRequest("o3-mini");
        request.ReasoningEffort = "high";
        await client.GetResponseAsync(request);

        Assert.Contains("\"reasoning_effort\":\"high\"", handler.LastRequestBody!);
    }

    #endregion
}
