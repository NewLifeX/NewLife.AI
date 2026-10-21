using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using NewLife;
using NewLife.AI.Tools;
using Xunit;

namespace XUnitTest.Tools;

/// <summary>触发词匹配器测试。覆盖双语匹配、英文词边界与未翻译回退</summary>
[Collection("Localization")]
[DisplayName("触发词匹配")]
public class TriggerMatcherTests : IDisposable
{
    private static CultureInfo? _previous;

    public TriggerMatcherTests() => _previous = NewLife.Localization.Culture;

    public void Dispose() => NewLife.Localization.Culture = _previous;

    [Fact]
    [DisplayName("中文消息命中英文触发词的译文")]
    public void IsMatched_ChineseContent_HitsTranslatedTrigger()
    {
        NewLife.Localization.Culture = new CultureInfo("zh-Hans");

        Assert.True(TriggerMatcher.IsMatched("今天天气怎么样", "weather,forecast"));
        Assert.True(TriggerMatcher.IsMatched("帮我翻译这段", "translate,translate this"));
        Assert.True(TriggerMatcher.IsMatched("画一个柱状图", "bar chart,line chart,pie chart,chart"));
    }

    [Fact]
    [DisplayName("扩充后的触发词短语在中文下可命中")]
    public void IsMatched_ExpandedTerms_HitInChinese()
    {
        NewLife.Localization.Culture = new CultureInfo("zh-Hans");

        Assert.True(TriggerMatcher.IsMatched("给我一个工作流", "workflow"));
        Assert.True(TriggerMatcher.IsMatched("用泳道展示任务", "swimlane"));
        Assert.True(TriggerMatcher.IsMatched("帮我画一张图", "draw a diagram,diagram"));
        Assert.True(TriggerMatcher.IsMatched("按省份分布展示", "province"));
        Assert.True(TriggerMatcher.IsMatched("把这份报告整理一下", "report"));
        Assert.True(TriggerMatcher.IsMatched("查一下数据库表", "database tables"));
    }

    [Fact]
    [DisplayName("词条登记多个同义词时任一命中即可")]
    public void IsMatched_SynonymTerms_AnyHits()
    {
        NewLife.Localization.Culture = new CultureInfo("zh-Hans");

        // trigger:timeline 登记为"时间轴|时间线"，两种说法都应命中
        Assert.True(TriggerMatcher.IsMatched("画一条时间线", "timeline"));
        Assert.True(TriggerMatcher.IsMatched("列出时间轴", "timeline"));
        // trigger:mind map 登记为"思维导图|脑图"
        Assert.True(TriggerMatcher.IsMatched("生成一个脑图", "mind map"));
        // trigger:word document 登记为"文档|报告"
        Assert.True(TriggerMatcher.IsMatched("帮我写报告", "word document"));
    }

    [Fact]
    [DisplayName("中文消息里的英文专名直接命中源文")]
    public void IsMatched_ChineseContent_HitsLatinProperNoun()
    {
        Assert.True(TriggerMatcher.IsMatched("帮我执行SQL", "sql,run sql"));
        Assert.True(TriggerMatcher.IsMatched("我的IP在哪", "ip,ip location"));
        Assert.True(TriggerMatcher.IsMatched("生成PPT", "ppt,powerpoint"));
        // 词边界同样适用于专名：zip / mysql 不应命中
        Assert.False(TriggerMatcher.IsMatched("unzip this file", "ip,ip location"));
        Assert.False(TriggerMatcher.IsMatched("mysql is slow", "sql,run sql"));
    }

    [Fact]
    [DisplayName("英文消息命中源文触发词")]
    public void IsMatched_EnglishContent_HitsSourceTrigger()
    {
        Assert.True(TriggerMatcher.IsMatched("What's the weather like today?", "weather,forecast"));
        Assert.True(TriggerMatcher.IsMatched("please build excel report", "build excel,excel sheet,spreadsheet"));
    }

    [Fact]
    [DisplayName("英文触发词按词边界匹配，不命中单词片段")]
    public void IsMatched_EnglishContent_RespectsWordBoundary()
    {
        // chart 不应命中 charter，map 不应命中 mapping
        Assert.False(TriggerMatcher.IsMatched("sign the charter", "chart"));
        Assert.False(TriggerMatcher.IsMatched("data mapping task", "map annotation"));
        Assert.False(TriggerMatcher.IsMatched("drawing board", "draw"));

        // 同一规则下整词仍应命中
        Assert.True(TriggerMatcher.IsMatched("show me a chart", "chart"));
        Assert.True(TriggerMatcher.IsMatched("draw a diagram please", "draw"));
    }

    [Fact]
    [DisplayName("触发词匹配大小写不敏感")]
    public void IsMatched_IgnoresCase()
    {
        Assert.True(TriggerMatcher.IsMatched("WEATHER FORECAST", "weather"));
        Assert.True(TriggerMatcher.IsMatched("Kanban Board", "kanban"));
    }

    [Fact]
    [DisplayName("未登记译文的触发词只按源文匹配")]
    public void IsMatched_UnregisteredTerm_FallsBackToSource()
    {
        NewLife.Localization.Culture = new CultureInfo("zh-Hans");

        // no-such-trigger-term 没有词条，中文消息不应命中
        Assert.False(TriggerMatcher.IsMatched("完全无关的一句话", "no-such-trigger-term"));
        Assert.True(TriggerMatcher.IsMatched("no-such-trigger-term", "no-such-trigger-term"));
    }

    [Fact]
    [DisplayName("空内容或空触发词返回不命中")]
    public void IsMatched_EmptyInput_ReturnsFalse()
    {
        Assert.False(TriggerMatcher.IsMatched(null, "weather"));
        Assert.False(TriggerMatcher.IsMatched("", "weather"));
        Assert.False(TriggerMatcher.IsMatched("今天天气", null));
        Assert.False(TriggerMatcher.IsMatched("今天天气", " , ， "));
    }

    [Fact]
    [DisplayName("触发词拆分去除空白项")]
    public void Split_IgnoresBlankItems()
    {
        var words = TriggerMatcher.Split(" weather ,， forecast , ");

        Assert.Equal(2, words.Length);
        Assert.Contains("weather", words);
        Assert.Contains("forecast", words);
    }

    [Fact]
    [DisplayName("已登记触发词词条在简体中文下有译文")]
    public void TermKeys_HasChineseTranslation()
    {
        NewLife.Localization.Culture = new CultureInfo("zh-Hans");

        var missing = TriggerMatcher.TermKeys
            .Select(e => e.Split(':')[1])
            .Where(e => TriggerMatcher.TranslateTerm(e) == null)
            .ToList();

        Assert.Empty(missing);
    }
}
