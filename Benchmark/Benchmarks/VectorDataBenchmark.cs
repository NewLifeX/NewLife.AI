using BenchmarkDotNet.Attributes;
using NewLife.AI.Embedding;

namespace NewLife.AI.Benchmark;

/// <summary>向量数据（VectorData）基准。衡量 base64 序列化/反序列化与 ToVector 解码的吞吐</summary>
[MemoryDiagnoser]
public class VectorDataBenchmark
{
    private VectorData _data = null!;
    private Single[] _vector = null!;

    [GlobalSetup]
    public void Setup()
    {
        var rnd = new Random(42);
        _vector = Enumerable.Range(0, 512).Select(_ => (Single)rnd.NextDouble()).ToArray();
        _data = VectorData.FromVector("local-hash-v2", _vector);
    }

    [Benchmark(Baseline = true)]
    public Single[] ToVector() => _data.ToVector();

    [Benchmark]
    public VectorData FromVector() => VectorData.FromVector("local-hash-v2", _vector);
}
