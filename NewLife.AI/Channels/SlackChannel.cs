using System.Collections.Concurrent;
using NewLife.Log;
using NewLife.Remoting;

namespace NewLife.AI.Channels;

/// <summary>Slack 消息渠道。通过 Incoming Webhook 发送消息到 Slack 频道</summary>
/// <remarks>
/// 配置格式（target 字段）：Incoming Webhook URL
/// 例：https://hooks.slack.com/services/T00000000/B00000000/XXXXXXXXXXXXXXXXXXXXXXXX
///
/// Slack Incoming Webhook 文档：https://api.slack.com/messaging/webhooks
/// </remarks>
public class SlackChannel : IMessageChannel, ILogFeature
{
    #region 属性
    /// <summary>渠道类型</summary>
    public String ChannelType => "Slack";

    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    private readonly ConcurrentDictionary<String, ApiHttpClient> _clients = new();
    #endregion

    private ApiHttpClient GetClient(String webhookUrl)
    {
        var client = _clients.GetOrAdd(webhookUrl, url => new ApiHttpClient(url));
        client.Log = Log;
        return client;
    }

    /// <summary>发送消息到 Slack 频道</summary>
    /// <param name="target">目标。Incoming Webhook URL</param>
    /// <param name="content">消息内容（支持 Slack mrkdwn 格式）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否发送成功</returns>
    public async Task<Boolean> SendMessageAsync(String target, String content, CancellationToken cancellationToken = default)
    {
        if (String.IsNullOrWhiteSpace(target)) throw new ArgumentNullException(nameof(target));

        // Slack Incoming Webhook 消息格式（Block Kit）
        var payload = new
        {
            blocks = new Object[]
            {
                new
                {
                    type = "section",
                    text = new
                    {
                        type = "mrkdwn",
                        text = ConvertToSlackMarkdown(content),
                    }
                }
            }
        };

        var client = GetClient(target);
        try
        {
            // Slack Webhook 直接 POST JSON 到 Webhook URL
            var result = await client.InvokeAsync<String>("", payload, cancellationToken).ConfigureAwait(false);
            Log.Debug("Slack 发送成功：{0}", result);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("Slack 发送失败：{0}", ex.Message);
            return false;
        }
    }

    /// <summary>验证配置是否有效（要求是有效的 Slack Webhook URL）</summary>
    /// <param name="config">Slack Incoming Webhook URL</param>
    /// <returns>是否有效</returns>
    public Task<Boolean> ValidateConfigAsync(String config)
    {
        if (String.IsNullOrWhiteSpace(config)) return Task.FromResult(false);
        var valid = config.StartsWithIgnoreCase("https://hooks.slack.com/");
        return Task.FromResult(valid);
    }

    /// <summary>将 Markdown 转换为 Slack mrkdwn 格式</summary>
    /// <param name="markdown">标准 Markdown 文本</param>
    /// <returns>Slack mrkdwn 格式文本</returns>
    private static String ConvertToSlackMarkdown(String markdown)
    {
        if (markdown.IsNullOrEmpty()) return markdown;

        // 代码块：```lang ... ``` → ``` ... ```（Slack 不支持语言标记）
        markdown = System.Text.RegularExpressions.Regex.Replace(
            markdown, @"```\w*\n?", "```");

        // 粗体：**text** → *text*
        markdown = System.Text.RegularExpressions.Regex.Replace(
            markdown, @"\*\*(.+?)\*\*", "*$1*");

        // 链接：[text](url) → <url|text>
        markdown = System.Text.RegularExpressions.Regex.Replace(
            markdown, @"\[(.+?)\]\((.+?)\)", "<$2|$1>");

        // 标题：## text → *text*（无原生标题，用粗体代替）
        markdown = System.Text.RegularExpressions.Regex.Replace(
            markdown, @"^#{1,6}\s+(.+)$", "*$1*", System.Text.RegularExpressions.RegexOptions.Multiline);

        return markdown;
    }
}
