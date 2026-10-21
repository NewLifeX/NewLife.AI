using System.ComponentModel;
using System.Text.Json;

namespace NewLife.ChatAI.Models;

/// <summary>单张幻灯片的数据描述</summary>
public class PptPageModel
{
    /// <summary>本页幻灯片标题（用于预览卡片）</summary>
    [Description("Title of this slide")]
    public String Title { get; set; } = String.Empty;

    /// <summary>布局模式：title_only / title_content / two_column / chart_only / blank</summary>
    [Description("Slide layout: title_only (title only, for covers) / title_content (title + body, default) / two_column (two columns) / chart_only (full-width chart) / blank (blank canvas)")]
    public String? Layout { get; set; }

    /// <summary>背景色（16进制 RGB，如 "1F497D"）。不填则使用主题默认背景</summary>
    [Description("Slide background color, hex RGB such as \"1F497D\"; when omitted, the theme background is used")]
    public String? Background { get; set; }

    /// <summary>元素列表</summary>
    [Description("Slide element list; each element describes a text block, table, chart, or image")]
    public PptElement[] Elements { get; set; } = [];

    /// <summary>页脚文本（null 表示不显示）</summary>
    [Description("Footer text displayed at the bottom of the slide; null hides it")]
    public String? Footer { get; set; }

    /// <summary>是否在右下角显示幻灯片序号</summary>
    [Description("Whether to show the slide number in the bottom-right corner; default false")]
    public Boolean ShowPageNumber { get; set; }

    /// <summary>演讲者备注</summary>
    [Description("Speaker notes used as a reminder while presenting")]
    public String? Notes { get; set; }

    /// <summary>切换动画：fade / push / wipe / zoom / split / cut</summary>
    [Description("Transition animation: fade / push / wipe / zoom / split / cut")]
    public String? Transition { get; set; }
}

/// <summary>幻灯片元素（文字/表格/图表/图片）</summary>
public class PptElement
{
    /// <summary>元素类型：text / table / chart / image</summary>
    [Description("Element type: text / table / chart / image")]
    public String Type { get; set; } = "text";

    /// <summary>语义角色（text 元素）：title / subtitle / body / caption / kpi。布局引擎据此自动定位</summary>
    [Description("Semantic role (text elements only): title / subtitle / body / caption / kpi. The layout engine uses it to position the element automatically, so no coordinates are needed")]
    public String? Role { get; set; }

    /// <summary>文字内容（type=text 时必填）</summary>
    [Description("Text content (required when type=text)")]
    public String? Content { get; set; }

    /// <summary>字号（磅），不填由布局引擎按 role 决定</summary>
    [Description("Font size in points (optional); when omitted, the layout engine picks one by role (title≈28, kpi≈44, body≈16)")]
    public Int32? FontSize { get; set; }

    /// <summary>是否粗体</summary>
    [Description("Whether the text is bold; default false")]
    public Boolean? Bold { get; set; }

    /// <summary>是否斜体</summary>
    [Description("Whether the text is italic (optional for type=text); default false")]
    public Boolean? Italic { get; set; }

    /// <summary>是否下划线</summary>
    [Description("Whether the text is underlined (optional for type=text); default false")]
    public Boolean? Underline { get; set; }

    /// <summary>文字颜色（16进制 RGB，如 "FFFFFF"）</summary>
    [Description("Text color, hex RGB such as \"FFFFFF\"; when omitted, the theme text color is inherited")]
    public String? Color { get; set; }

    /// <summary>表格列标题（type=table 时必填）</summary>
    [Description("Table header array (required when type=table), e.g. [\"Metric\",\"Q1\",\"Q2\",\"MoM\"]")]
    public String[]? Headers { get; set; }

    /// <summary>表格数据行（type=table 时必填），每行为一个单元格值数组</summary>
    [Description("Table data rows (required when type=table); each row is an array of cell values, e.g. [[\"Alice\",\"85\"],[\"Bob\",\"92\"]]")]
    public String[][]? Rows { get; set; }

    /// <summary>图表类型（type=chart 时必填）：bar / line / pie</summary>
    [Description("Chart type (required when type=chart): bar / line / pie")]
    public String? ChartType { get; set; }

    /// <summary>分类轴标签（type=chart 时必填）</summary>
    [Description("Chart category labels (required when type=chart), e.g. [\"Q1\",\"Q2\",\"Q3\",\"Q4\"]")]
    public String[]? Categories { get; set; }

    /// <summary>数据系列（type=chart 时必填），每项含 name 和 data</summary>
    [Description("Data series (required when type=chart); each item has name (series name) and data (numeric array)")]
    public ChartSeries[]? Series { get; set; }

    /// <summary>图片 URL（type=image 时必填）。支持 https:// 外链或 /cube/image?id=xxx 内部附件</summary>
    [Description("Image URL (required when type=image); supports external https:// links or internal attachments such as /cube/image?id=xxx")]
    public String? Src { get; set; }

    /// <summary>文字对齐（text 元素）：l（左对齐）/ ctr（居中）/ r（右对齐）</summary>
    [Description("Text alignment (text elements): l (left) / ctr (center) / r (right)")]
    public String? Alignment { get; set; }

    /// <summary>文本框背景色（text 元素，16进制 RGB，如 \"EFF6FF\"）</summary>
    [Description("Text box background color (text elements), hex RGB such as \"EFF6FF\"; transparent when empty")]
    public String? BackgroundColor { get; set; }

    /// <summary>富文本片段（text 元素）。有内容时忽略 Content/FontSize/Bold/Color，支持混合格式</summary>
    [Description("Rich text runs (text elements); when present, Content/FontSize/Bold/Color are ignored")]
    public TextRun[]? Runs { get; set; }

    /// <summary>基本形状配置（type=shape 时必填）</summary>
    [Description("Shape configuration (required when type=shape)")]
    public ShapeInfo? Shape { get; set; }

    /// <summary>表格样式（type=table 时可选，控制表头背景色和斑马纹）</summary>
    [Description("Table style (optional when type=table): headerBgColor (header background), headerFontColor (header text color), stripeColor (striped rows)")]
    public TableStyle? TableStyle { get; set; }

    /// <summary>将 Headers + Rows 合并为 AddTable 需要的行集合（首行为表头）。Rows 为嵌套数组，每行一个 String[]</summary>
    public IEnumerable<String[]> ToRows()
    {
        if (Headers != null) yield return Headers;
        if (Rows != null)
        {
            foreach (var row in Rows)
                yield return row;
        }
    }
}

/// <summary>富文本片段（text 元素），支持同一文本框内混合字体/颜色/大小</summary>
public class TextRun
{
    /// <summary>文本内容</summary>
    [Description("Text content")]
    public String Text { get; set; } = String.Empty;

    /// <summary>字号（磅），0 表示继承文本框默认字号</summary>
    [Description("Font size in points; 0 inherits the text box default")]
    public Int32 FontSize { get; set; }

    /// <summary>是否粗体</summary>
    [Description("Whether the text is bold")]
    public Boolean Bold { get; set; }

    /// <summary>是否斜体</summary>
    [Description("Whether the text is italic")]
    public Boolean Italic { get; set; }

    /// <summary>是否下划线</summary>
    [Description("Whether the text is underlined")]
    public Boolean Underline { get; set; }

    /// <summary>文字颜色（16进制 RGB，如 \"FF6B6B\"）</summary>
    [Description("Text color, hex RGB such as \"FF6B6B\"")]
    public String? Color { get; set; }

    /// <summary>超链接 URL，不为空时点击该片段跳转</summary>
    [Description("Hyperlink URL; the run becomes clickable when set")]
    public String? HyperlinkUrl { get; set; }
}

/// <summary>基本形状配置（type=shape）</summary>
public class ShapeInfo
{
    /// <summary>形状类型：rect / ellipse / roundRect / triangle / diamond / arrow</summary>
    [Description("Shape type: rect / ellipse / roundRect / triangle / diamond / arrow")]
    public String ShapeType { get; set; } = "rect";

    /// <summary>填充色（16进制 RGB），null 表示无填充</summary>
    [Description("Fill color (hex RGB, e.g. 2563EB); null means no fill")]
    public String? FillColor { get; set; }

    /// <summary>边框颜色（16进制 RGB），null 表示无边框</summary>
    [Description("Border color (hex RGB); null means no border")]
    public String? LineColor { get; set; }

    /// <summary>形状内文字（可选）</summary>
    [Description("Text displayed inside the shape (optional)")]
    public String? Text { get; set; }

    /// <summary>文字颜色（16进制 RGB）</summary>
    [Description("Text color inside the shape (hex RGB)")]
    public String? FontColor { get; set; }

    /// <summary>文字字号（磅），0 表示默认</summary>
    [Description("Font size of the text inside the shape (points); 0 means default")]
    public Int32 FontSize { get; set; }
}

/// <summary>表格样式配置</summary>
public class TableStyle
{
    /// <summary>表头行背景色（16进制 RGB，如 \"2563EB\"）</summary>
    [Description("Header row background color (hex RGB, e.g. 2563EB); when omitted, no special style is applied")]
    public String? HeaderBgColor { get; set; }

    /// <summary>表头行文字颜色（16进制 RGB，如 \"FFFFFF\"）</summary>
    [Description("Header row text color (hex RGB, e.g. FFFFFF); when omitted, the default is inherited")]
    public String? HeaderFontColor { get; set; }

    /// <summary>斑马纹颜色（偶数数据行背景色，如 \"EFF6FF\"）</summary>
    [Description("Striped row color (background of even data rows, e.g. EFF6FF light blue); when omitted, no stripes are drawn")]
    public String? StripeColor { get; set; }
}

/// <summary>图表数据系列</summary>
public class ChartSeries
{
    /// <summary>系列名称</summary>
    public String Name { get; set; } = String.Empty;

    /// <summary>数值数组，与 Categories 一一对应</summary>
    public Double[] Data { get; set; } = [];
}


/// <summary>主题色板工具。优先从 CardStyle.ThemeColors 读取（JSON 13色槽格式），缺省时回退内置色板</summary>
public static class ThemeColors
{
    private static readonly String[] _defaultBlue = ["2563EB", "1D4ED8", "60A5FA", "93C5FD", "1E40AF", "DBEAFE"];

    /// <summary>获取主题强调色数组（Accent1~6，16进制 RGB，无 # 前缀）。
    /// 优先从 CardStyle.ThemeColors 读取（JSON 13色槽），缺省回退内置色板</summary>
    public static String[] Get(String? theme)
    {
#if STARCHAT
        if (!theme.IsNullOrEmpty())
        {
            var cardStyle = NewLife.StarChat.Entity.CardStyle.GetByKey(theme!);
            if (cardStyle?.Enable == true && !cardStyle.ThemeColors.IsNullOrEmpty())
            {
                // 尝试解析 JSON 13色槽格式
                try
                {
                    using var doc = JsonDocument.Parse(cardStyle.ThemeColors!);
                    var root = doc.RootElement;
                    String Strip(String? hex) => (hex ?? String.Empty).TrimStart('#').ToUpperInvariant();
                    static String? Prop(JsonElement e, String n) =>
                        e.TryGetProperty(n, out var v) ? v.GetString() : null;
                    // Accent1=primary, Accent2=secondary, Accent3=accent, Accent4=foreground, Accent5=card, Accent6=muted
                    var accent1 = Strip(Prop(root, "primary"));
                    var accent2 = Strip(Prop(root, "secondary"));
                    var accent3 = Strip(Prop(root, "accent"));
                    var accent4 = Strip(Prop(root, "foreground"));
                    var accent5 = Strip(Prop(root, "card"));
                    var accent6 = Strip(Prop(root, "muted"));
                    if (!accent1.IsNullOrEmpty())
                        return [accent1, accent2.IsNullOrEmpty() ? accent1 : accent2,
                                accent3.IsNullOrEmpty() ? accent1 : accent3,
                                accent4.IsNullOrEmpty() ? "374151" : accent4,
                                accent5.IsNullOrEmpty() ? "F8FAFC" : accent5,
                                accent6.IsNullOrEmpty() ? "F1F5F9" : accent6];
                }
                catch { }
            }
        }
#endif
        return GetBuiltin(theme);
    }

    private static String[] GetBuiltin(String? theme) => (theme?.ToLowerInvariant() ?? String.Empty) switch
    {
        "blue" => ["2563EB", "1D4ED8", "60A5FA", "93C5FD", "1E40AF", "DBEAFE"],
        "dark" => ["6366F1", "4F46E5", "818CF8", "A5B4FC", "3730A3", "E0E7FF"],
        "corporate" => ["374151", "1F2937", "6B7280", "9CA3AF", "111827", "F3F4F6"],
        "warm" => ["EA580C", "C2410C", "FB923C", "FED7AA", "9A3412", "FFF7ED"],
        "green" => ["16A34A", "15803D", "4ADE80", "BBF7D0", "14532D", "DCFCE7"],
        "minimal" => ["18181B", "27272A", "71717A", "A1A1AA", "09090B", "FAFAFA"],
        "ocean" => ["0EA5E9", "0284C7", "38BDF8", "7DD3FC", "0369A1", "BAE6FD"],
        "sunset" => ["F97316", "C026D3", "FB923C", "E879F9", "7C3AED", "F0ABFC"],
        "forest" => ["059669", "065F46", "34D399", "A7F3D0", "064E3B", "D1FAE5"],
        "slate" => ["64748B", "475569", "94A3B8", "CBD5E1", "1E293B", "F8FAFC"],
        "amber" => ["F59E0B", "D97706", "FCD34D", "FDE68A", "92400E", "FEF3C7"],
        _ => ["2563EB", "1D4ED8", "60A5FA", "93C5FD", "1E40AF", "DBEAFE"],
    };

    /// <summary>获取主题默认幻灯片背景色（从 JSON background 字段读取，无 # 前缀）</summary>
    public static String GetBackground(String? theme)
    {
#if STARCHAT
        if (!theme.IsNullOrEmpty())
        {
            var cardStyle = NewLife.StarChat.Entity.CardStyle.GetByKey(theme!);
            if (cardStyle?.Enable == true && !cardStyle.ThemeColors.IsNullOrEmpty())
            {
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(cardStyle.ThemeColors!);
                    static String? PropBg(JsonElement e, String n) =>
                        e.TryGetProperty(n, out var v) ? v.GetString() : null;
                    var bg = PropBg(doc.RootElement, "background");
                    if (!bg.IsNullOrEmpty()) return bg!.TrimStart('#').ToUpperInvariant();
                }
                catch { }
            }
        }
#endif
        return (theme?.ToLowerInvariant() ?? String.Empty) switch
        {
            "dark" or "ocean" or "sunset" or "forest" or "slate" or "amber" => "0F172A",
            _ => "FFFFFF",
        };
    }

    /// <summary>获取主题主色（Accent1，无 # 前缀）</summary>
    public static String GetPrimary(String? theme) => Get(theme)[0];

    /// <summary>获取主题浅色（Accent6，用于斑马纹/卡片背景，无 # 前缀）</summary>
    public static String GetLight(String? theme) => Get(theme)[5];
}

/// <summary>show_slide 工具结果 JSON 结构</summary>
public sealed record SlideResult(
    String SlideId,
    String Title,
    Int32 SlideCount,
    String DownloadUrl,
    Int64 AttachmentId,
    Int64 FileSize,
    String[] SlideTitles,
    String? Theme);
