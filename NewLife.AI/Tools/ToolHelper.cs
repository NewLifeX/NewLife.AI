using System.Collections.Concurrent;
using System.Net;
using System.Text.RegularExpressions;

namespace NewLife.AI.Tools;

/// <summary>工具服务内部辅助方法。提供 SSRF 防护、HTML 文本提取等共用功能</summary>
public static class ToolHelper
{
    /// <summary>共享处理器缓存。按 allowRedirect 分组（该配置作用于 handler 层），连接复用避免重复创建连接池</summary>
    private static readonly ConcurrentDictionary<String, HttpMessageHandler> _handlers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>校验是否为 SSRF 风险地址（私有/回环/链路本地）。对非 IP 的主机名解析 DNS 后检查（A-11），结果缓存避免重复查询</summary>
    /// <param name="host">主机名或 IP</param>
    public static Boolean IsSsrfRisk(String host)
    {
        if (String.IsNullOrEmpty(host)) return true;
        var lower = host.ToLowerInvariant();

        if (lower == "localhost" || lower == "ip6-localhost" || lower == "ip6-loopback") return true;

        if (!IPAddress.TryParse(host, out var ip))
        {
            // A-11：非字面 IP 的主机名可能解析到内网地址（DNS rebinding / 域名指向内网），解析后检查
            ip = ResolveHost(host);
            if (ip == null) return true;   // 解析失败无法确认目标非内网，保守视为风险（A-73）
        }

        return IsPrivateIp(ip);
    }

    /// <summary>解析主机名为 IP 地址（仅取第一个 A 记录），带进程级缓存避免每次请求查询 DNS</summary>
    /// <param name="host">主机名</param>
    /// <returns>解析出的 IP；解析失败返回 null</returns>
    private static IPAddress? ResolveHost(String host)
    {
        if (_dnsCache.TryGetValue(host, out var cached)) return cached;

        IPAddress? result = null;
        try
        {
            var addresses = Dns.GetHostAddresses(host);
            // 优先取 IPv4，其次任意
            result = addresses.FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                ?? addresses.FirstOrDefault();
        }
        catch { }

        // 仅缓存非空解析结果：解析失败不缓存，避免域名后解析到内网（DNS rebinding）时被旧缓存放行
        if (result != null)
            _dnsCache[host] = result;
        return result;
    }

    /// <summary>主机名 → IP 解析缓存（进程级，防止每次请求都查询 DNS）</summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<String, IPAddress?> _dnsCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>判断 IP 是否为私有/回环/链路本地地址</summary>
    /// <param name="ip">IP 地址</param>
    /// <returns>是则 true</returns>
    private static Boolean IsPrivateIp(IPAddress ip)
    {
        var bytes = ip.GetAddressBytes();
        if (bytes.Length == 4)
        {
            if (bytes[0] == 127) return true;                          // 127.x.x.x 回环
            if (bytes[0] == 10) return true;                           // 10.x.x.x 私有
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true; // 172.16-31.x.x
            if (bytes[0] == 192 && bytes[1] == 168) return true;       // 192.168.x.x
            if (bytes[0] == 169 && bytes[1] == 254) return true;       // 169.254.x.x 链路本地
            if (bytes[0] == 0) return true;                            // 0.0.0.0
        }
        if (bytes.Length == 16 && ip.Equals(IPAddress.IPv6Loopback)) return true;
        if (bytes.Length == 16 && bytes[0] == 0xfc) return true;       // fc00::/7 IPv6 唯一本地地址
        if (bytes.Length == 16 && bytes[0] == 0xfe && (bytes[1] & 0xc0) == 0x80) return true; // fe80::/10 链路本地

        return false;
    }

    /// <summary>从 HTML 字符串中提取纯文本正文</summary>
    /// <param name="html">原始 HTML 内容</param>
    public static String ExtractTextFromHtml(String html)
    {
        if (String.IsNullOrEmpty(html)) return String.Empty;

        var text = Regex.Replace(html, @"<(script|style)[^>]*>[\s\S]*?</\1>", " ", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<[^>]+>", " ");
        text = WebUtility.HtmlDecode(text);
        text = Regex.Replace(text, @"[ \t]+", " ");
        text = Regex.Replace(text, @"\n{3,}", "\n\n");
        return text.Trim();
    }

    /// <summary>创建带默认配置的 HttpClient（自动解压、重定向、30秒超时）。内部按 allowRedirect 池化 handler，调用方释放 HttpClient 不关闭共享连接</summary>
    /// <param name="allowRedirect">是否允许自动重定向。抓取用户可控 URL 时应传 false，防止重定向绕过 SSRF 校验（A-73）</param>
    public static HttpClient CreateDefaultHttpClient(Boolean allowRedirect = true)
    {
        var key = allowRedirect ? "Redirect" : "NoRedirect";
        var handler = _handlers.GetOrAdd(key, _ => new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            AllowAutoRedirect = allowRedirect,
            MaxAutomaticRedirections = 5,
        });
        var client = new HttpClient(handler, disposeHandler: false);
        client.Timeout = TimeSpan.FromSeconds(30);
        // 使用拟真浏览器 UA，降低百度/知乎/CSDN 等站点对机器人标识的拦截概率
        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");
        return client;
    }
}
