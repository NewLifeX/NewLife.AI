using System;
using BenchmarkDotNet.Attributes;
using NewLife.AI.Clients;
using NewLife.AI.Clients.OpenAI;
using NewLife.AI.Models;

namespace NewLife.AI.Benchmark;

/// <summary>流式 SSE chunk 解析基准。模拟 ChatStreamAsync 每 chunk 的完整管线（data: 前缀剥离 + 错误检测 + JSON 解析），Web 默认流式对话的最高频路径</summary>
[MemoryDiagnoser]
public class StreamChunkParseBenchmark
{
    /// <summary>暴露 protected 解析方法的子类</summary>
    private sealed class ExposedClient : OpenAIChatClient
    {
        public ExposedClient() : base(new AiClientOptions { ApiKey = "test", Model = "gpt-4o" }) { }

        /// <summary>执行每 chunk 完整管线：错误检测 + ParseChunk</summary>
        public IChatResponse? ProcessChunk(String data, IChatRequest request)
        {
            EnsureNoStreamError(data, Name);
            return ParseChunk(data, request, null);
        }
    }

    private ExposedClient _client = null!;
    private IChatRequest _request = null!;
    private String _textChunk = null!;
    private String _toolChunk = null!;
    private String _finishChunk = null!;

    [GlobalSetup]
    public void Setup()
    {
        _client = new ExposedClient();
        _request = new ChatRequest { Model = "gpt-4o" };

        _textChunk = """data: {"id":"chatcmpl-abc","object":"chat.completion.chunk","model":"gpt-4o","choices":[{"index":0,"delta":{"content":"这是一段流式输出文本"},"finish_reason":null}]}""";
        _toolChunk = """data: {"id":"chatcmpl-def","object":"chat.completion.chunk","model":"gpt-4o","choices":[{"index":0,"delta":{"tool_calls":[{"index":0,"id":"call_1","type":"function","function":{"name":"get_weather","arguments":"{\"city\":"}}]},"finish_reason":null}]}""";
        _finishChunk = """data: {"id":"chatcmpl-ghi","object":"chat.completion.chunk","model":"gpt-4o","choices":[{"index":0,"delta":{},"finish_reason":"stop"}]}""";
    }

    /// <summary>完整管线：data: 前缀剥离 + 错误检测 + 解析</summary>
    private IChatResponse? ProcessSse(String line)
    {
        if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return null;
        var data = line.Substring(5).Trim();
        if (data == "[DONE]" || data.Length == 0) return null;
        return _client.ProcessChunk(data, _request);
    }

    [Benchmark(Baseline = true)]
    public IChatResponse? Parse_TextChunk() => ProcessSse(_textChunk);

    [Benchmark]
    public IChatResponse? Parse_ToolChunk() => ProcessSse(_toolChunk);

    [Benchmark]
    public IChatResponse? Parse_FinishChunk() => ProcessSse(_finishChunk);
}
