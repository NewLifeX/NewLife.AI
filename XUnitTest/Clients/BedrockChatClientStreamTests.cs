#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using NewLife.AI.Clients;
using NewLife.AI.Clients.Bedrock;
using NewLife.AI.Models;
using Xunit;
using XUnitTest.Helpers;

namespace XUnitTest.Clients;

/// <summary>Bedrock 协议级测试。通过 StubHttpMessageHandler 模拟服务商响应，验证 Converse chat/stream/think 协议解析与 SigV4 认证，无需真实 AWS 凭证</summary>
public class BedrockChatClientStreamTests
{
    /// <summary>构建指向 stub 地址的 Bedrock 客户端（携带 SigV4 凭证）</summary>
    private static BedrockChatClient CreateClient(StubHttpMessageHandler handler)
    {
        var client = new BedrockChatClient(new AiClientOptions
        {
            Endpoint = "https://stub.local",
            ApiKey = "AKIDEXAMPLE",
            Organization = "SECRETKEY",
            Model = "anthropic.claude-sonnet-4-20250514-v1:0",
            Protocol = "us-east-1",
        });
        client.HttpClient = new HttpClient(handler);
        return client;
    }

    /// <summary>构建简单用户请求</summary>
    private static ChatRequest CreateRequest()
        => new()
        {
            Model = "anthropic.claude-sonnet-4-20250514-v1:0",
            Messages = [new ChatMessage { Role = "user", Content = "你好" }],
        };

    #region 非流式

    [Fact]
    [DisplayName("非流式_camelCase响应_解析文本推理与用量")]
    public async Task NonStream_CamelCaseResponse_ParsesTextReasoningUsage()
    {
        const String json = """{"output":{"message":{"role":"assistant","content":[{"text":"你好"},{"reasoningContent":{"reasoningText":"推理过程"}}],"stopReason":"end_turn"}},"usage":{"inputTokens":10,"outputTokens":5,"totalTokens":15}}""";
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json(json));
        using var client = CreateClient(handler);

        var response = await client.GetResponseAsync(CreateRequest());

        Assert.Equal("你好", response.Text);
        Assert.Equal(10, response.Usage!.InputTokens);
        Assert.Equal(5, response.Usage.OutputTokens);
        Assert.Equal(15, response.Usage.TotalTokens);

        var msg = response.Messages?.FirstOrDefault()?.Message;
        Assert.Equal("推理过程", msg?.ReasoningContent);
    }

    [Fact]
    [DisplayName("认证_SigV4签名头_Authorization与X-Amz-Date")]
    public async Task Auth_SigV4Headers_Present()
    {
        const String json = """{"output":{"message":{"role":"assistant","content":[{"text":"ok"}],"stopReason":"end_turn"}},"usage":{"inputTokens":1,"outputTokens":1,"totalTokens":2}}""";
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json(json));
        using var client = CreateClient(handler);

        await client.GetResponseAsync(CreateRequest());

        Assert.True(handler.LastRequestHeaders.ContainsKey("Authorization"), "缺少 Authorization 头");
        Assert.True(handler.LastRequestHeaders.ContainsKey("X-Amz-Date"), "缺少 X-Amz-Date 头");
        Assert.True(handler.LastRequestHeaders.ContainsKey("X-Amz-Content-Sha256"), "缺少 X-Amz-Content-Sha256 头");
        Assert.StartsWith("AWS4-HMAC-SHA256", handler.LastRequestHeaders["Authorization"]);
        Assert.Contains("/model/anthropic.claude-sonnet-4-20250514-v1%3A0/converse", handler.LastRequestUrl!);
    }

    #endregion

    #region 流式

    [Fact]
    [DisplayName("流式_event/data格式_文本与推理增量解析")]
    public async Task Stream_EventDataFormat_TextAndReasoningParsed()
    {
        // AWS ConverseStream SSE data 为嵌套格式：{"contentBlockDelta":{...}} 等
        var sse = String.Join("\n",
        [
            "event: messageStart",
            """data: {"messageStart":{"role":"assistant"}}""",
            "",
            "event: contentBlockDelta",
            """data: {"contentBlockDelta":{"contentBlockIndex":0,"delta":{"reasoningContent":{"reasoningText":"推理过程"}}}}""",
            "",
            "event: contentBlockDelta",
            """data: {"contentBlockDelta":{"contentBlockIndex":0,"delta":{"text":"你好"}}}""",
            "",
            "event: messageStop",
            """data: {"messageStop":{"stopReason":"end_turn"}}""",
            "",
            "event: metadata",
            """data: {"metadata":{"usage":{"inputTokens":10,"outputTokens":5,"totalTokens":15}}}""",
            "",
        ]);

        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Sse(sse));
        using var client = CreateClient(handler);

        var chunks = new List<IChatResponse>();
        await foreach (var chunk in client.GetStreamingResponseAsync(CreateRequest()))
            chunks.Add(chunk);

        Assert.Equal(4, chunks.Count);
        Assert.Equal("你好", String.Concat(chunks.Select(c => c.Text)));

        var reasoning = String.Concat(chunks.Select(c => c.Messages?.FirstOrDefault()?.Delta?.ReasoningContent ?? ""));
        Assert.Equal("推理过程", reasoning);

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
            "event: error",
            """data: {"error":{"message":"模型访问被拒绝"}}""",
            "",
        ]);

        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Sse(sse));
        using var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await foreach (var _ in client.GetStreamingResponseAsync(CreateRequest())) { }
        });

        Assert.Contains("模型访问被拒绝", ex.Message);
    }

    [Fact]
    [DisplayName("流式_toolUse块_input分片_累积为完整工具调用")]
    public async Task Stream_ToolUseBlocks_ArgumentsAccumulated()
    {
        // Bedrock Converse 流式工具：contentBlockStart(toolUse 带 toolUseId/name) + contentBlockDelta(toolUse.input 分片)，
        // 客户端应转 OpenAI 兼容 tool_call 增量块，消费端按 Id 合并追加参数为完整 JSON
        var sse = String.Join("\n",
        [
            "event: contentBlockStart",
            """data: {"contentBlockStart":{"contentBlockIndex":0,"start":{"toolUse":{"toolUseId":"tooluse_01","name":"get_weather"}}}}""",
            "",
            "event: contentBlockDelta",
            """data: {"contentBlockDelta":{"contentBlockIndex":0,"delta":{"toolUse":{"input":"{\"location\":\"北京"}}}}""",
            "",
            "event: contentBlockDelta",
            """data: {"contentBlockDelta":{"contentBlockIndex":0,"delta":{"toolUse":{"input":"\"}"}}}}""",
            "",
            "event: contentBlockStop",
            """data: {"contentBlockStop":{"contentBlockIndex":0}}""",
            "",
            "event: messageStop",
            """data: {"messageStop":{"stopReason":"tool_use"}}""",
            "",
        ]);

        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Sse(sse));
        using var client = CreateClient(handler);

        // 收集工具调用增量（模拟消费端 MergeToolCallDelta：按 Id 匹配并追加 arguments）
        List<ToolCall>? toolCalls = null;
        await foreach (var chunk in client.GetStreamingResponseAsync(CreateRequest()))
        {
            var delta = chunk.Messages?.FirstOrDefault()?.Delta;
            if (delta?.ToolCalls is { } tcs)
            {
                toolCalls ??= [];
                foreach (var tc in tcs)
                {
                    var existing = toolCalls.FirstOrDefault(t => t.Id == tc.Id);
                    if (existing == null)
                    {
                        toolCalls.Add(new ToolCall
                        {
                            Id = tc.Id,
                            Type = tc.Type,
                            Function = new FunctionCall { Name = tc.Function?.Name, Arguments = tc.Function?.Arguments ?? "" },
                        });
                    }
                    else if (!String.IsNullOrEmpty(tc.Function?.Arguments))
                    {
                        existing.Function!.Arguments += tc.Function!.Arguments;
                    }
                }
            }
        }

        Assert.NotNull(toolCalls);
        var call = Assert.Single(toolCalls!);
        Assert.Equal("tooluse_01", call.Id);
        Assert.Equal("get_weather", call.Function!.Name);
        Assert.Equal("""{"location":"北京"}""", call.Function.Arguments);
    }

    #endregion

    #region 思考请求序列化

    [Fact]
    [DisplayName("请求体_EnableThinking_序列化additionalModelRequestFields")]
    public async Task RequestBody_EnableThinking_SerializesThinking()
    {
        const String json = """{"output":{"message":{"role":"assistant","content":[{"text":"ok"}],"stopReason":"end_turn"}},"usage":{"inputTokens":1,"outputTokens":1,"totalTokens":2}}""";
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json(json));
        using var client = CreateClient(handler);

        var request = CreateRequest();
        request.EnableThinking = true;
        await client.GetResponseAsync(request);

        Assert.Contains("additionalModelRequestFields", handler.LastRequestBody!);
        Assert.Contains("\"thinking\"", handler.LastRequestBody!);
        Assert.Contains("\"type\":\"enabled\"", handler.LastRequestBody!);
        Assert.Contains("\"budget_tokens\"", handler.LastRequestBody!);
    }

    #endregion
}
