using NewLife.Log;
using NewLife.Remoting;

namespace NewLife.AI.Channels;

/// <summary>Webhook消息渠道。通过POST JSON推送消息到指定URL</summary>
public class WebhookChannel : IMessageChannel, ILogFeature
{
    #region 属性
    /// <summary>渠道类型</summary>
    public String ChannelType => "Webhook";

    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;
    #endregion

    /// <summary>发送消息到Webhook</summary>
    /// <param name="target">目标。Webhook URL</param>
    /// <param name="content">消息内容</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否发送成功</returns>
    public async Task<Boolean> SendMessageAsync(String target, String content, CancellationToken cancellationToken = default)
    {
        if (String.IsNullOrWhiteSpace(target)) throw new ArgumentNullException(nameof(target));

        // 通用Webhook消息格式
        var payload = new
        {
            content,
            timestamp = DateTime.UtcNow.ToString("o"),
            source = "NewLife.AI",
        };

        var client = new ApiHttpClient(target) { Log = Log };
        try
        {
            var result = await client.InvokeAsync<String>("", payload, cancellationToken).ConfigureAwait(false);
            Log.Debug("Webhook发送成功：{0}", result);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("Webhook发送失败：{0}", ex.Message);
            return false;
        }
    }

    /// <summary>验证配置是否有效</summary>
    /// <param name="config">JSON格式配置</param>
    /// <returns>是否有效</returns>
    public Task<Boolean> ValidateConfigAsync(String config) => Task.FromResult(true);
}
