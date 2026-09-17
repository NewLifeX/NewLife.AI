using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using NewLife.ChatAI.Helpers;
using NewLife.ChatAI.Models;
using NewLife.Office.Word;
using Xunit;
using ExcelWriter = NewLife.Office.Excel.ExcelWriter;
using PptxWriter = NewLife.Office.Ppt.PptxWriter;
using ChartSeries = NewLife.Office.Ppt.ChartSeries;
using LayoutEngine = NewLife.Office.Ppt.LayoutEngine;
using PptCellStyle = NewLife.Office.Ppt.CellStyle;

namespace XUnitTest.Helpers;

/// <summary>办公文档样张导出。生成 Word/Excel/PPT 样例文件供人工验收品牌视觉（输出到测试目录下"样本文档"）</summary>
[DisplayName("办公文档样张导出")]
public class OfficeSampleExportTests
{
    private static readonly String _outDir = Path.Combine(AppContext.BaseDirectory, "样本文档");

    [Fact]
    [DisplayName("样张—Word品牌文档生成")]
    public void Export_WordSample()
    {
        Directory.CreateDirectory(_outDir);
        var path = Path.Combine(_outDir, "样张-Word品牌文档.docx");

        var palette = OfficePalette.Resolve("blue");
        using var writer = new WordWriter();
        writer.StylesXml = OfficeStyleKit.BuildWordStylesXml(palette);
        writer.DocumentProperties = new DocumentProperties { Title = "星语季度报告" };

        var page = writer.PageSettings;
        page.MarginTop = page.MarginBottom = 1417;
        page.MarginLeft = page.MarginRight = 1417;
        page.FooterText = "星语季度报告";

        writer.AppendHeading("2026 年第三季度运营报告", 1);
        writer.AppendParagraph("本季度平台整体运行平稳，用户规模与内容产出保持双增长，AI 文档生成能力完成品牌化升级。");

        writer.AppendHeading("核心指标", 2);
        writer.AppendParagraph("▲ 128,400", ParagraphStyle.Normal, new RunProperties { Bold = true, FontSize = 28, ForeColor = palette.Primary });
        writer.AppendParagraph("月活跃用户（环比 +18%）", ParagraphStyle.Normal, new RunProperties { FontSize = 11, ForeColor = palette.PaperMutedForeground });

        writer.AppendHeading("分渠道数据", 2);
        var rows = new List<IEnumerable<String>>
        {
            new String[] { "渠道", "新增用户", "转化率" },
            new String[] { "官网直销", "3,240", "12.8%" },
            new String[] { "生态合作", "5,180", "9.4%" },
            new String[] { "内容投放", "2,760", "7.6%" },
        };
        writer.AppendTable(rows, firstRowHeader: true, OfficeStyleKit.CreateWordTableStyle(palette));

        writer.AppendHeading("重点提示", 2);
        var (bg, border) = OfficeStyleKit.GetCalloutColors("success");
        var callout = new Paragraph
        {
            BackgroundColor = bg,
            Borders = new ParagraphBorders { Left = new Border { Style = BorderStyle.Single, Color = border, Width = 18 } },
        };
        callout.Runs.Add(new Run { Text = "文档生成链路已完成品牌化改造：字体、配色、间距与表格样式统一走卡片风格主题。" });
        writer.AppendParagraph(callout);

        writer.AppendHeading("引用", 2);
        var quote = new Paragraph
        {
            Borders = new ParagraphBorders { Left = new Border { Style = BorderStyle.Single, Color = palette.PaperBorder, Width = 12 } },
        };
        quote.Runs.Add(new Run { Text = "好的排版让信息传达效率大幅提升。", Properties = new RunProperties { Italic = true, ForeColor = palette.PaperMutedForeground } });
        writer.AppendParagraph(quote);

        writer.Save(path);
        Assert.True(File.Exists(path));
    }

    [Fact]
    [DisplayName("样张—Excel品牌报表生成")]
    public void Export_ExcelSample() => ExportExcelCore("enterprise-blue", "样张-Excel品牌报表.xlsx");

    /// <summary>导出 Excel 样张（标题带 + 品牌表头 + 真数值数据 + 主色淡染斑马纹）</summary>
    private static void ExportExcelCore(String themeKey, String fileName)
    {
        Directory.CreateDirectory(_outDir);
        var path = Path.Combine(_outDir, fileName);

        var palette = OfficePalette.Resolve(themeKey);
        using var fs = new FileStream(path, FileMode.Create, FileAccess.ReadWrite);
        using var writer = new ExcelWriter(fs);
        writer.DefaultFontName = palette.ChineseFont;
        writer.DefaultFontSize = 11;

        var headers = new[] { "日期", "渠道", "订单数", "营收（元）", "环比" };

        // 标题带：跨列合并的品牌深色标题行（与 BuildExcelToolService 输出一致）
        writer.WriteRow("经营报表", ["星语季度经营报表"], OfficeStyleKit.CreateExcelTitleFormat(palette));
        writer.MergeCell("经营报表", 0, 0, 0, headers.Length - 1);
        writer.SetRowHeight("经营报表", 1, 38);

        writer.WriteHeader("经营报表", headers, OfficeStyleKit.CreateExcelHeaderFormat(palette));
        writer.SetRowHeight("经营报表", 2, 24);

        var data = new[]
        {
            new[] { "2026-07", "官网直销", "1,208", "182,400", "+12.4%" },
            new[] { "2026-08", "官网直销", "1,355", "203,900", "+11.8%" },
            new[] { "2026-07", "生态合作", "866", "98,650", "+6.2%" },
            new[] { "2026-08", "生态合作", "978", "112,300", "+13.8%" },
        };
        for (var i = 0; i < data.Length; i++)
        {
            var style = OfficeStyleKit.CreateExcelDataFormat(palette, i % 2 == 1);
            writer.WriteRow("经营报表", data[i].Cast<Object>().ToArray(), style);
            writer.SetRowHeight("经营报表", 3 + i, 22);
        }

        var widths = OfficeStyleKit.EstimateColumnWidths(headers, data);
        for (var c = 0; c < widths.Length; c++)
            writer.SetColumnWidth("经营报表", c, widths[c]);

        writer.FreezePane("经营报表", 2, 0);
        writer.Save();
        Assert.True(File.Exists(path));
    }

    [Fact]
    [DisplayName("样张—PPT品牌演示生成")]
    public void Export_PptSample() => ExportPptCore("enterprise-blue", "样张-PPT品牌演示.pptx");

    [Fact]
    [DisplayName("样张—主题变体（暖色）验证卡片风格驱动三件套配色")]
    public void Export_ThemeVariants_Warm()
    {
        ExportExcelCore("warm", "样张-Excel品牌报表-暖色.xlsx");
        ExportPptCore("warm", "样张-PPT品牌演示-暖色.pptx");
    }

    /// <summary>导出 PPT 样张（品牌深色封面 + LayoutEngine 自动排版，与 BuildPptToolService 输出一致）</summary>
    private static void ExportPptCore(String themeKey, String fileName)
    {
        Directory.CreateDirectory(_outDir);
        var path = Path.Combine(_outDir, fileName);

        var palette = OfficePalette.Resolve(themeKey);
        using var writer = new PptxWriter();
        writer.SetAccentColors(palette.SeriesColors);
        writer.SetThemeFonts(palette.LatinFont, palette.ChineseFont);

        // 封面：品牌深色底 + 亮色装饰柱（标题 40pt 白字居中，副标题柔和主色）；由 LayoutEngine 计算坐标
        var cover = writer.AddSlide();
        cover.Layout = "title_only";
        writer.SetBackground(0, OfficeStyleKit.Shade(palette.Primary, 0.5));
        var coverTitle = writer.AddTextBox(0, "星语 · 文档设计系统", 0, 0, 1, 1, 40, true);
        coverTitle.Role = "title";
        coverTitle.FontColor = "FFFFFF";
        coverTitle.Alignment = "ctr";
        var coverSubtitle = writer.AddTextBox(0, "Word / Excel / PPT 品牌化输出", 0, 0, 1, 1, 20);
        coverSubtitle.Role = "subtitle";
        coverSubtitle.FontColor = OfficeStyleKit.Tint(palette.Primary, 0.45);
        coverSubtitle.Alignment = "ctr";
        var coverBar = writer.AddShape(0, "rect", 0.0, 0.0, 1.5, 19.05, palette.Accent);
        if (coverBar != null) coverBar.LineColor = null;
        LayoutEngine.Apply(cover);

        // 指标页：标题 + KPI 行（KPI 主色、页码页脚）
        var kpiSlide = writer.AddSlide();
        kpiSlide.Layout = "title_content";
        writer.AddShape(1, "rect", 2.0, 3.65, 29.87, 0.09, palette.Primary);   // 标题分隔线（与服务装饰一致）
        var kpiTitle = writer.AddTextBox(1, "本季度核心指标", 0, 0, 1, 1, 32, true);
        kpiTitle.Role = "title";
        kpiTitle.FontColor = palette.PaperForeground;
        var kpi1 = writer.AddTextBox(1, "128,400", 0, 0, 1, 1, 44, true); kpi1.Role = "kpi"; kpi1.FontColor = palette.Primary;
        var kpi2 = writer.AddTextBox(1, "98.6%", 0, 0, 1, 1, 44, true); kpi2.Role = "kpi"; kpi2.FontColor = palette.Primary;
        var kpi3 = writer.AddTextBox(1, "12.8%", 0, 0, 1, 1, 44, true); kpi3.Role = "kpi"; kpi3.FontColor = palette.Primary;
        var kpiBody = writer.AddTextBox(1, "月活跃用户、服务可用性与付费转化率同比保持双位数增长。", 0, 0, 1, 1, 16);
        kpiBody.Role = "body";
        kpiBody.FontColor = palette.PaperMutedForeground;
        writer.SetSlideFooter(1, "星语 · 文档设计系统", true);
        LayoutEngine.Apply(kpiSlide);

        // 图表页：标题 + 品牌色系柱状图
        var chartSlide = writer.AddSlide();
        chartSlide.Layout = "title_content";
        writer.AddShape(2, "rect", 2.0, 3.65, 29.87, 0.09, palette.Primary);
        var chartTitle = writer.AddTextBox(2, "季度营收与利润", 0, 0, 1, 1, 32, true);
        chartTitle.Role = "title";
        chartTitle.FontColor = palette.PaperForeground;
        var chart = writer.AddBarChart(2, ["Q1", "Q2", "Q3", "Q4"], 0, 0, 1, 1);
        if (chart?.Series != null)
        {
            chart.Series.Add(new ChartSeries { Name = "营收", Values = [120, 190, 240, 305], Color = palette.SeriesColors[0] });
            chart.Series.Add(new ChartSeries { Name = "利润", Values = [30, 62, 88, 121], Color = palette.SeriesColors[1] });
        }
        writer.SetSlideFooter(2, "星语 · 文档设计系统", true);
        LayoutEngine.Apply(chartSlide);

        // 表格页：品牌表头 + 主色淡染斑马纹
        var tableSlide = writer.AddSlide();
        tableSlide.Layout = "title_content";
        writer.AddShape(3, "rect", 2.0, 3.65, 29.87, 0.09, palette.Primary);
        var tableTitle = writer.AddTextBox(3, "分渠道数据", 0, 0, 1, 1, 32, true);
        tableTitle.Role = "title";
        tableTitle.FontColor = palette.PaperForeground;
        var tableRows = new List<String[]>
        {
            new[] { "渠道", "新增用户", "转化率" },
            new[] { "官网直销", "3,240", "12.8%" },
            new[] { "生态合作", "5,180", "9.4%" },
            new[] { "内容投放", "2,760", "7.6%" },
        };
        var table = writer.AddTable(3, tableRows, 0, 0, 1, firstRowHeader: true);
        if (table != null)
        {
            // 全表统一字号 14pt，表头 15pt 加粗（与服务输出一致）
            for (var r = 0; r < tableRows.Count; r++)
                for (var c = 0; c < 3; c++)
                    table.CellStyles[(r, c)] = new PptCellStyle { FontSize = 14 };
            var ppHeader = new PptCellStyle { BackgroundColor = palette.Primary, FontColor = palette.OnPrimary, Bold = true, FontSize = 15 };
            for (var c = 0; c < 3; c++)
                table.CellStyles[(0, c)] = ppHeader;
            var ppStripe = new PptCellStyle { BackgroundColor = OfficeStyleKit.Tint(palette.Primary, 0.08), FontSize = 14 };
            for (var r = 2; r < tableRows.Count; r += 2)
                for (var c = 0; c < 3; c++)
                    table.CellStyles[(r, c)] = ppStripe;
        }
        writer.SetSlideFooter(3, "星语 · 文档设计系统", true);
        LayoutEngine.Apply(tableSlide);

        // 要点页：标题 + 项目符号列表（多行文本段落化渲染，130% 行距）
        var bulletSlide = writer.AddSlide();
        bulletSlide.Layout = "title_content";
        writer.AddShape(4, "rect", 2.0, 3.65, 29.87, 0.09, palette.Primary);
        var bulletTitle = writer.AddTextBox(4, "交付节奏", 0, 0, 1, 1, 32, true);
        bulletTitle.Role = "title";
        bulletTitle.FontColor = palette.PaperForeground;
        var bulletBody = writer.AddTextBox(4, "第 1 周：需求冻结与接口评审\n第 2-3 周：开发联调与自动化测试\n第 4 周：性能压测与验收上线", 0, 0, 1, 1, 16);
        bulletBody.Role = "body";
        bulletBody.FontColor = palette.PaperForeground;
        OfficeStyleKit.FillParagraphs(bulletBody, bulletBody.Text, 16, false, palette.PaperForeground, "•");
        writer.SetSlideFooter(4, "星语 · 文档设计系统", true);
        LayoutEngine.Apply(bulletSlide);

        writer.Save(path);
        Assert.True(File.Exists(path));
    }
}
