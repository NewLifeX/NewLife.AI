using System.ComponentModel;

namespace NewLife.ChatAI.Models;

/// <summary>Excel 工作表数据描述</summary>
public class ExcelSheetModel
{
    [Description("Worksheet name, e.g. Q2 Data")]
    public String Name { get; set; } = "Sheet1";

    [Description("Column header array, e.g. [\"Date\",\"Revenue\",\"MoM\"]")]
    public String[]? Headers { get; set; }

    [Description("Data rows (2-D array); each row is an array of cell values, e.g. [[\"Alice\",\"85\"],[\"Bob\",\"92\"]]")]
    public String[][]? Rows { get; set; }

    [Description("Header style (optional): headerBgColor (header background), headerFontColor (header text color), stripeColor (striped rows)")]
    public TableStyle? Style { get; set; }

    [Description("Column width array (optional), e.g. [12, 20, 15]; values are approximate character widths")]
    public Int32[]? ColumnWidths { get; set; }

    [Description("Number format (optional), e.g. \"#,##0.00\" or \"yyyy-MM-dd\"")]
    public String? NumberFormat { get; set; }

    [Description("Conditional format array (optional). Fields of each item: range (e.g. \"C2:C10\"), type (dataBar/colorScale/greaterThan/lessThan, etc.), color (primary color), value (threshold)")]
    public ConditionalFormat[]? ConditionalFormats { get; set; }

    [Description("Chart definition array (optional): type=bar/line/pie + title + categories + series")]
    public ExcelChartModel[]? Charts { get; set; }

    [Description("Number of frozen top rows (0 = none, 1 = freeze the first row; default 1)")]
    public Int32? FreezeRows { get; set; } = 1;

    [Description("Auto filter range (e.g. \"A1:E1\"); disabled when empty")]
    public String? AutoFilter { get; set; }

    [Description("Dropdown validation array (optional)")]
    public ExcelDropdownModel[]? Dropdowns { get; set; }
}

/// <summary>条件格式定义（数据条/色阶/阈值规则）</summary>
public class ConditionalFormat
{
    [Description("Applied range (e.g. \"C2:C10\")")]
    public String? Range { get; set; }

    [Description("Condition type: dataBar / colorScale / greaterThan / lessThan / equal / between")]
    public String? Type { get; set; }

    [Description("Color (hex RGB, e.g. 2563EB) used for data bars, color scales, or highlighting")]
    public String? Color { get; set; }

    [Description("Threshold or formula (e.g. 1000 or =AVERAGE(C2:C10))")]
    public String? Value { get; set; }
}

/// <summary>Excel 图表定义</summary>
public class ExcelChartModel
{
    [Description("Chart type: bar / line / pie")]
    public String Type { get; set; } = "bar";

    [Description("Chart title (optional)")]
    public String? Title { get; set; }

    [Description("Category axis label array, e.g. [\"Q1\",\"Q2\",\"Q3\"]")]
    public String[]? Categories { get; set; }

    [Description("Data series array; each item has name and data")]
    public ChartSeries[]? Series { get; set; }
}

/// <summary>Excel 下拉验证</summary>
public class ExcelDropdownModel
{
    [Description("Applied range (e.g. \"A2:A100\")")]
    public String? Range { get; set; }

    [Description("Dropdown option array")]
    public String[]? Items { get; set; }
}

/// <summary>build_excel 工具结果 JSON</summary>
public sealed record ExcelResult(
    String BuildId,
    String Title,
    Int32 SheetCount,
    String[] SheetNames,
    String DownloadUrl,
    Int64 AttachmentId,
    Int64 FileSize,
    String? Theme);
