#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using NewLife.AI.Clients;
using NewLife.AI.Clients.DashScope;
using NewLife.AI.Models;
using Xunit;
using XUnitTest.Helpers;

namespace XUnitTest.Clients;

/// <summary>DashScope 协议级测试。通过 StubHttpMessageHandler 模拟服务商响应，验证原生协议 chat/stream/think 解析，无需真实 API Key</summary>
public class DashScopeChatClientStreamTests
{
    /// <summary>构建指向 stub 地址的 DashScope 客户端（原生协议）</summary>
    private static DashScopeChatClient CreateClient(StubHttpMessageHandler handler)
    {
        var client = new DashScopeChatClient(new AiClientOptions
        {
            Endpoint = "https://stub.local",
            ApiKey = "test-key",
            Model = "qwen-plus",
        });
        client.HttpClient = new HttpClient(handler);
        return client;
    }

    /// <summary>构建简单用户请求</summary>
    private static ChatRequest CreateRequest()
        => new()
        {
            Model = "qwen-plus",
            Messages = [new ChatMessage { Role = "user", Content = "你好" }],
        };

    #region 非流式

    [Fact]
    [DisplayName("非流式_原生响应_解析文本与用量")]
    public async Task NonStream_NativeResponse_ParsesTextAndUsage()
    {
        const String json = """{"output":{"choices":[{"message":{"role":"assistant","content":"你好，我是通义千问"},"finish_reason":"stop"}]},"usage":{"input_tokens":10,"output_tokens":5,"total_tokens":15},"request_id":"req_1"}""";
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json(json));
        using var client = CreateClient(handler);

        var response = await client.GetResponseAsync(CreateRequest());

        Assert.Equal("你好，我是通义千问", response.Text);
        Assert.Equal(10, response.Usage!.InputTokens);
        Assert.Equal(5, response.Usage.OutputTokens);
        Assert.Equal(15, response.Usage.TotalTokens);
        Assert.Contains("/services/aigc/text-generation/generation", handler.LastRequestUrl!);
    }

    [Fact]
    [DisplayName("非流式_响应含错误码_抛HttpRequestException")]
    public async Task NonStream_ErrorCode_Throws()
    {
        const String json = """{"code":"InvalidApiKey","message":"无效的API-KEY","request_id":"req_1"}""";
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json(json));
        using var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetResponseAsync(CreateRequest()));

        Assert.Contains("InvalidApiKey", ex.Message);
    }

    #endregion

    #region 流式

    [Fact]
    [DisplayName("流式_原生SSE格式_逐块解析并拼接文本")]
    public async Task Stream_NativeSse_CollectsChunks()
    {
        var sse = String.Join("\n",
        [
            "id: 1",
            "event: result",
            """data: {"output":{"choices":[{"message":{"role":"assistant","content":"你"}}]},"usage":{"input_tokens":10,"output_tokens":5,"total_tokens":15},"request_id":"req_1"}""",
            "",
            "id: 2",
            "event: result",
            """data: {"output":{"choices":[{"message":{"role":"assistant","content":"好"}}]}}""",
            "",
            "id: 3",
            "event: result",
            """data: {"output":{"choices":[{"message":{"role":"assistant","content":""},"finish_reason":"stop"}]}}""",
            "",
            "id: 4",
            "event: completed",
            """data: {}""",
            "",
        ]);

        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Sse(sse));
        using var client = CreateClient(handler);

        var chunks = new List<IChatResponse>();
        await foreach (var chunk in client.GetStreamingResponseAsync(CreateRequest()))
            chunks.Add(chunk);

        // 3 个 result 数据块 + 结尾 completed 的空块，至少 3 个有效块
        Assert.True(chunks.Count >= 3, $"应至少收到 3 个有效块，实际 {chunks.Count}");
        Assert.Equal("你好", String.Concat(chunks.Select(c => c.Text)));

        var usage = chunks.LastOrDefault(c => c.Usage != null)?.Usage;
        Assert.NotNull(usage);
        Assert.Equal(10, usage!.InputTokens);
        Assert.Equal(5, usage.OutputTokens);
    }

    [Fact]
    [DisplayName("流式_event为error_抛HttpRequestException")]
    public async Task Stream_ErrorEvent_Throws()
    {
        var sse = String.Join("\n",
        [
            "id: 1",
            "event: error",
            """data: {"code":"Throttling.RateQuota","message":"触发限流","request_id":"req_1"}""",
            "",
        ]);

        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Sse(sse));
        using var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await foreach (var _ in client.GetStreamingResponseAsync(CreateRequest())) { }
        });

        Assert.Contains("Throttling.RateQuota", ex.Message);
    }

    #endregion

    #region 思考请求序列化

    [Fact]
    [DisplayName("请求体_EnableThinking_序列化enable_thinking")]
    public async Task RequestBody_EnableThinking_Serializes()
    {
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json(
            """{"output":{"choices":[{"message":{"role":"assistant","content":"ok"},"finish_reason":"stop"}]},"usage":{"input_tokens":1,"output_tokens":1,"total_tokens":2},"request_id":"req_1"}"""));
        using var client = CreateClient(handler);

        var request = CreateRequest();
        request.EnableThinking = true;
        await client.GetResponseAsync(request);

        Assert.Contains("enable_thinking", handler.LastRequestBody!);
    }

    #endregion
}
