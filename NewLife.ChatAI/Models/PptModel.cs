using System.ComponentModel;
using System.Text.Json;

namespace NewLife.ChatAI.Models;

/// <summary>单张幻灯片的数据描述</summary>
public class PptPageModel
{
    /// <summary>本页幻灯片标题（用于预览卡片）</summary>
    [Description("本页幻灯片的标题")]
    public String Title { get; set; } = String.Empty;

    /// <summary>布局模式：title_only / title_content / two_column / chart_only / blank</summary>
    [Description("页面布局：title_only（仅标题，封面用）/ title_content（标题+正文，默认）/ two_column（左右两栏）/ chart_only（全幅图表）/ blank（空白画布）")]
    public String? Layout { get; set; }

    /// <summary>背景色（16进制 RGB，如 "1F497D"）。不填则使用主题默认背景</summary>
    [Description("幻灯片背景色，16进制 RGB，如 \"1F497D\"。不填时使用主题默认背景")]
    public String? Background { get; set; }

    /// <summary>元素列表</summary>
    [Description("页面元素列表，每个元素描述一段文字、表格、图表或图片")]
    public PptElement[] Elements { get; set; } = [];

    /// <summary>页脚文本（null 表示不显示）</summary>
    [Description("页脚文本，显示在幻灯片底部，null 表示不显示")]
    public String? Footer { get; set; }

    /// <summary>是否在右下角显示幻灯片序号</summary>
    [Description("是否在右下角显示幻灯片序号，默认 false")]
    public Boolean ShowPageNumber { get; set; }

    /// <summary>演讲者备注</summary>
    [Description("演讲者备注文本，用于演讲时提示")]
    public String? Notes { get; set; }

    /// <summary>切换动画：fade / push / wipe / zoom / split / cut</summary>
    [Description("切换动画：fade（淡入淡出）/ push（推入）/ wipe（擦除）/ zoom（缩放）/ split（分裂）/ cut（直切）")]
    public String? Transition { get; set; }
}

/// <summary>幻灯片元素（文字/表格/图表/图片）</summary>
public class PptElement
{
    /// <summary>元素类型：text / table / chart / image</summary>
    [Description("元素类型：text（文字）/ table（表格）/ chart（图表）/ image（图片）")]
    public String Type { get; set; } = "text";

    /// <summary>语义角色（text 元素）：title / subtitle / body / caption / kpi。布局引擎据此自动定位</summary>
    [Description("语义角色（text 元素专用）：title（标题）/ subtitle（副标题）/ body（正文）/ caption（注释/来源）/ kpi（大数字指标）。布局引擎据此自动定位，无需指定坐标")]
    public String? Role { get; set; }

    /// <summary>文字内容（type=text 时必填）</summary>
    [Description("文字内容（type=text 时必填）")]
    public String? Content { get; set; }

    /// <summary>字号（磅），不填由布局引擎按 role 决定</summary>
    [Description("字号（磅），可选，不填时由布局引擎按 role 自动决定（title≈28、kpi≈44、body≈16）")]
    public Int32? FontSize { get; set; }

    /// <summary>是否粗体</summary>
    [Description("是否加粗，默认 false")]
    public Boolean? Bold { get; set; }

    /// <summary>是否斜体</summary>
    [Description("是否斜体（type=text 时可选），默认 false")]
    public Boolean? Italic { get; set; }

    /// <summary>是否下划线</summary>
    [Description("是否下划线（type=text 时可选），默认 false")]
    public Boolean? Underline { get; set; }

    /// <summary>文字颜色（16进制 RGB，如 "FFFFFF"）</summary>
    [Description("文字颜色，16进制 RGB，如 \"FFFFFF\"。不填时继承主题文字色")]
    public String? Color { get; set; }

    /// <summary>表格列标题（type=table 时必填）</summary>
    [Description("表格列标题数组（type=table 时必填），如 [\"指标\",\"Q1\",\"Q2\",\"环比\"]")]
    public String[]? Headers { get; set; }

    /// <summary>表格数据行（type=table 时必填），每行为一个单元格值数组</summary>
    [Description("表格数据行（type=table 时必填），每行为一个单元格值数组，如 [[\"张三\",\"85\"],[\"李四\",\"92\"]]")]
    public String[][]? Rows { get; set; }

    /// <summary>图表类型（type=chart 时必填）：bar / line / pie</summary>
    [Description("图表类型（type=chart 时必填）：bar（柱状图）/ line（折线图）/ pie（饼图）")]
    public String? ChartType { get; set; }

    /// <summary>分类轴标签（type=chart 时必填）</summary>
    [Description("图表分类标签（type=chart 时必填），如 [\"Q1\",\"Q2\",\"Q3\",\"Q4\"]")]
    public String[]? Categories { get; set; }

    /// <summary>数据系列（type=chart 时必填），每项含 name 和 data</summary>
    [Description("数据系列（type=chart 时必填），每项包含 name（系列名）和 data（数值数组）")]
    public ChartSeries[]? Series { get; set; }

    /// <summary>图片 URL（type=image 时必填）。支持 https:// 外链或 /cube/image?id=xxx 内部附件</summary>
    [Description("图片 URL（type=image 时必填），支持 https:// 外链或 /cube/image?id=xxx 内部附件")]
    public String? Src { get; set; }

    /// <summary>文字对齐（text 元素）：l（左对齐）/ ctr（居中）/ r（右对齐）</summary>
    [Description("文字对齐（text 元素）：l（左对齐）/ ctr（居中）/ r（右对齐）")]
    public String? Alignment { get; set; }

    /// <summary>文本框背景色（text 元素，16进制 RGB，如 \"EFF6FF\"）</summary>
    [Description("文本框背景色（text 元素），16进制 RGB，如 \"EFF6FF\"，为空则透明")]
    public String? BackgroundColor { get; set; }

    /// <summary>富文本片段（text 元素）。有内容时忽略 Content/FontSize/Bold/Color，支持混合格式</summary>
    [Description("富文本片段（text 元素，有内容时忽略 Content/FontSize/Bold/Color）")]
    public TextRun[]? Runs { get; set; }

    /// <summary>基本形状配置（type=shape 时必填）</summary>
    [Description("基本形状配置（type=shape 时必填）")]
    public ShapeInfo? Shape { get; set; }

    /// <summary>表格样式（type=table 时可选，控制表头背景色和斑马纹）</summary>
    [Description("表格样式（type=table 时可选）：headerBgColor 表头背景色、headerFontColor 表头字色、stripeColor 斑马纹色")]
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
    [Description("文本内容")]
    public String Text { get; set; } = String.Empty;

    /// <summary>字号（磅），0 表示继承文本框默认字号</summary>
    [Description("字号（磅），0 表示继承文本框默认字号")]
    public Int32 FontSize { get; set; }

    /// <summary>是否粗体</summary>
    [Description("是否粗体")]
    public Boolean Bold { get; set; }

    /// <summary>是否斜体</summary>
    [Description("是否斜体")]
    public Boolean Italic { get; set; }

    /// <summary>是否下划线</summary>
    [Description("是否下划线")]
    public Boolean Underline { get; set; }

    /// <summary>文字颜色（16进制 RGB，如 \"FF6B6B\"）</summary>
    [Description("文字颜色，16进制 RGB，如 \"FF6B6B\"")]
    public String? Color { get; set; }

    /// <summary>超链接 URL，不为空时点击该片段跳转</summary>
    [Description("超链接 URL，不为空时点击该片段跳转")]
    public String? HyperlinkUrl { get; set; }
}

/// <summary>基本形状配置（type=shape）</summary>
public class ShapeInfo
{
    /// <summary>形状类型：rect / ellipse / roundRect / triangle / diamond / arrow</summary>
    [Description("形状类型：rect（矩形）/ ellipse（椭圆）/ roundRect（圆角矩形）/ triangle（三角形）/ diamond（菱形）/ arrow（箭头）")]
    public String ShapeType { get; set; } = "rect";

    /// <summary>填充色（16进制 RGB），null 表示无填充</summary>
    [Description("填充色（16进制 RGB，如 \"2563EB\"），null 表示无填充")]
    public String? FillColor { get; set; }

    /// <summary>边框颜色（16进制 RGB），null 表示无边框</summary>
    [Description("边框颜色（16进制 RGB），null 表示无边框")]
    public String? LineColor { get; set; }

    /// <summary>形状内文字（可选）</summary>
    [Description("形状内显示的文字（可选）")]
    public String? Text { get; set; }

    /// <summary>文字颜色（16进制 RGB）</summary>
    [Description("形状内文字颜色（16进制 RGB）")]
    public String? FontColor { get; set; }

    /// <summary>文字字号（磅），0 表示默认</summary>
    [Description("形状内文字字号（磅），0 表示默认")]
    public Int32 FontSize { get; set; }
}

/// <summary>表格样式配置</summary>
public class TableStyle
{
    /// <summary>表头行背景色（16进制 RGB，如 \"2563EB\"）</summary>
    [Description("表头行背景色（16进制 RGB，如 \"2563EB\"），不填则无特殊样式")]
    public String? HeaderBgColor { get; set; }

    /// <summary>表头行文字颜色（16进制 RGB，如 \"FFFFFF\"）</summary>
    [Description("表头行文字颜色（16进制 RGB，如 \"FFFFFF\"），不填则继承默认")]
    public String? HeaderFontColor { get; set; }

    /// <summary>斑马纹颜色（偶数数据行背景色，如 \"EFF6FF\"）</summary>
    [Description("斑马纹颜色（偶数数据行背景色，如 \"EFF6FF\" 浅蓝），不填则无斑马纹")]
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
