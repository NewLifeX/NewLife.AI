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
    [DisplayName("WebhookChannel—ValidateConfigAsync 始终返回 true（无需校验 config）")]
    public async Task WebhookChannel_ValidateConfig_AlwaysTrue()
    {
        var ch = new WebhookChannel();
        Assert.True(await ch.ValidateConfigAsync(""));
        Assert.True(await ch.ValidateConfigAsync("   "));
        Assert.True(await ch.ValidateConfigAsync("https://hooks.example.com/notify"));
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
}
