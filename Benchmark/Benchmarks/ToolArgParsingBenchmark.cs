using System.Text.Json;
using BenchmarkDotNet.Attributes;
using NewLife.AI.Tools;

namespace NewLife.AI.Benchmark;

/// <summary>工具复杂参数解析基准：类型化数组参数（IList&lt;POCO&gt;）vs JSON 字符串参数（工具内二次解析）。
/// 对比 show_timeline/show_kanban 等工具在本次"JSON 字符串参数类型化"改造前后的完整调用链路开销。
/// 说明：Benchmark 项目不引用 NewLife.ChatAI，此处用机制等价的演示 POCO（BenchItem）替代 TimelineItem，
/// 两条路径走的都是 ToolRegistry.DeserializeArguments → ConvertValue 同一套框架代码，结论可迁移。</summary>
[MemoryDiagnoser]
public class ToolArgParsingBenchmark
{
    /// <summary>演示 POCO：模拟 TimelineItem（date/title/description/color/category）</summary>
    public sealed record BenchItem(String Date, String Title, String? Description, String? Color, String? Category);

    /// <summary>回显工具：暴露两条参数形态的等价工具</summary>
    private sealed class EchoTool
    {
        /// <summary>类型化路径：接收 IList&lt;BenchItem&gt;，框架 ConvertValue 自动转换</summary>
        [ToolDescription("echo_typed")]
        public String EchoTyped(IList<BenchItem>? items) => items?.Count.ToString() ?? "0";

        /// <summary>字符串路径（旧方案）：接收 JSON 数组字符串，工具内二次 JsonNode.Parse</summary>
        [ToolDescription("echo_string")]
        public String EchoString(String items)
        {
            var node = System.Text.Json.Nodes.JsonNode.Parse(items);
            return node?.AsArray().Count.ToString() ?? "0";
        }
    }

    private ToolRegistry _registry = null!;
    private String _typedArgs5 = null!;
    private String _typedArgs50 = null!;
    private String _stringArgs5 = null!;
    private String _stringArgs50 = null!;

    [GlobalSetup]
    public void Setup()
    {
        _registry = new ToolRegistry();
        _registry.AddTools(new EchoTool());

        _typedArgs5 = BuildTypedArgs(5);
        _typedArgs50 = BuildTypedArgs(50);
        _stringArgs5 = BuildStringArgs(5);
        _stringArgs50 = BuildStringArgs(50);
    }

    /// <summary>构造原生数组形态的 arguments（LLM 按 schema 输出的标准形态）</summary>
    private static String BuildTypedArgs(Int32 n)
    {
        var items = Enumerable.Range(1, n).Select(i => new
        {
            date = $"202{i % 9 + 1}",
            title = $"里程碑 {i}",
            description = $"第 {i} 个里程碑的描述文本",
        }).ToList();
        return JsonSerializer.Serialize(new { items });
    }

    /// <summary>构造 JSON 字符串包裹形态的 arguments（旧方案下模型手工转义后的形态）</summary>
    private static String BuildStringArgs(Int32 n)
    {
        var items = Enumerable.Range(1, n).Select(i => new
        {
            date = $"202{i % 9 + 1}",
            title = $"里程碑 {i}",
            description = $"第 {i} 个里程碑的描述文本",
        }).ToList();
        var arrJson = JsonSerializer.Serialize(items);
        return JsonSerializer.Serialize(new { items = arrJson });
    }

    [Benchmark(Baseline = true)]
    public async Task<String> String_5() => await _registry.InvokeAsync("echo_string", _stringArgs5);

    [Benchmark]
    public async Task<String> String_50() => await _registry.InvokeAsync("echo_string", _stringArgs50);

    [Benchmark]
    public async Task<String> Typed_5() => await _registry.InvokeAsync("echo_typed", _typedArgs5);

    [Benchmark]
    public async Task<String> Typed_50() => await _registry.InvokeAsync("echo_typed", _typedArgs50);
}
