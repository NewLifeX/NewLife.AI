using System;
using System.Linq;

namespace NewLife.ChatAI;

/// <summary>语言代码工具。与前端 Web/src/i18n/languages.ts 及 NewLife.Localization 词表对齐</summary>
/// <remarks>
/// 当前 NewLife.Core 包只公开 <see cref="RequestCultureScope"/>，未公开 SupportedCultures/NormalizeCulture，
/// 因此在此维护同一份语言清单与归一化规则；Core 公开后可直接改为委托。
/// </remarks>
public static class LanguageHelper
{
    /// <summary>支持的语言代码：英文源文 + 31 种译文</summary>
    public static readonly String[] SupportedCultures =
    [
        "zh-CN", "zh-TW", "en-US",
        "ja-JP", "ko-KR", "de-DE", "fr-FR", "es-ES", "pt-BR", "it-IT", "nl-NL", "pl-PL",
        "cs-CZ", "sv-SE", "da-DK", "nb-NO", "fi-FI", "el-GR", "ro-RO", "hu-HU",
        "ru-RU", "uk-UA", "tr-TR",
        "ar-SA", "he-IL", "fa-IR",
        "hi-IN", "bn-BD", "th-TH", "vi-VN", "id-ID", "ms-MY",
    ];

    /// <summary>默认语言</summary>
    public const String DefaultCulture = "zh-CN";

    /// <summary>从右到左书写的语言</summary>
    public static readonly String[] RtlCultures = ["ar-SA", "he-IL", "fa-IR"];

    /// <summary>归一化语言名称。把 en、de-AT、zh-Hant 等映射到受支持的区域语言</summary>
    /// <param name="name">语言名称，可带 Accept-Language 权重（如 en-US,en;q=0.9）</param>
    /// <returns>受支持的语言名称；无法匹配时返回 null</returns>
    public static String? NormalizeCulture(String? name)
    {
        if (name.IsNullOrWhiteSpace()) return null;

        var first = name.Split(',')[0].Trim();
        var p = first.IndexOf(';');
        if (p > 0) first = first.Substring(0, p).Trim();
        if (first.IsNullOrEmpty()) return null;

        if (first.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
        {
            var hant = first.IndexOf("Hant", StringComparison.OrdinalIgnoreCase) >= 0
                || first.EndsWith("-TW", StringComparison.OrdinalIgnoreCase)
                || first.EndsWith("-HK", StringComparison.OrdinalIgnoreCase)
                || first.EndsWith("-MO", StringComparison.OrdinalIgnoreCase);

            return hant ? "zh-TW" : "zh-CN";
        }

        foreach (var item in SupportedCultures)
        {
            if (item.EqualIgnoreCase(first)) return item;
        }

        var lang = first;
        var dash = first.IndexOf('-');
        if (dash > 0) lang = first.Substring(0, dash);
        foreach (var item in SupportedCultures)
        {
            if (item.StartsWith(lang + "-", StringComparison.OrdinalIgnoreCase)) return item;
        }

        return null;
    }

    /// <summary>归一化语言名称，未识别时回退默认语言</summary>
    /// <param name="name">语言名称</param>
    /// <returns>受支持的语言名称</returns>
    public static String NormalizeOrDefault(String? name) => NormalizeCulture(name) ?? DefaultCulture;

    /// <summary>是否从右到左语言</summary>
    /// <param name="name">语言名称</param>
    /// <returns></returns>
    public static Boolean IsRtl(String? name) => RtlCultures.Any(e => e.EqualIgnoreCase(NormalizeCulture(name)));
}
