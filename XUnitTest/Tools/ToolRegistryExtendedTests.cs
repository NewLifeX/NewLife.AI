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
            registry.AddTool(null!, static (_, _) => Task.FromResult("ok")));
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
        registry.AddTool("my_tool", static (_, _) => Task.FromResult("result"), "描述");

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
}
