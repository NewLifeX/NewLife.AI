using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using NewLife;
using L10n = NewLife.Localization;
using Xunit;

namespace XUnitTest.Localization;

/// <summary>本地化测试集合。Localization.Culture 是进程级全局状态，禁用并行避免相互干扰</summary>
[CollectionDefinition("Localization", DisableParallelization = true)]
public class LocalizationCollection { }

/// <summary>运行时多语言测试。覆盖简体/繁体中文翻译、占位符格式化、未注册语言回退与 31 种语言表注册</summary>
[Collection("Localization")]
public class LocalizationTests
{
    private static readonly String[] _cultures =
    [
        "zh-Hans", "zh-Hant", "es", "de", "ja", "fr", "pt", "ru", "ko", "ar",
        "bn", "cs", "da", "el", "fa", "fi", "he", "hi", "hu", "id",
        "it", "ms", "nb", "nl", "pl", "ro", "sv", "th", "tr", "uk", "vi",
    ];

    private static T WithCulture<T>(String name, Func<T> func)
    {
        var old = L10n.Culture;
        try
        {
            L10n.Culture = new CultureInfo(name);
            return func();
        }
        finally
        {
            L10n.Culture = old;
        }
    }

    [Fact]
    [DisplayName("简体中文词表翻译英文源文")]
    public void Chinese_Translate()
        => Assert.Equal("消息列表不能为空", WithCulture("zh-CN", () => L10n.Translate("Message list cannot be empty")));

    [Fact]
    [DisplayName("繁体中文词表翻译英文源文")]
    public void TraditionalChinese_Translate()
    {
        var text = WithCulture("zh-TW", () => L10n.Translate("Agent list cannot be empty"));
        Assert.DoesNotContain("Agent list", text);
        Assert.NotEqual("代理列表不能为空", text);
    }

    [Fact]
    [DisplayName("占位符按目标语言格式化")]
    public void Placeholder_Formatted()
        => Assert.Equal(
            "音色 'v1' 不在模型 'qwen' 的合法音色列表中",
            WithCulture("zh-CN", () => L10n.Translate("Voice '{0}' is not in the allowed voice list of model '{1}'", "v1", "qwen")));

    [Fact]
    [DisplayName("未注册语言回退英文源文")]
    public void UnsupportedCulture_FallsBackToEnglish()
        => Assert.Equal("Message list cannot be empty", WithCulture("sw-KE", () => L10n.Translate("Message list cannot be empty")));

    [Fact]
    [DisplayName("31 种语言词表均已注册")]
    public void AllLocales_Registered()
    {
        foreach (var code in _cultures)
        {
            Assert.True(L10n.IsSupported(new CultureInfo(code)), $"缺少语言表：{code}");
        }
    }

    [Fact]
    [DisplayName("Accept-Language 选择权重最高的受支持语言")]
    public void ParseAcceptLanguage_PicksSupported()
    {
        var culture = RequestCultureScope.ParseAcceptLanguage("fr-FR;q=0.5, ja-JP;q=0.9, en;q=0.8");
        Assert.NotNull(culture);
        Assert.Equal("ja", culture!.TwoLetterISOLanguageName);
        Assert.Null(RequestCultureScope.ParseAcceptLanguage("sw-KE"));
    }

    [Fact]
    [DisplayName("请求语言作用域切换并还原 UI 文化")]
    public void RequestCultureScope_SwitchesAndRestores()
    {
        var old = L10n.Culture;
        L10n.Culture = null;
        var saved = CultureInfo.CurrentUICulture;
        try
        {
            using (var _ = RequestCultureScope.Create("de-DE"))
            {
                Assert.Equal("de", CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
            }
            Assert.Equal(saved, CultureInfo.CurrentUICulture);
        }
        finally
        {
            L10n.Culture = old;
            CultureInfo.CurrentUICulture = saved;
        }
    }

    [Fact]
    [DisplayName("请求语言作用域在并行异步流中互不串扰")]
    public async Task RequestCultureScope_IsolatesParallelFlows()
    {
        var old = L10n.Culture;
        var saved = CultureInfo.CurrentUICulture;
        L10n.Culture = null;
        try
        {
            var pairs = new (String Accept, String Lang)[]
            {
                ("de-DE", "de"), ("zh-CN", "zh"), ("ja-JP", "ja"), ("fr-FR", "fr"), ("ru-RU", "ru"), ("ko-KR", "ko"),
            };

            var tasks = pairs.Select(async pair =>
            {
                using var _ = RequestCultureScope.Create(pair.Accept);
                await Task.Delay(20);
                var before = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
                await Task.Yield();
                var after = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
                return before == pair.Lang && after == pair.Lang;
            });

            var results = await Task.WhenAll(tasks);
            Assert.All(results, ok => Assert.True(ok, "并行异步流中的 UI 文化发生串扰"));
        }
        finally
        {
            L10n.Culture = old;
            CultureInfo.CurrentUICulture = saved;
        }
    }

    [Fact]
    [DisplayName("宿主固定语言时请求语言作用域不覆盖宿主设置")]
    public void RequestCultureScope_HostCultureWins()
    {
        var old = L10n.Culture;
        var saved = CultureInfo.CurrentUICulture;
        try
        {
            L10n.Culture = new CultureInfo("ja");

            using (var _ = RequestCultureScope.Create("de-DE"))
            {
                Assert.NotEqual("de", CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
                Assert.Equal("エージェント一覧を空にすることはできません", L10n.Translate("Agent list cannot be empty"));
            }
        }
        finally
        {
            L10n.Culture = old;
            CultureInfo.CurrentUICulture = saved;
        }
    }
}
