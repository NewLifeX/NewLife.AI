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
    public void Export_ExcelSample()
    {
        Directory.CreateDirectory(_outDir);
        var path = Path.Combine(_outDir, "样张-Excel品牌报表.xlsx");

        var palette = OfficePalette.Resolve("enterprise-blue");
        using var fs = new FileStream(path, FileMode.Create, FileAccess.ReadWrite);
        using var writer = new ExcelWriter(fs);
        writer.DefaultFontName = palette.ChineseFont;
        writer.DefaultFontSize = 11;

        var headers = new[] { "日期", "渠道", "订单数", "营收（元）", "环比" };
        writer.WriteHeader("经营报表", headers, OfficeStyleKit.CreateExcelHeaderFormat(palette));
        writer.SetRowHeight("经营报表", 1, 22);

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
        }

        var widths = OfficeStyleKit.EstimateColumnWidths(headers, data);
        for (var c = 0; c < widths.Length; c++)
            writer.SetColumnWidth("经营报表", c, widths[c]);

        writer.FreezePane("经营报表", 1, 0);
        writer.Save();
        Assert.True(File.Exists(path));
    }

    [Fact]
    [DisplayName("样张—PPT品牌演示生成")]
    public void Export_PptSample()
    {
        Directory.CreateDirectory(_outDir);
        var path = Path.Combine(_outDir, "样张-PPT品牌演示.pptx");

        var palette = OfficePalette.Resolve("dark");
        using var writer = new PptxWriter();
        writer.SetAccentColors(palette.SeriesColors);
        writer.SetThemeFonts(palette.LatinFont, palette.ChineseFont);

        // 封面（深色背景 + 品牌色柱 + 反色标题）
        writer.AddSlide();
        writer.SetBackground(0, palette.Background);
        var title = writer.AddTextBox(0, "星语 · 文档设计系统", 4, 6, 24, 3, 36, true);
        title.FontColor = palette.Foreground;
        title.Alignment = "ctr";
        var subtitle = writer.AddTextBox(0, "Word / Excel / PPT 品牌化升级", 4, 9.5, 24, 2, 16);
        subtitle.FontColor = palette.MutedForeground;
        subtitle.Alignment = "ctr";
        writer.AddShape(0, "rect", 0, 0, 1.5, 19.05, palette.Primary);

        // 内容页 + 品牌色图表
        writer.AddSlide();
        var chart = writer.AddBarChart(1, ["Q1", "Q2", "Q3", "Q4"], 2, 4.5, 28, 12);
        chart.Series.Add(new NewLife.Office.Ppt.ChartSeries { Name = "营收", Values = [120, 190, 240, 305], Color = palette.SeriesColors[0] });
        chart.Series.Add(new NewLife.Office.Ppt.ChartSeries { Name = "利润", Values = [30, 62, 88, 121], Color = palette.SeriesColors[1] });

        // 表格页（品牌表头 + 斑马纹）
        writer.AddSlide();
        var tableTitle = writer.AddTextBox(2, "分渠道数据", 2, 1.6, 26, 1.6, 25, true);
        tableTitle.FontColor = "262626";
        var tableRows = new List<String[]>
        {
            new[] { "渠道", "新增用户", "转化率" },
            new[] { "官网直销", "3,240", "12.8%" },
            new[] { "生态合作", "5,180", "9.4%" },
            new[] { "内容投放", "2,760", "7.6%" },
        };
        var table = writer.AddTable(2, tableRows, 2, 4.2, 26);
        var ppHeader = new NewLife.Office.Ppt.CellStyle { BackgroundColor = palette.Primary, FontColor = palette.OnPrimary, Bold = true };
        for (var c = 0; c < 3; c++)
            table.CellStyles[(0, c)] = ppHeader;
        var ppStripe = new NewLife.Office.Ppt.CellStyle { BackgroundColor = palette.PaperMuted };
        for (var r = 2; r < tableRows.Count; r += 2)
            for (var c = 0; c < 3; c++)
                table.CellStyles[(r, c)] = ppStripe;

        writer.Save(path);
        Assert.True(File.Exists(path));
    }
}
