using System.Collections.Concurrent;
using System.Text;
using NewLife.Log;
using NewLife.Remoting;

namespace NewLife.AI.Channels;

/// <summary>QQ 机器人消息渠道。对接 QQ 官方机器人开放平台 API 实现消息收发</summary>
/// <remarks>
/// QQ 机器人开放平台支持群聊和私聊场景，通过 WebSocket 或 Webhook 接收消息，
/// 通过 HTTP API 发送消息。
/// 
/// 配置格式：
/// - target: 格式 {BotAppId}:{BotToken}:{TargetType}:{TargetId}
///   例：102345678:abcdef123456:group:123456789
/// - TargetType: group（群聊）或 c2c（私聊）
/// 
/// QQ 机器人 API 文档：https://bot.q.qq.com/wiki/
/// 
/// 注意：QQ 机器人需要先在 QQ 开放平台注册应用并获取 Bot Token。
/// 本实现使用 HTTP API 发送消息，接收消息通过 Webhook 回调。
/// </remarks>
public class QQChannel : IMessageChannel, ILogFeature
{
    #region 属性
    /// <summary>渠道类型</summary>
    public String ChannelType => "QQ";

    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    // QQ 机器人 API 基础地址
    private const String _baseUrl = "https://api.sgroup.qq.com";
    private const String _sandboxUrl = "https://sandbox.api.sgroup.qq.com";

    // token 缓存：key=BotAppId, value=Token
    private static readonly ConcurrentDictionary<String, String> _tokenCache = new();
    #endregion

    /// <summary>发送消息到 QQ 群或用户</summary>
    /// <param name="target">目标。格式：{BotAppId}:{BotToken}:{TargetType}:{TargetId}</param>
    /// <param name="content">消息内容（支持 Markdown 格式）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否发送成功</returns>
    public async Task<Boolean> SendMessageAsync(String target, String content, CancellationToken cancellationToken = default)
    {
        if (String.IsNullOrWhiteSpace(target)) throw new ArgumentNullException(nameof(target));

        // 解析目标格式：{BotAppId}:{BotToken}:{TargetType}:{TargetId}
        var parts = target.Split(':');
        if (parts.Length < 4) return false;

        var botAppId = parts[0];
        var botToken = parts[1];
        var targetType = parts[2]; // "group" 或 "c2c"
        var targetId = parts[3];

        // 缓存 token 避免重复解析
        _tokenCache[botAppId] = botToken;

        // QQ Bot API 使用 HTTPS header 鉴权，直接使用 HttpClient 发送
        var isSandbox = false;
        var baseUrl = isSandbox ? _sandboxUrl : _baseUrl;

        var request = new HttpRequestMessage();
        request.Headers.Add("Authorization", $"QQBot {botToken}");

        Object payload;
        String apiPath;

        if (targetType.EqualIgnoreCase("group"))
        {
            apiPath = $"/v2/groups/{targetId}/messages";
            payload = new
            {
                content = new
                {
                    type = "markdown",
                    markdown = new
                    {
                        content,
                    }
                },
                msg_type = 0,
            };
        }
        else
        {
            apiPath = $"/v2/users/{targetId}/messages";
            payload = new
            {
                content = new
                {
                    type = "markdown",
                    markdown = new
                    {
                        content,
                    }
                },
                msg_type = 0,
            };
        }

        request.Method = HttpMethod.Post;
        request.RequestUri = new Uri(baseUrl + apiPath);
        var json = NewLife.Serialization.JsonHelper.ToJson(payload);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            using var httpClient = new HttpClient();
            var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                Log.Debug("QQ 机器人发送成功：{0}", result);
                return true;
            }

            var errorBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            Log.Error("QQ 机器人发送失败：HTTP {0} - {1}", (Int32)response.StatusCode, errorBody);
            return false;
        }
        catch (Exception ex)
        {
            Log.Error("QQ 机器人发送失败：{0}", ex.Message);
            return false;
        }
    }

    /// <summary>验证配置是否有效</summary>
    /// <param name="config">JSON 格式配置，需包含 appId 和 secret</param>
    /// <returns>是否有效</returns>
    public Task<Boolean> ValidateConfigAsync(String config)
    {
        if (String.IsNullOrWhiteSpace(config)) return Task.FromResult(false);
        // 简单验证：包含 appId 和 secret 关键字即可
        return Task.FromResult(config.Contains("appId") && config.Contains("secret"));
    }
}
