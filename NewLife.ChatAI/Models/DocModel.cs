using System.ComponentModel;

namespace NewLife.ChatAI.Models;

/// <summary>Word 文档节定义</summary>
public class DocSectionModel
{
    [Description("节标题文本，如「本周完成」")]
    public String? Heading { get; set; }

    [Description("标题级别：1=一级（最大），2=二级，3=三级，默认1")]
    public Int32 HeadingLevel { get; set; } = 1;

    [Description("正文段落文本（可选，与 elements 配合使用）")]
    public String? Content { get; set; }

    [Description("结构化元素数组（可选）：paragraph/bullet_list/ordered_list/table/image/page_break/divider/callout/kpi/quote/code")]
    public DocElement[]? Elements { get; set; }
}

/// <summary>Word 文档元素（段落/列表/表格/图片/分页/分隔线/高亮框/KPI/引用/代码）</summary>
public class DocElement
{
    [Description("元素类型：paragraph（段落）/ bullet_list（无序列表）/ ordered_list（有序列表）/ table（表格）/ image（图片）/ page_break（分页）/ divider（分隔线）/ callout（高亮提示框）/ kpi（大数字指标）/ quote（引用块）/ code（代码块）")]
    public String? Type { get; set; }

    [Description("文本内容（type=paragraph/callout/quote/code 时使用）")]
    public String? Text { get; set; }

    [Description("列表条目数组（type=bullet_list 或 ordered_list 时使用）")]
    public String[]? Items { get; set; }

    [Description("表格列标题（type=table 时必填）")]
    public String[]? Headers { get; set; }

    [Description("表格数据行（type=table 时必填），每行为一个单元格值数组，如 [[\"张三\",\"85\"],[\"李四\",\"92\"]]")]
    public String[][]? Rows { get; set; }

    [Description("表格样式（type=table 时可选）：headerBgColor 表头背景色、headerFontColor 表头字色、stripeColor 斑马纹色")]
    public TableStyle? TableStyle { get; set; }

    [Description("图片 URL（type=image 时必填），支持 https:// 外链或 /cube/image?id=xxx 内部附件")]
    public String? Src { get; set; }

    [Description("图片宽度（厘米），type=image 时有效，默认 14cm")]
    public Double? WidthCm { get; set; }

    [Description("图片高度（厘米），type=image 时有效，默认 10cm")]
    public Double? HeightCm { get; set; }

    // === 文本格式（paragraph/callout/quote 通用）===
    [Description("是否加粗（type=paragraph/callout/quote 时可选）")]
    public Boolean? Bold { get; set; }

    [Description("是否斜体（type=paragraph/callout/quote 时可选）")]
    public Boolean? Italic { get; set; }

    [Description("是否下划线（type=paragraph/callout/quote 时可选）")]
    public Boolean? Underline { get; set; }

    [Description("字号（磅），如 12（type=paragraph/callout 时可选）")]
    public Int32? FontSize { get; set; }

    [Description("文字颜色（16进制 RGB，如 \"2563EB\"，type=paragraph/callout/quote 时可选）")]
    public String? Color { get; set; }

    [Description("文字对齐：left/center/right/justify（type=paragraph 时可选）")]
    public String? Alignment { get; set; }

    [Description("段落背景色（16进制 RGB，如 \"EFF6FF\"，type=paragraph 时可选）")]
    public String? BackgroundColor { get; set; }

    // === 语义块属性 ===
    [Description("高亮框变体（type=callout 时有效）：info（蓝）/ success（绿）/ warning（黄）/ danger（红），默认 info")]
    public String? Variant { get; set; }

    [Description("KPI 指标数值（type=kpi 时必填），如 \"1,768万\"")]
    public String? KpiValue { get; set; }

    [Description("KPI 指标说明（type=kpi 时可选），如 \"环比增长\"")]
    public String? KpiLabel { get; set; }

    [Description("KPI 趋势（type=kpi 时可选）：up（上升）/ down（下降）/ flat（持平）")]
    public String? KpiTrend { get; set; }

    [Description("引用来源（type=quote 时可选），如 \"《2026年度报告》\"")]
    public String? QuoteSource { get; set; }

    [Description("代码语言（type=code 时可选），如 sql/python/javascript，用于选择语法高亮色")]
    public String? CodeLanguage { get; set; }
}

/// <summary>build_doc 工具结果 JSON</summary>
public sealed record DocResult(
    String BuildId,
    String Title,
    Int32 SectionCount,
    String DownloadUrl,
    Int64 AttachmentId,
    Int64 FileSize,
    String? Theme);
