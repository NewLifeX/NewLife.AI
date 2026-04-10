using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using NewLife;
using NewLife.AI.Tools;
using NewLife.ChatAI.Services;
using Xunit;

namespace XUnitTest.Services;

/// <summary>NativeToolSyncService 扫描逻辑单元测试</summary>
[DisplayName("ChatAI 内置工具同步服务测试")]
public class NativeToolSyncServiceTests
{
    // 通过反射访问私有 _seeds 字典
    private static Dictionary<String, (String, String, String)> GetSeeds()
    {
        var field = typeof(NativeToolSyncService).GetField("_seeds",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (Dictionary<String, (String, String, String)>)field!.GetValue(null)!;
    }

    // ── ToolRegistry 注册类型跟踪 ──────────────────────────────────────────

    [Fact]
    [DisplayName("注册内置工具后 RegisteredTypes 包含所有工具类型")]
    public void ToolRegistry_AddTools_TracksRegisteredTypes()
    {
        var registry = new ToolRegistry();
        registry.AddTools(new BuiltinToolService());
        registry.AddTools(new HolidayToolService());
        registry.AddTools(new CurrentUserTool());

        Assert.Contains(typeof(BuiltinToolService), registry.RegisteredTypes);
        Assert.Contains(typeof(HolidayToolService), registry.RegisteredTypes);
        Assert.Contains(typeof(CurrentUserTool), registry.RegisteredTypes);
    }

    [Fact]
    [DisplayName("注册同一类型两次不产生重复 RegisteredTypes 条目")]
    public void ToolRegistry_AddToolsTwice_NoDuplicateTypes()
    {
        var registry = new ToolRegistry();
        registry.AddTools(new BuiltinToolService());
        registry.AddTools(new BuiltinToolService());

        var count = registry.RegisteredTypes.Count(t => t == typeof(BuiltinToolService));
        Assert.Equal(1, count);
    }

    [Fact]
    [DisplayName("注册 NetworkToolService 后 RegisteredTypes 包含该类型")]
    public void ToolRegistry_AddNetworkTools_TracksRegisteredType()
    {
        var sp = new ServiceCollection().BuildServiceProvider();
        var registry = new ToolRegistry();
        registry.AddTools(new NetworkToolService(sp));

        Assert.Contains(typeof(NetworkToolService), registry.RegisteredTypes);
    }

    // ── 工具扫描（不依赖数据库）───────────────────────────────────────────

    [Fact]
    [DisplayName("扫描 BuiltinToolService 能找到 get_current_time 和 calculate 工具")]
    public void ScanType_BuiltinToolService_FindsAllTaggedMethods()
    {
        var methods = typeof(BuiltinToolService)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetCustomAttribute<ToolDescriptionAttribute>() != null)
            .ToList();

        Assert.True(methods.Count >= 2);
        var names = methods.Select(m =>
            ToolSchemaBuilder.BuildFromMethod(m).Function!.Name).ToList();
        Assert.Contains("get_current_time", names);
        Assert.Contains("calculate", names);
    }

    [Fact]
    [DisplayName("扫描 HolidayToolService 能找到 query_date_info 工具")]
    public void ScanType_HolidayToolService_FindsQueryDateInfo()
    {
        var methods = typeof(HolidayToolService)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetCustomAttribute<ToolDescriptionAttribute>() != null)
            .ToList();

        Assert.True(methods.Count >= 1);
        var names = methods.Select(m =>
            ToolSchemaBuilder.BuildFromMethod(m).Function!.Name).ToList();
        Assert.Contains("query_date_info", names);
    }

    [Fact]
    [DisplayName("扫描 CurrentUserTool 能找到 get_current_user 工具")]
    public void ScanType_CurrentUserTool_FindsGetCurrentUser()
    {
        var methods = typeof(CurrentUserTool)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetCustomAttribute<ToolDescriptionAttribute>() != null)
            .ToList();

        Assert.True(methods.Count >= 1);
        var names = methods.Select(m =>
            ToolSchemaBuilder.BuildFromMethod(m).Function!.Name).ToList();
        Assert.Contains("get_current_user", names);
    }

    [Fact]
    [DisplayName("扫描 NetworkToolService 能找到所有网络工具")]
    public void ScanType_NetworkToolService_FindsAllNetworkTools()
    {
        var methods = typeof(NetworkToolService)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetCustomAttribute<ToolDescriptionAttribute>() != null)
            .ToList();

        var names = methods.Select(m =>
            ToolSchemaBuilder.BuildFromMethod(m).Function!.Name).ToList();

        Assert.Contains("web_fetch", names);
        Assert.Contains("web_search", names);
        Assert.Contains("get_ip_location", names);
        Assert.Contains("get_weather", names);
        Assert.Contains("translate", names);
    }

    // ── 种子配置完整性 ─────────────────────────────────────────────────────

    [Fact]
    [DisplayName("种子字典中 DisplayName（Item3）均不为空")]
    public void Seeds_AllHaveNonEmptyDisplayName()
    {
        var seeds = GetSeeds();
        foreach (var kv in seeds)
            Assert.False(String.IsNullOrEmpty(kv.Value.Item3),
                $"种子 '{kv.Key}' 的 DisplayName 为空，应填写中文显示名");
    }

    [Fact]
    [DisplayName("ChatAI 种子包含所有已注册工具名称")]
    public void Seeds_ContainsAllChatAiToolNames()
    {
        var seeds = GetSeeds();

        // BuiltinToolService
        Assert.True(seeds.ContainsKey("get_current_time"), "缺少 get_current_time 种子");
        Assert.True(seeds.ContainsKey("calculate"), "缺少 calculate 种子");

        // HolidayToolService
        Assert.True(seeds.ContainsKey("query_date_info"), "缺少 query_date_info 种子");

        // CurrentUserTool
        Assert.True(seeds.ContainsKey("get_current_user"), "缺少 get_current_user 种子");

        // NetworkToolService
        Assert.True(seeds.ContainsKey("get_ip_location"), "缺少 get_ip_location 种子");
        Assert.True(seeds.ContainsKey("get_weather"), "缺少 get_weather 种子");
        Assert.True(seeds.ContainsKey("web_search"), "缺少 web_search 种子");
        Assert.True(seeds.ContainsKey("web_fetch"), "缺少 web_fetch 种子");
        Assert.True(seeds.ContainsKey("translate"), "缺少 translate 种子");
    }

    [Fact]
    [DisplayName("网络工具种子包含正确的 Providers（Item1）配置")]
    public void Seeds_NetworkTools_HaveProviders()
    {
        var seeds = GetSeeds();

        Assert.True(seeds.TryGetValue("get_ip_location", out var ipSeed));
        Assert.Contains("pconline", ipSeed.Item1);

        Assert.True(seeds.TryGetValue("get_weather", out var weatherSeed));
        Assert.Contains("nmc", weatherSeed.Item1);

        Assert.True(seeds.TryGetValue("web_search", out var searchSeed));
        Assert.Contains("bing", searchSeed.Item1);
    }

    // ── 锁定保护逻辑（纯逻辑，不依赖数据库）─────────────────────────────

    [Fact]
    [DisplayName("新记录 DisplayName 应从种子获取中文名（query_date_info → 日期节假日查询）")]
    public void NewRecord_QueryDateInfo_UsesSeedChineseName()
    {
        var method = typeof(HolidayToolService)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .First(m => m.GetCustomAttribute<ToolDescriptionAttribute>() != null);
        var chatTool = ToolSchemaBuilder.BuildFromMethod(method);
        var toolName = chatTool.Function!.Name;

        Assert.Equal("query_date_info", toolName);

        var seeds = GetSeeds();
        seeds.TryGetValue(toolName, out var seed);

        // Item3 = DisplayName：新记录应使用种子中文名，而非 snake_case 工具名
        var displayName = String.IsNullOrEmpty(seed.Item3) ? toolName : seed.Item3;
        Assert.Equal("日期节假日查询", displayName);
        Assert.NotEqual(toolName, displayName);
    }

    // ── IsSystem 属性验证 ─────────────────────────────────────────────────

    [Fact]
    [DisplayName("get_current_time 标注为系统工具（IsSystem=true）")]
    public void GetCurrentTime_IsSystemTool()
    {
        var method = typeof(BuiltinToolService).GetMethod(nameof(BuiltinToolService.GetCurrentTime))!;
        var attr = method.GetCustomAttribute<ToolDescriptionAttribute>()!;
        Assert.True(attr.IsSystem);
    }

    [Fact]
    [DisplayName("calculate 未标注为系统工具（IsSystem=false）")]
    public void Calculate_IsNotSystemTool()
    {
        var method = typeof(BuiltinToolService).GetMethod(nameof(BuiltinToolService.Calculate))!;
        var attr = method.GetCustomAttribute<ToolDescriptionAttribute>()!;
        Assert.False(attr.IsSystem);
    }

    [Fact]
    [DisplayName("query_date_info 标注为系统工具（IsSystem=true）")]
    public void QueryDateInfo_IsSystemTool()
    {
        var method = typeof(HolidayToolService).GetMethod(nameof(HolidayToolService.QueryDateInfo))!;
        var attr = method.GetCustomAttribute<ToolDescriptionAttribute>()!;
        Assert.True(attr.IsSystem);
    }

    [Fact]
    [DisplayName("get_current_user 标注为系统工具（IsSystem=true）")]
    public void GetCurrentUser_IsSystemTool()
    {
        var method = typeof(CurrentUserTool).GetMethod(nameof(CurrentUserTool.GetCurrentUser))!;
        var attr = method.GetCustomAttribute<ToolDescriptionAttribute>()!;
        Assert.True(attr.IsSystem);
    }
}
