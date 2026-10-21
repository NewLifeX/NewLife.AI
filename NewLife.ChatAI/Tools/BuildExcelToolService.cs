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
        Triggers = "build excel,excel sheet,spreadsheet",
        AssistantTriggers = "excel,xlsx,spreadsheet,report table,build_excel",
        ReadOnly = false)]
    [DisplayName("Excel Workbook")]
    [Description("Generate an Excel workbook (.xlsx) and return a download link. Supports multiple worksheets, header styles, column widths, number formats, conditional formats, frozen panes, auto filters, and dropdown validation. The theme parameter accepts a card style key or a built-in name (blue/dark/corporate/warm/green/minimal)")]
    public async Task<ToolResult> BuildExcel(
        [Description("Workbook title (up to 30 characters), e.g. Q2 Revenue Report")] String title,
        [Description(@"Worksheet array (JSON). Fields of each sheet: name, headers (header array), rows (2-D array; each row is an array of cell values, e.g. [[""Alice"",""85""],[""Bob"",""92""]]), style ({headerBgColor,headerFontColor,stripeColor}), columnWidths (e.g. [12,20,15]), numberFormat (e.g. ""#,##0.00"" or ""yyyy-MM-dd""), conditionalFormats (array of {range,type,color,value?}; type supports dataBar/colorScale/greaterThan, etc.), charts (chart array), freezeRows (number of frozen rows), autoFilter (filter range), dropdowns (dropdown validation)")] ExcelSheetModel[]? sheets,
        [Description("Theme (optional): a card style key (e.g. enterprise-blue) or a built-in name (blue/dark/corporate/warm/green/minimal) that controls header and overall colors; when omitted, the conversation style or brand default is used")] String? theme = null,
        ToolCallContext? context = null)
    {
        if (title.IsNullOrEmpty())
            throw new ToolException("Invalid parameter: title cannot be empty", "Provide a workbook title and try again.");

        if (sheets == null || sheets.Length == 0)
            throw new ToolException("Invalid parameter: sheets cannot be empty", "Provide a sheet array and try again, or reply to the user that the Excel file cannot be generated.");
        var sheetList = sheets;

        // 主题解析：显式传参 > 会话卡片风格 > 品牌默认
        var themeKey = OfficeStyleKit.ResolveTheme(theme, context?.Request?.ConversationId);
        log.Info("[BuildExcel] Generating: {0}, {1} sheets, theme: {2}", title, sheetList.Length, themeKey ?? "default");

        var xlsxBytes = await BuildXlsxAsync(sheetList, title, themeKey);
        var archiveResult = await ArchiveFileAsync(xlsxBytes, title,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", ".xlsx", "Excel");

        log.Info("[BuildExcel] Generated: {0}, {1} bytes, URL: {2}", title, xlsxBytes.Length, archiveResult.Url);

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

    private async Task<Byte[]> BuildXlsxAsync(ExcelSheetModel[] sheetList, String title, String? theme)
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

            // 标题带：跨列合并的品牌深色标题行（对齐真实报表习惯），后续内容整体下移一行
            var offset = 0;
            if (sheet.Headers is { Length: > 0 })
            {
                writer.WriteRow(sheetName, [title], OfficeStyleKit.CreateExcelTitleFormat(palette));
                if (sheet.Headers.Length > 1)
                    writer.MergeCell(sheetName, 0, 0, 0, sheet.Headers.Length - 1);
                writer.SetRowHeight(sheetName, 1, 38);
                offset = 1;

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
                writer.SetRowHeight(sheetName, 1 + offset, 24);
            }

            // 列宽：AI 显式指定优先，否则按内容智能估算（中文按 2 字符宽）；先行计算供数据行换行判断
            var widths = sheet.ColumnWidths is { Length: > 0 }
                ? sheet.ColumnWidths
                : OfficeStyleKit.EstimateColumnWidths(sheet.Headers ?? [], sheet.Rows);

            if (sheet.Rows is { Length: > 0 })
            {
                var dataStyle = OfficeStyleKit.CreateExcelDataFormat(palette, false, sheet.NumberFormat);
                var stripeStyle = OfficeStyleKit.CreateExcelDataFormat(palette, true, sheet.NumberFormat);
                if (sheet.Style != null && !sheet.Style.StripeColor.IsNullOrEmpty())
                    stripeStyle.BackgroundColor = sheet.Style.StripeColor;

                CellFormat? dataWrap = null;
                CellFormat? stripeWrap = null;
                var baseRow = offset + (sheet.Headers is { Length: > 0 } ? 1 : 0);
                var rows = sheet.Rows.Select(r => r.Cast<Object>().ToArray()).ToList();
                for (var ri = 0; ri < rows.Count; ri++)
                {
                    // 斑马纹：奇数行叠加淡染底色（0-based，第0行是首数据行）
                    var striped = ri % 2 == 1;
                    var needWrap = HasOverflowText(rows[ri], widths);
                    // 长文本行开自动换行、交给 Excel 自适应行高；常规行固定 22 磅提升呼吸感
                    var style = striped
                        ? (needWrap ? stripeWrap ??= OfficeStyleKit.CreateExcelDataFormat(palette, true, sheet.NumberFormat, true) : stripeStyle)
                        : (needWrap ? dataWrap ??= OfficeStyleKit.CreateExcelDataFormat(palette, false, sheet.NumberFormat, true) : dataStyle);
                    if (needWrap && striped && sheet.Style?.StripeColor.IsNullOrEmpty() == false)
                        style.BackgroundColor = sheet.Style.StripeColor;
                    writer.WriteRow(sheetName, rows[ri], style);
                    if (!needWrap) writer.SetRowHeight(sheetName, baseRow + ri + 1, 22);
                }
            }

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
                    writer.AddConditionalFormat(sheetName, ShiftRange(cf.Range!, offset), type, cf.Value, cf.Color);
                }
            }

            if (sheet.FreezeRows is > 0)
                writer.FreezePane(sheetName, sheet.FreezeRows.Value + offset, 0);

            if (!sheet.AutoFilter.IsNullOrEmpty())
                writer.SetAutoFilter(sheetName, ShiftRange(sheet.AutoFilter!, offset));

            if (sheet.Dropdowns != null)
                foreach (var dd in sheet.Dropdowns.Where(d => d.Items is { Length: > 0 }))
                    writer.AddDropdownValidation(sheetName, ShiftRange(dd.Range ?? "A2:A1000", offset), dd.Items!);

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

    /// <summary>判断数据行是否存在超出列宽的文本（需要自动换行并由 Excel 自适应行高）</summary>
    /// <param name="row">单元格值数组</param>
    /// <param name="widths">列宽数组（字符宽）</param>
    /// <returns>是否需换行</returns>
    private static Boolean HasOverflowText(Object[] row, Int32[] widths)
    {
        for (var c = 0; c < row.Length && c < widths.Length; c++)
        {
            if (row[c] is not String text || text.Length == 0) continue;
            if (OfficeStyleKit.DisplayWidth(text) > widths[c]) return true;
        }
        return false;
    }

    /// <summary>把 Excel 区域引用整体下移若干行（如 "A1:E5" → "A2:E6"），用于标题带占位后的区域修正</summary>
    /// <param name="range">区域引用（单元格或区域）</param>
    /// <param name="rows">下移行数</param>
    /// <returns>修正后的区域引用</returns>
    private static String ShiftRange(String range, Int32 rows)
    {
        if (rows == 0 || range.IsNullOrEmpty()) return range;

        var parts = range!.Split(':');
        for (var i = 0; i < parts.Length; i++)
            parts[i] = ShiftCellRef(parts[i], rows);
        return String.Join(":", parts);
    }

    /// <summary>把单元格引用下移若干行（保留列名与绝对引用符号）</summary>
    private static String ShiftCellRef(String cellRef, Int32 rows)
    {
        var i = 0;
        while (i < cellRef.Length && (cellRef[i] < '0' || cellRef[i] > '9')) i++;
        // 无行号（整列引用如 "A"）或行号非法时保持原样
        if (i >= cellRef.Length || !Int32.TryParse(cellRef[i..], out var row)) return cellRef;
        return cellRef[..i] + (row + rows);
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
        if (!await att.SaveFile(ms, null, fileName)) throw new InvalidOperationException(Localization.Translate("{0} archiving failed", kind));
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
