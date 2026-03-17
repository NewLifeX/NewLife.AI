namespace NewLife.AI.Channels;

/// <summary>消息渠道接口。统一抽象多平台消息收发能力</summary>
public interface IMessageChannel
{
    /// <summary>渠道类型。DingTalk/WeCom/Feishu/Webhook/Slack/Telegram</summary>
    String ChannelType { get; }

    /// <summary>发送消息到指定目标</summary>
    /// <param name="target">目标。群ID或用户ID</param>
    /// <param name="content">消息内容。支持纯文本和Markdown</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否发送成功</returns>
    Task<Boolean> SendMessageAsync(String target, String content, CancellationToken cancellationToken = default);

    /// <summary>验证渠道配置是否有效</summary>
    /// <param name="config">JSON格式的渠道配置</param>
    /// <returns>是否有效</returns>
    Task<Boolean> ValidateConfigAsync(String config);
}
