#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NewLife.AI.Clients;
using NewLife.AI.Models;
using NewLife.AI.Tools;
using Xunit;

namespace XUnitTest.Tools;

/// <summary>ToolChatClient 多轮思考回传测试。验证 assistant 消息的思考签名/redacted_thinking 经工具循环透传到下一轮请求（Anthropic 多轮协议必需）</summary>
public class ToolChatClientThinkRoundTripTests
{
    /// <summary>最小工具提供者：暴露 get_weather 工具并返回固定结果</summary>
    private sealed class FakeToolProvider : IToolProvider
    {
        public IList<ChatTool> GetTools(ISet<String>? filterNames = null, Boolean includeSystem = true)
            => [new ChatTool { Type = "function", Function = new FunctionDefinition { Name = "get_weather", Description = "查询天气" } }];

        public Task<IToolResult> CallToolAsync(String toolName, String? arguments, ToolCallContext? context = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IToolResult>(new ToolResult("{\"temperature\":25}"));
    }

    /// <summary>伪内层客户端：记录请求并返回脚本化响应（非流式 + 流式双通道）</summary>
    private sealed class FakeInnerClient : IChatClient
    {
        public readonly List<IChatRequest> Requests = [];
        private readonly Queue<IChatResponse> _nonStream = new();
        private readonly Queue<IEnumerable<IChatResponse>> _stream = new();

        public void EnqueueResponse(IChatResponse response) => _nonStream.Enqueue(response);

        public void EnqueueStream(params IChatResponse[] chunks) => _stream.Enqueue(chunks);

        public Task<IChatResponse> GetResponseAsync(IChatRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(_nonStream.Dequeue());
        }

        public async IAsyncEnumerable<IChatResponse> GetStreamingResponseAsync(IChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            foreach (var chunk in _stream.Dequeue())
                yield return chunk;
        }

        public void Dispose() { }
    }

    [Fact]
    [DisplayName("非流式工具轮次_思考签名随assistant消息透传到下一轮请求")]
    public async Task NonStream_ToolTurn_ThinkingSignatureCarried()
    {
        // 第一轮响应：assistant 带 tool_calls + 思考签名（Anthropic 思考+工具轮次回传必需）
        var round1 = new ChatResponse { Object = "chat.completion" };
        var assistantMsg = new ChatMessage
        {
            Role = "assistant",
            Content = "我来查天气",
            ReasoningContent = "需要调用工具",
            ToolCalls =
            [
                new ToolCall { Id = "call_1", Type = "function", Function = new FunctionCall { Name = "get_weather", Arguments = "{\"city\":\"Beijing\"}" } }
            ],
        };
        assistantMsg["Signature"] = "sig_round1";
        var c1 = round1.Add(null, null, FinishReason.ToolCalls);
        c1.Message = assistantMsg;

        // 第二轮响应：最终文本
        var round2 = new ChatResponse { Object = "chat.completion" };
        round2.Add("天气晴朗", null, FinishReason.Stop);

        var inner = new FakeInnerClient();
        inner.EnqueueResponse(round1);
        inner.EnqueueResponse(round2);
        using var client = new ToolChatClient(inner, new FakeToolProvider());

        var response = await client.GetResponseAsync(new ChatRequest
        {
            Model = "claude-sonnet-4-6",
            Messages = [new ChatMessage { Role = "user", Content = "北京天气" }],
        });

        Assert.Equal("天气晴朗", response.Text);
        Assert.Equal(2, inner.Requests.Count);

        // 第二轮请求中的 assistant 消息应携带思考签名（原样回传必需）
        var round2Req = inner.Requests[1];
        var assistant = round2Req.Messages.FirstOrDefault(m => m.Role == "assistant");
        Assert.NotNull(assistant);
        Assert.Equal("需要调用工具", assistant!.ReasoningContent);
        Assert.Equal("sig_round1", assistant["Signature"]);
    }

    [Fact]
    [DisplayName("流式工具轮次_思考签名从delta累积并透传")]
    public async Task Stream_ToolTurn_ThinkingSignatureAccumulated()
    {
        var inner = new FakeInnerClient();

        // 第一轮流式：thinking_delta → signature_delta → tool_calls（含 finish_reason=tool_calls）
        var thinkingDelta = new ChatResponse { Object = "chat.completion.chunk" };
        thinkingDelta.AddDelta(null, "推理中", null);

        var signatureDelta = new ChatResponse { Object = "chat.completion.chunk" };
        var sc = signatureDelta.AddDelta(null, null, null);
        sc.Delta = new ChatMessage { Role = "assistant" };
        sc.Delta["Signature"] = "sig_stream";

        var toolCallDelta = new ChatResponse { Object = "chat.completion.chunk" };
        toolCallDelta.AddToolCallDelta("call_1", "get_weather", "{\"city\":\"Beijing\"}", FinishReason.ToolCalls);

        inner.EnqueueStream(thinkingDelta, signatureDelta, toolCallDelta);

        // 第二轮流式：最终回答
        var finalChunk = new ChatResponse { Object = "chat.completion.chunk" };
        finalChunk.AddDelta("天气晴朗", null, FinishReason.Stop);
        inner.EnqueueStream(finalChunk);

        using var client = new ToolChatClient(inner, new FakeToolProvider());

        var chunks = new List<IChatResponse>();
        await foreach (var chunk in client.GetStreamingResponseAsync(new ChatRequest
        {
            Model = "claude-sonnet-4-6",
            Messages = [new ChatMessage { Role = "user", Content = "北京天气" }],
        }))
        {
            chunks.Add(chunk);
        }

        Assert.True(inner.Requests.Count >= 2, "应至少发起两轮流式请求");
        var round2Req = inner.Requests[1];
        var assistant = round2Req.Messages.FirstOrDefault(m => m.Role == "assistant");
        Assert.NotNull(assistant);
        Assert.Equal("推理中", assistant!.ReasoningContent);
        Assert.Equal("sig_stream", assistant["Signature"]);
    }
}
