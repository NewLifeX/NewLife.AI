using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using NewLife;
using NewLife.AI.Coding;
using NewLife.AI.Tools;
using NewLife.ChatAI.Entity;
using Xunit;
using L10n = NewLife.Localization;

namespace XUnitTest.Tools;

/// <summary>内置工具显示名测试。显示名以英文为源文，展示期按当前语言翻译</summary>
[Collection("Localization")]
public class ToolDisplayNameTests
{
    private static readonly Type[] _toolTypes =
    [
        typeof(BuiltinToolService),
        typeof(NetworkToolService),
        typeof(CodingTools),
        typeof(NewLife.ChatAI.Tools.BuildDocToolService),
        typeof(NewLife.ChatAI.Tools.BuildExcelToolService),
        typeof(NewLife.ChatAI.Tools.BuildPptToolService),
        typeof(NewLife.ChatAI.Tools.ChartToolService),
        typeof(NewLife.ChatAI.Tools.CurrentUserTool),
        typeof(NewLife.ChatAI.Tools.DbQueryToolService),
        typeof(NewLife.ChatAI.Tools.HolidayToolService),
        typeof(NewLife.ChatAI.Tools.KanbanToolService),
        typeof(NewLife.ChatAI.Tools.MapAnnotationToolService),
        typeof(NewLife.ChatAI.Tools.MindmapToolService),
        typeof(NewLife.ChatAI.Tools.TimelineToolService),
        typeof(NewLife.ChatAI.Tools.WidgetToolService),
    ];

    /// <summary>收集所有内置工具方法的显示名</summary>
    public static IEnumerable<(String Tool, String DisplayName)> GetDisplayNames()
    {
        foreach (var type in _toolTypes)
        {
            foreach (var method in ToolRegistry.GetToolMethods(type))
            {
                var attr = method.GetCustomAttribute<DisplayNameAttribute>();
                if (attr == null) continue;

                var model = new SampleNativeTool();
                ToolRegistry.DescribeMethod(type, method, model);
                yield return (model.Name ?? method.Name, model.DisplayName ?? "");
            }
        }
    }

    /// <summary>判断文本是否含中日韩汉字</summary>
    private static Boolean HasCjk(String? text)
    {
        if (text == null) return false;
        foreach (var c in text)
        {
            if ((c >= 0x2E80 && c <= 0x9FFF) || (c >= 0xF900 && c <= 0xFAFF)) return true;
        }
        return false;
    }

    [Fact]
    [DisplayName("内置工具显示名统一为英文源文，不含汉字")]
    public void DisplayNames_AreEnglishSource()
    {
        var list = new List<String>();
        foreach (var (tool, displayName) in GetDisplayNames())
        {
            if (displayName.IsNullOrEmpty()) list.Add($"{tool}：缺少显示名");
            else if (HasCjk(displayName)) list.Add($"{tool}：显示名仍为中文 {displayName}");
        }

        Assert.True(list.Count == 0, String.Join(Environment.NewLine, list));
    }

    [Fact]
    [DisplayName("内置工具显示名可按当前语言翻译")]
    public void DisplayNames_Translated()
    {
        Assert.Equal("当前时间", WithCulture("zh-CN", () => L10n.Translate("Current Time")));
        Assert.Equal("現在時刻", WithCulture("ja", () => L10n.Translate("Current Time")));
        Assert.Equal("현재 시간", WithCulture("ko", () => L10n.Translate("Current Time")));
        Assert.Equal("Текущее время", WithCulture("ru", () => L10n.Translate("Current Time")));
    }

    [Fact]
    [DisplayName("未注册语言时显示名回退英文源文")]
    public void DisplayNames_FallBackToSource()
        => Assert.Equal("Current Time", WithCulture("sw-KE", () => L10n.Translate("Current Time")));

    private static T WithCulture<T>(String name, Func<T> func)
    {
        var old = L10n.Culture;
        try
        {
            L10n.Culture = new CultureInfo(name);
            return func();
        }
        finally
        {
            L10n.Culture = old;
        }
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
        public String? Providers { get; set; }
        public String? Endpoint { get; set; }
        public String? ApiKey { get; set; }
        public Int32 Sort { get; set; }
        public String? Remark { get; set; }
    }
}
