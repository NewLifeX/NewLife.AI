using BenchmarkDotNet.Attributes;
using NewLife.AI.Clients;
using NewLife.AI.Clients.OpenAI;
using NewLife.AI.Models;

namespace NewLife.AI.Benchmark;

/// <summary>请求体构建（ChatCompletionRequest.BuildBody）基准。衡量每次请求构建协议字典的开销</summary>
[MemoryDiagnoser]
public class BuildBodyBenchmark
{
    private IChatRequest _simple = null!;
    private IChatRequest _withTools = null!;

    [GlobalSetup]
    public void Setup()
    {
        _simple = new ChatRequest
        {
            Model = "gpt-4o",
            Messages =
            [
                new ChatMessage { Role = "system", Content = "You are a helpful assistant" },
                new ChatMessage { Role = "user", Content = "Hello, how are you?" },
            ],
            Temperature = 0.7,
            MaxTokens = 2000,
        };

        _withTools = new ChatRequest
        {
            Model = "gpt-4o",
            Messages =
            [
                new ChatMessage { Role = "user", Content = "查询天气" },
            ],
            Tools =
            [
                new ChatTool
                {
                    Type = "function",
                    Function = new FunctionDefinition { Name = "get_weather", Description = "获取天气", Parameters = """{"type":"object","properties":{"city":{"type":"string"}}}""" },
                },
            ],
        };
    }

    [Benchmark(Baseline = true)]
    public IDictionary<String, Object> Build_Simple() => ChatCompletionRequest.BuildBody(_simple);

    [Benchmark]
    public IDictionary<String, Object> Build_WithTools() => ChatCompletionRequest.BuildBody(_withTools);
}
