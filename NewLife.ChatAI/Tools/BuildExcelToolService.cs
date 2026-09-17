using System.ComponentModel;
using System.Text.Json;
using NewLife;
using NewLife.AI.Tools;
using NewLife.ChatAI.Helpers;
using NewLife.ChatAI.Models;
using NewLife.Collections;
using NewLife.Log;
using NewLife.Office.Excel;
using NewLife.Serialization;
using Attachment = NewLife.Cube.Entity.Attachment;

namespace NewLife.ChatAI.Tools;

/// <summary>Excel 生成工具服务。接收结构化表格 JSON，用 ExcelWriter 生成 .xlsx 并归档到附件表</summary>
/// <param name="log">日志</param>
public class BuildExcelToolService(ILog log)
{
    #region 工具方法

    /// <summary>生成 Excel 电子表格（.xlsx）并归档到附件表，返回下载链接</summary>
    [ToolDescription("build_excel", IsSystem = false,
        Triggers = "生成Excel,制作Excel,生成报表,导出Excel",
        AssistantTriggers = "Excel,报表,xlsx,电子表格,数据表,生成Excel,生成报表,导出Excel,build_excel",
        ReadOnly = false)]
    [DisplayName("Excel生成")]
    [Description("生成 Excel 电子表格（.xlsx）并返回下载链接。支持多工作表、表头样式、列宽、数字格式、条件格式、冻结窗格、自动筛选、下拉验证。theme 参数使用卡片风格 Key 或内置名（blue/dark/corporate/warm/green/minimal）")]
    public async Task<ToolResult> BuildExcel(
        [Description("工作簿标题（≤ 30 字），如「Q2营收报表」")] String title,
        [Description(@"工作表数组（JSON）。每表字段：name（表名）、headers（列头数组）、rows（二维数组，每行为一个单元格值数组，如 [[""张三"",""85""],[""李四"",""92""]]）、style（{headerBgColor,headerFontColor,stripeColor}）、columnWidths（列宽数组，如[12,20,15]）、numberFormat（数字格式，如""#,##0.00""或""yyyy-MM-dd""）、conditionalFormats（条件格式数组，每项{range,type,color,value?}，type支持dataBar/colorScale/greaterThan等）、charts（图表数组）、freezeRows（冻结行数）、autoFilter（筛选范围）、dropdowns（下拉验证）")] ExcelSheetModel[]? sheets,
        [Description("主题（可选）：卡片风格 Key（如 enterprise-blue）或内置名（blue/dark/corporate/warm/green/minimal），决定表头与整体配色；不传则沿用会话风格或品牌默认")] String? theme = null,
        ToolCallContext? context = null)
    {
        if (title.IsNullOrEmpty())
            throw new ToolException("参数错误：title 不能为空", "请提供工作簿标题后重试。");

        if (sheets == null || sheets.Length == 0)
            throw new ToolException("参数错误：sheets 不能为空", "请提供工作表数组后重试，或直接回复用户说明无法生成 Excel。");
        var sheetList = sheets;

        // 主题解析：显式传参 > 会话卡片风格 > 品牌默认
        var themeKey = OfficeStyleKit.ResolveTheme(theme, context?.Request?.ConversationId);
        log.Info("[BuildExcel] 开始生成：{0}，{1} 个工作表，主题：{2}", title, sheetList.Length, themeKey ?? "default");

        var xlsxBytes = await BuildXlsxAsync(sheetList, themeKey);
        var archiveResult = await ArchiveFileAsync(xlsxBytes, title,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", ".xlsx", "Excel");

        log.Info("[BuildExcel] 生成完成：{0}，{1} 字节，URL：{2}", title, xlsxBytes.Length, archiveResult.Url);

        var buildId = context?.ToolCallId ?? $"xlsx_{Guid.NewGuid():N}";
        var sheetNames = sheetList.Select(s => s.Name).ToArray();
        var result = new ExcelResult(buildId, title, sheetList.Length, sheetNames,
            archiveResult.Url, archiveResult.AttachmentId, xlsxBytes.Length, themeKey);

        // 从 JsonSerializerOptions.Default 派生以携带 TypeInfoResolver，避免序列化触发 MakeReadOnly 抛错
        var resultJson = JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerOptions.Default) { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        return ToolResult.ForAudiences(resultJson, $"[已生成{sheetList.Length}张工作表：{title}]");
    }

    #endregion

    #region Excel 构建

    private async Task<Byte[]> BuildXlsxAsync(ExcelSheetModel[] sheetList, String? theme)
    {
        using var ms = new MemoryStream();
        var writer = new ExcelWriter(ms);

        // 品牌视觉：卡片风格主题 → 完整调色板；工作簿默认字体 + 表头/数据样式统一品牌化
        var palette = OfficePalette.Resolve(theme);
        writer.DefaultFontName = palette.ChineseFont;
        writer.DefaultFontSize = 11;

        foreach (var sheet in sheetList)
        {
            var sheetName = sheet.Name ?? "Sheet1";

            // 工作表标签色跟随品牌主色
            writer.SetSheetTabColor(sheetName, palette.Primary);

            // 表头（headers 为空时跳过，避免空行占用首行且行高留白）
            if (sheet.Headers is { Length: > 0 })
            {
                // 表头样式：品牌默认，AI 显式传色时覆盖（只给背景色时自动配对比字色）
                var headerStyle = OfficeStyleKit.CreateExcelHeaderFormat(palette);
                if (sheet.Style != null)
                {
                    if (!sheet.Style.HeaderBgColor.IsNullOrEmpty())
                        headerStyle.BackgroundColor = sheet.Style.HeaderBgColor;
                    if (!sheet.Style.HeaderFontColor.IsNullOrEmpty())
                        headerStyle.FontColor = sheet.Style.HeaderFontColor;
                    else if (!sheet.Style.HeaderBgColor.IsNullOrEmpty())
                        headerStyle.FontColor = OfficeStyleKit.Luma(sheet.Style.HeaderBgColor) < 160 ? "FFFFFF" : "262626";
                }

                writer.WriteHeader(sheetName, sheet.Headers, headerStyle);

                // 表头行高，增强层次感
                writer.SetRowHeight(sheetName, 1, 22);
            }

            if (sheet.Rows is { Length: > 0 })
            {
                var dataStyle = OfficeStyleKit.CreateExcelDataFormat(palette, false, sheet.NumberFormat);
                var stripeStyle = OfficeStyleKit.CreateExcelDataFormat(palette, true, sheet.NumberFormat);
                if (sheet.Style != null && !sheet.Style.StripeColor.IsNullOrEmpty())
                    stripeStyle.BackgroundColor = sheet.Style.StripeColor;

                var rows = sheet.Rows.Select(r => r.Cast<Object>().ToArray()).ToList();
                for (var ri = 0; ri < rows.Count; ri++)
                {
                    // 斑马纹：奇数行叠加浅底（0-based，第0行是首数据行）
                    var style = ri % 2 == 1 ? stripeStyle : dataStyle;
                    writer.WriteRow(sheetName, rows[ri], style);
                }
            }

            // 列宽：AI 显式指定优先，否则按内容智能估算（中文按 2 字符宽）
            var widths = sheet.ColumnWidths is { Length: > 0 }
                ? sheet.ColumnWidths
                : OfficeStyleKit.EstimateColumnWidths(sheet.Headers ?? [], sheet.Rows);
            for (var ci = 0; ci < widths.Length; ci++)
                writer.SetColumnWidth(sheetName, ci, widths[ci]);

            // 条件格式
            if (sheet.ConditionalFormats is { Length: > 0 })
            {
                foreach (var cf in sheet.ConditionalFormats)
                {
                    if (cf.Range.IsNullOrEmpty() || cf.Type.IsNullOrEmpty()) continue;
                    var type = cf.Type.ToLowerInvariant() switch
                    {
                        "databar" => ConditionalFormatValues.DataBar,
                        "colorscale" => ConditionalFormatValues.ColorScale,
                        "greaterthan" => ConditionalFormatValues.GreaterThan,
                        "lessthan" => ConditionalFormatValues.LessThan,
                        "equal" => ConditionalFormatValues.Equal,
                        "between" => ConditionalFormatValues.Between,
                        "notequal" => ConditionalFormatValues.NotEqual,
                        "notbetween" => ConditionalFormatValues.NotBetween,
                        _ => ConditionalFormatValues.GreaterThan,
                    };
                    writer.AddConditionalFormat(sheetName, cf.Range, type, cf.Value, cf.Color);
                }
            }

            if (sheet.FreezeRows is > 0)
                writer.FreezePane(sheetName, sheet.FreezeRows.Value, 0);

            if (!sheet.AutoFilter.IsNullOrEmpty())
                writer.SetAutoFilter(sheetName, sheet.AutoFilter!);

            if (sheet.Dropdowns != null)
                foreach (var dd in sheet.Dropdowns.Where(d => d.Items is { Length: > 0 }))
                    writer.AddDropdownValidation(sheetName, dd.Range ?? "A2:A1000", dd.Items!);

            // 图表：模型声明支持但此前未实现，补齐 AddChart 调用
            if (sheet.Charts is { Length: > 0 })
            {
                foreach (var chartModel in sheet.Charts)
                {
                    var chart = new ExcelChart
                    {
                        Type = chartModel.Type ?? "bar",
                        Title = chartModel.Title,
                        Categories = chartModel.Categories,
                    };
                    if (chartModel.Series != null)
                    {
                        foreach (var s in chartModel.Series)
                        {
                            if (s.Data == null) continue;
                            chart.Series.Add(new ExcelChartSeries { Name = s.Name, Data = s.Data });
                        }
                    }
                    writer.AddChart(sheetName, chart);
                }
            }
        }

        writer.Save();
        return ms.ToArray();
    }

    #endregion

    #region 归档

    private static async Task<ArchiveResult> ArchiveFileAsync(Byte[] fileBytes, String title,
        String contentType, String extension, String kind)
    {
        var safeTitle = CleanFileName(title);
        var fileName = $"{safeTitle}-{DateTime.Now:yyyyMMddHHmmssfff}{extension}";
        var att = new Attachment { Category = "StarChat", ContentType = contentType, Size = fileBytes.Length, Enable = true, UploadTime = DateTime.Now };
        try { att["Remark"] = $"AI生成{kind}｜标题:{title}"; } catch { }
        await using var ms = new MemoryStream(fileBytes);
        if (!await att.SaveFile(ms, null, fileName)) throw new InvalidOperationException($"{kind} 归档失败");
        return new ArchiveResult(att.Id, $"/cube/file?id={att.Id}{extension}");
    }

    private static String CleanFileName(String name)
    {
        if (name.IsNullOrEmpty()) return "未命名";

        var invalid = Path.GetInvalidFileNameChars();
        var sb = Pool.StringBuilder.Get();
        foreach (var c in name) { if (Array.IndexOf(invalid, c) < 0 && sb.Length < 30) sb.Append(c); }
        return sb.Return(true).Trim();
    }

    private sealed record ArchiveResult(Int64 AttachmentId, String Url);

    #endregion

    #region 日志

    #endregion
}
