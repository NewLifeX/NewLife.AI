using BenchmarkDotNet.Attributes;
using NewLife.AI.Embedding;

namespace NewLife.AI.Benchmark;

/// <summary>本地哈希嵌入（HashTextEmbedder）基准。衡量短/长文本向量化的吞吐与内存分配</summary>
[MemoryDiagnoser]
public class HashEmbeddingBenchmark
{
    private HashTextEmbedder _embedder = null!;
    private String _shortText = null!;
    private String _longText = null!;

    [GlobalSetup]
    public void Setup()
    {
        _embedder = new HashTextEmbedder();
        _shortText = "如何使用 NewLife 框架开发 Web 应用";
        _longText = String.Join(" ", Enumerable.Range(0, 200)
            .Select(i => $"这是用于测试的第 {i} 个句子，包含一些常见的中文词汇、术语和标点符号。"));
    }

    [Benchmark(Baseline = true)]
    public Single[] Embed_Short() => _embedder.Embed(_shortText);

    [Benchmark]
    public Single[] Embed_Long() => _embedder.Embed(_longText);
}
