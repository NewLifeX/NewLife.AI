using System.Text.Json;
using NewLife.ChatAI.Helpers;

namespace NewLife.ChatAI.Models;

/// <summary>办公文档调色板。由卡片风格主题（13 色槽 + 字体对 + 图表色）解析而来，为 Word/Excel/PPT 生成器提供统一的品牌视觉</summary>
/// <remarks>
/// 与 <see cref="ThemeColors"/> 的区别：ThemeColors 只映射 6 个强调色供 PPT 使用；
/// 本类消费完整 13 色槽，完成"纸面适配"（深色主题的浅色文字/深色块映射为白纸底可读的中性色），
/// 并把字体对的 Web 字体栈映射为 Office 字体对（仅用 Windows 标配字体，不依赖第三方安装）。
/// </remarks>
public class OfficePalette
{
    #region 色槽
    /// <summary>品牌主色。用于标题、表头底、装饰线</summary>
    public String Primary { get; set; } = "2563EB";

    /// <summary>主色上的文字色（表头白字等）</summary>
    public String OnPrimary { get; set; } = "FFFFFF";

    /// <summary>辅助色</summary>
    public String Secondary { get; set; } = "1D4ED8";

    /// <summary>辅助色上的文字色</summary>
    public String OnSecondary { get; set; } = "FFFFFF";

    /// <summary>强调色（高亮/图表）</summary>
    public String Accent { get; set; } = "60A5FA";

    /// <summary>强调色上的文字色</summary>
    public String OnAccent { get; set; } = "FFFFFF";

    /// <summary>页面背景色</summary>
    public String Background { get; set; } = "FFFFFF";

    /// <summary>正文文字色</summary>
    public String Foreground { get; set; } = "374151";

    /// <summary>卡片背景色</summary>
    public String Card { get; set; } = "FFFFFF";

    /// <summary>卡片文字色</summary>
    public String CardForeground { get; set; } = "374151";

    /// <summary>柔和底（斑马纹/浅色块）</summary>
    public String Muted { get; set; } = "DBEAFE";

    /// <summary>次要文字色</summary>
    public String MutedForeground { get; set; } = "6B7280";

    /// <summary>边框色</summary>
    public String Border { get; set; } = "D1D5DB";
    #endregion

    #region 字体
    /// <summary>西文字体（Office 字体名，如 Segoe UI）</summary>
    public String LatinFont { get; set; } = "Segoe UI";

    /// <summary>中文字体（Office 字体名，如 微软雅黑）</summary>
    public String ChineseFont { get; set; } = "微软雅黑";

    /// <summary>等宽字体（代码块西文，如 Consolas）</summary>
    public String MonoFont { get; set; } = "Consolas";
    #endregion

    #region 派生
    /// <summary>是否深色主题（背景亮度低于阈值）</summary>
    public Boolean IsDark { get; private set; }

    /// <summary>图表系列色。未显式指定颜色的系列按序回退使用</summary>
    public String[] SeriesColors { get; set; } = [];

    /// <summary>纸面正文色。深色主题的浅色正文映射为深灰，保证白纸上的可读性</summary>
    public String PaperForeground => OfficeStyleKit.Luma(Foreground) > 190 ? "262626" : Foreground;

    /// <summary>纸面次要文字色</summary>
    public String PaperMutedForeground => OfficeStyleKit.Luma(MutedForeground) > 190 ? "6B7280" : MutedForeground;

    /// <summary>纸面斑马纹。深色块映射为浅灰</summary>
    public String PaperMuted => OfficeStyleKit.Luma(Muted) < 150 ? "F7F7F8" : Muted;

    /// <summary>纸面边框。过深或过浅的边框色映射为中性浅灰</summary>
    public String PaperBorder
    {
        get
        {
            var luma = OfficeStyleKit.Luma(Border);
            return luma < 170 || luma > 240 ? "E0E0E0" : Border;
        }
    }

    /// <summary>纸面卡片底色。深色卡片映射为白色</summary>
    public String PaperCard => OfficeStyleKit.Luma(Card) < 150 ? "FFFFFF" : Card;
    #endregion

    #region 解析
    /// <summary>解析主题为办公文档调色板</summary>
    /// <param name="theme">卡片风格 Key（如 enterprise-blue）或内置主题名（如 blue/dark），空或不可识别时用默认配色</param>
    /// <returns>办公文档调色板（始终非空）</returns>
    public static OfficePalette Resolve(String? theme)
    {
        var palette = new OfficePalette();
        var parsed = false;

#if STARCHAT
        // 从卡片风格表读取完整 13 色槽 + 字体对 + 图表色
        if (!theme.IsNullOrEmpty())
        {
            var cardStyle = NewLife.StarChat.Entity.CardStyle.GetByKey(theme!);
            if (cardStyle?.Enable == true)
            {
                parsed = palette.LoadThemeColors(cardStyle.ThemeColors);
                palette.LoadFontPairing(cardStyle.FontPairing);
                palette.LoadChartColors(cardStyle.ChartColors);
            }
        }
#endif

        // 未命中卡片风格（或 NAI 独立构建）：从内置 6 色强调色板映射
        if (!parsed)
        {
            var accents = ThemeColors.Get(theme);
            if (accents.Length >= 6)
            {
                palette.Primary = accents[0];
                palette.Secondary = accents[1];
                palette.Accent = accents[2];
                palette.Muted = accents[5];
            }
            palette.Background = ThemeColors.GetBackground(theme);
        }

        if (palette.SeriesColors.Length == 0)
            palette.SeriesColors = [palette.Primary, palette.Accent, palette.Secondary, "8B5CF6", "10B981", "F59E0B"];

        palette.IsDark = OfficeStyleKit.Luma(palette.Background) < 128;

        // 深底深字兜底：内置主题只提供背景与强调色，文字槽仍是浅色主题的深灰默认值
        if (palette.IsDark)
        {
            if (OfficeStyleKit.Luma(palette.Foreground) < 128) palette.Foreground = "F1F5F9";
            if (OfficeStyleKit.Luma(palette.MutedForeground) < 128) palette.MutedForeground = "94A3B8";
        }

        return palette;
    }

    /// <summary>加载 13 色槽 JSON。返回是否至少解析出主色</summary>
    private Boolean LoadThemeColors(String? json)
    {
        if (json.IsNullOrEmpty()) return false;
        try
        {
            using var doc = JsonDocument.Parse(json!);
            var root = doc.RootElement;
            String? Prop(String name) => root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

            var primary = Prop("primary");
            if (primary.IsNullOrEmpty()) return false;

            Primary = OfficeStyleKit.NormalizeColor(primary, Primary);
            OnPrimary = OfficeStyleKit.NormalizeColor(Prop("onPrimary"), OnPrimary);
            Secondary = OfficeStyleKit.NormalizeColor(Prop("secondary"), Secondary);
            OnSecondary = OfficeStyleKit.NormalizeColor(Prop("onSecondary"), OnSecondary);
            Accent = OfficeStyleKit.NormalizeColor(Prop("accent"), Accent);
            OnAccent = OfficeStyleKit.NormalizeColor(Prop("onAccent"), OnAccent);
            Background = OfficeStyleKit.NormalizeColor(Prop("background"), Background);
            Foreground = OfficeStyleKit.NormalizeColor(Prop("foreground"), Foreground);
            Card = OfficeStyleKit.NormalizeColor(Prop("card"), Card);
            CardForeground = OfficeStyleKit.NormalizeColor(Prop("cardForeground"), CardForeground);
            Muted = OfficeStyleKit.NormalizeColor(Prop("muted"), Muted);
            MutedForeground = OfficeStyleKit.NormalizeColor(Prop("mutedForeground"), MutedForeground);
            Border = OfficeStyleKit.NormalizeColor(Prop("border"), Border);
            return true;
        }
        catch { return false; }
    }

    /// <summary>加载字体对 JSON（heading/body/data = "字体名/字重"），映射为 Office 字体对</summary>
    private void LoadFontPairing(String? json)
    {
        if (json.IsNullOrEmpty()) return;
        try
        {
            using var doc = JsonDocument.Parse(json!);
            var root = doc.RootElement;
            if (root.TryGetProperty("body", out var body) && body.ValueKind == JsonValueKind.String)
            {
                var (latin, chinese) = MapFont(body.GetString());
                LatinFont = latin;
                ChineseFont = chinese;
            }
        }
        catch { }
    }

    /// <summary>加载图表色 JSON（series 数组）</summary>
    private void LoadChartColors(String? json)
    {
        if (json.IsNullOrEmpty()) return;
        try
        {
            using var doc = JsonDocument.Parse(json!);
            var root = doc.RootElement;
            if (root.TryGetProperty("series", out var series) && series.ValueKind == JsonValueKind.Array)
            {
                var list = new List<String>();
                foreach (var item in series.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.String) continue;
                    var color = OfficeStyleKit.TryNormalizeColor(item.GetString());
                    if (color != null) list.Add(color);
                }
                if (list.Count > 0) SeriesColors = list.ToArray();
            }
        }
        catch { }
    }

    /// <summary>Web 字体栈映射为 Office 字体对（中英文分离）。衬线族映射宋体+Georgia，其余映射微软雅黑+Segoe UI</summary>
    private static (String Latin, String Chinese) MapFont(String? pairing)
    {
        var name = pairing.IsNullOrEmpty() ? null : pairing!.Split('/')[0].Trim();
        if (name != null)
        {
            if (name.Contains("Mono", StringComparison.OrdinalIgnoreCase))
                return ("Consolas", "微软雅黑");
            if (name.Contains("Serif", StringComparison.OrdinalIgnoreCase) || name.Contains("Song", StringComparison.OrdinalIgnoreCase)
                || name.EqualIgnoreCase("Georgia") || name.EqualIgnoreCase("Times New Roman"))
                return ("Georgia", "宋体");
        }
        return ("Segoe UI", "微软雅黑");
    }
    #endregion
}
