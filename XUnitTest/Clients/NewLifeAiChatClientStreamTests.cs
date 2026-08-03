#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using NewLife;
using NewLife.AI.Clients;
using NewLife.AI.Clients.OpenAI;
using NewLife.AI.Models;
using Xunit;
using XUnitTest.Helpers;

namespace XUnitTest.Clients;

/// <summary>新生命 AI 网关客户端协议级测试。通过 StubHttpMessageHandler 模拟网关响应，验证 OpenAI/Anthropic/Gemini 三路流式与非流式解析，无需真实 API Key</summary>
public class NewLifeAiChatClientStreamTests
{
    /// <summary>构建指向 stub 地址的新生命 AI 客户端</summary>
    private static NewLifeAIChatClient CreateClient(StubHttpMessageHandler handler, String model = "qwen3.6-flash")
    {
        var client = new NewLifeAIChatClient(new AiClientOptions
        {
            Endpoint = "https://stub.local",
            ApiKey = "test-key",
            Model = model,
        });
        client.HttpClient = new HttpClient(handler);
        return client;
    }

    /// <summary>构建简单用户请求</summary>
    private static ChatRequest CreateRequest(String model = "qwen3.6-flash", String prompt = "你好")
        => new()
        {
            Model = model,
            Messages = [new ChatMessage { Role = "user", Content = prompt }],
        };

    #region OpenAI Chat Completions 流式（继承 OpenAI 协议，/v1/chat/completions）

    [Fact]
    [DisplayName("OpenAI路径_流式_解析文本并终止")]
    public async Task OpenAiPath_Stream_ParsesTextAndTerminates()
    {
        const String sse = """
data: {"id":"1","object":"chat.completion.chunk","choices":[{"index":0,"delta":{"content":"你"},"finish_reason":null}]}

data: {"id":"1","object":"chat.completion.chunk","choices":[{"index":0,"delta":{"content":"好"},"finish_reason":null}]}

data: [DONE]

""";
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Sse(sse));
        using var client = CreateClient(handler);

        var list = new List<String>();
        await foreach (var chunk in client.GetStreamingResponseAsync(CreateRequest()))
        {
            if (!chunk.Text.IsNullOrEmpty()) list.Add(chunk.Text);
        }

        Assert.Equal("你好", String.Join("", list));
        Assert.Contains("/v1/chat/completions", handler.LastRequestUrl!);
    }

    [Fact]
    [DisplayName("OpenAI路径_流式_无空格data前缀_仍可解析")]
    public async Task OpenAiPath_Stream_NoSpaceDataPrefix_Parses()
    {
        // 部分服务商省略 data: 后的空格，应兼容
        const String sse = """
data:{"id":"1","object":"chat.completion.chunk","choices":[{"index":0,"delta":{"content":"兼容"},"finish_reason":null}]}

data:[DONE]

""";
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Sse(sse));
        using var client = CreateClient(handler);

        var list = new List<String>();
        await foreach (var chunk in client.GetStreamingResponseAsync(CreateRequest()))
        {
            if (!chunk.Text.IsNullOrEmpty()) list.Add(chunk.Text);
        }

        Assert.Equal("兼容", String.Join("", list));
    }

    [Fact]
    [DisplayName("OpenAI路径_流式_错误对象_抛异常不静默")]
    public async Task OpenAiPath_Stream_ErrorObject_Throws()
    {
        const String sse = """
data: {"error":{"message":"余额不足","code":"insufficient_quota"}}

""";
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Sse(sse));
        using var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await foreach (var _ in client.GetStreamingResponseAsync(CreateRequest())) { }
        });

        Assert.Contains("insufficient_quota", ex.Message);
    }

    #endregion

    #region Anthropic Messages（/v1/messages）

    [Fact]
    [DisplayName("Anthropic路径_流式_解析文本")]
    public async Task AnthropicPath_Stream_ParsesText()
    {
        const String sse = """
data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"你好，Claude"}}

data: {"type":"message_delta","delta":{"stop_reason":"end_turn"},"usage":{"output_tokens":8}}

data: {"type":"message_stop"}

""";
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Sse(sse));
        using var client = CreateClient(handler);

        var list = new List<String>();
        await foreach (var chunk in client.MessagesStreamAsync(CreateRequest()))
        {
            if (!chunk.Text.IsNullOrEmpty()) list.Add(chunk.Text);
        }

        Assert.Equal("你好，Claude", String.Join("", list));
        Assert.Contains("/v1/messages", handler.LastRequestUrl!);
    }

    [Fact]
    [DisplayName("Anthropic路径_流式_思考增量_提取到ReasoningContent")]
    public async Task AnthropicPath_Stream_ThinkingDelta_ExtractsReasoning()
    {
        const String sse = """
data: {"type":"content_block_delta","index":0,"delta":{"type":"thinking_delta","thinking":"正在思考"}}

data: {"type":"content_block_delta","index":1,"delta":{"type":"text_delta","text":"回答内容"}}

data: {"type":"message_stop"}

""";
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Sse(sse));
        using var client = CreateClient(handler);

        var list = new List<String>();
        var reasoning = new List<String>();
        await foreach (var chunk in client.MessagesStreamAsync(CreateRequest()))
        {
            if (!chunk.Text.IsNullOrEmpty()) list.Add(chunk.Text);
            var r = chunk.Messages?.FirstOrDefault()?.Delta?.ReasoningContent;
            if (!r.IsNullOrEmpty()) reasoning.Add(r);
        }

        Assert.Equal("回答内容", String.Join("", list));
        Assert.Equal("正在思考", String.Join("", reasoning));
    }

    [Fact]
    [DisplayName("Anthropic路径_非流式_解析文本与用量")]
    public async Task AnthropicPath_NonStream_ParsesTextAndUsage()
    {
        const String json = """{"id":"msg_1","type":"message","role":"assistant","model":"qwen3.6-flash","content":[{"type":"text","text":"你好"}],"usage":{"input_tokens":10,"output_tokens":5}}""";
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json(json));
        using var client = CreateClient(handler);

        var response = await client.MessagesAsync(CreateRequest());

        Assert.Equal("你好", response.Text);
        Assert.Equal(10, response.Usage!.InputTokens);
        Assert.Equal(5, response.Usage.OutputTokens);
    }

    #endregion

    #region Google Gemini（/v1/gemini）

    [Fact]
    [DisplayName("Gemini路径_流式_解析文本并跳过思考part")]
    public async Task GeminiPath_Stream_ParsesTextAndSkipsThought()
    {
        const String sse = """
data: {"candidates":[{"content":{"role":"model","parts":[{"text":"思考中","thought":true},{"text":"正文回答"}]},"finishReason":"STOP"}]}

""";
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Sse(sse));
        using var client = CreateClient(handler);

        var list = new List<String>();
        await foreach (var chunk in client.GeminiStreamAsync(CreateRequest()))
        {
            if (!chunk.Text.IsNullOrEmpty()) list.Add(chunk.Text);
        }

        Assert.Equal("正文回答", String.Join("", list));
        Assert.Contains("/v1/gemini", handler.LastRequestUrl!);
    }

    [Fact]
    [DisplayName("Gemini路径_非流式_解析文本与用量")]
    public async Task GeminiPath_NonStream_ParsesTextAndUsage()
    {
        const String json = """{"candidates":[{"content":{"role":"model","parts":[{"text":"你好，Gemini"}]},"finishReason":"STOP"}],"usageMetadata":{"promptTokenCount":10,"candidatesTokenCount":5,"totalTokenCount":15}}""";
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json(json));
        using var client = CreateClient(handler);

        var response = await client.GeminiAsync(CreateRequest());

        Assert.Equal("你好，Gemini", response.Text);
        Assert.Equal(10, response.Usage!.InputTokens);
        Assert.Equal(5, response.Usage.OutputTokens);
    }

    [Fact]
    [DisplayName("Gemini路径_流式_错误对象_抛异常")]
    public async Task GeminiPath_Stream_ErrorObject_Throws()
    {
        const String sse = """
data: {"error":{"message":"模型不可用","code":"model_not_found"}}

""";
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Sse(sse));
        using var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await foreach (var _ in client.GeminiStreamAsync(CreateRequest())) { }
        });

        Assert.Contains("model_not_found", ex.Message);
    }

    #endregion
}
