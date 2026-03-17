using NewLife.Log;
using NewLife.Remoting;

namespace NewLife.AI.Channels;

/// <summary>企业微信消息渠道。对接企业微信机器人API实现消息收发</summary>
public class WeComChannel : IMessageChannel, ILogFeature
{
    #region 属性
    /// <summary>渠道类型</summary>
    public String ChannelType => "WeCom";

    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;
    #endregion

    /// <summary>发送消息到企业微信</summary>
    /// <param name="target">目标。群Webhook地址</param>
    /// <param name="content">消息内容</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否发送成功</returns>
    public async Task<Boolean> SendMessageAsync(String target, String content, CancellationToken cancellationToken = default)
    {
        if (String.IsNullOrWhiteSpace(target)) throw new ArgumentNullException(nameof(target));

        // 企业微信机器人消息格式
        var payload = new
        {
            msgtype = "markdown",
            markdown = new
            {
                content,
            }
        };

        var client = new ApiHttpClient(target) { Log = Log };
        try
        {
            var result = await client.InvokeAsync<String>("", payload, cancellationToken).ConfigureAwait(false);
            Log.Debug("企业微信发送成功：{0}", result);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("企业微信发送失败：{0}", ex.Message);
            return false;
        }
    }

    /// <summary>验证配置是否有效</summary>
    /// <param name="config">JSON格式配置</param>
    /// <returns>是否有效</returns>
    public Task<Boolean> ValidateConfigAsync(String config) => Task.FromResult(!String.IsNullOrWhiteSpace(config));
}
