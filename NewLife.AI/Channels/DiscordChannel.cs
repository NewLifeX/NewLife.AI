using System.Net.Http.Headers;
using System.Text;
using NewLife.Log;

namespace NewLife.AI.Channels;

/// <summary>Discord 消息渠道。通过 Discord Bot API 发送消息到 Discord 频道</summary>
/// <remarks>
/// Discord Bot 使用 REST API 发送消息，通过 WebSocket Gateway 接收消息。
/// 本实现提供 HTTP 发送能力；接收消息需通过 Discord 的 Interactions Endpoint URL
/// 或 Gateway 长连接（需要单独的后台服务维护）。
/// 
/// 配置格式（target 字段）：{BotToken}:{ChannelId}
/// 例：MTExODExODExODExODExODEx.Gxxxxx.xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx:123456789012345678
/// 
/// Discord Bot API 文档：https://discord.com/developers/docs/intro
/// </remarks>
public class DiscordChannel : IMessageChannel, ILogFeature
{
    #region 属性
    /// <summary>渠道类型</summary>
    public String ChannelType => "Discord";

    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    private static readonly HttpClient _http = new();
    #endregion

    /// <summary>发送消息到 Discord 频道</summary>
    /// <param name="target">目标。频道 ID（格式：{BotToken}:{ChannelId}）</param>
    /// <param name="content">消息内容（支持 Discord Markdown 格式）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否发送成功</returns>
    public async Task<Boolean> SendMessageAsync(String target, String content, CancellationToken cancellationToken = default)
    {
        if (String.IsNullOrWhiteSpace(target)) throw new ArgumentNullException(nameof(target));

        // target 格式：{BotToken}:{ChannelId}
        var lastColon = target.LastIndexOf(':');
        if (lastColon <= 0) return false;

        var botToken = target[..lastColon];
        var channelId = target[(lastColon + 1)..];

        if (String.IsNullOrWhiteSpace(botToken) || String.IsNullOrWhiteSpace(channelId))
            return false;

        // 构建 Discord REST API 请求
        var request = new HttpRequestMessage(HttpMethod.Post,
            $"https://discord.com/api/v10/channels/{channelId}/messages");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bot", botToken);

        var payload = new { content };
        var json = NewLife.Serialization.JsonHelper.ToJson(payload);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                Log.Debug("Discord 发送成功");
                return true;
            }

            var errorBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            Log.Error("Discord 发送失败：HTTP {0} - {1}", (Int32)response.StatusCode, errorBody);
            return false;
        }
        catch (Exception ex)
        {
            Log.Error("Discord 发送失败：{0}", ex.Message);
            return false;
        }
    }

    /// <summary>验证配置是否有效</summary>
    /// <param name="config">Bot Token 字符串</param>
    /// <returns>是否有效</returns>
    public Task<Boolean> ValidateConfigAsync(String config)
    {
        if (String.IsNullOrWhiteSpace(config)) return Task.FromResult(false);
        // Discord Bot Token 通常较长，至少 50 字符
        var valid = config.Length > 50;
        return Task.FromResult(valid);
    }
}
