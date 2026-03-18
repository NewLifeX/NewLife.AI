using System.Net;
using System.Text.RegularExpressions;

namespace NewLife.AI.Tools;

/// <summary>工具服务内部辅助方法。提供 SSRF 防护、HTML 文本提取等共用功能</summary>
public static class ToolHelper
{
    /// <summary>校验是否为 SSRF 风险地址（私有/回环/链路本地）</summary>
    /// <param name="host">主机名或 IP</param>
    public static Boolean IsSsrfRisk(String host)
    {
        if (String.IsNullOrEmpty(host)) return true;
        var lower = host.ToLowerInvariant();

        if (lower == "localhost" || lower == "ip6-localhost" || lower == "ip6-loopback") return true;

        if (!IPAddress.TryParse(host, out var ip))
            return false;

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

    /// <summary>创建带默认配置的 HttpClient（自动解压、重定向、30秒超时）</summary>
    public static HttpClient CreateDefaultHttpClient()
    {
        var client = new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5,
        });
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (compatible; NewLife.AI/1.0)");
        return client;
    }
}
