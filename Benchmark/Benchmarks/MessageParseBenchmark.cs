using BenchmarkDotNet.Attributes;
using NewLife.AI.Models;

namespace NewLife.AI.Benchmark;

/// <summary>消息多模态解析（ChatMessage.ParseMultimodalContent）基准。衡量历史消息构建时的 JSON 解析开销</summary>
[MemoryDiagnoser]
public class MessageParseBenchmark
{
    private Object _multimodal = null!;
    private Object _textOnly = null!;

    [GlobalSetup]
    public void Setup()
    {
        _multimodal = """[{"type":"text","text":"hello"},{"type":"image_url","image_url":{"url":"data:image/png;base64,AAAA"}},{"type":"audio","audio":{"url":"data:audio/wav;base64,BBBB"}}]""";
        _textOnly = "纯文本内容";
    }

    [Benchmark(Baseline = true)]
    public IList<AIContent>? Parse_Multimodal() => ChatMessage.ParseMultimodalContent(_multimodal);

    [Benchmark]
    public IList<AIContent>? Parse_Text() => ChatMessage.ParseMultimodalContent(_textOnly);
}
