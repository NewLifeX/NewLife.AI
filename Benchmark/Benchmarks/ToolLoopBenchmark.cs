using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using NewLife.AI.Clients;
using NewLife.AI.Models;
using NewLife.AI.Services;
using NewLife.AI.Tools;

namespace NewLife.AI.Benchmark;

/// <summary>工具调用循环（ToolChatClient.GetResponseAsync）基准。覆盖无工具调用与真实工具执行两种路径（Agent 对话的实际形态）</summary>
[MemoryDiagnoser]
public class ToolLoopBenchmark
{
    private ToolChatClient _plainClient = null!;
    private ToolChatClient _toolClient = null!;
    private IChatRequest _plainRequest = null!;
    private IChatRequest _toolRequest = null!;

    /// <summary>固定返回纯文本（无工具调用）</summary>
    private sealed class PlainFakeClient : IChatClient
    {
        public Task<IChatResponse> GetResponseAsync(IChatRequest request, CancellationToken cancellationToken = default)
        {
            var resp = new ChatResponse();
            resp.Add("这是模拟的模型回复内容，不包含工具调用。");
            return Task.FromResult<IChatResponse>(resp);
        }

        public IAsyncEnumerable<IChatResponse> GetStreamingResponseAsync(IChatRequest request, CancellationToken cancellationToken = default)
            => throw new System.NotImplementedException();

        public void Dispose() { }
    }

    /// <summary>脚本化客户端：首轮（请求无 tool 消息）返回工具调用，工具执行后返回最终文本。
    /// 以请求内容判定轮次，保证每次基准迭代独立复位</summary>
    private sealed class ToolCallingFakeClient : IChatClient
    {
        public Task<IChatResponse> GetResponseAsync(IChatRequest request, CancellationToken cancellationToken = default)
        {
            var resp = new ChatResponse();
            var firstRound = request.Messages.All(m => m.Role != "tool");
            if (firstRound)
            {
                resp.Messages =
                [
                    new ChatChoice
                    {
                        Message = new ChatMessage
                        {
                            Role = "assistant",
                            Content = null,
                            ToolCalls = [new ToolCall { Id = "call_1", Function = new FunctionCall { Name = "get_time", Arguments = "{}" } }],
                        },
                    },
                ];
            }
            else
            {
                resp.Add("工具已执行，这是最终回答。");
            }
            return Task.FromResult<IChatResponse>(resp);
        }

        public IAsyncEnumerable<IChatResponse> GetStreamingResponseAsync(IChatRequest request, CancellationToken cancellationToken = default)
            => throw new System.NotImplementedException();

        public void Dispose() { }
    }

    /// <summary>最小工具提供者：暴露 get_time 工具并返回固定结果</summary>
    private sealed class FakeToolProvider : IToolProvider
    {
        public IList<ChatTool> GetTools(ISet<string>? filterNames = null, bool includeSystem = true)
            => [new ChatTool { Type = "function", Function = new FunctionDefinition { Name = "get_time", Description = "获取当前时间" } }];

        public Task<IToolResult> CallToolAsync(string toolName, string? arguments, ToolCallContext? context = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IToolResult>(new ToolResult("2026-08-11 10:00:00"));
    }

    [GlobalSetup]
    public void Setup()
    {
        // 无工具路径
        _plainClient = new ToolChatClient(new PlainFakeClient())
        {
            ToolSetting = new ToolSetting { ToolMaxIterations = 5, ToolResultMaxChars = 3000 },
        };
        _plainRequest = new ChatRequest
        {
            Model = "test-model",
            Messages = [new ChatMessage { Role = "user", Content = "你好，请介绍一下你自己" }],
        };

        // 工具执行路径：一次工具调用后收尾
        _toolClient = new ToolChatClient(new ToolCallingFakeClient(), new FakeToolProvider())
        {
            ToolSetting = new ToolSetting { ToolMaxIterations = 5, ToolResultMaxChars = 3000 },
        };
        _toolRequest = new ChatRequest
        {
            Model = "test-model",
            Messages = [new ChatMessage { Role = "user", Content = "现在几点了？" }],
        };
    }

    [Benchmark(Baseline = true)]
    public async Task<IChatResponse> Loop_PlainText() => await _plainClient.GetResponseAsync(_plainRequest);

    [Benchmark]
    public async Task<IChatResponse> Loop_WithToolCall() => await _toolClient.GetResponseAsync(_toolRequest);
}

