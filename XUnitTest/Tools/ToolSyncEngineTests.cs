using System;
using System.Collections.Generic;
using System.ComponentModel;
using NewLife.AI.Interfaces;
using NewLife.AI.Tools;
using NewLife.ChatAI.Entity;
using Xunit;

namespace XUnitTest.Tools;

/// <summary>ToolRegistry 工具同步辅助方法单元测试</summary>
[DisplayName("ToolRegistry 同步辅助方法测试")]
public class ToolSyncEngineTests
{
    [Fact]
    [DisplayName("NormalizeTriggers 去空白去重并统一分隔符")]
    public void NormalizeTriggers_ShouldTrimDistinctAndJoinByComma()
    {
        var value = ToolRegistry.NormalizeTriggers("天气, 天气，查询天气,查询天气 ,, ");

        Assert.Equal("天气,查询天气", value);
    }

    [Fact]
    [DisplayName("DescribeMethod 优先使用 DisplayNameAttribute 作为显示名")]
    public void DescribeMethod_ShouldUseDisplayNameAttribute()
    {
        var method = typeof(SampleToolService).GetMethod(nameof(SampleToolService.QueryWeather))!;
        INativeTool model = new SampleNativeTool();

        ToolRegistry.DescribeMethod(typeof(SampleToolService), method, model);

        Assert.Equal("天气查询", model.DisplayName);
        Assert.Equal("query_weather", model.Name);
        Assert.Equal("查询天气,看天气", model.Triggers);
        Assert.True(model.IsSystem);
        Assert.True(model.Enable);
    }

    [Fact]
    [DisplayName("SyncNativeTools 新增记录时写入显示名源文")]
    public void SyncNativeTools_NewRecord_WritesDisplayName()
    {
        var registry = new ToolRegistry();
        registry.AddTools<SampleToolService>();

        var saved = new List<SampleNativeTool>();
        var count = registry.SyncNativeTools<SampleNativeTool>(_ => null, saved.Add);

        Assert.Equal(1, count);
        var record = Assert.Single(saved);
        Assert.Equal("query_weather", record.Name);
        Assert.Equal("天气查询", record.DisplayName);
    }

    [Fact]
    [DisplayName("SyncNativeTools 重扫时用源文覆盖未锁定的旧显示名")]
    public void SyncNativeTools_ExistingRecord_UpdatesDisplayName()
    {
        var registry = new ToolRegistry();
        registry.AddTools<SampleToolService>();

        // 模拟历史数据：显示名停留在旧的中文源文
        var existing = new SampleNativeTool { Name = "query_weather", DisplayName = "天气查询（旧）" };
        var saved = new List<SampleNativeTool>();

        registry.SyncNativeTools<SampleNativeTool>(_ => existing, saved.Add);

        Assert.Equal("天气查询", existing.DisplayName);
        Assert.Single(saved);
    }

    [Fact]
    [DisplayName("SyncNativeTools 不改动已锁定记录的显示名")]
    public void SyncNativeTools_LockedRecord_KeepsDisplayName()
    {
        var registry = new ToolRegistry();
        registry.AddTools<SampleToolService>();

        var existing = new SampleNativeTool { Name = "query_weather", DisplayName = "手工改名", IsLocked = true };
        var saved = new List<SampleNativeTool>();

        registry.SyncNativeTools<SampleNativeTool>(_ => existing, saved.Add);

        Assert.Equal("手工改名", existing.DisplayName);
        Assert.Single(saved);
    }

    private class SampleNativeTool : INativeTool
    {
        public Int32 Id { get; set; }
        public String? Name { get; set; }
        public String? DisplayName { get; set; }
        public String? ClassName { get; set; }
        public String? MethodName { get; set; }
        public String? Description { get; set; }
        public String? Parameters { get; set; }
        public String? Triggers { get; set; }
        public String? AssistantTriggers { get; set; }
        public Boolean Enable { get; set; }
        public Boolean IsSystem { get; set; }
        public Boolean IsLocked { get; set; }
        // ToolResponseRouting 已移除，路由由 IToolResult.Contents.Audience 替代
        public String? Providers { get; set; }
        public String? Endpoint { get; set; }
        public String? ApiKey { get; set; }
        public Int32 Sort { get; set; }
        public String? Remark { get; set; }
    }

    private class SampleToolService
    {
        [DisplayName("天气查询")]
        [Description("查询天气。用于根据城市查看天气")]
        [ToolDescription("query_weather", IsSystem = true, Triggers = "查询天气， 看天气,查询天气")]
        public String QueryWeather([Description("城市名称")] String city) => city;
    }
}
