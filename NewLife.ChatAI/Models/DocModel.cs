using System.ComponentModel;

namespace NewLife.ChatAI.Models;

/// <summary>Word 文档节定义</summary>
public class DocSectionModel
{
    [Description("Section heading text, e.g. Completed this week")]
    public String? Heading { get; set; }

    [Description("Heading level: 1 = level 1 (largest), 2 = level 2, 3 = level 3; default 1")]
    public Int32 HeadingLevel { get; set; } = 1;

    [Description("Body paragraph text (optional; used together with elements)")]
    public String? Content { get; set; }

    [Description("Structured element array (optional): paragraph/bullet_list/ordered_list/table/image/page_break/divider/callout/kpi/quote/code")]
    public DocElement[]? Elements { get; set; }
}

/// <summary>Word 文档元素（段落/列表/表格/图片/分页/分隔线/高亮框/KPI/引用/代码）</summary>
public class DocElement
{
    [Description("Element type: paragraph / bullet_list / ordered_list / table / image / page_break / divider / callout / kpi / quote / code")]
    public String? Type { get; set; }

    [Description("Text content (used when type=paragraph/callout/quote/code)")]
    public String? Text { get; set; }

    [Description("List item array (used when type=bullet_list or ordered_list)")]
    public String[]? Items { get; set; }

    [Description("Table header row (required when type=table)")]
    public String[]? Headers { get; set; }

    [Description("Table data rows (required when type=table); each row is an array of cell values, e.g. [[\"Alice\",\"85\"],[\"Bob\",\"92\"]]")]
    public String[][]? Rows { get; set; }

    [Description("Table style (optional for type=table): headerBgColor (header background), headerFontColor (header text color), stripeColor (striped rows)")]
    public TableStyle? TableStyle { get; set; }

    [Description("Image URL (required when type=image); supports external https:// links or internal attachments such as /cube/image?id=xxx")]
    public String? Src { get; set; }

    [Description("Image width in centimeters (type=image); default 14cm")]
    public Double? WidthCm { get; set; }

    [Description("Image height in centimeters (type=image); default 10cm")]
    public Double? HeightCm { get; set; }

    // === 文本格式（paragraph/callout/quote 通用）===
    [Description("Whether the text is bold (optional for type=paragraph/callout/quote)")]
    public Boolean? Bold { get; set; }

    [Description("Whether the text is italic (optional for type=paragraph/callout/quote)")]
    public Boolean? Italic { get; set; }

    [Description("Whether the text is underlined (optional for type=paragraph/callout/quote)")]
    public Boolean? Underline { get; set; }

    [Description("Font size in points, e.g. 12 (optional for type=paragraph/callout)")]
    public Int32? FontSize { get; set; }

    [Description("Text color (hex RGB, e.g. 2563EB; optional for type=paragraph/callout/quote)")]
    public String? Color { get; set; }

    [Description("Text alignment: left/center/right/justify (optional for type=paragraph)")]
    public String? Alignment { get; set; }

    [Description("Paragraph background color (hex RGB, e.g. EFF6FF; optional for type=paragraph)")]
    public String? BackgroundColor { get; set; }

    // === 语义块属性 ===
    [Description("Callout variant (type=callout): info (blue) / success (green) / warning (yellow) / danger (red); default info")]
    public String? Variant { get; set; }

    [Description("KPI value (required when type=kpi), e.g. 17.68M")]
    public String? KpiValue { get; set; }

    [Description("KPI label (optional when type=kpi), e.g. month-over-month growth")]
    public String? KpiLabel { get; set; }

    [Description("KPI trend (optional when type=kpi): up / down / flat")]
    public String? KpiTrend { get; set; }

    [Description("Quote source (optional when type=quote), e.g. 2026 Annual Report")]
    public String? QuoteSource { get; set; }

    [Description("Code language (optional when type=code), e.g. sql/python/javascript; selects the syntax highlighting colors")]
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
