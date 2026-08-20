using System.Reflection;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using NewLife.AI.Tools;

namespace NewLife.AI.Benchmark;

/// <summary>工具 Schema 体积基准：类型化数组参数 vs JSON 字符串参数生成的 JSON Schema 大小。
/// 回答"类型化后发给模型的工具声明是否显著变大（token 开销）"。
/// 对比 show_timeline 类工具改造前后的 schema：字符串版本 items 为 {"type":"string"}，
/// 类型化版本 items 为 array + 元素对象属性（date/title/description/color/category）。</summary>
[MemoryDiagnoser]
public class SchemaSizeBenchmark
{
    /// <summary>演示 POCO：模拟 TimelineItem</summary>
    public sealed record BenchItem(String Date, String Title, String? Description, String? Color, String? Category);

    private MethodInfo _typedMethod = null!;
    private MethodInfo _stringMethod = null!;

    [GlobalSetup]
    public void Setup()
    {
        _typedMethod = typeof(SchemaSizeBenchmark).GetMethod(nameof(ShowTimelineTyped))!;
        _stringMethod = typeof(SchemaSizeBenchmark).GetMethod(nameof(ShowTimelineString))!;

        // 输出两个版本的 schema 实际体积（字节数），回答"类型化后发给模型的声明变大多少"
        Console.WriteLine($"[SchemaSize] StringVersion bytes = {SchemaBytes(_stringMethod)}");
        Console.WriteLine($"[SchemaSize] TypedVersion bytes   = {SchemaBytes(_typedMethod)}");
        Console.WriteLine($"[SchemaSize] Typed/String ratio   = {(Double)SchemaBytes(_typedMethod) / SchemaBytes(_stringMethod):F2}");
    }

    /// <summary>类型化版本（当前改造后）：items/palette 为强类型数组</summary>
    public static String ShowTimelineTyped(
        String title,
        IList<BenchItem>? items,
        String? layout = null,
        IList<String>? palette = null,
        String? density = null) => "";

    /// <summary>字符串版本（改造前）：items/palette 为 JSON 字符串</summary>
    public static String ShowTimelineString(
        String title,
        String items,
        String? layout = null,
        String? palette = null,
        String? density = null) => "";

    private static Int32 SchemaBytes(MethodInfo method)
        => JsonSerializer.Serialize(ToolSchemaBuilder.BuildFromMethod(method).Function!.Parameters).Length;

    [Benchmark(Baseline = true)]
    public Int32 Schema_StringVersion() => SchemaBytes(_stringMethod);

    [Benchmark]
    public Int32 Schema_TypedVersion() => SchemaBytes(_typedMethod);
}
