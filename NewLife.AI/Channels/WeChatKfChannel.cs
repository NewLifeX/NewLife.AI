using System.Collections.Concurrent;
using NewLife.Log;
using NewLife.Remoting;

namespace NewLife.AI.Channels;

/// <summary>微信客服消息渠道。对接微信客服 API 实现消息收发</summary>
/// <remarks>
/// 微信客服（WeChat Customer Service / KF）是微信生态中独立的客服消息系统，
/// 与公众号/服务号使用不同的 API 体系和权限管理。
/// 
/// 配置格式：
/// - target: 用户 OpenId
/// - AppKey: 公众号 AppId
/// - Secret: 客服 Secret
/// 
/// 微信客服 API 文档：https://developer.work.weixin.qq.com/document/path/94669
/// </remarks>
public class WeChatKfChannel : IMessageChannel, ILogFeature
{
    #region 属性
    /// <summary>渠道类型</summary>
    public String ChannelType => "WeChatKF";

    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    private readonly ConcurrentDictionary<String, ApiHttpClient> _clients = new();

    // token 缓存：key=AppId, value=(Token, ExpireTime)
    private static readonly ConcurrentDictionary<String, TokenCache> _tokenCache = new();

    private sealed class TokenCache
    {
        public String? Token { get; set; }
        public DateTime ExpireTime { get; set; }
        public Boolean IsValid => Token != null && DateTime.UtcNow < ExpireTime;
    }
    #endregion

    private ApiHttpClient GetClient(String baseUrl)
    {
        var client = _clients.GetOrAdd(baseUrl, url => new ApiHttpClient(url));
        client.Log = Log;
        return client;
    }

    /// <summary>获取 access_token（带缓存）</summary>
    /// <param name="appId">AppId</param>
    /// <param name="secret">Secret</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>access_token，获取失败时返回 null</returns>
    private async Task<String?> GetAccessTokenAsync(String appId, String secret, CancellationToken cancellationToken)
    {
        if (_tokenCache.TryGetValue(appId, out var cache) && cache.IsValid)
            return cache.Token;

        var client = GetClient("https://api.weixin.qq.com");
        try
        {
            var url = $"/cgi-bin/token?grant_type=client_credential&appid={appId}&secret={secret}";
            var result = await client.InvokeAsync<String>(url, null, cancellationToken).ConfigureAwait(false);
            if (result.IsNullOrWhiteSpace()) return null;

            // 解析 JSON 响应获取 access_token
            var tokenKey = "\"access_token\":\"";
            var tokenStart = result.IndexOf(tokenKey, StringComparison.Ordinal);
            if (tokenStart >= 0)
            {
                tokenStart += tokenKey.Length;
                var tokenEnd = result.IndexOf('"', tokenStart);
                if (tokenEnd > tokenStart)
                {
                    var token = result.Substring(tokenStart, tokenEnd - tokenStart);
                    if (!token.IsNullOrWhiteSpace())
                    {
                        var expiresIn = 7000;
                        var expKey = "\"expires_in\":";
                        var expStart = result.IndexOf(expKey, StringComparison.Ordinal);
                        if (expStart >= 0)
                        {
                            expStart += expKey.Length;
                            var expEnd = result.IndexOfAny(new[] { ',', '}' }, expStart);
                            if (expEnd > expStart)
                            {
                                var expStr = result.Substring(expStart, expEnd - expStart);
                                if (Int32.TryParse(expStr, out var exp) && exp > 0)
                                    expiresIn = exp - 200;
                            }
                        }

                        _tokenCache[appId] = new TokenCache
                        {
                            Token = token,
                            ExpireTime = DateTime.UtcNow.AddSeconds(expiresIn > 0 ? expiresIn : 7000),
                        };

                        return token;
                    }
                }
            }

            Log.Error("微信客服 access_token 获取失败：{0}", result);
            return null;
        }
        catch (Exception ex)
        {
            Log.Error("微信客服 access_token 获取异常：{0}", ex.Message);
            return null;
        }
    }

    /// <summary>发送消息到微信客服用户</summary>
    /// <param name="target">目标。用户 OpenId（格式：{AppId}:{OpenId}）</param>
    /// <param name="content">消息内容（支持文本格式）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否发送成功</returns>
    public async Task<Boolean> SendMessageAsync(String target, String content, CancellationToken cancellationToken = default)
    {
        if (String.IsNullOrWhiteSpace(target)) throw new ArgumentNullException(nameof(target));

        // target 格式：{AppId}:{OpenId}
        var parts = target.Split(new[] { ':' }, 2);
        var appId = parts[0];
        var openId = parts.Length > 1 ? parts[1] : target;

        var secret = "";

        var token = await GetAccessTokenAsync(appId, secret, cancellationToken).ConfigureAwait(false);
        if (token == null) return false;

        // 微信客服消息（JSON 格式）
        var payload = new
        {
            touser = openId,
            msgtype = "text",
            text = new
            {
                content,
            }
        };

        var client = GetClient("https://api.weixin.qq.com");
        try
        {
            var url = $"/cgi-bin/message/custom/send?access_token={token}";
            var result = await client.InvokeAsync<String>(url, payload, cancellationToken).ConfigureAwait(false);
            Log.Debug("微信客服发送成功：{0}", result);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("微信客服发送失败：{0}", ex.Message);
            return false;
        }
    }

    /// <summary>验证配置是否有效</summary>
    /// <param name="config">JSON 格式配置，需包含 appId 和 secret</param>
    /// <returns>是否有效</returns>
    public Task<Boolean> ValidateConfigAsync(String config)
    {
        if (String.IsNullOrWhiteSpace(config)) return Task.FromResult(false);
        return Task.FromResult(config.Contains("appId") && config.Contains("secret"));
    }
}
