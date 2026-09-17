using System;
using System.ComponentModel;
using System.Xml;
using NewLife.ChatAI.Helpers;
using NewLife.ChatAI.Models;
using NewLife.Office.Excel;
using Xunit;

namespace XUnitTest.Helpers;

/// <summary>办公文档样式工具测试。覆盖 Word 样式表生成、Excel 样式与列宽估算、颜色工具</summary>
[DisplayName("办公文档样式工具测试")]
public class OfficeStyleKitTests
{
    [Fact]
    [DisplayName("BuildWordStylesXml—输出合法XML且包含品牌样式")]
    public void BuildWordStylesXml_ContainsBrandStyles()
    {
        var palette = OfficePalette.Resolve("blue");
        var xml = OfficeStyleKit.BuildWordStylesXml(palette);

        // XML 合法性（含中文、实体字符处理）
        var doc = new XmlDocument();
        doc.LoadXml(xml);

        // 默认字体与行距/段后
        Assert.Contains("微软雅黑", xml);
        Assert.Contains("<w:spacing w:line=\"312\" w:lineRule=\"auto\" w:after=\"120\"/>", xml);
        // 标题品牌主色、表格纸面边框
        Assert.Contains($"<w:color w:val=\"{palette.Primary}\"/>", xml);
        Assert.Contains($"w:color=\"{palette.PaperBorder}\"", xml);
        // 全部被引用 styleId 齐备（缺失会导致段落样式引用断裂）
        Assert.Contains("w:styleId=\"Normal\"", xml);
        Assert.Contains("w:styleId=\"Heading1\"", xml);
        Assert.Contains("w:styleId=\"Heading6\"", xml);
        Assert.Contains("w:styleId=\"TableGrid\"", xml);
    }

    [Fact]
    [DisplayName("BuildWordStylesXml—空参数抛出ArgumentNullException")]
    public void BuildWordStylesXml_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => OfficeStyleKit.BuildWordStylesXml(null!));
    }

    [Fact]
    [DisplayName("CreateExcelHeaderFormat—品牌底配反色字并居中")]
    public void CreateExcelHeaderFormat_UsesBrandColors()
    {
        var palette = OfficePalette.Resolve("blue");
        var fmt = OfficeStyleKit.CreateExcelHeaderFormat(palette);

        Assert.Equal(palette.Primary, fmt.BackgroundColor);
        Assert.Equal(palette.OnPrimary, fmt.FontColor);
        Assert.True(fmt.Bold);
        Assert.Equal(HorizontalAlignment.Center, fmt.HAlign);
        Assert.Equal(VerticalAlignment.Center, fmt.VAlign);
    }

    [Fact]
    [DisplayName("CreateExcelDataFormat—斑马纹叠加浅底且数字格式透传")]
    public void CreateExcelDataFormat_StripeAndFormat()
    {
        var palette = OfficePalette.Resolve("blue");

        var normal = OfficeStyleKit.CreateExcelDataFormat(palette, false, "#,##0.00");
        Assert.Null(normal.BackgroundColor);
        Assert.Equal("#,##0.00", normal.NumberFormat);

        var stripe = OfficeStyleKit.CreateExcelDataFormat(palette, true);
        Assert.Equal(palette.PaperMuted, stripe.BackgroundColor);
    }

    [Fact]
    [DisplayName("EstimateColumnWidths—中文按2字符宽并限制在8~50")]
    public void EstimateColumnWidths_ChineseWide_Clamped()
    {
        var headers = new[] { "序号", "产品名称", "备注" };
        var rows = new[]
        {
            new[] { "1", "无线键盘", "这是一段非常长的中文备注文本用来验证列宽上限被正确截断到五十字符宽度不要再长了真的不要再长了" },
        };
        var widths = OfficeStyleKit.EstimateColumnWidths(headers, rows);

        Assert.Equal(3, widths.Length);
        Assert.Equal(8, widths[0]);       // 过短列 → 下限 8
        Assert.Equal(11, widths[1]);      // "产品名称"宽 8 + 内边距 3
        Assert.Equal(50, widths[2]);      // 超长文本 → 上限 50
    }

    [Fact]
    [DisplayName("EstimateColumnWidths—空数据行时按表头估算")]
    public void EstimateColumnWidths_NullRows_UsesHeaders()
    {
        var widths = OfficeStyleKit.EstimateColumnWidths(["A"], null);

        Assert.Equal(8, widths[0]);
    }

    [Fact]
    [DisplayName("TryNormalizeColor—规范化与无效判定")]
    public void TryNormalizeColor_NormalizesAndRejects()
    {
        Assert.Equal("AABBCC", OfficeStyleKit.TryNormalizeColor("#aabbcc"));
        Assert.Equal("AABBCC", OfficeStyleKit.TryNormalizeColor(" AaBbCc "));
        Assert.Null(OfficeStyleKit.TryNormalizeColor("xyzxyz"));
        Assert.Null(OfficeStyleKit.TryNormalizeColor("12345"));
        Assert.Null(OfficeStyleKit.TryNormalizeColor(null));
        Assert.Equal("FFFFFF", OfficeStyleKit.NormalizeColor(null, "FFFFFF"));
    }

    [Fact]
    [DisplayName("Luma与IsDarkColor—亮度计算与深色判定")]
    public void Luma_And_IsDarkColor()
    {
        Assert.Equal(0, OfficeStyleKit.Luma("000000"));
        Assert.Equal(255, OfficeStyleKit.Luma("FFFFFF"));
        Assert.True(OfficeStyleKit.IsDarkColor("0F172A"));
        Assert.False(OfficeStyleKit.IsDarkColor("FFFFFF"));
        Assert.Equal(128, OfficeStyleKit.Luma("bad"));   // 无效颜色 → 中值
    }

    [Fact]
    [DisplayName("GetCalloutColors—四变体语义色")]
    public void GetCalloutColors_Variants()
    {
        Assert.Equal(("F0FDF4", "22C55E"), OfficeStyleKit.GetCalloutColors("success"));
        Assert.Equal(("FFFBEB", "F59E0B"), OfficeStyleKit.GetCalloutColors("warning"));
        Assert.Equal(("FEF2F2", "EF4444"), OfficeStyleKit.GetCalloutColors("danger"));
        Assert.Equal(("EFF6FF", "3B82F6"), OfficeStyleKit.GetCalloutColors("info"));
        Assert.Equal(("EFF6FF", "3B82F6"), OfficeStyleKit.GetCalloutColors(null));
    }

    [Fact]
    [DisplayName("ResolveTheme—显式传参优先_NAI独立构建无会话回退")]
    public void ResolveTheme_ExplicitWins()
    {
        Assert.Equal("enterprise-blue", OfficeStyleKit.ResolveTheme("enterprise-blue", "12345"));
        Assert.Null(OfficeStyleKit.ResolveTheme(null, "12345"));
        Assert.Null(OfficeStyleKit.ResolveTheme(null, null));
    }
}
