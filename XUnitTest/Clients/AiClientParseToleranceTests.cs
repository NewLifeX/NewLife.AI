#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.Http;
using System.Threading.Tasks;
using NewLife;
using NewLife.AI.Clients;
using NewLife.AI.Clients.OpenAI;
using NewLife.AI.Models;
using Xunit;
using XUnitTest.Helpers;

namespace XUnitTest.Clients;

/// <summary>流式解析容错测试。验证畸形数据块被跳过但不中断整个流（G1 修复：WARN 日志 + 跳过，而非静默吞掉或中断）</summary>
public class AiClientParseToleranceTests
{
    private static OpenAIChatClient CreateClient(StubHttpMessageHandler handler)
    {
        var client = new OpenAIChatClient(new AiClientOptions
        {
            Endpoint = "https://stub.local",
            ApiKey = "test-key",
            Model = "gpt-4o",
        });
        client.HttpClient = new HttpClient(handler);
        return client;
    }

    private static ChatRequest CreateRequest(String prompt = "你好")
        => new()
        {
            Model = "gpt-4o",
            Messages = [new ChatMessage { Role = "user", Content = prompt }],
        };

    [Fact]
    [DisplayName("流式_畸形块夹在有效块间_跳过不中断")]
    public async Task Stream_MalformedChunkBetweenValidOnes_SkippedWithoutBreak()
    {
        const String sse = """
data: {"id":"1","object":"chat.completion.chunk","choices":[{"index":0,"delta":{"content":"前"},"finish_reason":null}]}

data: this is not json {{{broken

data: {"id":"1","object":"chat.completion.chunk","choices":[{"index":0,"delta":{"content":"后"},"finish_reason":null}]}

data: [DONE]

""";
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Sse(sse));
        using var client = CreateClient(handler);

        var list = new List<String>();
        await foreach (var chunk in client.GetStreamingResponseAsync(CreateRequest()))
        {
            if (!chunk.Text.IsNullOrEmpty()) list.Add(chunk.Text);
        }

        // 畸形块被跳过，前后有效块正常输出，流不中断
        Assert.Equal("前后", String.Join("", list));
    }

    [Fact]
    [DisplayName("流式_有效JSON但非chunk结构_跳过不中断")]
    public async Task Stream_ValidJsonButNotChunkShape_SkippedWithoutBreak()
    {
        // 合法 JSON 但缺 choices 结构（如服务商心跳/keepalive），应跳过而非报错
        const String sse = """
data: {"id":"1","object":"chat.completion.chunk","choices":[{"index":0,"delta":{"content":"正文"},"finish_reason":null}]}

data: {"keepalive":true,"ts":1700000000}

data: [DONE]

""";
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Sse(sse));
        using var client = CreateClient(handler);

        var list = new List<String>();
        await foreach (var chunk in client.GetStreamingResponseAsync(CreateRequest()))
        {
            if (!chunk.Text.IsNullOrEmpty()) list.Add(chunk.Text);
        }

        Assert.Equal("正文", String.Join("", list));
    }

    [Fact]
    [DisplayName("流式_仅畸形块_返回空流不抛异常")]
    public async Task Stream_OnlyMalformedChunks_EmptyStreamNoThrow()
    {
        const String sse = """
data: {not valid json

data: [DONE]

""";
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Sse(sse));
        using var client = CreateClient(handler);

        var list = new List<String>();
        await foreach (var chunk in client.GetStreamingResponseAsync(CreateRequest()))
        {
            if (!chunk.Text.IsNullOrEmpty()) list.Add(chunk.Text);
        }

        Assert.Empty(list);
    }
}
