using System.ComponentModel;

namespace NewLife.ChatAI.Models;

/// <summary>Excel 工作表数据描述</summary>
public class ExcelSheetModel
{
    [Description("工作表名称，如「Q2数据」")]
    public String Name { get; set; } = "Sheet1";

    [Description("列标题数组，如 [\"日期\",\"营收\",\"环比\"]")]
    public String[]? Headers { get; set; }

    [Description("数据行（平铺的单元格值），按列顺序排列，自动按表头数量分组")]
    public String[]? Rows { get; set; }

    [Description("表头样式（可选）：headerBgColor 表头背景色、headerFontColor 表头字色、stripeColor 斑马纹色")]
    public TableStyle? Style { get; set; }

    [Description("列宽数组（可选），如 [12, 20, 15]，数字单位近似字符宽度")]
    public Int32[]? ColumnWidths { get; set; }

    [Description("数字格式（可选），如 \"#,##0.00\" 或 \"yyyy-MM-dd\"")]
    public String? NumberFormat { get; set; }

    [Description("条件格式数组（可选）。每项字段：range（如\"C2:C10\"）、type（dataBar/colorScale/greaterThan/lessThan 等）、color（主色）、value（阈值）")]
    public ConditionalFormat[]? ConditionalFormats { get; set; }

    [Description("图表定义数组（可选）：type=bar/line/pie + title + categories + series")]
    public ExcelChartModel[]? Charts { get; set; }

    [Description("冻结首几行（0=不冻结，1=冻结首行，默认1）")]
    public Int32? FreezeRows { get; set; } = 1;

    [Description("自动筛选范围（如 \"A1:E1\"，为空则不启用）")]
    public String? AutoFilter { get; set; }

    [Description("下拉列表验证数组（可选）")]
    public ExcelDropdownModel[]? Dropdowns { get; set; }
}

/// <summary>条件格式定义（数据条/色阶/阈值规则）</summary>
public class ConditionalFormat
{
    [Description("应用范围（如 \"C2:C10\"）")]
    public String? Range { get; set; }

    [Description("条件类型：dataBar（数据条）/ colorScale（色阶）/ greaterThan（大于）/ lessThan（小于）/ equal（等于）/ between（介于）")]
    public String? Type { get; set; }

    [Description("颜色（16进制 RGB，如 \"2563EB\"），用于数据条/色阶/高亮")]
    public String? Color { get; set; }

    [Description("阈值或公式（如 \"1000\" 或 \"=AVERAGE(C2:C10)\"）")]
    public String? Value { get; set; }
}

/// <summary>Excel 图表定义</summary>
public class ExcelChartModel
{
    [Description("图表类型：bar（柱状）/ line（折线）/ pie（饼图）")]
    public String Type { get; set; } = "bar";

    [Description("图表标题（可选）")]
    public String? Title { get; set; }

    [Description("分类轴标签数组，如 [\"Q1\",\"Q2\",\"Q3\"]")]
    public String[]? Categories { get; set; }

    [Description("数据系列数组，每项含 name 和 data")]
    public ChartSeries[]? Series { get; set; }
}

/// <summary>Excel 下拉验证</summary>
public class ExcelDropdownModel
{
    [Description("应用范围（如 \"A2:A100\"）")]
    public String? Range { get; set; }

    [Description("下拉选项数组")]
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
