using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using NewLife.AI.Channels;
using Xunit;

namespace XUnitTest.Channels;

/// <summary>消息渠道（WeComChannel、DingTalkChannel、FeishuChannel、WebhookChannel）单元测试</summary>
[DisplayName("消息渠道单元测试")]
public class ChannelTests
{
    // ── 纯逻辑测试（无需 HTTP；只验证属性和不依赖网络的方法）─────────────────

    #region WeComChannel

    [Fact]
    [DisplayName("WeComChannel—ChannelType 值为 WeCom")]
    public void WeComChannel_ChannelType_IsWeCom()
    {
        IMessageChannel ch = new WeComChannel();
        Assert.Equal("WeCom", ch.ChannelType);
    }

    [Fact]
    [DisplayName("WeComChannel—ValidateConfigAsync 空字符串返回 false")]
    public async Task WeComChannel_ValidateConfig_EmptyString_False()
    {
        var ch = new WeComChannel();
        Assert.False(await ch.ValidateConfigAsync(""));
    }

    [Fact]
    [DisplayName("WeComChannel—ValidateConfigAsync 非空字符串返回 true")]
    public async Task WeComChannel_ValidateConfig_NonEmpty_True()
    {
        var ch = new WeComChannel();
        Assert.True(await ch.ValidateConfigAsync("https://qyapi.weixin.qq.com/cgi-bin/webhook/send?key=abc"));
    }

    [Fact]
    [DisplayName("WeComChannel—ValidateConfigAsync null 返回 false")]
    public async Task WeComChannel_ValidateConfig_Null_False()
    {
        var ch = new WeComChannel();
        Assert.False(await ch.ValidateConfigAsync(null!));
    }

    [Fact]
    [DisplayName("WeComChannel—SendMessageAsync 空 target 抛 ArgumentNullException")]
    public async Task WeComChannel_Send_EmptyTarget_Throws()
    {
        var ch = new WeComChannel();
        await Assert.ThrowsAsync<ArgumentNullException>(() => ch.SendMessageAsync("", "content"));
    }

    [Fact]
    [DisplayName("WeComChannel—SendMessageAsync null target 抛 ArgumentNullException")]
    public async Task WeComChannel_Send_NullTarget_Throws()
    {
        var ch = new WeComChannel();
        await Assert.ThrowsAsync<ArgumentNullException>(() => ch.SendMessageAsync(null!, "content"));
    }

    #endregion

    #region DingTalkChannel

    [Fact]
    [DisplayName("DingTalkChannel—ChannelType 值为 DingTalk")]
    public void DingTalkChannel_ChannelType_IsDingTalk()
    {
        IMessageChannel ch = new DingTalkChannel();
        Assert.Equal("DingTalk", ch.ChannelType);
    }

    [Fact]
    [DisplayName("DingTalkChannel—ValidateConfigAsync 空字符串返回 false")]
    public async Task DingTalkChannel_ValidateConfig_EmptyString_False()
    {
        var ch = new DingTalkChannel();
        Assert.False(await ch.ValidateConfigAsync(""));
    }

    [Fact]
    [DisplayName("DingTalkChannel—ValidateConfigAsync 有效 URL 返回 true")]
    public async Task DingTalkChannel_ValidateConfig_ValidUrl_True()
    {
        var ch = new DingTalkChannel();
        Assert.True(await ch.ValidateConfigAsync("https://oapi.dingtalk.com/robot/send?access_token=abc"));
    }

    [Fact]
    [DisplayName("DingTalkChannel—SendMessageAsync 空 target 抛 ArgumentNullException")]
    public async Task DingTalkChannel_Send_EmptyTarget_Throws()
    {
        var ch = new DingTalkChannel();
        await Assert.ThrowsAsync<ArgumentNullException>(() => ch.SendMessageAsync("", "content"));
    }

    [Fact]
    [DisplayName("DingTalkChannel—SendMessageAsync null target 抛 ArgumentNullException")]
    public async Task DingTalkChannel_Send_NullTarget_Throws()
    {
        var ch = new DingTalkChannel();
        await Assert.ThrowsAsync<ArgumentNullException>(() => ch.SendMessageAsync(null!, "content"));
    }

    #endregion

    #region FeishuChannel

    [Fact]
    [DisplayName("FeishuChannel—ChannelType 值为 Feishu")]
    public void FeishuChannel_ChannelType_IsFeishu()
    {
        IMessageChannel ch = new FeishuChannel();
        Assert.Equal("Feishu", ch.ChannelType);
    }

    [Fact]
    [DisplayName("FeishuChannel—ValidateConfigAsync 空字符串返回 false")]
    public async Task FeishuChannel_ValidateConfig_EmptyString_False()
    {
        var ch = new FeishuChannel();
        Assert.False(await ch.ValidateConfigAsync(""));
    }

    [Fact]
    [DisplayName("FeishuChannel—ValidateConfigAsync 有效配置返回 true")]
    public async Task FeishuChannel_ValidateConfig_ValidString_True()
    {
        var ch = new FeishuChannel();
        Assert.True(await ch.ValidateConfigAsync("https://open.feishu.cn/open-apis/bot/v2/hook/abc"));
    }

    [Fact]
    [DisplayName("FeishuChannel—SendMessageAsync 空 target 抛 ArgumentNullException")]
    public async Task FeishuChannel_Send_EmptyTarget_Throws()
    {
        var ch = new FeishuChannel();
        await Assert.ThrowsAsync<ArgumentNullException>(() => ch.SendMessageAsync("", "content"));
    }

    [Fact]
    [DisplayName("FeishuChannel—SendMessageAsync null target 抛 ArgumentNullException")]
    public async Task FeishuChannel_Send_NullTarget_Throws()
    {
        var ch = new FeishuChannel();
        await Assert.ThrowsAsync<ArgumentNullException>(() => ch.SendMessageAsync(null!, "content"));
    }

    #endregion

    #region WebhookChannel

    [Fact]
    [DisplayName("WebhookChannel—ChannelType 值为 Webhook")]
    public void WebhookChannel_ChannelType_IsWebhook()
    {
        IMessageChannel ch = new WebhookChannel();
        Assert.Equal("Webhook", ch.ChannelType);
    }

    [Fact]
    [DisplayName("WebhookChannel—ValidateConfigAsync 校验合法 http/https URL（A-08）")]
    public async Task WebhookChannel_ValidateConfig_ValidatesUrl()
    {
        var ch = new WebhookChannel();
        // 空/空白/非 URL 均无效
        Assert.False(await ch.ValidateConfigAsync(""));
        Assert.False(await ch.ValidateConfigAsync("   "));
        Assert.False(await ch.ValidateConfigAsync("not-a-url"));
        Assert.False(await ch.ValidateConfigAsync("ftp://hooks.example.com/notify"));

        // 合法的 http/https 绝对 URL 有效
        Assert.True(await ch.ValidateConfigAsync("https://hooks.example.com/notify"));
        Assert.True(await ch.ValidateConfigAsync("http://hooks.example.com/notify"));
    }



    [Fact]
    [DisplayName("WebhookChannel—SendMessageAsync 空 target 抛 ArgumentNullException")]
    public async Task WebhookChannel_Send_EmptyTarget_Throws()
    {
        var ch = new WebhookChannel();
        await Assert.ThrowsAsync<ArgumentNullException>(() => ch.SendMessageAsync("", "content"));
    }

    [Fact]
    [DisplayName("WebhookChannel—SendMessageAsync null target 抛 ArgumentNullException")]
    public async Task WebhookChannel_Send_NullTarget_Throws()
    {
        var ch = new WebhookChannel();
        await Assert.ThrowsAsync<ArgumentNullException>(() => ch.SendMessageAsync(null!, "content"));
    }

    #endregion

    // ── 渠道实现 IMessageChannel ──────────────────────────────────────────────

    #region 接口一致性

    [Fact]
    [DisplayName("所有渠道类—实现 IMessageChannel 接口")]
    public void AllChannels_ImplementIMessageChannel()
    {
        Assert.IsAssignableFrom<IMessageChannel>(new WeComChannel());
        Assert.IsAssignableFrom<IMessageChannel>(new DingTalkChannel());
        Assert.IsAssignableFrom<IMessageChannel>(new FeishuChannel());
        Assert.IsAssignableFrom<IMessageChannel>(new WebhookChannel());
    }

    [Fact]
    [DisplayName("所有渠道类—ChannelType 各不相同")]
    public void AllChannels_HaveUniqueChannelTypes()
    {
        var types = new[]
        {
            new WeComChannel().ChannelType,
            new DingTalkChannel().ChannelType,
            new FeishuChannel().ChannelType,
            new WebhookChannel().ChannelType,
        };
        var distinct = new System.Collections.Generic.HashSet<String>(types);
        Assert.Equal(4, distinct.Count);
    }

    #endregion

    // ── 补充渠道（A-04：覆盖薄）──────────────────────────────────────────────

    #region 微信渠道

    [Fact]
    [DisplayName("WeChatMpChannel—未配置 AppSecret 时发送明确失败（A-05）")]
    public async Task WeChatMp_Send_WithoutAppSecret_Fails()
    {
        var ch = new WeChatMpChannel();
        // AppSecret 为 null/空时不发起网络请求，明确返回 false（原实现硬编码空串必然失败且无提示）
        Assert.False(await ch.SendMessageAsync("wx123:openid456", "hello"));
    }

    [Fact]
    [DisplayName("WeChatMpChannel—配置 AppSecret 后不再因未配置失败")]
    public void WeChatMp_AppSecret_Injectable()
    {
        var ch = new WeChatMpChannel { AppSecret = "test-secret" };
        Assert.Equal("test-secret", ch.AppSecret);
    }

    [Fact]
    [DisplayName("WeChatKfChannel—未配置 AppSecret 时发送明确失败（A-06）")]
    public async Task WeChatKf_Send_WithoutAppSecret_Fails()
    {
        var ch = new WeChatKfChannel();
        Assert.False(await ch.SendMessageAsync("wx123:openid456", "hello"));
    }

    [Fact]
    [DisplayName("WeChatKfChannel—配置 AppSecret 后不再因未配置失败")]
    public void WeChatKf_AppSecret_Injectable()
    {
        var ch = new WeChatKfChannel { AppSecret = "test-secret" };
        Assert.Equal("test-secret", ch.AppSecret);
    }

    [Fact]
    [DisplayName("WeChatMpChannel—ValidateConfigAsync 需含 appId 和 secret 关键字")]
    public async Task WeChatMp_ValidateConfig_RequiresKeys()
    {
        var ch = new WeChatMpChannel();
        Assert.True(await ch.ValidateConfigAsync("{\"appId\":\"wx1\",\"secret\":\"s1\"}"));
        Assert.False(await ch.ValidateConfigAsync("{\"appId\":\"wx1\"}"));
        Assert.False(await ch.ValidateConfigAsync(""));
    }

    #endregion

    #region 其他渠道

    [Fact]
    [DisplayName("QQChannel—ChannelType 为 QQ，非法 target 返回 false（A-01/A-07）")]
    public async Task QQ_Send_InvalidTarget_ReturnsFalse()
    {
        var ch = new QQChannel();
        // target 至少 4 段 {BotAppId}:{BotToken}:{TargetType}:{TargetId}
        Assert.False(await ch.SendMessageAsync("102345678", "hello"));
    }

    [Fact]
    [DisplayName("QQChannel—ValidateConfigAsync 校验配置关键字")]
    public async Task QQ_ValidateConfig_Validates()
    {
        var ch = new QQChannel();
        Assert.True(await ch.ValidateConfigAsync("{\"appId\":\"1\",\"secret\":\"2\"}"));
        Assert.False(await ch.ValidateConfigAsync(""));
    }

    [Fact]
    [DisplayName("DiscordChannel—ChannelType 为 Discord")]
    public void Discord_ChannelType_IsDiscord()
    {
        IMessageChannel ch = new DiscordChannel();
        Assert.Equal("Discord", ch.ChannelType);
    }

    [Fact]
    [DisplayName("DiscordChannel—非法 target（无冒号分隔）返回 false（A-02）")]
    public async Task Discord_Send_InvalidTarget_ReturnsFalse()
    {
        var ch = new DiscordChannel();
        Assert.False(await ch.SendMessageAsync("notoken", "hello"));
    }

    [Fact]
    [DisplayName("TelegramChannel—ValidateConfigAsync 至少两个冒号")]
    public async Task Telegram_ValidateConfig_RequiresTwoColons()
    {
        var ch = new TelegramChannel();
        Assert.True(await ch.ValidateConfigAsync("1234567890:AAHxxxxxxxxxxxxxxxxxxxxxxxx:-1001234567890"));
        Assert.False(await ch.ValidateConfigAsync("1234567890"));
    }

    [Fact]
    [DisplayName("SlackChannel—ValidateConfigAsync 校验 Webhook URL 前缀")]
    public async Task Slack_ValidateConfig_ValidatesUrl()
    {
        var ch = new SlackChannel();
        Assert.True(await ch.ValidateConfigAsync("https://hooks.slack.com/services/T/B/X"));
        Assert.False(await ch.ValidateConfigAsync("https://example.com/hook"));
    }

    [Fact]
    [DisplayName("WeComBotChannel—ValidateConfigAsync 校验企微 Webhook URL")]
    public async Task WeComBot_ValidateConfig_ValidatesUrl()
    {
        var ch = new WeComBotChannel();
        Assert.True(await ch.ValidateConfigAsync("https://qyapi.weixin.qq.com/cgi-bin/webhook/send?key=abc"));
        Assert.False(await ch.ValidateConfigAsync("https://example.com/hook"));
    }

    #endregion

    // ── Markdown 转换纯逻辑测试 ───────────────────────────────────────────────

    #region Slack Markdown

    [Fact]
    [DisplayName("SlackChannel—粗体 **text** 转为 *text*")]
    public void SlackMarkdown_Bold_Converts()
    {
        Assert.Equal("这是 *加粗* 文本", SlackChannel.ConvertToSlackMarkdown("这是 **加粗** 文本"));
    }

    [Fact]
    [DisplayName("SlackChannel—链接 [text](url) 转为 <url|text>")]
    public void SlackMarkdown_Link_Converts()
    {
        Assert.Equal("访问 <https://x.com|官网>", SlackChannel.ConvertToSlackMarkdown("访问 [官网](https://x.com)"));
    }

    [Fact]
    [DisplayName("SlackChannel—代码块去除语言标记")]
    public void SlackMarkdown_CodeBlock_StripsLang()
    {
        var result = SlackChannel.ConvertToSlackMarkdown("```csharp\nvar x = 1;\n```");
        Assert.Contains("```", result);
        Assert.DoesNotContain("csharp", result);
    }

    [Fact]
    [DisplayName("SlackChannel—标题 ## 转为粗体")]
    public void SlackMarkdown_Heading_Converts()
    {
        Assert.Equal("*标题内容*", SlackChannel.ConvertToSlackMarkdown("## 标题内容"));
    }

    [Fact]
    [DisplayName("SlackChannel—空输入原样返回")]
    public void SlackMarkdown_Empty_ReturnsAsIs()
    {
        Assert.Equal("", SlackChannel.ConvertToSlackMarkdown(""));
    }

    #endregion

    #region Telegram HTML

    [Fact]
    [DisplayName("TelegramChannel—粗体 **text** 转为 <b>text</b>")]
    public void TelegramHtml_Bold_Converts()
    {
        Assert.Equal("<b>加粗</b>", TelegramChannel.ConvertToHtml("**加粗**"));
    }

    [Fact]
    [DisplayName("TelegramChannel—链接 [text](url) 转为 <a href>")]
    public void TelegramHtml_Link_Converts()
    {
        Assert.Equal("<a href=\"https://x.com\">官网</a>", TelegramChannel.ConvertToHtml("[官网](https://x.com)"));
    }

    [Fact]
    [DisplayName("TelegramChannel—HTML 特殊字符转义")]
    public void TelegramHtml_EscapesSpecialChars()
    {
        Assert.Equal("a &amp; b &lt;c&gt;", TelegramChannel.ConvertToHtml("a & b <c>"));
    }

    [Fact]
    [DisplayName("TelegramChannel—行内代码 `code` 转为 <code>code</code>")]
    public void TelegramHtml_InlineCode_Converts()
    {
        Assert.Equal("使用 <code>dotnet</code>", TelegramChannel.ConvertToHtml("使用 `dotnet`"));
    }

    [Fact]
    [DisplayName("TelegramChannel—代码块保持原样不转换")]
    public void TelegramHtml_CodeBlock_Preserved()
    {
        var result = TelegramChannel.ConvertToHtml("```\n**not bold**\n```");
        Assert.Contains("**not bold**", result);  // 代码块内不转换
        Assert.Contains("<pre><code>", result);
    }

    [Fact]
    [DisplayName("TelegramChannel—空输入原样返回")]
    public void TelegramHtml_Empty_ReturnsAsIs()
    {
        Assert.Equal("", TelegramChannel.ConvertToHtml(""));
    }

    #endregion
}
