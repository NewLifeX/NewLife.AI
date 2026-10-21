#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using NewLife.ChatAI.Entity;
using L10n = NewLife.Localization;
using Xunit;

namespace XUnitTest.Tools;

/// <summary>内置工具名称匹配测试。验证 @提及 同时接受工具名、显示名源文与当前语言译文</summary>
/// <remarks>Localization 是进程级全局状态，归入串行集合，避免与其他语言测试相互干扰</remarks>
[Collection("Localization")]
public class NativeToolNameMatchTests
{
    private const String Display = "Get data context";
    private const String DisplayZh = "获取数据上下文";
    private const String TestCulture = "xx-Tool";

    /// <summary>构造内置工具实体（不访问数据库）</summary>
    private static NativeTool CreateTool() => new()
    {
        Name = "get_data_context",
        DisplayName = Display,
        Description = "Gets the data context of the current page",
    };

    /// <summary>切换到测试语言并注册词表，Dispose 时还原</summary>
    private sealed class LanguageScope : IDisposable
    {
        private readonly CultureInfo? _old = L10n.Culture;
        private readonly CultureInfo _culture = new(TestCulture);

        public LanguageScope()
        {
            L10n.Register(_culture, new Dictionary<String, String> { [Display] = DisplayZh });
            L10n.Culture = _culture;
        }

        public void Dispose()
        {
            L10n.Culture = _old;
            L10n.Unregister(_culture);
        }
    }

    [Fact]
    [DisplayName("按工具名匹配")]
    public void Match_ByName()
    {
        using var _ = new LanguageScope();

        Assert.True(CreateTool().MatchName("get_data_context"));
    }

    [Fact]
    [DisplayName("按显示名源文匹配")]
    public void Match_BySourceDisplayName()
    {
        using var _ = new LanguageScope();

        Assert.True(CreateTool().MatchName(Display));
    }

    [Fact]
    [DisplayName("按显示名译文匹配（界面 @提及 使用译文）")]
    public void Match_ByLocalizedDisplayName()
    {
        using var _ = new LanguageScope();

        Assert.True(CreateTool().MatchName(DisplayZh));
    }

    [Fact]
    [DisplayName("无关名称不匹配")]
    public void NoMatch_ForUnknownName()
    {
        using var _ = new LanguageScope();

        Assert.False(CreateTool().MatchName("unknown_tool"));
        Assert.False(CreateTool().MatchName(""));
    }

    [Fact]
    [DisplayName("未注册语言时回退源文匹配")]
    public void Match_SourceOnly_WhenCultureUnsupported()
    {
        var old = L10n.Culture;
        try
        {
            L10n.Culture = new CultureInfo("sw-KE");

            var tool = CreateTool();
            Assert.True(tool.MatchName(Display));
            Assert.False(tool.MatchName(DisplayZh));
        }
        finally
        {
            L10n.Culture = old;
        }
    }
}
