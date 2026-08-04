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

/// <summary>ToolChatClient 健壮性测试。覆盖 per-tool 熔断隔离、流式 ToolCallContext.Response 透传、循环迭代回调异常不中断</summary>
public class ToolChatClientRobustnessTests
{
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

    /// <summary>双工具提供者：fail_tool 总是抛异常，ok_tool 总是成功。记录调用历史与最后一次上下文</summary>
    private sealed class MultiToolProvider : IToolProvider
    {
        public readonly List<(String name, String? args)> Calls = [];

        public ToolCallContext? LastContext { get; private set; }

        public IList<ChatTool> GetTools(ISet<String>? filterNames = null, Boolean includeSystem = true)
            =>
            [
                new ChatTool { Type = "function", Function = new FunctionDefinition { Name = "fail_tool", Description = "总是失败的工具" } },
                new ChatTool { Type = "function", Function = new FunctionDefinition { Name = "ok_tool", Description = "总是成功的工具" } },
            ];

        public Task<IToolResult> CallToolAsync(String toolName, String? arguments, ToolCallContext? context = null, CancellationToken cancellationToken = default)
        {
            Calls.Add((toolName, arguments));
            LastContext = context;
            if (toolName == "fail_tool")
                throw new InvalidOperationException("模拟工具执行失败");
            return Task.FromResult<IToolResult>(new ToolResult("{\"ok\":true}"));
        }
    }

    /// <summary>构造带指定工具调用的非流式响应轮次</summary>
    private static ChatResponse BuildToolRound(params (String id, String name)[] calls)
    {
        var response = new ChatResponse { Object = "chat.completion" };
        var msg = new ChatMessage
        {
            Role = "assistant",
            Content = "执行工具",
            ToolCalls = calls.Select(c => new ToolCall
            {
                Id = c.id,
                Type = "function",
                Function = new FunctionCall { Name = c.name, Arguments = "{}" },
            }).ToList(),
        };
        var choice = response.Add(null, null, FinishReason.ToolCalls);
        choice.Message = msg;
        return response;
    }

    [Fact]
    [DisplayName("per-tool 熔断：单工具连续失败熔断不影响同 Provider 其它工具")]
    public async Task PerToolCircuitBreaker_Isolation()
    {
        var provider = new MultiToolProvider();
        var inner = new FakeInnerClient();

        // 第一轮：fail_tool 首次失败（达到阈值1 → 熔断）+ ok_tool 成功
        inner.EnqueueResponse(BuildToolRound(("c1", "fail_tool"), ("c2", "ok_tool")));
        // 第二轮：fail_tool 已被熔断（返回 CIRCUIT_OPEN 错误）+ ok_tool 仍正常
        inner.EnqueueResponse(BuildToolRound(("c3", "fail_tool"), ("c4", "ok_tool")));
        // 第三轮：最终回答
        var final = new ChatResponse { Object = "chat.completion" };
        final.Add("最终回答", null, FinishReason.Stop);
        inner.EnqueueResponse(final);

        using var client = new ToolChatClient(inner, provider)
        {
            FailureThreshold = 1,
            CooldownSeconds = 60,
        };

        var response = await client.GetResponseAsync(new ChatRequest
        {
            Model = "test",
            Messages = [new ChatMessage { Role = "user", Content = "测试" }],
        });

        Assert.Equal("最终回答", response.Text);
        Assert.Equal(3, inner.Requests.Count);

        // fail_tool 仅真正执行 1 次（第二轮被熔断拦截，不进入 CallToolAsync）
        var failCalls = provider.Calls.Where(c => c.name == "fail_tool").ToList();
        Assert.Single(failCalls);

        // ok_tool 不受 fail_tool 熔断连坐，两轮均正常执行
        var okCalls = provider.Calls.Where(c => c.name == "ok_tool").ToList();
        Assert.Equal(2, okCalls.Count);
    }

    [Fact]
    [DisplayName("流式工具轮次：ToolCallContext.Response 携带本轮聚合响应")]
    public async Task Stream_ToolCallContext_ResponseAggregated()
    {
        var provider = new MultiToolProvider();
        var inner = new FakeInnerClient();

        // 第一轮流式：thinking_delta → tool_calls（含 finish_reason=tool_calls）
        var thinkingDelta = new ChatResponse { Object = "chat.completion.chunk" };
        thinkingDelta.AddDelta(null, "推理中", null);

        var toolCallDelta = new ChatResponse { Object = "chat.completion.chunk" };
        toolCallDelta.AddToolCallDelta("call_1", "ok_tool", "{}", FinishReason.ToolCalls);

        inner.EnqueueStream(thinkingDelta, toolCallDelta);

        // 第二轮流式：最终回答
        var finalChunk = new ChatResponse { Object = "chat.completion.chunk" };
        finalChunk.AddDelta("天气晴朗", null, FinishReason.Stop);
        inner.EnqueueStream(finalChunk);

        using var client = new ToolChatClient(inner, provider);

        var chunks = new List<IChatResponse>();
        await foreach (var chunk in client.GetStreamingResponseAsync(new ChatRequest
        {
            Model = "test",
            Messages = [new ChatMessage { Role = "user", Content = "北京天气" }],
        }))
        {
            chunks.Add(chunk);
        }

        // 工具上下文中应携带本轮聚合响应（含思考、正文与工具调用）
        Assert.NotNull(provider.LastContext);
        Assert.NotNull(provider.LastContext!.Response);
        var msg = provider.LastContext.Response!.Messages?.FirstOrDefault()?.Message;
        Assert.NotNull(msg);
        Assert.Equal("推理中", msg!.ReasoningContent);
        Assert.NotNull(msg.ToolCalls);
        Assert.Single(msg.ToolCalls!);
        Assert.Equal("ok_tool", msg.ToolCalls![0].Function?.Name);
    }

    [Fact]
    [DisplayName("OnLoopIteration 回调同步异常不中断工具循环")]
    public async Task LoopIteration_SyncException_DoesNotBreakLoop()
    {
        var provider = new MultiToolProvider();
        var inner = new FakeInnerClient();

        // 第一轮：ok_tool 成功
        inner.EnqueueResponse(BuildToolRound(("c1", "ok_tool")));
        // 第二轮：最终回答
        var final = new ChatResponse { Object = "chat.completion" };
        final.Add("最终回答", null, FinishReason.Stop);
        inner.EnqueueResponse(final);

        using var client = new ToolChatClient(inner, provider)
        {
            // 回调同步抛异常，应被记录且不中断循环
            OnLoopIteration = (_, _) => throw new InvalidOperationException("模拟回调异常"),
        };

        var response = await client.GetResponseAsync(new ChatRequest
        {
            Model = "test",
            Messages = [new ChatMessage { Role = "user", Content = "测试" }],
        });

        // 循环未被中断，正常产出最终回答
        Assert.Equal("最终回答", response.Text);
        Assert.Equal(2, inner.Requests.Count);
        Assert.Single(provider.Calls);
    }
}
