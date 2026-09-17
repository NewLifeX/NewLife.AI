using NewLife.ChatAI.Models;
using NewLife.Collections;
using NewLife.Office.Excel;

namespace NewLife.ChatAI.Helpers;

/// <summary>办公文档样式工具。把 <see cref="OfficePalette"/> 转换为 Word 样式表 / Excel 单元格样式 / 颜色决策</summary>
/// <remarks>
/// 设计原则：Word/Excel 保持白纸底 + 品牌色点缀（打印友好），颜色统一走调色板的 Paper* 适配属性；
/// AI 显式传入的样式参数始终优先于本套默认样式，本套样式只负责兜底。
/// </remarks>
public static class OfficeStyleKit
{
    #region Word
    /// <summary>生成 Word 品牌样式表 XML（word/styles.xml）</summary>
    /// <param name="palette">办公文档调色板</param>
    /// <returns>完整 styles.xml 字符串，注入 WordWriter.StylesXml</returns>
    /// <remarks>
    /// 包含 docDefaults（字体/字号/行距/段后/正文色）、Normal、Heading1~6（品牌配色与间距阶梯）、
    /// TableGrid（纸面边框色）。必须自带全部被引用 styleId，否则段落样式引用会断裂。
    /// </remarks>
    public static String BuildWordStylesXml(OfficePalette palette)
    {
        if (palette == null) throw new ArgumentNullException(nameof(palette));

        const String W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var sb = Pool.StringBuilder.Get();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sb.Append($"<w:styles xmlns:w=\"{W}\">");
        // 默认排版：西文与中文字体分离，五号字（10.5pt），1.3 倍行距，段后 6pt，正文用纸面适配色
        sb.Append("<w:docDefaults><w:rPrDefault><w:rPr>");
        var latin = EscapeXml(palette.LatinFont);
        var chinese = EscapeXml(palette.ChineseFont);
        sb.Append($"<w:rFonts w:ascii=\"{latin}\" w:hAnsi=\"{latin}\" w:eastAsia=\"{chinese}\"/>");
        sb.Append("<w:sz w:val=\"21\"/>");
        sb.Append($"<w:color w:val=\"{palette.PaperForeground}\"/>");
        sb.Append("</w:rPr></w:rPrDefault><w:pPrDefault><w:pPr>");
        sb.Append("<w:spacing w:line=\"312\" w:lineRule=\"auto\" w:after=\"120\"/>");
        sb.Append("</w:pPr></w:pPrDefault></w:docDefaults>");
        sb.Append("<w:style w:type=\"paragraph\" w:default=\"1\" w:styleId=\"Normal\"><w:name w:val=\"Normal\"/></w:style>");
        // 标题阶梯：1~3 级品牌主色、4~6 级正文深灰；段前段后递减间距 + keepNext 防孤行
        var colors = new[] { palette.Primary, palette.Primary, palette.Primary, palette.PaperForeground, palette.PaperForeground, palette.PaperMutedForeground };
        var sizes = new[] { 40, 32, 28, 26, 24, 22 };
        var befores = new[] { 340, 300, 260, 240, 200, 160 };
        var afters = new[] { 140, 120, 100, 80, 60, 60 };
        for (var i = 1; i <= 6; i++)
        {
            sb.Append($"<w:style w:type=\"paragraph\" w:styleId=\"Heading{i}\"><w:name w:val=\"heading {i}\"/><w:basedOn w:val=\"Normal\"/><w:pPr><w:keepNext/><w:spacing w:before=\"{befores[i - 1]}\" w:after=\"{afters[i - 1]}\"/><w:outlineLvl w:val=\"{i - 1}\"/></w:pPr><w:rPr><w:b/><w:color w:val=\"{colors[i - 1]}\"/><w:sz w:val=\"{sizes[i - 1]}\"/></w:rPr></w:style>");
        }
        sb.Append("<w:style w:type=\"table\" w:styleId=\"TableGrid\"><w:name w:val=\"Table Grid\"/>");
        sb.Append("<w:tblPr><w:tblBorders>");
        foreach (var edge in new[] { "top", "left", "bottom", "right", "insideH", "insideV" })
        {
            sb.Append($"<w:{edge} w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"{palette.PaperBorder}\"/>");
        }
        sb.Append("</w:tblBorders></w:tblPr></w:style>");
        sb.Append("</w:styles>");
        return sb.Return(true);
    }

    /// <summary>创建 Word 表格品牌样式（表头品牌底色 + 反色文字 + 浅色边框 + 斑马纹）</summary>
    /// <param name="palette">办公文档调色板</param>
    /// <returns>Word 表格样式</returns>
    public static NewLife.Office.Word.TableStyle CreateWordTableStyle(OfficePalette palette) => new()
    {
        BorderColor = palette.PaperBorder,
        BorderSize = 4,
        HeaderBgColor = palette.Primary,
        HeaderFontColor = palette.OnPrimary,
        HeaderBold = true,
        StripeColor = palette.PaperMuted,
    };
    #endregion

    #region Excel
    /// <summary>创建 Excel 表头单元格样式（品牌底 + 反色文字 + 居中）</summary>
    /// <param name="palette">办公文档调色板</param>
    /// <returns>表头单元格样式</returns>
    public static CellFormat CreateExcelHeaderFormat(OfficePalette palette) => new()
    {
        Bold = true,
        FontName = palette.ChineseFont,
        FontSize = 11,
        FontColor = palette.OnPrimary,
        BackgroundColor = palette.Primary,
        HAlign = HorizontalAlignment.Center,
        VAlign = VerticalAlignment.Center,
        Border = BorderStyle.Thin,
        BorderColor = palette.PaperBorder,
    };

    /// <summary>创建 Excel 数据单元格样式（垂直居中 + 浅色边框；stripe=true 时叠加斑马纹底色）</summary>
    /// <param name="palette">办公文档调色板</param>
    /// <param name="stripe">是否斑马纹行</param>
    /// <param name="numberFormat">数字格式（可选）</param>
    /// <returns>数据单元格样式</returns>
    public static CellFormat CreateExcelDataFormat(OfficePalette palette, Boolean stripe = false, String? numberFormat = null)
    {
        var fmt = new CellFormat
        {
            FontName = palette.ChineseFont,
            FontSize = 10.5,
            VAlign = VerticalAlignment.Center,
            Border = BorderStyle.Thin,
            BorderColor = palette.PaperBorder,
            NumberFormat = numberFormat,
        };
        if (stripe) fmt.BackgroundColor = palette.PaperMuted;
        return fmt;
    }

    /// <summary>估算列宽（字符数）。综合表头与数据行的最大显示宽度，中文/全角按 2 字符计，限制在 8~50</summary>
    /// <param name="headers">表头数组</param>
    /// <param name="rows">数据行（可空）</param>
    /// <returns>与列数等长的列宽数组</returns>
    public static Int32[] EstimateColumnWidths(String[] headers, String[][]? rows)
    {
        var n = headers.Length;
        var widths = new Double[n];
        for (var i = 0; i < n; i++)
            widths[i] = DisplayWidth(headers[i]);
        if (rows != null)
        {
            foreach (var row in rows)
            {
                if (row == null) continue;
                for (var i = 0; i < n && i < row.Length; i++)
                {
                    var w = DisplayWidth(row[i]);
                    if (w > widths[i]) widths[i] = w;
                }
            }
        }
        var result = new Int32[n];
        for (var i = 0; i < n; i++)
        {
            var w = (Int32)(widths[i] + 3);   // 单元格内边距
            result[i] = w < 8 ? 8 : w > 50 ? 50 : w;
        }
        return result;
    }

    /// <summary>按字符宽度估算字符串显示宽度（中文/全角按 2 计）</summary>
    private static Double DisplayWidth(String? text)
    {
        if (text.IsNullOrEmpty()) return 0;
        var w = 0d;
        foreach (var ch in text!)
            w += ch > 0x7F ? 2 : 1;
        return w;
    }
    #endregion

    #region 颜色工具
    /// <summary>标准化颜色值为 6 位大写十六进制（去掉 # 前缀）；无效时返回 fallback</summary>
    /// <param name="hex">颜色值（可带 # 前缀）</param>
    /// <param name="fallback">无效时的回退值</param>
    /// <returns>6 位大写十六进制颜色</returns>
    public static String NormalizeColor(String? hex, String fallback) => TryNormalizeColor(hex) ?? fallback;

    /// <summary>尝试标准化颜色值；无法识别时返回 null</summary>
    /// <param name="hex">颜色值（可带 # 前缀）</param>
    /// <returns>6 位大写十六进制颜色或 null</returns>
    public static String? TryNormalizeColor(String? hex)
    {
        if (hex.IsNullOrEmpty()) return null;
        var s = hex!.Trim().TrimStart('#').Trim();
        if (s.Length != 6) return null;
        foreach (var ch in s)
        {
            if (!Uri.IsHexDigit(ch)) return null;
        }
        return s.ToUpperInvariant();
    }

    /// <summary>计算颜色亮度（0~255，ITU-R BT.601 加权）；无效颜色返回中值 128</summary>
    /// <param name="hex">颜色值</param>
    /// <returns>亮度值</returns>
    public static Int32 Luma(String? hex)
    {
        var s = TryNormalizeColor(hex);
        if (s == null) return 128;
        var r = Convert.ToInt32(s[..2], 16);
        var g = Convert.ToInt32(s[2..4], 16);
        var b = Convert.ToInt32(s[4..6], 16);
        return (r * 299 + g * 587 + b * 114) / 1000;
    }

    /// <summary>判断颜色是否为深色（亮度低于阈值）</summary>
    /// <param name="hex">颜色值</param>
    /// <param name="threshold">亮度阈值（默认 128）</param>
    /// <returns>是否深色</returns>
    public static Boolean IsDarkColor(String? hex, Int32 threshold = 128) => Luma(hex) < threshold;

    /// <summary>获取提示框（callout）的背景色与左边框色（语义柔和色板）</summary>
    /// <param name="variant">变体：info/success/warning/danger</param>
    /// <returns>（背景色, 边框色）</returns>
    public static (String Background, String Border) GetCalloutColors(String? variant) => (variant?.ToLowerInvariant()) switch
    {
        "success" => ("F0FDF4", "22C55E"),
        "warning" => ("FFFBEB", "F59E0B"),
        "danger" => ("FEF2F2", "EF4444"),
        _ => ("EFF6FF", "3B82F6"),
    };

    /// <summary>XML 属性与文本转义（字体名等可能来自管理端配置，防止破坏样式表结构）</summary>
    /// <param name="value">原始文本</param>
    /// <returns>转义后的文本</returns>
    private static String EscapeXml(String value) => value
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    #endregion

    #region 主题解析
    /// <summary>解析生效主题：显式传参优先，其次会话卡片风格（StarChat），最后返回 null 由调用方走品牌默认</summary>
    /// <param name="theme">AI 显式传入的主题 Key</param>
    /// <param name="conversationId">会话编号（字符串形式，可空）</param>
    /// <returns>生效主题 Key 或 null</returns>
    public static String? ResolveTheme(String? theme, String? conversationId)
    {
        if (!theme.IsNullOrEmpty()) return theme;

#if STARCHAT
        var convId = conversationId.ToLong();
        if (convId > 0)
        {
            var conversation = NewLife.StarChat.Entity.Conversation.FindById(convId);
            if (conversation?.CardStyle.IsNullOrEmpty() == false) return conversation.CardStyle;
        }
#endif
        return null;
    }
    #endregion
}
