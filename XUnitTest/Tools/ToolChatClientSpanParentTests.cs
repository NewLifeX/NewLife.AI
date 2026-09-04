using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NewLife.AI.Clients;
using NewLife.AI.Models;
using NewLife.AI.Services;
using NewLife.AI.Tools;
using NewLife.Log;
using Xunit;

namespace XUnitTest.Tools;

/// <summary>ToolChatClient 埋点父级归属测试。async iterator 跨 yield 后 DefaultSpan.Current 会重置为消费方上下文，
/// 回归验证：工具与每轮模型流式调用仍能正确挂到 ai:tool:loop 之下，且不污染外层上下文
/// （对应星尘调用链中 ai:tool:*/ai:Streaming 父级错乱问题）</summary>
[DisplayName("ToolChatClient埋点父级测试")]
public class ToolChatClientSpanParentTests
{
    /// <summary>记录每次 NewSpan 的名称、父级与跟踪标识。内部用 DefaultSpan 走标准父级继承（读 DefaultSpan.Current）</summary>
    private sealed class CaptureTracer : ITracer
    {
        public sealed record Captured(String Name, String Id, String? ParentId, String TraceId);

        public List<Captured> Spans { get; } = [];

        public Int32 Period { get; set; } = 15;
        public Int32 MaxSamples { get; set; } = 1;
        public Int32 MaxErrors { get; set; } = 10;
        public Int32 Timeout { get; set; } = 15000;
        public Int32 MaxTagLength { get; set; } = 1024;
        public String? AttachParameter { get; set; } = "traceparent";
        public ITracerResolver Resolver { get; set; } = new DefaultTracerResolver();

        public ISpanBuilder BuildSpan(String name) => null!;

        public ISpan NewSpan(String name) => Create(name);

        public ISpan NewSpan(String name, Object? tag)
        {
            var span = Create(name);
            if (tag != null) span.SetTag(tag);
            return span;
        }

        public ISpanBuilder[] TakeAll() => [];

        private ISpan Create(String name)
        {
            var span = new DefaultSpan(this) { Name = name };
            span.Start();
            Spans.Add(new Captured(name, span.Id, span.ParentId, span.TraceId));
            return span;
        }
    }

    /// <summary>流式伪内层客户端：每轮入队一组 chunk；自身迭代器段0创建 ai:Streaming:mock 埋点（仿 AiClientBase）</summary>
    private sealed class SpanAwareInnerClient : IChatClient
    {
        public readonly List<IChatRequest> Requests = [];

        private readonly Queue<IEnumerable<IChatResponse>> _stream = new();

        public ITracer? Tracer { get; set; }

        /// <summary>入队一轮流式响应序列</summary>
        public void EnqueueStream(params IChatResponse[] chunks) => _stream.Enqueue(chunks);

        public Task<IChatResponse> GetResponseAsync(IChatRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public async IAsyncEnumerable<IChatResponse> GetStreamingResponseAsync(IChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Requests.Add(request);

            // 模拟 AiClientBase.GetStreamingResponseAsync：在内层迭代器首段创建模型流式埋点
            using var span = Tracer?.NewSpan("ai:Streaming:mock");

            foreach (var chunk in _stream.Dequeue())
                yield return chunk;
        }

        public void Dispose() { }
    }

    /// <summary>非流式伪内层客户端：脚本化两轮（工具调用 → 最终文本），每轮创建 ai:Chat:mock 埋点</summary>
    private sealed class SyncInnerClient : IChatClient
    {
        private readonly String _toolName;
        private readonly String _toolArgs;
        private readonly String _finalReply;
        private Int32 _callCount;

        public ITracer? Tracer { get; set; }

        public SyncInnerClient(String toolName, String toolArgs, String finalReply)
        {
            _toolName = toolName;
            _toolArgs = toolArgs;
            _finalReply = finalReply;
        }

        public Task<IChatResponse> GetResponseAsync(IChatRequest request, CancellationToken cancellationToken = default)
        {
            _callCount++;

            // 模拟 AiClientBase.GetResponseAsync：创建模型调用埋点
            using var span = Tracer?.NewSpan("ai:Chat:mock");

            ChatResponse resp;
            if (_callCount == 1)
            {
                resp = new ChatResponse
                {
                    Messages =
                    [
                        new ChatChoice
                        {
                            Message = new ChatMessage
                            {
                                Role = "assistant",
                                Content = null,
                                ToolCalls =
                                [
                                    new ToolCall
                                    {
                                        Id = "call_1",
                                        Type = "function",
                                        Function = new FunctionCall { Name = _toolName, Arguments = _toolArgs }
                                    }
                                ]
                            }
                        }
                    ]
                };
            }
            else
            {
                resp = new ChatResponse
                {
                    Messages = [new ChatChoice { Message = new ChatMessage { Role = "assistant", Content = _finalReply } }]
                };
            }
            return Task.FromResult<IChatResponse>(resp);
        }

        public IAsyncEnumerable<IChatResponse> GetStreamingResponseAsync(IChatRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public void Dispose() { }
    }

    /// <summary>提供单工具的提供者，记录真实执行</summary>
    private sealed class OneToolProvider : IToolProvider
    {
        public Int32 CallCount { get; private set; }

        public IList<ChatTool> GetTools(ISet<String>? filterNames = null)
            => [new ChatTool { Function = new FunctionDefinition { Name = "get_weather", Description = "查询天气" } }];

        public Task<IToolResult> CallToolAsync(String toolName, String? arguments, ToolCallContext? context = null, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult<IToolResult>(new ToolResult("晴，25℃"));
        }
    }

    [Fact]
    [DisplayName("流式多轮_每轮模型与工具埋点均挂 ai:tool:loop 之下且不污染外层")]
    public async Task Streaming_AllRounds_ModelAndToolSpansParentUnderToolLoop()
    {
        var tracer = new CaptureTracer();
        var inner = new SpanAwareInnerClient { Tracer = tracer };
        var provider = new OneToolProvider();

        // 第1轮：get_weather 工具调用；第2轮：最终文本
        var c1 = new ChatResponse { Object = "chat.completion.chunk" };
        c1.AddToolCallDelta("call_1", "get_weather", "{\"city\":\"上海\"}", FinishReason.ToolCalls);
        inner.EnqueueStream(c1);

        var c2 = new ChatResponse { Object = "chat.completion.chunk" };
        c2.AddDelta("天气晴朗", null, FinishReason.Stop);
        inner.EnqueueStream(c2);

        // 外层模拟 ai:flowInvokeLlm / ai:StreamSend 等稳定环境（正常 async 方法内 Current 存活）
        using var outer = tracer.NewSpan("outer");

        using var client = new ToolChatClient(inner, provider) { Tracer = tracer };
        var chunks = new List<IChatResponse>();
        await foreach (var chunk in client.GetStreamingResponseAsync(new ChatRequest
        {
            Model = "test-model",
            Messages = [new ChatMessage { Role = "user", Content = "上海天气" }],
        }))
        {
            chunks.Add(chunk);
        }

        // 修复要点1：重挂只作用于迭代器内部段，不得泄漏污染消费方上下文
        Assert.Same(outer, DefaultSpan.Current);

        var loop = tracer.Spans.First(s => s.Name == "ai:tool:loop");

        // ai:tool:loop 的父级为外层稳定环境
        Assert.Equal(outer.Id, loop.ParentId);

        // 修复要点2：每一轮模型流式调用都挂在 ai:tool:loop 之下（修复前第2轮父级错乱为 outer）
        var streams = tracer.Spans.Where(s => s.Name == "ai:Streaming:mock").ToList();
        Assert.Equal(2, streams.Count);
        foreach (var s in streams)
            Assert.Equal(loop.Id, s.ParentId);

        // 修复要点3：工具执行埋点挂在 ai:tool:loop 之下
        var tool = tracer.Spans.First(s => s.Name == "ai:tool:get_weather");
        Assert.Equal(loop.Id, tool.ParentId);

        // 同一请求内 TraceId 全局一致
        Assert.All(tracer.Spans, s => Assert.Equal(loop.TraceId, s.TraceId));

        // 工具真实执行且驱动两轮流式请求
        Assert.Equal(1, provider.CallCount);
        Assert.True(inner.Requests.Count >= 2);
    }

    [Fact]
    [DisplayName("非流式_多轮模型与工具埋点均挂 ai:tool:loop 之下")]
    public async Task Sync_AllRounds_ModelAndToolSpansParentUnderToolLoop()
    {
        var tracer = new CaptureTracer();
        var inner = new SyncInnerClient("get_weather", "{\"city\":\"上海\"}", "上海今天晴") { Tracer = tracer };
        var provider = new OneToolProvider();

        using var outer = tracer.NewSpan("outer");

        using var client = new ToolChatClient(inner, provider) { Tracer = tracer };
        var response = await client.GetResponseAsync(new ChatRequest
        {
            Model = "test-model",
            Messages = [new ChatMessage { Role = "user", Content = "上海天气" }],
        }, CancellationToken.None);

        Assert.Equal("上海今天晴", response.Messages?.FirstOrDefault()?.Message?.Content as String);
        Assert.Same(outer, DefaultSpan.Current);

        var loop = tracer.Spans.First(s => s.Name == "ai:tool:loop");
        Assert.Equal(outer.Id, loop.ParentId);

        // 两轮模型调用与工具执行都挂在 loop 之下（并行工具执行后主流程 Current 已被每轮重挂拉回）
        var chats = tracer.Spans.Where(s => s.Name == "ai:Chat:mock").ToList();
        Assert.Equal(2, chats.Count);
        foreach (var s in chats)
            Assert.Equal(loop.Id, s.ParentId);

        var tool = tracer.Spans.First(s => s.Name == "ai:tool:get_weather");
        Assert.Equal(loop.Id, tool.ParentId);

        Assert.All(tracer.Spans, s => Assert.Equal(loop.TraceId, s.TraceId));
        Assert.Equal(1, provider.CallCount);
    }
}
