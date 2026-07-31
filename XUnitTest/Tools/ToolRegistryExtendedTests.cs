using System;
using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NewLife.AI.Tools;
using Xunit;

namespace XUnitTest.Tools;

/// <summary>ToolRegistry 扩展注册方法及 AddToolsFromAssembly 测试</summary>
[DisplayName("工具注册表扩展测试")]
public class ToolRegistryExtendedTests
{
    // ── AddTool（委托注册）────────────────────────────────────────────────

    [Fact]
    [DisplayName("AddTool—名称为 null 抛出 ArgumentNullException")]
    public void AddTool_NullName_Throws()
    {
        var registry = new ToolRegistry();
        Assert.Throws<ArgumentNullException>(() =>
            registry.AddTool(null!, static (_, _, _) => Task.FromResult("ok")));
    }

    [Fact]
    [DisplayName("AddTool—委托为 null 抛出 ArgumentNullException")]
    public void AddTool_NullHandler_Throws()
    {
        var registry = new ToolRegistry();
        Assert.Throws<ArgumentNullException>(() =>
            registry.AddTool("tool", null!));
    }

    [Fact]
    [DisplayName("AddTool—注册后 Tools 列表包含该工具")]
    public void AddTool_RegisteredToolAppearsInList()
    {
        var registry = new ToolRegistry();
        registry.AddTool("my_tool", static (_, _, _) => Task.FromResult("result"), "描述");

        Assert.Single(registry.Tools);
        Assert.Equal("my_tool", registry.Tools[0].Function!.Name);
        Assert.Equal("描述", registry.Tools[0].Function!.Description);
    }

    // ── AddTools（实例注册）──────────────────────────────────────────────

    [Fact]
    [DisplayName("AddTools<T>—注册 BuiltinToolService 后包含 get_current_time 和 calculate")]
    public void AddTools_BuiltinToolService_RegistersBothTools()
    {
        var registry = new ToolRegistry();
        registry.AddTools(new BuiltinToolService());

        var names = new System.Collections.Generic.HashSet<String>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in registry.Tools)
            names.Add(t.Function!.Name);

        Assert.Contains("get_current_time", names);
        Assert.Contains("calculate", names);
    }

    [Fact]
    [DisplayName("AddTools(Object)—null 实例抛出 ArgumentNullException")]
    public void AddTools_NullInstance_Throws()
    {
        var registry = new ToolRegistry();
        Assert.Throws<ArgumentNullException>(() => registry.AddTools((Object)null!));
    }

    // ── AddToolsFromAssembly ──────────────────────────────────────────────

    [Fact]
    [DisplayName("AddToolsFromAssembly—null 程序集抛出 ArgumentNullException")]
    public void AddToolsFromAssembly_NullAssembly_Throws()
    {
        var registry = new ToolRegistry();
        Assert.Throws<ArgumentNullException>(() => registry.AddToolsFromAssembly(null!));
    }

    [Fact]
    [DisplayName("AddToolsFromAssembly—扫描 NewLife.AI 程序集注册 BuiltinToolService 方法")]
    public void AddToolsFromAssembly_NewLifeAiAssembly_RegistersBuiltinTools()
    {
        var registry = new ToolRegistry();
        var assembly = typeof(BuiltinToolService).Assembly;
        registry.AddToolsFromAssembly(assembly);

        var names = new System.Collections.Generic.HashSet<String>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in registry.Tools)
            names.Add(t.Function!.Name);

        Assert.Contains("get_current_time", names);
        Assert.Contains("calculate", names);
    }

    // ── InvokeAsync ────────────────────────────────────────────────────────

    [Fact]
    [DisplayName("InvokeAsync—未注册工具抛出 KeyNotFoundException")]
    public async Task InvokeAsync_UnknownTool_ThrowsKeyNotFoundException()
    {
        var registry = new ToolRegistry();
        await Assert.ThrowsAsync<System.Collections.Generic.KeyNotFoundException>(() =>
            registry.InvokeAsync("nonexistent", null));
    }

    // ── AddToolAlias（工具名别名）──────────────────────────────────────────

    [Fact]
    [DisplayName("AddToolAlias—别名可路由到目标工具")]
    public async Task AddToolAlias_RoutesToTarget()
    {
        var registry = new ToolRegistry();
        registry.AddTool("run_sql", static (_, _, _) => Task.FromResult("执行成功"));
        registry.AddToolAlias("query_sql", "run_sql");

        var result = await registry.InvokeAsync("query_sql", "{}");

        Assert.Equal("执行成功", result);
    }

    [Fact]
    [DisplayName("InvokeAsync—未注册工具错误信息含相近工具建议")]
    public async Task InvokeAsync_UnknownTool_MessageContainsSuggestion()
    {
        var registry = new ToolRegistry();
        registry.AddTool("run_sql", static (_, _, _) => Task.FromResult("ok"));

        var ex = await Assert.ThrowsAsync<System.Collections.Generic.KeyNotFoundException>(() =>
            registry.InvokeAsync("run_sqll", null));

        Assert.Contains("run_sql", ex.Message);
        Assert.Contains("可用工具", ex.Message);
    }

    [Fact]
    [DisplayName("InvokeAsync—调用 calculate 工具返回正确结果")]
    public async Task InvokeAsync_Calculate_ReturnsResult()
    {
        var registry = new ToolRegistry();
        registry.AddTools(new BuiltinToolService());

        var result = await registry.InvokeAsync("calculate", "{\"expression\":\"2+3\"}");

        Assert.NotNull(result);
        Assert.Contains("5", result);
    }

    [Fact]
    [DisplayName("InvokeAsync—调用 get_current_time 返回含 datetime 的字符串")]
    public async Task InvokeAsync_GetCurrentTime_ReturnsDatetime()
    {
        var registry = new ToolRegistry();
        registry.AddTools(new BuiltinToolService());

        var result = await registry.InvokeAsync("get_current_time", null);

        Assert.Contains("datetime:", result);
    }

    // ── TryInvokeAsync ─────────────────────────────────────────────────────

    [Fact]
    [DisplayName("TryInvokeAsync—未注册工具返回 error JSON（不抛异常）")]
    public async Task TryInvokeAsync_UnknownTool_ReturnsErrorJson()
    {
        var registry = new ToolRegistry();
        var result = await registry.TryInvokeAsync("ghost_tool", null);

        Assert.Contains("error", result);
        Assert.Contains("ghost_tool", result);
    }

    [Fact]
    [DisplayName("TryInvokeAsync—调用 calculate 工具返回正确结果")]
    public async Task TryInvokeAsync_Calculate_ReturnsResult()
    {
        var registry = new ToolRegistry();
        registry.AddTools(new BuiltinToolService());

        var result = await registry.TryInvokeAsync("calculate", "{\"expression\":\"10*10\"}");

        Assert.DoesNotContain("\"error\"", result);
        Assert.Contains("100", result);
    }

    // ── 重复注册不覆盖原工具 ──────────────────────────────────────────────

    [Fact]
    [DisplayName("AddTools—重复注册同名工具不覆盖，注册表数量不增加")]
    public void AddTools_DuplicateTool_NotOverwritten()
    {
        var registry = new ToolRegistry();
        registry.AddTools(new BuiltinToolService());
        var countBefore = registry.Tools.Count;

        // 再次注册同一实例，不应增加重复条目
        registry.AddTools(new BuiltinToolService());
        Assert.Equal(countBefore, registry.Tools.Count);
    }

    // ── LLM 数组参数兼容 ──────────────────────────────────────────────────

    /// <summary>测试用数组参数工具服务，验证 LLM 将 String[] 以 JSON 字符串形式传递时的兼容性</summary>
    private sealed class ArrayParamToolService
    {
        /// <summary>接收两个字符串数组参数，返回合并结果</summary>
        /// <param name="firsts">第一个城市列表</param>
        /// <param name="lasts">第二个城市列表</param>
        [ToolDescription("merge_cities")]
        [DisplayName("合并城市列表")]
        [Description("将两个城市列表合并为一个")]
        public String MergeCities(
            [Description("第一个城市列表")] String[] firsts,
            [Description("第二个城市列表")] String[] lasts)
        {
            return String.Join(",", firsts) + "|" + String.Join(",", lasts);
        }

        /// <summary>接收一个整型数组参数</summary>
        [ToolDescription("sum_numbers")]
        [DisplayName("数组求和")]
        [Description("对整型数组求和")]
        public Int32 SumNumbers([Description("数值列表")] Int32[] numbers)
        {
            var sum = 0;
            foreach (var n in numbers) sum += n;
            return sum;
        }

        /// <summary>接收一个 List 参数</summary>
        [ToolDescription("concat_tags")]
        [DisplayName("连接标签")]
        [Description("将标签列表连接成字符串")]
        public String ConcatTags([Description("标签列表")] System.Collections.Generic.List<String> tags)
        {
            return String.Join(",", tags);
        }
    }

    [Fact]
    [DisplayName("String[] 参数—JSON 数组字符串兼容（如 \"[\"上海\"]\"）")]
    public async Task StringArray_FromJsonString_Works()
    {
        var registry = new ToolRegistry();
        registry.AddTools(new ArrayParamToolService());

        // LLM 将 String[] 参数传成 JSON 字符串（外层 JSON 值被解析为字符串）
        var result = await registry.InvokeAsync("merge_cities",
            "{\"firsts\":\"[\\\"上海\\\"]\",\"lasts\":\"[\\\"武汉\\\",\\\"济南\\\"]\"}");

        Assert.Equal("上海|武汉,济南", result);
    }

    [Fact]
    [DisplayName("String[] 参数—正常 JSON 数组（非字符串）保持正常")]
    public async Task StringArray_FromNormalArray_Works()
    {
        var registry = new ToolRegistry();
        registry.AddTools(new ArrayParamToolService());

        // 正常情况：LLM 正确传递数组
        var result = await registry.InvokeAsync("merge_cities",
            "{\"firsts\":[\"上海\"],\"lasts\":[\"武汉\",\"济南\"]}");

        Assert.Equal("上海|武汉,济南", result);
    }

    [Fact]
    [DisplayName("Int32[] 参数—JSON 数组字符串兼容")]
    public async Task Int32Array_FromJsonString_Works()
    {
        var registry = new ToolRegistry();
        registry.AddTools(new ArrayParamToolService());

        var result = await registry.InvokeAsync("sum_numbers",
            "{\"numbers\":\"[1, 2, 3, 4, 5]\"}");

        Assert.Equal("15", result);
    }

    [Fact]
    [DisplayName("List<String> 参数—JSON 数组字符串兼容")]
    public async Task ListString_FromJsonString_Works()
    {
        var registry = new ToolRegistry();
        registry.AddTools(new ArrayParamToolService());

        var result = await registry.InvokeAsync("concat_tags",
            "{\"tags\":\"[\\\"AI\\\",\\\"Chat\\\",\\\"Tool\\\"]\"}");

        Assert.Equal("AI,Chat,Tool", result);
    }
}
