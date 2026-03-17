using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using NewLife.Collections;
using NewLife.Serialization;

namespace NewLife.AI.Tools;

/// <summary>内置工具服务。提供系统级原生 .NET 工具，通过 ToolRegistry 注册后供 AI 模型调用</summary>
/// <remarks>
/// 内置工具无需外部依赖，开箱即用。典型场景：
/// <list type="bullet">
/// <item>获取当前时间 — 模型无法感知实时时间，必须通过工具调用获取</item>
/// <item>数学计算 — 避免模型计算错误</item>
/// <item>网页爬取 — 获取指定 URL 的网页正文内容</item>
/// <item>网页搜索 — 通过搜索引擎 API 检索最新信息</item>
/// </list>
/// </remarks>
public class BuiltinToolService
{
    #region 属性

    private readonly HttpClient _http;

    /// <summary>搜索 API 密钥。Bing 填写 Azure Cognitive Services 密钥；Serper 填写 serper.dev 密钥；留空则退化为 DuckDuckGo 即时问答（无需密钥，功能有限）</summary>
    public String? SearchApiKey { get; set; }

    /// <summary>搜索引擎提供商：bing（默认）/ serper / duckduckgo</summary>
    public String SearchProvider { get; set; } = "bing";

    #endregion

    #region 构造

    /// <summary>初始化内置工具服务</summary>
    /// <param name="httpClient">HTTP 客户端；为 null 时自动创建默认实例（含自动解压缩与重定向）</param>
    public BuiltinToolService(HttpClient? httpClient = null)
    {
        if (httpClient != null)
        {
            _http = httpClient;
        }
        else
        {
            _http = new HttpClient(new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 5,
            });
            _http.Timeout = TimeSpan.FromSeconds(30);
            _http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (compatible; NewLife.AI/1.0)");
        }
    }

    #endregion

    #region 时间工具

    /// <summary>获取当前日期和时间信息，包括完整日期、星期、时间、时区、Unix时间戳等</summary>
    /// <param name="timezone">时区名称，如 Asia/Shanghai、America/New_York。默认使用服务器本地时区</param>
    [ToolDescription("get_current_time")]
    public String GetCurrentTime(String? timezone = null)
    {
        DateTimeOffset now;
        String tzName;

        if (!String.IsNullOrEmpty(timezone))
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById(timezone);
                now = TimeZoneInfo.ConvertTime(DateTimeOffset.Now, tz);
                tzName = tz.DisplayName;
            }
            catch (TimeZoneNotFoundException)
            {
                now = DateTimeOffset.Now;
                tzName = TimeZoneInfo.Local.DisplayName;
            }
        }
        else
        {
            now = DateTimeOffset.Now;
            tzName = TimeZoneInfo.Local.DisplayName;
        }

        var sb = Pool.StringBuilder.Get();
        sb.AppendLine($"datetime: {now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"date: {now:yyyy-MM-dd}");
        sb.AppendLine($"time: {now:HH:mm:ss}");
        sb.AppendLine($"dayOfWeek: {now.DayOfWeek} ({GetChineseDayOfWeek(now.DayOfWeek)})");
        sb.AppendLine($"weekOfYear: {CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(now.DateTime, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday)}");
        sb.AppendLine($"timezone: {tzName}");
        sb.AppendLine($"utcOffset: {now.Offset}");
        sb.Append($"unixTimestamp: {now.ToUnixTimeSeconds()}");
        return sb.Return(true);
    }

    private static String GetChineseDayOfWeek(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "星期一",
        DayOfWeek.Tuesday => "星期二",
        DayOfWeek.Wednesday => "星期三",
        DayOfWeek.Thursday => "星期四",
        DayOfWeek.Friday => "星期五",
        DayOfWeek.Saturday => "星期六",
        DayOfWeek.Sunday => "星期日",
        _ => day.ToString(),
    };

    #endregion

    #region 数学工具

    /// <summary>计算数学表达式的结果。支持加减乘除、括号、取模等基本运算</summary>
    /// <param name="expression">数学表达式，如 (3 + 5) * 2 - 10 / 3</param>
    [ToolDescription("calculate")]
    public String Calculate(String expression)
    {
        if (String.IsNullOrWhiteSpace(expression))
            return "{\"error\": \"expression is required\"}";

        // 安全校验：仅允许数字、运算符、括号、空格、小数点
        var sanitized = expression.Trim();
        foreach (var c in sanitized)
        {
            if (!Char.IsDigit(c) && c != '+' && c != '-' && c != '*' && c != '/' && c != '%'
                && c != '(' && c != ')' && c != '.' && c != ' ')
                return $"{{\"error\": \"invalid character '{c}' in expression\"}}";
        }

        try
        {
            using var dt = new System.Data.DataTable();
            var result = dt.Compute(sanitized, null);
            return $"{{\"expression\": \"{sanitized}\", \"result\": {result}}}";
        }
        catch (Exception ex)
        {
            return $"{{\"error\": \"{ex.Message}\"}}";
        }
    }

    #endregion

    #region 网页工具

    /// <summary>爬取指定 URL 的网页内容并提取正文文本。适用于读取文章、文档或任何公开网页</summary>
    /// <param name="url">要爬取的网页地址，必须是完整的 http/https URL</param>
    /// <param name="maxLength">返回的最大字符数，防止超长内容占用过多 token。默认 5000</param>
    [ToolDescription("web_fetch")]
    public async Task<String> WebFetchAsync(String url, Int32 maxLength = 5000, CancellationToken cancellationToken = default)
    {
        if (String.IsNullOrWhiteSpace(url))
            return "{\"error\": \"url is required\"}";

        // 仅允许 http/https，防止 SSRF 访问内网或其他协议
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return "{\"error\": \"url must be a valid http/https address\"}";

        if (IsSsrfRisk(uri.Host))
            return "{\"error\": \"access to private/internal network addresses is not allowed\"}";

        try
        {
            var resp = await _http.GetAsync(uri, cancellationToken).ConfigureAwait(false);
            var html = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!resp.IsSuccessStatusCode)
                return $"{{\"error\": \"HTTP {(Int32)resp.StatusCode}\"}}";

            var text = ExtractTextFromHtml(html);
            if (maxLength > 0 && text.Length > maxLength)
                text = text[..maxLength] + $"\n\n[已截断，原文共 {text.Length} 字符]";

            return text;
        }
        catch (Exception ex)
        {
            return $"{{\"error\": \"{ex.Message}\"}}";
        }
    }

    /// <summary>使用搜索引擎检索互联网信息，返回标题、链接和摘要列表。适用于查找最新资讯、事实核查等场景</summary>
    /// <param name="query">搜索关键词或自然语言问题</param>
    /// <param name="count">返回结果数量，1~10 之间。默认 5</param>
    [ToolDescription("web_search")]
    public async Task<String> WebSearchAsync(String query, Int32 count = 5, CancellationToken cancellationToken = default)
    {
        if (String.IsNullOrWhiteSpace(query))
            return "{\"error\": \"query is required\"}";

        count = Math.Max(1, Math.Min(count, 10));

        try
        {
            return SearchProvider.ToLowerInvariant() switch
            {
                "serper" => await SearchSerperAsync(query, count, cancellationToken).ConfigureAwait(false),
                "duckduckgo" => await SearchDuckDuckGoAsync(query, cancellationToken).ConfigureAwait(false),
                _ => await SearchBingAsync(query, count, cancellationToken).ConfigureAwait(false),
            };
        }
        catch (Exception ex)
        {
            return $"{{\"error\": \"{ex.Message}\"}}";
        }
    }

    #endregion

    #region 辅助

    private async Task<String> SearchBingAsync(String query, Int32 count, CancellationToken ct)
    {
        // 无密钥时降级到 DuckDuckGo
        if (String.IsNullOrEmpty(SearchApiKey))
            return await SearchDuckDuckGoAsync(query, ct).ConfigureAwait(false);

        var encoded = Uri.EscapeDataString(query);
        using var req = new HttpRequestMessage(HttpMethod.Get,
            $"https://api.bing.microsoft.com/v7.0/search?q={encoded}&count={count}&mkt=zh-CN");
        req.Headers.Add("Ocp-Apim-Subscription-Key", SearchApiKey);

        var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
            return $"{{\"error\": \"Bing Search API error {(Int32)resp.StatusCode}\"}}";

        return FormatBingResults(json);
    }

    private async Task<String> SearchSerperAsync(String query, Int32 count, CancellationToken ct)
    {
        if (String.IsNullOrEmpty(SearchApiKey))
            return "{\"error\": \"SearchApiKey is required for Serper provider\"}";

        var body = $"{{\"q\":{query.ToJson()},\"num\":{count},\"hl\":\"zh-cn\"}}";
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://google.serper.dev/search");
        req.Headers.Add("X-API-KEY", SearchApiKey);
        req.Content = new StringContent(body, Encoding.UTF8, "application/json");

        var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
            return $"{{\"error\": \"Serper API error {(Int32)resp.StatusCode}\"}}";

        return FormatSerperResults(json);
    }

    private async Task<String> SearchDuckDuckGoAsync(String query, CancellationToken ct)
    {
        var encoded = Uri.EscapeDataString(query);
        var resp = await _http.GetAsync(
            $"https://api.duckduckgo.com/?q={encoded}&format=json&no_redirect=1&no_html=1", ct)
            .ConfigureAwait(false);
        var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
        return FormatDuckDuckGoResults(json);
    }

    /// <summary>校验是否为 SSRF 风险地址（私有/回环/链路本地）</summary>
    /// <param name="host">主机名或 IP</param>
    private static Boolean IsSsrfRisk(String host)
    {
        if (String.IsNullOrEmpty(host)) return true;
        var lower = host.ToLowerInvariant();

        // 回环
        if (lower == "localhost" || lower == "ip6-localhost" || lower == "ip6-loopback") return true;

        if (!System.Net.IPAddress.TryParse(host, out var ip))
            return false;

        var bytes = ip.GetAddressBytes();
        // IPv4
        if (bytes.Length == 4)
        {
            // 127.x.x.x
            if (bytes[0] == 127) return true;
            // 10.x.x.x
            if (bytes[0] == 10) return true;
            // 172.16-31.x.x
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;
            // 192.168.x.x
            if (bytes[0] == 192 && bytes[1] == 168) return true;
            // 169.254.x.x (链路本地 / AWS metadata)
            if (bytes[0] == 169 && bytes[1] == 254) return true;
            // 0.0.0.0
            if (bytes[0] == 0) return true;
        }
        // IPv6 回环 ::1
        if (bytes.Length == 16 && ip.Equals(System.Net.IPAddress.IPv6Loopback)) return true;

        return false;
    }

    /// <summary>从 HTML 字符串中提取纯文本正文</summary>
    /// <param name="html">原始 HTML 内容</param>
    private static String ExtractTextFromHtml(String html)
    {
        if (String.IsNullOrEmpty(html)) return String.Empty;

        // 移除 <script> 和 <style> 块
        var text = Regex.Replace(html, @"<(script|style)[^>]*>[\s\S]*?</\1>", " ", RegexOptions.IgnoreCase);
        // 移除所有 HTML 标签
        text = Regex.Replace(text, @"<[^>]+>", " ");
        // 解码 HTML 实体
        text = WebUtility.HtmlDecode(text);
        // 合并多余空白
        text = Regex.Replace(text, @"[ \t]+", " ");
        text = Regex.Replace(text, @"\n{3,}", "\n\n");
        return text.Trim();
    }

    /// <summary>格式化 Bing 搜索结果 JSON 为可读列表</summary>
    /// <param name="json">Bing Search API 响应 JSON</param>
    private static String FormatBingResults(String json)
    {
        try
        {
            var root = json.ToJsonEntity<BingSearchResponse>();
            var items = root?.WebPages?.Value;
            if (items == null || items.Count == 0)
                return "[]";

            var sb = Pool.StringBuilder.Get();
            sb.Append("[");
            for (var i = 0; i < items.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append($"{{\"title\":{items[i].Name.ToJson()},\"url\":{items[i].Url.ToJson()},\"snippet\":{items[i].Snippet.ToJson()}}}");
            }
            sb.Append("]");
            return sb.Return(true);
        }
        catch (Exception ex)
        {
            return $"{{\"error\": \"{ex.Message}\"}}";
        }
    }

    /// <summary>格式化 Serper 搜索结果 JSON 为可读列表</summary>
    /// <param name="json">Serper API 响应 JSON</param>
    private static String FormatSerperResults(String json)
    {
        try
        {
            var root = json.ToJsonEntity<SerperSearchResponse>();
            var items = root?.Organic;
            if (items == null || items.Count == 0)
                return "[]";

            var sb = Pool.StringBuilder.Get();
            sb.Append("[");
            for (var i = 0; i < items.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append($"{{\"title\":{items[i].Title.ToJson()},\"url\":{items[i].Link.ToJson()},\"snippet\":{items[i].Snippet.ToJson()}}}");
            }
            sb.Append("]");
            return sb.Return(true);
        }
        catch (Exception ex)
        {
            return $"{{\"error\": \"{ex.Message}\"}}";
        }
    }

    /// <summary>格式化 DuckDuckGo 即时问答结果为可读文本</summary>
    /// <param name="json">DuckDuckGo API 响应 JSON</param>
    private static String FormatDuckDuckGoResults(String json)
    {
        try
        {
            var root = json.ToJsonEntity<DuckDuckGoResponse>();
            var sb = Pool.StringBuilder.Get();
            sb.Append("[");
            var first = true;

            // 优先输出摘要
            if (!String.IsNullOrEmpty(root?.AbstractText))
            {
                sb.Append($"{{\"title\":{root.Heading.ToJson()},\"url\":{root.AbstractURL.ToJson()},\"snippet\":{root.AbstractText.ToJson()}}}");
                first = false;
            }

            // 补充相关主题
            if (root?.RelatedTopics != null)
            {
                foreach (var topic in root.RelatedTopics)
                {
                    if (String.IsNullOrEmpty(topic.Text)) continue;
                    if (!first) sb.Append(",");
                    sb.Append($"{{\"title\":{topic.Text.ToJson()},\"url\":{topic.FirstURL.ToJson()},\"snippet\":\"\"}}");
                    first = false;
                    if (sb.Length > 3000) break;
                }
            }

            sb.Append("]");
            return sb.Return(true);
        }
        catch (Exception ex)
        {
            return $"{{\"error\": \"{ex.Message}\"}}";
        }
    }

    #endregion

    #region 内部模型

    private class BingWebItem { public String? Name { get; set; } public String? Url { get; set; } public String? Snippet { get; set; } }
    private class BingWebPages { public List<BingWebItem>? Value { get; set; } }
    private class BingSearchResponse { public BingWebPages? WebPages { get; set; } }

    private class SerperItem { public String? Title { get; set; } public String? Link { get; set; } public String? Snippet { get; set; } }
    private class SerperSearchResponse { public List<SerperItem>? Organic { get; set; } }

    private class DuckDuckGoTopic { public String? Text { get; set; } public String? FirstURL { get; set; } }
    private class DuckDuckGoResponse
    {
        public String? Heading { get; set; }
        public String? AbstractText { get; set; }
        public String? AbstractURL { get; set; }
        public List<DuckDuckGoTopic>? RelatedTopics { get; set; }
    }

    #endregion
}
