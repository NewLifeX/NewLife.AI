using System;
using System.Globalization;
using NewLife;
using Xunit;

namespace XUnitTest.I18n;

/// <summary>下游组件多语言词表测试。校验 Locales/*.yml 自动注册与缺失回退</summary>
[System.ComponentModel.DisplayName("多语言词表测试")]
public class LocalizationTests
{
    /* 只改线程/异步上下文的 UI 文化，避免进程级 Localization.Culture 干扰并行用例 */
    private static String WithCulture(String culture, Func<String> action)
    {
        var saved = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo(culture);
            return action();
        }
        finally
        {
            CultureInfo.CurrentUICulture = saved;
        }
    }

    private static void EnsureChatAITables()
    {
        // 调用 ChatAI 方法触发程序集加载，执行源生成器注入的模块初始化（注册词表）
        _ = NewLife.ChatAI.LanguageHelper.NormalizeOrDefault("zh-CN");
    }

    [Fact]
    [System.ComponentModel.DisplayName("ChatAI 词表：简体中文/繁体中文按 UI 文化翻译")]
    public void ChatAIMessages_Translated()
    {
        EnsureChatAITables();

        var zhCn = WithCulture("zh-CN", () => Localization.Translate("Unauthorized. Please sign in."));
        Assert.Equal("未登录，请先登录。", zhCn);

        var zhTw = WithCulture("zh-TW", () => Localization.Translate("Unauthorized. Please sign in."));
        Assert.Equal("未登入，請先登入。", zhTw);

        var body = WithCulture("zh-CN", () => Localization.Translate("Invalid request body."));
        Assert.Equal("请求体格式错误。", body);
    }

    [Fact]
    [System.ComponentModel.DisplayName("ChatAI 词表：无译文语言回退英文源文")]
    public void ChatAIMessages_FallbackToEnglish()
    {
        EnsureChatAITables();

        var text = WithCulture("sw-KE", () => Localization.Translate("Unauthorized. Please sign in."));
        Assert.Equal("Unauthorized. Please sign in.", text);
    }

    [Fact]
    [System.ComponentModel.DisplayName("请求语言：Accept-Language 按权重选择受支持语言")]
    public void RequestCultureScope_ParsesAcceptLanguage()
    {
        var culture = RequestCultureScope.ParseAcceptLanguage("sw-KE;q=1.0, zh-TW;q=0.5");
        Assert.NotNull(culture);
        Assert.Equal("zh-TW", culture!.Name);
    }

    [Theory]
    [InlineData("zh", "zh-CN")]
    [InlineData("zh-Hans", "zh-CN")]
    [InlineData("zh-CN", "zh-CN")]
    [InlineData("zh-Hant", "zh-TW")]
    [InlineData("zh-HK", "zh-TW")]
    [InlineData("en", "en-US")]
    [InlineData("en-GB", "en-US")]
    [InlineData("ja", "ja-JP")]
    [InlineData("de-AT", "de-DE")]
    [InlineData("fr-CA", "fr-FR")]
    [InlineData("pt-PT", "pt-BR")]
    [InlineData("ar", "ar-SA")]
    [System.ComponentModel.DisplayName("语言归一化：旧值与区域变体映射到受支持语言")]
    public void NormalizeCulture_MapsAliases(String input, String expected)
    {
        Assert.Equal(expected, NewLife.ChatAI.LanguageHelper.NormalizeCulture(input));
    }

    [Theory]
    [InlineData("sw-KE")]
    [InlineData("")]
    [System.ComponentModel.DisplayName("语言归一化：不支持的语言返回 null")]
    public void NormalizeCulture_Unsupported_ReturnsNull(String input)
    {
        Assert.Null(NewLife.ChatAI.LanguageHelper.NormalizeCulture(input));
    }
}
