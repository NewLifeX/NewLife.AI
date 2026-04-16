using System;
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
    private static ToolDescriptionAttribute GetToolAttribute(Type type, String methodName) =>
        type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance)!
            .GetCustomAttribute<ToolDescriptionAttribute>()!;

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

    // ── 特性元数据完整性 ───────────────────────────────────────────────────

    [Fact]
    [DisplayName("核心工具方法均配置 DisplayName，供同步时直接使用")]
    public void ToolMethods_AllHaveDisplayNameAttribute()
    {
        var methods = typeof(BuiltinToolService)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetCustomAttribute<ToolDescriptionAttribute>() != null)
            .Concat(typeof(NetworkToolService)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.GetCustomAttribute<ToolDescriptionAttribute>() != null))
            .Concat(typeof(HolidayToolService)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.GetCustomAttribute<ToolDescriptionAttribute>() != null))
            .Concat(typeof(CurrentUserTool)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.GetCustomAttribute<ToolDescriptionAttribute>() != null));

        foreach (var method in methods)
            Assert.False(String.IsNullOrEmpty(method.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName),
                $"方法 {method.DeclaringType?.Name}.{method.Name} 缺少 DisplayName 标注");
    }

    [Fact]
    [DisplayName("网络工具在特性中声明严格触发词")]
    public void NetworkTools_TriggersConfiguredOnAttribute()
    {
        Assert.Contains("今天天气怎么样", GetToolAttribute(typeof(NetworkToolService), nameof(NetworkToolService.GetWeatherAsync)).Triggers);
        Assert.Contains("帮我搜索", GetToolAttribute(typeof(NetworkToolService), nameof(NetworkToolService.WebSearchAsync)).Triggers);
        Assert.Contains("读取网页", GetToolAttribute(typeof(NetworkToolService), nameof(NetworkToolService.WebFetchAsync)).Triggers);
    }

    [Fact]
    [DisplayName("严格天气触发词不再包含宽泛词下雨")]
    public void GetWeather_TriggersAreSpecific()
    {
        var triggers = GetToolAttribute(typeof(NetworkToolService), nameof(NetworkToolService.GetWeatherAsync)).Triggers ?? "";
        Assert.DoesNotContain("下雨", triggers);
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
        Assert.False(attr.Enable);
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
