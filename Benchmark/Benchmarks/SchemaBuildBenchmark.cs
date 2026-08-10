using System.Reflection;
using BenchmarkDotNet.Attributes;
using NewLife.AI.Models;
using NewLife.AI.Tools;

namespace NewLife.AI.Benchmark;

/// <summary>工具 Schema 构建（ToolSchemaBuilder）基准。衡量注册时对简单/复杂方法构建函数 Schema 的开销</summary>
[MemoryDiagnoser]
public class SchemaBuildBenchmark
{
    private MethodInfo _simpleMethod = null!;
    private MethodInfo _complexMethod = null!;

    [GlobalSetup]
    public void Setup()
    {
        _simpleMethod = typeof(SchemaBuildBenchmark).GetMethod(nameof(SimpleTool))!;
        _complexMethod = typeof(SchemaBuildBenchmark).GetMethod(nameof(ComplexTool))!;
    }

    /// <summary>简单工具：基础类型参数</summary>
    public static String SimpleTool(String query, Int32 count, Boolean enabled) => "";

    /// <summary>复杂工具：嵌套对象 + 集合参数</summary>
    public static String ComplexTool(String query, ComplexParam options, IList<String> tags) => "";

    /// <summary>复杂参数类型</summary>
    public sealed class ComplexParam
    {
        public String? Name { get; set; }
        public Int32 Limit { get; set; }
        public IList<String>? Tags { get; set; }
        public Dictionary<String, Int32>? Weights { get; set; }
    }

    [Benchmark(Baseline = true)]
    public ChatTool Build_Simple() => ToolSchemaBuilder.BuildFromMethod(_simpleMethod);

    [Benchmark]
    public ChatTool Build_Complex() => ToolSchemaBuilder.BuildFromMethod(_complexMethod);
}
