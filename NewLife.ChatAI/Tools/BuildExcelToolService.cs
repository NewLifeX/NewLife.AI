using System.ComponentModel;
using System.Text.Json;
using NewLife;
using NewLife.AI.Tools;
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

    [ToolDescription("build_excel", IsSystem = false,
        Triggers = "生成Excel,制作Excel,生成报表,导出Excel",
        AssistantTriggers = "Excel,报表,xlsx,电子表格,数据表,生成Excel,生成报表,导出Excel,build_excel",
        ReadOnly = false)]
    [DisplayName("Excel生成")]
    [Description("生成 Excel 电子表格（.xlsx）并返回下载链接。支持多工作表、表头样式、列宽、数字格式、条件格式、冻结窗格、自动筛选、下拉验证。theme 参数使用卡片风格 Key 或内置名（blue/dark/corporate/warm/green）")]
    public async Task<ToolResult> BuildExcel(
        [Description("工作簿标题（≤ 30 字），如「Q2营收报表」")] String title,
        [Description(@"工作表数组（JSON）。每表字段：name（表名）、headers（列头数组）、rows（二维数组，每行为一个单元格值数组，如 [[""张三"",""85""],[""李四"",""92""]]）、style（{headerBgColor,headerFontColor,stripeColor}）、columnWidths（列宽数组，如[12,20,15]）、numberFormat（数字格式，如""#,##0.00""或""yyyy-MM-dd""）、conditionalFormats（条件格式数组，每项{range,type,color,value?}，type支持dataBar/colorScale/greaterThan等）、charts（图表数组）、freezeRows（冻结行数）、autoFilter（筛选范围）、dropdowns（下拉验证）")] String sheets,
        [Description("主题（可选）：卡片风格 Key 或内置名（blue/dark/corporate/warm/green/minimal），决定表头颜色")] String? theme = null,
        ToolCallContext? context = null)
    {
        if (title.IsNullOrEmpty())
            throw new ToolException("参数错误：title 不能为空", "请提供工作簿标题后重试。");

        if (sheets.IsNullOrEmpty())
            throw new ToolException("参数错误：sheets 不能为空", "请提供工作表 JSON 数组后重试，或直接回复用户说明无法生成 Excel。");
        var sheetList = sheets.ToJsonEntity<ExcelSheetModel[]>();
        if (sheetList == null)
            throw new ToolException("sheets JSON 格式错误", "请检查 JSON 语法后重试，或直接回复用户说明无法生成 Excel。");
        if (sheetList.Length == 0)
            throw new ToolException("sheets 不能为空数组", "请提供至少一个工作表后重试，或直接回复用户说明无法生成 Excel。");

        log.Info("[BuildExcel] 开始生成：{0}，{1} 个工作表，主题：{2}", title, sheetList.Length, theme ?? "blue");

        var xlsxBytes = await BuildXlsxAsync(sheetList, theme);
        var archiveResult = await ArchiveFileAsync(xlsxBytes, title,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", ".xlsx", "Excel");

        log.Info("[BuildExcel] 生成完成：{0}，{1} 字节，URL：{2}", title, xlsxBytes.Length, archiveResult.Url);

        var buildId = context?.ToolCallId ?? $"xlsx_{Guid.NewGuid():N}";
        var sheetNames = sheetList.Select(s => s.Name).ToArray();
        var result = new ExcelResult(buildId, title, sheetList.Length, sheetNames,
            archiveResult.Url, archiveResult.AttachmentId, xlsxBytes.Length, theme);

        var resultJson = JsonSerializer.Serialize(result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        return new ToolResult(ToolContent.ForUser(resultJson),
            ToolContent.ForLlm($"[已生成{sheetList.Length}张工作表：{title}]"));
    }

    #endregion

    #region Excel 构建

    private async Task<Byte[]> BuildXlsxAsync(ExcelSheetModel[] sheetList, String? theme)
    {
        using var ms = new MemoryStream();
        var writer = new ExcelWriter(ms);

        // 从主题取表头背景色（Accent1）
        var accentColor = ThemeColors.GetPrimary(theme);

        foreach (var sheet in sheetList)
        {
            var sheetName = sheet.Name ?? "Sheet1";

            // 表头样式（优先用 sheet 自定义样式，其次用主题色）
            var headerBg   = sheet.Style?.HeaderBgColor ?? accentColor;
            var headerFont = sheet.Style?.HeaderFontColor ?? "FFFFFF";
            var stripe     = sheet.Style?.StripeColor ?? ThemeColors.GetLight(theme);
            var headerStyle = new CellFormat
            {
                Bold = true,
                FontColor = headerFont,
                BackgroundColor = headerBg,
                HAlign = HorizontalAlignment.Center,
                Border = BorderStyle.Thin,
            };

            writer.WriteHeader(sheetName, sheet.Headers ?? [], headerStyle);

            if (sheet.Rows is { Length: > 0 })
            {
                var dataStyle = new CellFormat
                {
                    Border = BorderStyle.Thin,
                    NumberFormat = sheet.NumberFormat,
                };
                var stripeStyle = new CellFormat
                {
                    Border = BorderStyle.Thin,
                    BackgroundColor = stripe,
                    NumberFormat = sheet.NumberFormat,
                };
                var rows = sheet.Rows.Select(r => r.Cast<Object>().ToArray()).ToList();
                for (var ri = 0; ri < rows.Count; ri++)
                {
                    // 斑马纹：奇数行用 stripe（0-based，第0行是首数据行=偶数视觉行）
                    var style = !stripe.IsNullOrEmpty() && ri % 2 == 1 ? stripeStyle : dataStyle;
                    writer.WriteRow(sheetName, rows[ri], style);
                }
            }

            // 列宽
            if (sheet.ColumnWidths is { Length: > 0 })
            {
                for (var ci = 0; ci < sheet.ColumnWidths.Length; ci++)
                    writer.SetColumnWidth(sheetName, ci, sheet.ColumnWidths[ci]);
            }

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
