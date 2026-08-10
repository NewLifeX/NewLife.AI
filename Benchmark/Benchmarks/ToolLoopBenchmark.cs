using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using NewLife.AI.Clients;
using NewLife.AI.Models;
using NewLife.AI.Services;
using NewLife.AI.Tools;

namespace NewLife.AI.Benchmark;

/// <summary>工具调用循环（ToolChatClient.GetResponseAsync）基准。衡量无工具调用时的循环开销</summary>
[MemoryDiagnoser]
public class ToolLoopBenchmark
{
    private ToolChatClient _client = null!;
    private IChatRequest _plainRequest = null!;

    private sealed class FakeChatClient : IChatClient
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

    [GlobalSetup]
    public void Setup()
    {
        var registry = new ToolRegistry();
        registry.AddTools<ToolLoopBenchmark>();

        _client = new ToolChatClient(new FakeChatClient(), registry)
        {
            ToolSetting = new ToolSetting { ToolMaxIterations = 5, ToolResultMaxChars = 3000 },
            SelectedTools = null,
        };

        _plainRequest = new ChatRequest
        {
            Model = "test-model",
            Messages = [new ChatMessage { Role = "user", Content = "你好，请介绍一下你自己" }],
        };
    }

    [Benchmark]
    public async Task<IChatResponse> Loop_PlainText() => await _client.GetResponseAsync(_plainRequest);

    /// <summary>测试工具方法（供 ToolRegistry 注册）</summary>
    [ToolDescription("获取当前时间")]
    public static String GetCurrentTime() => System.DateTime.Now.ToString("HH:mm:ss");
}
