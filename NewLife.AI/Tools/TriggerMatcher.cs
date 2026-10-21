using System;
using System.Collections.Generic;
using NewLife;

namespace NewLife.AI.Tools;

/// <summary>触发词匹配器。消息内容命中任一触发词即视为激活</summary>
/// <remarks>
/// 触发词以英文短语为源文登记（便于评审，也便于用词表翻译），匹配时同时纳入当前语言的译文，
/// 因此中文用户说"今天天气"与英文用户说"weather forecast"都能命中 weather 词条；
/// 词表没有译文时只按源文匹配，不报错。
/// <para>英文等纯 ASCII 触发词按词边界匹配，避免 map 命中 mapping、chart 命中 charter；
/// 中文等含非 ASCII 字符的触发词按子串匹配，符合中文不带分隔符的习惯。</para>
/// </remarks>
public static class TriggerMatcher
{
    /// <summary>触发词译文的词条前缀。界面文案里有 Weather、Chart 等显示名，加前缀可避免大小写撞名</summary>
    public const String TermPrefix = "trigger:";

    /// <summary>已登记译文的触发词。源文写在工具特性里，这里集中登记词表键，便于核对词表是否漏译</summary>
    public static String[] TermKeys { get; } =
    [
        "trigger:calculate",
        "trigger:web fetch",
        "trigger:web search",
        "trigger:weather",
        "trigger:translate",
        "trigger:word document",
        "trigger:spreadsheet",
        "trigger:slides",
        "trigger:bar chart",
        "trigger:line chart",
        "trigger:pie chart",
        "trigger:chart",
        "trigger:table schema",
        "trigger:kanban",
        "trigger:map annotation",
        "trigger:mind map",
        "trigger:timeline",
        "trigger:milestone",
        "trigger:roadmap",
        "trigger:visualize",
    ];

    /// <summary>查触发词在当前语言下的译文，未登记时返回 null</summary>
    /// <param name="word">触发词源文</param>
    /// <returns>译文，未登记时为 null</returns>
    public static String? TranslateTerm(String word)
    {
        if (word.IsNullOrWhiteSpace()) return null;

        var key = TermPrefix + word;
        var text = Localization.Translate(key);
        return String.Equals(text, key, StringComparison.Ordinal) ? null : text;
    }

    /// <summary>判断消息内容是否命中任一触发词</summary>
    /// <param name="content">待匹配的文本，通常是用户消息或上一轮助手回复</param>
    /// <param name="triggers">逗号分隔的触发词列表，英文短语为源文</param>
    /// <returns>命中任一触发词时返回 true</returns>
    public static Boolean IsMatched(String? content, String? triggers)
    {
        if (content.IsNullOrWhiteSpace() || triggers.IsNullOrWhiteSpace()) return false;

        foreach (var item in triggers.Split([',', '，'], StringSplitOptions.RemoveEmptyEntries))
        {
            var word = item.Trim();
            if (word.IsNullOrEmpty()) continue;

            if (ContainsWord(content!, word)) return true;

            // 当前语言译文一并参与匹配；未登记译文时返回 null，此处自动跳过
            var text = TranslateTerm(word);
            if (!text.IsNullOrEmpty() && ContainsWord(content!, text!))
                return true;
        }

        return false;
    }

    /// <summary>判断文本是否命中单个触发词，含词边界与译文处理</summary>
    /// <param name="content">待匹配的文本</param>
    /// <param name="word">单个触发词</param>
    /// <returns>命中时返回 true</returns>
    public static Boolean ContainsWord(String content, String word)
    {
        if (content.IsNullOrEmpty() || word.IsNullOrWhiteSpace()) return false;

        // 含非 ASCII 字符（中文、日文等）按子串匹配
        if (HasNonAscii(word)) return content.Contains(word, StringComparison.OrdinalIgnoreCase);

        // 纯 ASCII 按词边界匹配：前后紧邻的字符不能是字母、数字或下划线
        var start = 0;
        var last = content.Length - word.Length;
        while (start <= last)
        {
            var idx = content.IndexOf(word, start, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return false;

            var beforeOk = idx == 0 || !IsWordChar(content[idx - 1]);
            var end = idx + word.Length;
            var afterOk = end >= content.Length || !IsWordChar(content[end]);
            if (beforeOk && afterOk) return true;

            start = idx + 1;
        }

        return false;
    }

    /// <summary>把逗号分隔的触发词拆成单词列表，去除空白项</summary>
    /// <param name="triggers">逗号分隔的触发词文本</param>
    /// <returns>触发词数组</returns>
    public static String[] Split(String? triggers)
    {
        if (triggers.IsNullOrWhiteSpace()) return [];

        var list = new List<String>();
        foreach (var item in triggers.Split([',', '，']))
        {
            var word = item.Trim();
            if (!word.IsNullOrEmpty()) list.Add(word);
        }

        return [.. list];
    }

    private static Boolean HasNonAscii(String text)
    {
        foreach (var ch in text)
        {
            if (ch > 0x7F) return true;
        }

        return false;
    }

    private static Boolean IsWordChar(Char ch) => ch == '_' || Char.IsLetterOrDigit(ch);
}
