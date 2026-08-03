#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using NewLife;
using NewLife.AI.Clients;
using NewLife.AI.Clients.Anthropic;
using NewLife.AI.Models;
using Xunit;
using XUnitTest.Helpers;

namespace XUnitTest.Clients;

/// <summary>Anthropic 协议级测试。通过 StubHttpMessageHandler 模拟服务商响应，验证 chat/stream/think 协议解析与流式用量合并，无需真实 API Key</summary>
public class AnthropicChatClientStreamTests
{
    /// <summary>构建指向 stub 地址的 Anthropic 客户端</summary>
    private static AnthropicChatClient CreateClient(StubHttpMessageHandler handler)
    {
        var client = new AnthropicChatClient(new AiClientOptions
        {
            Endpoint = "https://stub.local",
            ApiKey = "test-key",
            Model = "claude-sonnet-4-6",
        });
        client.HttpClient = new HttpClient(handler);
        return client;
    }

    /// <summary>构建简单用户请求</summary>
    private static ChatRequest CreateRequest()
        => new()
        {
            Model = "claude-sonnet-4-6",
            Messages = [new ChatMessage { Role = "user", Content = "你好" }],
        };

    #region 非流式

    [Fact]
    [DisplayName("非流式_标准响应_解析文本与用量")]
    public async Task NonStream_StandardResponse_ParsesTextAndUsage()
    {
        const String json = """{"id":"msg_1","type":"message","role":"assistant","model":"claude-sonnet-4-6","content":[{"type":"text","text":"你好，我是 Claude"}],"stop_reason":"end_turn","usage":{"input_tokens":10,"output_tokens":5}}""";
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json(json));
        using var client = CreateClient(handler);

        var response = await client.GetResponseAsync(CreateRequest());

        Assert.Equal("msg_1", response.Id);
        Assert.Equal("你好，我是 Claude", response.Text);
        Assert.Equal(10, response.Usage!.InputTokens);
        Assert.Equal(5, response.Usage.OutputTokens);
        Assert.Equal(15, response.Usage.TotalTokens);
        Assert.Contains("/v1/messages", handler.LastRequestUrl!);
    }

    #endregion

    #region 流式

    [Fact]
    [DisplayName("流式_event/data格式_文本与思考增量分别解析")]
    public async Task Stream_EventDataFormat_TextAndThinkingParsed()
    {
        var sse = String.Join("\n",
        [
            "event: message_start",
            """data: {"type":"message_start","message":{"id":"msg_1","type":"message","role":"assistant","model":"claude-sonnet-4-6","content":[],"usage":{"input_tokens":100,"output_tokens":0}}}""",
            "",
            "event: content_block_start",
            """data: {"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}""",
            "",
            "event: content_block_delta",
            """data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"你好"}}""",
            "",
            "event: content_block_delta",
            """data: {"type":"content_block_delta","index":0,"delta":{"type":"thinking_delta","thinking":"推理过程"}}""",
            "",
            "event: content_block_stop",
            """data: {"type":"content_block_stop","index":0}""",
            "",
            "event: message_delta",
            """data: {"type":"message_delta","delta":{"stop_reason":"end_turn"},"usage":{"output_tokens":50}}""",
            "",
            "event: message_stop",
            """data: {"type":"message_stop"}""",
            "",
        ]);

        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Sse(sse));
        using var client = CreateClient(handler);

        var chunks = new List<IChatResponse>();
        await foreach (var chunk in client.GetStreamingResponseAsync(CreateRequest()))
            chunks.Add(chunk);

        // message_start / text_delta / thinking_delta / message_delta 共 4 个有效 chunk
        Assert.Equal(4, chunks.Count);

        var text = String.Concat(chunks.Select(c => c.Text));
        Assert.Equal("你好", text);

        var reasoning = String.Concat(chunks
            .Select(c => c.Messages?.FirstOrDefault()?.Delta?.ReasoningContent ?? ""));
        Assert.Equal("推理过程", reasoning);
    }

    [Fact]
    [DisplayName("流式_Anthropic分块用量_合并后input与output完整")]
    public async Task Stream_SplitUsage_MergedCompletely()
    {
        // Anthropic 将 input/output token 拆到 message_start 与 message_delta 两个互补 chunk，
        // GetStreamingResponseAsync 应通过 MergeChunkUsage 合并，末 chunk 用量完整
        var sse = String.Join("\n",
        [
            "event: message_start",
            """data: {"type":"message_start","message":{"id":"msg_1","type":"message","role":"assistant","model":"claude-sonnet-4-6","content":[],"usage":{"input_tokens":100,"output_tokens":0}}}""",
            "",
            "event: content_block_delta",
            """data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"你好"}}""",
            "",
            "event: message_delta",
            """data: {"type":"message_delta","delta":{"stop_reason":"end_turn"},"usage":{"output_tokens":50}}""",
            "",
            "event: message_stop",
            """data: {"type":"message_stop"}""",
            "",
        ]);

        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Sse(sse));
        using var client = CreateClient(handler);

        IChatResponse? lastWithUsage = null;
        await foreach (var chunk in client.GetStreamingResponseAsync(CreateRequest()))
        {
            if (chunk.Usage != null) lastWithUsage = chunk;
        }

        Assert.NotNull(lastWithUsage);
        Assert.NotNull(lastWithUsage!.Usage);
        Assert.Equal(100, lastWithUsage.Usage!.InputTokens);
        Assert.Equal(50, lastWithUsage.Usage.OutputTokens);
        Assert.Equal(150, lastWithUsage.Usage.TotalTokens);
    }

    [Fact]
    [DisplayName("流式_event为error_抛HttpRequestException")]
    public async Task Stream_ErrorEvent_Throws()
    {
        var sse = String.Join("\n",
        [
            "event: error",
            """data: {"type":"error","error":{"type":"overloaded_error","message":"服务过载，请稍后重试"}}""",
            "",
        ]);

        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Sse(sse));
        using var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await foreach (var _ in client.GetStreamingResponseAsync(CreateRequest())) { }
        });

        Assert.Contains("服务过载", ex.Message);
    }

    [Fact]
    [DisplayName("流式_signature_delta_签名透传到chunk")]
    public async Task Stream_SignatureDelta_ExtractsSignature()
    {
        var sse = String.Join("\n",
        [
            "event: message_start",
            """data: {"type":"message_start","message":{"id":"msg_1","type":"message","role":"assistant","model":"claude-sonnet-4-6","content":[],"usage":{"input_tokens":100,"output_tokens":0}}}""",
            "",
            "event: content_block_start",
            """data: {"type":"content_block_start","index":0,"content_block":{"type":"thinking","thinking":"","signature":""}}""",
            "",
            "event: content_block_delta",
            """data: {"type":"content_block_delta","index":0,"delta":{"type":"thinking_delta","thinking":"推理中"}}""",
            "",
            "event: content_block_delta",
            """data: {"type":"content_block_delta","index":0,"delta":{"type":"signature_delta","signature":"sig_stream_1"}}""",
            "",
            "event: content_block_stop",
            """data: {"type":"content_block_stop","index":0}""",
            "",
            "event: content_block_start",
            """data: {"type":"content_block_start","index":1,"content_block":{"type":"text","text":""}}""",
            "",
            "event: content_block_delta",
            """data: {"type":"content_block_delta","index":1,"delta":{"type":"text_delta","text":"结论"}}""",
            "",
            "event: message_stop",
            """data: {"type":"message_stop"}""",
            "",
        ]);

        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Sse(sse));
        using var client = CreateClient(handler);

        String? signature = null;
        var reasoning = "";
        await foreach (var chunk in client.GetStreamingResponseAsync(CreateRequest()))
        {
            var delta = chunk.Messages?.FirstOrDefault()?.Delta;
            if (delta != null)
            {
                if (delta["Signature"] is String s && !s.IsNullOrEmpty()) signature = s;
                if (!delta.ReasoningContent.IsNullOrEmpty()) reasoning += delta.ReasoningContent;
            }
        }

        Assert.Equal("sig_stream_1", signature);
        Assert.Equal("推理中", reasoning);
    }

    [Fact]
    [DisplayName("流式_redacted_thinking块_数据透传到chunk")]
    public async Task Stream_RedactedThinkingBlock_ExtractsData()
    {
        var sse = String.Join("\n",
        [
            "event: content_block_start",
            """data: {"type":"content_block_start","index":0,"content_block":{"type":"redacted_thinking","data":"red_encrypted"}}""",
            "",
            "event: content_block_stop",
            """data: {"type":"content_block_stop","index":0}""",
            "",
            "event: content_block_start",
            """data: {"type":"content_block_start","index":1,"content_block":{"type":"text","text":""}}""",
            "",
            "event: content_block_delta",
            """data: {"type":"content_block_delta","index":1,"delta":{"type":"text_delta","text":"结论"}}""",
            "",
            "event: message_stop",
            """data: {"type":"message_stop"}""",
            "",
        ]);

        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Sse(sse));
        using var client = CreateClient(handler);

        List<String>? redacted = null;
        await foreach (var chunk in client.GetStreamingResponseAsync(CreateRequest()))
        {
            var delta = chunk.Messages?.FirstOrDefault()?.Delta;
            if (delta?["RedactedThinking"] is IList<String> reds)
            {
                redacted ??= [];
                redacted.AddRange(reds);
            }
        }

        Assert.NotNull(redacted);
        Assert.Equal("red_encrypted", redacted![0]);
    }

    #endregion

    #region 思考请求序列化

    [Fact]
    [DisplayName("请求体_EnableThinking_序列化thinking字段")]
    public async Task RequestBody_EnableThinking_SerializesThinking()
    {
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json(
            """{"id":"msg_1","type":"message","role":"assistant","model":"claude-sonnet-4-6","content":[{"type":"text","text":"ok"}],"stop_reason":"end_turn","usage":{"input_tokens":1,"output_tokens":1}}"""));
        using var client = CreateClient(handler);

        var request = CreateRequest();
        request.EnableThinking = true;
        await client.GetResponseAsync(request);

        Assert.Contains("\"thinking\":", handler.LastRequestBody!);
        Assert.Contains("\"type\":\"enabled\"", handler.LastRequestBody!);
        Assert.Contains("\"budget_tokens\"", handler.LastRequestBody!);
    }

    [Fact]
    [DisplayName("请求体_EnableThinking=false_序列化disabled")]
    public async Task RequestBody_EnableThinkingFalse_SerializesDisabled()
    {
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json(
            """{"id":"msg_1","type":"message","role":"assistant","model":"claude-sonnet-4-6","content":[{"type":"text","text":"ok"}],"stop_reason":"end_turn","usage":{"input_tokens":1,"output_tokens":1}}"""));
        using var client = CreateClient(handler);

        var request = CreateRequest();
        request.EnableThinking = false;
        await client.GetResponseAsync(request);

        Assert.Contains("\"type\":\"disabled\"", handler.LastRequestBody!);
    }

    #endregion
}
