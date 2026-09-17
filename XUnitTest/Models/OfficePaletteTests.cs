using System.ComponentModel;
using NewLife.ChatAI.Models;
using Xunit;

namespace XUnitTest.Models;

/// <summary>办公文档调色板测试。覆盖内置主题回退、纸面适配与深色判定</summary>
[DisplayName("办公文档调色板测试")]
public class OfficePaletteTests
{
    [Fact]
    [DisplayName("Resolve—内置blue主题_回退强调色板")]
    public void Resolve_BuiltinBlue_UsesAccentPalette()
    {
        var palette = OfficePalette.Resolve("blue");

        Assert.Equal("2563EB", palette.Primary);
        Assert.Equal("1D4ED8", palette.Secondary);
        Assert.Equal("60A5FA", palette.Accent);
        Assert.False(palette.IsDark);
        Assert.NotEmpty(palette.SeriesColors);
    }

    [Fact]
    [DisplayName("Resolve—null主题_使用默认字体与配色")]
    public void Resolve_NullTheme_UsesDefaults()
    {
        var palette = OfficePalette.Resolve(null);

        Assert.Equal("2563EB", palette.Primary);
        Assert.Equal("Segoe UI", palette.LatinFont);
        Assert.Equal("微软雅黑", palette.ChineseFont);
        Assert.Equal("Consolas", palette.MonoFont);
    }

    [Fact]
    [DisplayName("Resolve—dark主题_识别深色背景")]
    public void Resolve_DarkTheme_IsDark()
    {
        var palette = OfficePalette.Resolve("dark");

        Assert.True(palette.IsDark);
        Assert.Equal("0F172A", palette.Background);
    }

    [Fact]
    [DisplayName("Paper适配—深色主题浅色文字映射深灰_浅色主题保留原值")]
    public void PaperForeground_DarkTheme_MapsToCarbon()
    {
        var light = OfficePalette.Resolve("blue");
        Assert.Equal(light.Foreground, light.PaperForeground);

        var dark = OfficePalette.Resolve("dark");
        dark.Foreground = "EDEDEF";   // 深色主题的浅色正文
        Assert.Equal("262626", dark.PaperForeground);
    }

    [Fact]
    [DisplayName("Paper适配—深色斑马纹映射浅灰")]
    public void PaperMuted_DarkTheme_MapsToLight()
    {
        var palette = OfficePalette.Resolve("blue");

        palette.Muted = "2A2A2C";     // 深色块
        Assert.Equal("F7F7F8", palette.PaperMuted);

        palette.Muted = "DBEAFE";     // 浅色块保留
        Assert.Equal("DBEAFE", palette.PaperMuted);
    }

    [Fact]
    [DisplayName("Paper适配—过深边框映射中性浅灰")]
    public void PaperBorder_TooDark_MapsToNeutral()
    {
        var palette = OfficePalette.Resolve("blue");

        palette.Border = "3A3A3C";
        Assert.Equal("E0E0E0", palette.PaperBorder);

        palette.Border = "CBD5E1";
        Assert.Equal("CBD5E1", palette.PaperBorder);
    }

    [Fact]
    [DisplayName("SeriesColors—缺省由主题色派生")]
    public void SeriesColors_Default_DerivesFromTheme()
    {
        var palette = OfficePalette.Resolve("blue");

        Assert.True(palette.SeriesColors.Length >= 6);
        Assert.Equal(palette.Primary, palette.SeriesColors[0]);
        Assert.Equal(palette.Accent, palette.SeriesColors[1]);
    }
}
