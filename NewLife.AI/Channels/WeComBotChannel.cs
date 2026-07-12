using System.Collections.Concurrent;
using NewLife.Log;
using NewLife.Remoting;

namespace NewLife.AI.Channels;

/// <summary>企业微信智能机器人渠道。对接企业微信群机器人Webhook API实现消息收发</summary>
/// <remarks>
/// 与 WeComChannel（企业微信应用消息）不同，WeComBotChannel 使用群机器人 Webhook 进行消息收发。
/// 配置格式（target 字段）：群机器人 Webhook URL
/// 例：https://qyapi.weixin.qq.com/cgi-bin/webhook/send?key=xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
/// 
/// 发送：POST markdown/text 消息到群机器人 Webhook URL
/// 接收：通过配置的回调 URL 接收群聊消息推送
/// 
/// 企业微信群机器人文档：https://developer.work.weixin.qq.com/document/path/91770
/// </remarks>
public class WeComBotChannel : IMessageChannel, ILogFeature
{
    #region 属性
    /// <summary>渠道类型</summary>
    public String ChannelType => "WeComBot";

    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    private readonly ConcurrentDictionary<String, ApiHttpClient> _clients = new();
    #endregion

    private ApiHttpClient GetClient(String target)
    {
        var client = _clients.GetOrAdd(target, url => new ApiHttpClient(url));
        client.Log = Log;
        return client;
    }

    /// <summary>发送消息到企业微信群机器人</summary>
    /// <param name="target">目标。群机器人 Webhook URL</param>
    /// <param name="content">消息内容（支持 Markdown 格式）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否发送成功</returns>
    public async Task<Boolean> SendMessageAsync(String target, String content, CancellationToken cancellationToken = default)
    {
        if (String.IsNullOrWhiteSpace(target)) throw new ArgumentNullException(nameof(target));

        // 企业微信群机器人消息格式
        var payload = new
        {
            msgtype = "markdown",
            markdown = new
            {
                content,
            }
        };

        var client = GetClient(target);
        try
        {
            var result = await client.InvokeAsync<String>("", payload, cancellationToken).ConfigureAwait(false);
            Log.Debug("企微智能机器人发送成功：{0}", result);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("企微智能机器人发送失败：{0}", ex.Message);
            return false;
        }
    }

    /// <summary>验证配置是否有效</summary>
    /// <param name="config">企微机器人 Webhook URL</param>
    /// <returns>是否有效</returns>
    public Task<Boolean> ValidateConfigAsync(String config)
    {
        if (String.IsNullOrWhiteSpace(config)) return Task.FromResult(false);
        var valid = config.StartsWithIgnoreCase("https://qyapi.weixin.qq.com/cgi-bin/webhook/send");
        return Task.FromResult(valid);
    }
}
