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
using Xunit;

namespace XUnitTest.Tools;

/// <summary>ToolChatClient 流式路径事件时序契约测试。补足流式路径（GetStreamingResponseAsync）的契约锚点：
/// start 先于 done、done 携带截断 LlmResult、Usage 跨轮补发、passthrough start/done 对、ContextLimit 中断、earlyStart 预览。</summary>
[DisplayName("ToolChatClient流式契约测试")]
public class ToolChatClientStreamContractTests
{
    /// <summary>提供多个 StarChat 侧工具的提供者，记录调用名。返回 Both 受众小结果</summary>
    private sealed class MultiToolProvider : IToolProvider
    {
        /// <summary>被调用的工具名列表</summary>
        public List<String> Called { get; } = [];

        /// <inheritdoc/>
        public IList<ChatTool> GetTools(ISet<String>? filterNames = null)
            =>
            [
                new ChatTool { Function = new FunctionDefinition { Name = "get_weather", Description = "查询天气" } },
                new ChatTool { Function = new FunctionDefinition { Name = "get_time", Description = "获取时间" } },
            ];

        /// <inheritdoc/>
        public Task<IToolResult> CallToolAsync(String toolName, String? arguments, ToolCallContext? context = null, CancellationToken cancellationToken = default)
        {
            Called.Add(toolName);
            return Task.FromResult<IToolResult>(new ToolResult($"{{\"{toolName}\":\"成功\"}}"));
        }
    }

    /// <summary>返回指定大小中文大文本结果的工具提供者（LLM 受众），并统计真实执行次数</summary>
    private sealed class BigResultToolProvider(Int32 charCount = 3000) : IToolProvider
    {
        /// <inheritdoc/>
        public IList<ChatTool> GetTools(ISet<String>? filterNames = null)
            => [new ChatTool { Function = new FunctionDefinition { Name = "big_result", Description = "返回大结果" } }];

        /// <inheritdoc/>
        public Task<IToolResult> CallToolAsync(String toolName, String? arguments, ToolCallContext? context = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IToolResult>(new ToolResult(new String('大', charCount)));
    }

    /// <summary>提供 StarChat 侧工具（server_tool）并记录调用次数的提供者（passthrough 对照）</summary>
    private sealed class ServerToolProvider : IToolProvider
    {
        /// <summary>服务端执行次数</summary>
        public Int32 CallCount { get; private set; }

        /// <inheritdoc/>
        public IList<ChatTool> GetTools(ISet<String>? filterNames = null)
            => [new ChatTool { Function = new FunctionDefinition { Name = "server_tool", Description = "服务端工具" } }];

        /// <inheritdoc/>
        public Task<IToolResult> CallToolAsync(String toolName, String? arguments, ToolCallContext? context = null, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult<IToolResult>(new ToolResult("服务端执行成功"));
        }
    }

    /// <summary>伪内层客户端：记录请求并返回脚本化响应（非流式 + 流式双通道，流式每轮一次 EnqueueStream）</summary>
    private sealed class FakeInnerClient : IChatClient
    {
        /// <summary>收到的全部请求（含每轮流式请求）</summary>
        public readonly List<IChatRequest> Requests = [];

        private readonly Queue<IEnumerable<IChatResponse>> _stream = new();

        /// <summary>入队一轮流式响应序列</summary>
        public void EnqueueStream(params IChatResponse[] chunks) => _stream.Enqueue(chunks);

        /// <inheritdoc/>
        public Task<IChatResponse> GetResponseAsync(IChatRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        /// <inheritdoc/>
        public async IAsyncEnumerable<IChatResponse> GetStreamingResponseAsync(IChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            foreach (var chunk in _stream.Dequeue())
                yield return chunk;
        }

        /// <inheritdoc/>
        public void Dispose() { }
    }

    /// <summary>从全部 chunk 中按序收集工具调用事件。ToolCallEvents 为 ChatResponse 专属属性，需转型读取</summary>
    private static List<ToolCallEventInfo> CollectEvents(List<IChatResponse> chunks)
        => chunks.SelectMany(c => (c as ChatResponse)?.ToolCallEvents ?? []).ToList();

    /// <summary>收集流式输出为 chunk 列表</summary>
    private static async Task<List<IChatResponse>> DrainAsync(ToolChatClient client, ChatRequest request)
    {
        var chunks = new List<IChatResponse>();
        await foreach (var chunk in client.GetStreamingResponseAsync(request))
        {
            chunks.Add(chunk);
        }
        return chunks;
    }

    [Fact]
    [DisplayName("流式同轮_start先于done且toolCallId配对")]
    public async Task Stream_SameRound_StartBeforeDoneWithPairedIds()
    {
        var provider = new MultiToolProvider();
        var inner = new FakeInnerClient();

        // 第一轮流式：单个工具调用（arguments 分 chunk 到达）
        var c1 = new ChatResponse { Object = "chat.completion.chunk" };
        c1.AddToolCallDelta("call_a", "get_weather", "{\"cit");
        var c2 = new ChatResponse { Object = "chat.completion.chunk" };
        c2.AddToolCallDelta("call_a", null!, "y\":\"Beijing\"}", FinishReason.ToolCalls);
        inner.EnqueueStream(c1, c2);

        // 第二轮流式：最终回答
        var final = new ChatResponse { Object = "chat.completion.chunk" };
        final.AddDelta("查询完成", null, FinishReason.Stop);
        inner.EnqueueStream(final);

        using var client = new ToolChatClient(inner, provider);
        var chunks = await DrainAsync(client, new ChatRequest
        {
            Model = "test-model",
            Messages = [new ChatMessage { Role = "user", Content = "查询北京天气" }],
        });

        var events = CollectEvents(chunks);

        // 顺序契约：同轮所有 start 事件先于第一个 done 事件（前端按序渲染卡片与结果）
        var firstDone = events.FindIndex(e => e.Type == "done");
        var lastStart = events.FindLastIndex(e => e.Type == "start");
        Assert.True(firstDone > lastStart, $"所有 start 应先于 done，实际事件：{String.Join(",", events.Select(e => $"{e.Type}:{e.Name}"))}");

        // toolCallId 配对：call_a 有 start 与 done
        Assert.Contains(events, e => e.Type == "start" && e.ToolCallId == "call_a");
        Assert.Contains(events, e => e.Type == "done" && e.ToolCallId == "call_a");

        // 服务端工具真实执行，且驱动了两轮流式请求
        Assert.Equal(new[] { "get_weather" }, provider.Called);
        Assert.True(inner.Requests.Count >= 2);
    }

    [Fact]
    [DisplayName("流式done事件_LlmResult为截断内容且与发给LLM一致_用户内容完整")]
    public async Task Stream_DoneEvent_LlmResultTruncatedAndConsistent()
    {
        var inner = new FakeInnerClient();

        var c1 = new ChatResponse { Object = "chat.completion.chunk" };
        c1.AddToolCallDelta("call_1", "big_result", "{}", FinishReason.ToolCalls);
        inner.EnqueueStream(c1);

        var final = new ChatResponse { Object = "chat.completion.chunk" };
        final.AddDelta("完成", null, FinishReason.Stop);
        inner.EnqueueStream(final);

        using var client = new ToolChatClient(inner, new BigResultToolProvider(3000))
        {
            ToolSetting = new ToolSetting { ToolMaxIterations = 10, ToolResultMaxChars = 100 },
        };

        var chunks = await DrainAsync(client, new ChatRequest
        {
            Model = "test-model",
            Messages = [new ChatMessage { Role = "user", Content = "请返回大结果" }],
        });

        var done = CollectEvents(chunks).FirstOrDefault(e => e.Type == "done");
        Assert.NotNull(done);

        // 用户受众完整（前端渲染不截断），Llm 受众截断（历史回放与发给 LLM 一致）
        Assert.Equal(3000, done!.Value?.Length);
        Assert.NotNull(done.LlmResult);
        Assert.True(done.LlmResult!.Length < 3000);
        Assert.StartsWith(new String('大', 50), done.LlmResult);
        Assert.EndsWith(new String('大', 50), done.LlmResult);

        // 第二轮请求中 role=tool 消息与 done 事件 LlmResult 完全一致（历史回放一致契约 5-A）
        var round2Req = inner.Requests[1];
        var toolMsg = round2Req.Messages.LastOrDefault(m => m?.Role == "tool");
        Assert.NotNull(toolMsg);
        Assert.Equal(done.LlmResult, toolMsg!.Content as String);
    }

    [Fact]
    [DisplayName("流式跨轮_含Usage的chunk后补发累计总量")]
    public async Task Stream_MultiRound_EmitsAccumulatedUsage()
    {
        var inner = new FakeInnerClient();

        // 第一轮流式（工具轮）：chunk 携带 Usage
        var c1 = new ChatResponse { Object = "chat.completion.chunk" };
        c1.AddToolCallDelta("call_1", "get_weather", "{\"city\":\"Beijing\"}", FinishReason.ToolCalls);
        c1.Usage = new UsageDetails { InputTokens = 10, OutputTokens = 5, TotalTokens = 15 };
        inner.EnqueueStream(c1);

        // 第二轮流式（最终回答）：chunk 携带 Usage，随后应补发跨轮累计 43
        var final = new ChatResponse { Object = "chat.completion.chunk" };
        final.AddDelta("天气晴朗", null, FinishReason.Stop);
        final.Usage = new UsageDetails { InputTokens = 20, OutputTokens = 8, TotalTokens = 28 };
        inner.EnqueueStream(final);

        using var client = new ToolChatClient(inner, new MultiToolProvider());
        var chunks = await DrainAsync(client, new ChatRequest
        {
            Model = "test-model",
            Messages = [new ChatMessage { Role = "user", Content = "北京天气" }],
        });

        // 第二轮的含 Usage chunk 后补发了 15+28=43 的跨轮累计总量 chunk
        Assert.Contains(chunks, c => c.Usage?.TotalTokens == 43);
    }

    [Fact]
    [DisplayName("流式passthrough_含客户端工具补发start/done对且服务端不执行")]
    public async Task Stream_Passthrough_EmitsStartDonePair()
    {
        var provider = new ServerToolProvider();
        var inner = new FakeInnerClient();

        var c1 = new ChatResponse { Object = "chat.completion.chunk" };
        c1.AddToolCallDelta("call_1", "client_tool", "{\"q\":1}", FinishReason.ToolCalls);
        inner.EnqueueStream(c1);

        using var client = new ToolChatClient(inner, provider);

        var request = new ChatRequest
        {
            Model = "test-model",
            Messages = [new ChatMessage { Role = "user", Content = "请调用客户端工具" }],
            Tools = [new ChatTool { Function = new FunctionDefinition { Name = "client_tool", Description = "客户端工具" } }],
        };

        var chunks = await DrainAsync(client, request);
        var events = CollectEvents(chunks);

        // 补发 start/done 事件对（done 携带完整 arguments），且服务端工具不执行、无第二轮 LLM 调用
        var starts = events.Where(e => e.Type == "start" && e.Name == "client_tool").ToList();
        var done = events.FirstOrDefault(e => e.Type == "done" && e.Name == "client_tool");
        Assert.NotEmpty(starts);
        Assert.NotNull(done);
        Assert.Equal("{\"q\":1}", done!.Value);
        Assert.Equal(0, provider.CallCount);
        Assert.Single(inner.Requests);
    }

    [Fact]
    [DisplayName("流式ContextLimit_极小预算第一轮即中断")]
    public async Task Stream_ContextLimit_TinyBudget_StopsFirstRound()
    {
        var inner = new FakeInnerClient();
        using var client = new ToolChatClient(inner, new BigResultToolProvider());

        var request = new ChatRequest
        {
            Model = "test-model",
            Messages = [new ChatMessage { Role = "user", Content = "请调用 big_result" }],
        };
        request["MaxInputTokens"] = 5;

        var chunks = await DrainAsync(client, request);

        Assert.Equal(ToolLoopStopReason.ContextLimit, client.StopReason);
        Assert.Empty(inner.Requests);   // 第一轮预算检查即中断，未发起任何 LLM 调用
        Assert.Empty(chunks);
    }

    [Fact]
    [DisplayName("流式earlyStart_参数分块到达时预览事件先于完整start")]
    public async Task Stream_EarlyStart_PreviewBeforeFullStart()
    {
        var provider = new MultiToolProvider();
        var inner = new FakeInnerClient();

        // 第一轮流式：arguments 分块到达（函数名首现即发 earlyStart 预览，args=null）
        var c1 = new ChatResponse { Object = "chat.completion.chunk" };
        c1.AddToolCallDelta("call_a", "get_weather", "{\"cit");
        var c2 = new ChatResponse { Object = "chat.completion.chunk" };
        c2.AddToolCallDelta("call_a", null!, "y\":\"Beijing\"}", FinishReason.ToolCalls);
        inner.EnqueueStream(c1, c2);

        var final = new ChatResponse { Object = "chat.completion.chunk" };
        final.AddDelta("天气晴朗", null, FinishReason.Stop);
        inner.EnqueueStream(final);

        using var client = new ToolChatClient(inner, provider);
        var chunks = await DrainAsync(client, new ChatRequest
        {
            Model = "test-model",
            Messages = [new ChatMessage { Role = "user", Content = "北京天气" }],
        });

        var events = CollectEvents(chunks);
        var starts = events.Where(e => e.Type == "start" && e.ToolCallId == "call_a").ToList();

        // 预览事件（args=null）先出现，完整 start（args 完整）随后
        Assert.True(starts.Count >= 2, $"应至少含预览+完整两个 start，实际 {starts.Count}");
        Assert.Contains(starts, e => e.Value == null);
        var previewIndex = starts.FindIndex(e => e.Value == null);
        var fullIndex = starts.FindIndex(e => e.Value != null);
        Assert.True(previewIndex >= 0 && fullIndex > previewIndex, "预览 start 应先于完整 start");

        // 完整 start 携带拼合后的完整 arguments
        var full = starts.First(e => e.Value != null);
        Assert.Equal("{\"city\":\"Beijing\"}", full.Value);
    }
}
