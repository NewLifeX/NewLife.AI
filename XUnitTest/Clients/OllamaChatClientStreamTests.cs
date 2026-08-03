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
using Xunit;
using XUnitTest.Helpers;

namespace XUnitTest.Clients;

/// <summary>Ollama 协议级测试。通过 StubHttpMessageHandler 模拟服务商响应，验证 NDJSON 流式与 think 参数，无需本地 Ollama 服务</summary>
public class OllamaChatClientStreamTests
{
    /// <summary>构建指向 stub 地址的 Ollama 客户端</summary>
    private static OllamaChatClient CreateClient(StubHttpMessageHandler handler)
    {
        var client = new OllamaChatClient(new AiClientOptions
        {
            Endpoint = "https://stub.local",
            Model = "qwen3:8b",
        });
        client.HttpClient = new HttpClient(handler);
        return client;
    }

    /// <summary>构建简单用户请求</summary>
    private static ChatRequest CreateRequest()
        => new()
        {
            Model = "qwen3:8b",
            Messages = [new ChatMessage { Role = "user", Content = "你好" }],
        };

    #region 流式

    [Fact]
    [DisplayName("流式_NDJSON逐行解析_文本与思考分别提取")]
    public async Task Stream_NdJson_CollectsChunks()
    {
        var ndjson = String.Join("\n",
        [
            """{"model":"qwen3:8b","message":{"role":"assistant","content":"你"},"done":false}""",
            """{"model":"qwen3:8b","message":{"role":"assistant","content":"好","thinking":"思考中"},"done":false}""",
            """{"model":"qwen3:8b","message":{"role":"assistant","content":""},"done":true,"done_reason":"stop","prompt_eval_count":10,"eval_count":5}""",
        ]) + "\n";

        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.NdJson(ndjson));
        using var client = CreateClient(handler);

        var chunks = new List<IChatResponse>();
        await foreach (var chunk in client.GetStreamingResponseAsync(CreateRequest()))
            chunks.Add(chunk);

        Assert.Equal(3, chunks.Count);
        Assert.Equal("你好", String.Concat(chunks.Select(c => c.Text)));

        var reasoning = String.Concat(chunks.Select(c => c.Messages?.FirstOrDefault()?.Delta?.ReasoningContent ?? ""));
        Assert.Equal("思考中", reasoning);

        var usage = chunks.LastOrDefault(c => c.Usage != null)?.Usage;
        Assert.NotNull(usage);
        Assert.Equal(10, usage!.InputTokens);
        Assert.Equal(5, usage.OutputTokens);
        Assert.Equal(15, usage.TotalTokens);
        Assert.Contains("/api/chat", handler.LastRequestUrl!);
    }

    [Fact]
    [DisplayName("流式_返回error对象_抛HttpRequestException")]
    public async Task Stream_ErrorObject_Throws()
    {
        var ndjson = """{"error":"model 'qwen3:8b' not found"}""" + "\n";

        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.NdJson(ndjson));
        using var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await foreach (var _ in client.GetStreamingResponseAsync(CreateRequest())) { }
        });

        Assert.Contains("not found", ex.Message);
    }

    #endregion

    #region 思考请求序列化

    [Fact]
    [DisplayName("请求体_EnableThinking_序列化think参数")]
    public async Task RequestBody_EnableThinking_SerializesThink()
    {
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.NdJson(
            """{"model":"qwen3:8b","message":{"role":"assistant","content":"ok"},"done":true,"done_reason":"stop","prompt_eval_count":1,"eval_count":1}"""));
        using var client = CreateClient(handler);

        var request = CreateRequest();
        request.EnableThinking = true;
        await foreach (var _ in client.GetStreamingResponseAsync(request)) { }

        Assert.Contains("\"think\":true", handler.LastRequestBody!);
    }

    [Fact]
    [DisplayName("请求体_EnableThinking=false_序列化think=false")]
    public async Task RequestBody_EnableThinkingFalse_SerializesThinkFalse()
    {
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.NdJson(
            """{"model":"qwen3:8b","message":{"role":"assistant","content":"ok"},"done":true,"done_reason":"stop","prompt_eval_count":1,"eval_count":1}"""));
        using var client = CreateClient(handler);

        var request = CreateRequest();
        request.EnableThinking = false;
        await foreach (var _ in client.GetStreamingResponseAsync(request)) { }

        Assert.Contains("\"think\":false", handler.LastRequestBody!);
    }

    #endregion
}
