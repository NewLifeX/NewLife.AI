using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using NewLife.Collections;
using NewLife.Serialization;

namespace NewLife.AI.Tools;

/// <summary>网络工具服务。提供网页抓取、搜索、IP 归属地查询、实时天气、文本翻译等能力，通过 ToolRegistry 注册后供 AI 模型调用</summary>
/// <remarks>
/// 国内网络优先策略：
/// <list type="bullet">
/// <item>IP 归属地：优先调用淘宝 IP 服务（国内稳定），失败时降级到 ip-api.com</item>
/// <item>天气：使用 wttr.in（全球 CDN，国内可直连）</item>
/// <item>翻译：使用 MyMemory（免费，每天 5000 词额度）</item>
/// <item>搜索：默认 Bing（国内可用），可切换 Serper / DuckDuckGo</item>
/// </list>
/// </remarks>
public class NetworkToolService
{
    #region 属性

    private readonly HttpClient _http;

    /// <summary>搜索 API 密钥。Bing 填写 Azure Cognitive Services 密钥；Serper 填写 serper.dev 密钥；留空则退化为 DuckDuckGo 即时问答（无需密钥，功能有限）</summary>
    public String? SearchApiKey { get; set; }

    /// <summary>搜索引擎提供商：bing（默认）/ serper / duckduckgo</summary>
    public String SearchProvider { get; set; } = "bing";

    #endregion

    #region 构造

    /// <summary>初始化网络工具服务</summary>
    /// <param name="httpClient">HTTP 客户端；为 null 时自动创建默认实例（含自动解压缩与重定向）</param>
    public NetworkToolService(HttpClient? httpClient = null)
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

    #region 网页工具

    /// <summary>爬取指定 URL 的网页内容并提取正文文本。适用于读取文章、文档或任何公开网页</summary>
    /// <param name="url">要爬取的网页地址，必须是完整的 http/https URL</param>
    /// <param name="maxLength">返回的最大字符数，防止超长内容占用过多 token。默认 5000</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ToolDescription("web_fetch")]
    public async Task<Object> WebFetchAsync(String url, Int32 maxLength = 5000, CancellationToken cancellationToken = default)
    {
        if (String.IsNullOrWhiteSpace(url))
            return new { error = "url is required" };

        // 仅允许 http/https，防止 SSRF 访问内网或其他协议
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return new { error = "url must be a valid http/https address" };

        if (IsSsrfRisk(uri.Host))
            return new { error = "access to private/internal network addresses is not allowed" };

        try
        {
            var resp = await _http.GetAsync(uri, cancellationToken).ConfigureAwait(false);
            var html = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!resp.IsSuccessStatusCode)
                return new { error = $"HTTP {(Int32)resp.StatusCode}" };

            var text = ExtractTextFromHtml(html);
            if (maxLength > 0 && text.Length > maxLength)
                text = text[..maxLength] + $"\n\n[已截断，原文共 {text.Length} 字符]";

            return text;
        }
        catch (Exception ex)
        {
            return new { error = ex.Message };
        }
    }

    /// <summary>使用搜索引擎检索互联网信息，返回标题、链接和摘要列表。适用于查找最新资讯、事实核查等场景</summary>
    /// <param name="query">搜索关键词或自然语言问题</param>
    /// <param name="count">返回结果数量，1~10 之间。默认 5</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ToolDescription("web_search")]
    public async Task<Object> WebSearchAsync(String query, Int32 count = 5, CancellationToken cancellationToken = default)
    {
        if (String.IsNullOrWhiteSpace(query))
            return new { error = "query is required" };

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
            return new { error = ex.Message };
        }
    }

    #endregion

    #region IP 归属地

    /// <summary>
    /// 查询 IP 地址的归属地信息（国家、省份、城市、运营商）。
    /// 优先使用淘宝 IP 服务（国内稳定），失败时自动降级到 ip-api.com。
    /// 不传入 ip 时查询本机当前公网 IP。
    /// 若需查询 Web 访问者 IP，请由调用方从 HTTP 请求头中提取后传入此参数
    /// </summary>
    /// <param name="ip">要查询的 IPv4/IPv6 地址；留空则自动查询本机当前公网 IP</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ToolDescription("get_ip_location")]
    public async Task<Object> GetIpLocationAsync(String? ip = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = String.IsNullOrWhiteSpace(ip) ? "myip" : ip.Trim();

            // 优先使用淘宝 IP 服务（大陆网络直连，速度快）
            var resp = await _http.GetAsync(
                $"https://ip.taobao.com/outGetIpInfo?ip={Uri.EscapeDataString(query)}&accessKey=alibaba-inc",
                cancellationToken).ConfigureAwait(false);
            var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            var taobao = json.ToJsonEntity<TaobaoIpResponse>();

            if (taobao?.Code == 0 && taobao.Data != null)
            {
                var d = taobao.Data;
                return new { ip = d.Ip, country = d.Country, area = d.Area, region = d.Region, city = d.City, county = d.County, isp = d.Isp };
            }

            // 降级到 ip-api.com
            var fallbackUrl = String.IsNullOrWhiteSpace(ip)
                ? "https://ip-api.com/json?fields=status,message,country,regionName,city,isp,query"
                : $"https://ip-api.com/json/{Uri.EscapeDataString(ip.Trim())}?fields=status,message,country,regionName,city,isp,query";

            var fbResp = await _http.GetAsync(fallbackUrl, cancellationToken).ConfigureAwait(false);
            var fbJson = await fbResp.Content.ReadAsStringAsync().ConfigureAwait(false);
            var fbData = fbJson.ToJsonEntity<IpApiResponse>();

            if (fbData?.Status != "success")
                return new { error = fbData?.Message ?? "IP lookup failed" };

            return new { ip = fbData.Query, country = fbData.Country, region = fbData.RegionName, city = fbData.City, isp = fbData.Isp };
        }
        catch (Exception ex)
        {
            return new { error = ex.Message };
        }
    }

    #endregion

    #region 天气工具

    /// <summary>获取指定城市的实时天气信息，包括温度、湿度、风速、天气描述等。无需 API 密钥</summary>
    /// <param name="city">城市名称，支持中英文，如 Shanghai、上海、New York</param>
    /// <param name="unit">温度单位：C（摄氏度，默认）或 F（华氏度）</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ToolDescription("get_weather")]
    public async Task<Object> GetWeatherAsync(String city, String unit = "C", CancellationToken cancellationToken = default)
    {
        if (String.IsNullOrWhiteSpace(city))
            return new { error = "city is required" };

        try
        {
            var encoded = Uri.EscapeDataString(city.Trim());
            var resp = await _http.GetAsync($"https://wttr.in/{encoded}?format=j1&lang=zh", cancellationToken).ConfigureAwait(false);
            var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!resp.IsSuccessStatusCode)
                return new { error = $"weather service returned {(Int32)resp.StatusCode}" };

            var data = json.ToJsonEntity<WttrResponse>();
            var cur = data?.CurrentCondition?.FirstOrDefault();
            if (cur == null)
                return new { error = "weather data unavailable" };

            var useF = unit.EqualIgnoreCase("F");
            var temp = useF ? $"{cur.TempF}°F" : $"{cur.TempC}°C";
            var feelsLike = useF ? $"{cur.FeelsLikeF}°F" : $"{cur.FeelsLikeC}°C";
            var desc = cur.WeatherDesc?.FirstOrDefault()?.Value ?? "";
            var area = data?.NearestArea?.FirstOrDefault();
            var areaName = area?.AreaName?.FirstOrDefault()?.Value ?? city;
            var country = area?.Country?.FirstOrDefault()?.Value ?? "";

            return new
            {
                city = areaName,
                country,
                description = desc,
                temp,
                feelsLike,
                humidity = $"{cur.Humidity}%",
                windSpeed = $"{cur.WindspeedKmph} km/h",
                visibility = $"{cur.Visibility} km",
                uvIndex = cur.UvIndex,
                observedAt = cur.ObservationTime,
            };
        }
        catch (Exception ex)
        {
            return new { error = ex.Message };
        }
    }

    #endregion

    #region 翻译工具

    /// <summary>将文本翻译为目标语言。支持 60+ 种语言，无需 API 密钥（每天免费额度 5000 词）</summary>
    /// <param name="text">要翻译的文本内容</param>
    /// <param name="targetLang">目标语言代码，如 zh（中文）、en（英文）、ja（日文）、fr（法文）、de（德文）、ko（韩文）</param>
    /// <param name="sourceLang">源语言代码，默认 auto（自动检测）</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ToolDescription("translate")]
    public async Task<Object> TranslateAsync(String text, String targetLang = "zh", String sourceLang = "auto", CancellationToken cancellationToken = default)
    {
        if (String.IsNullOrEmpty(text))
            return new { error = "text is required" };

        try
        {
            var q = Uri.EscapeDataString(text);
            var pair = Uri.EscapeDataString($"{sourceLang}|{targetLang}");
            var resp = await _http.GetAsync(
                $"https://api.mymemory.translated.net/get?q={q}&langpair={pair}",
                cancellationToken).ConfigureAwait(false);
            var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            var data = json.ToJsonEntity<MyMemoryResponse>();

            if (data?.ResponseStatus != 200)
                return new { error = data?.ResponseDetails ?? "translation failed" };

            var translated = data.ResponseData?.TranslatedText ?? "";
            return new { original = text, translated, sourceLang, targetLang };
        }
        catch (Exception ex)
        {
            return new { error = ex.Message };
        }
    }

    #endregion

    #region 辅助

    private async Task<Object> SearchBingAsync(String query, Int32 count, CancellationToken ct)
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
            return new { error = $"Bing Search API error {(Int32)resp.StatusCode}" };

        return FormatBingResults(json);
    }

    private async Task<Object> SearchSerperAsync(String query, Int32 count, CancellationToken ct)
    {
        if (String.IsNullOrEmpty(SearchApiKey))
            return new { error = "SearchApiKey is required for Serper provider" };

        var body = new { q = query, num = count, hl = "zh-cn" };
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://google.serper.dev/search");
        req.Headers.Add("X-API-KEY", SearchApiKey);
        req.Content = new StringContent(body.ToJson(), Encoding.UTF8, "application/json");

        var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
            return new { error = $"Serper API error {(Int32)resp.StatusCode}" };

        return FormatSerperResults(json);
    }

    private async Task<Object> SearchDuckDuckGoAsync(String query, CancellationToken ct)
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
    private static String ExtractTextFromHtml(String html)
    {
        if (String.IsNullOrEmpty(html)) return String.Empty;

        var text = Regex.Replace(html, @"<(script|style)[^>]*>[\s\S]*?</\1>", " ", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<[^>]+>", " ");
        text = WebUtility.HtmlDecode(text);
        text = Regex.Replace(text, @"[ \t]+", " ");
        text = Regex.Replace(text, @"\n{3,}", "\n\n");
        return text.Trim();
    }

    private static Object FormatBingResults(String json)
    {
        try
        {
            var root = json.ToJsonEntity<BingSearchResponse>();
            var items = root?.WebPages?.Value;
            if (items == null || items.Count == 0) return "[]";

            var sb = Pool.StringBuilder.Get();
            sb.Append('[');
            for (var i = 0; i < items.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(new { title = items[i].Name, url = items[i].Url, snippet = items[i].Snippet });
            }
            sb.Append(']');
            return sb.Return(true);
        }
        catch (Exception ex)
        {
            return new { error = ex.Message };
        }
    }

    private static Object FormatSerperResults(String json)
    {
        try
        {
            var root = json.ToJsonEntity<SerperSearchResponse>();
            var items = root?.Organic;
            if (items == null || items.Count == 0) return "[]";

            var sb = Pool.StringBuilder.Get();
            sb.Append('[');
            for (var i = 0; i < items.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(new { title = items[i].Title, url = items[i].Link, snippet = items[i].Snippet });
            }
            sb.Append(']');
            return sb.Return(true);
        }
        catch (Exception ex)
        {
            return new { error = ex.Message };
        }
    }

    private static Object FormatDuckDuckGoResults(String json)
    {
        try
        {
            var root = json.ToJsonEntity<DuckDuckGoResponse>();
            var sb = Pool.StringBuilder.Get();
            sb.Append('[');
            var first = true;

            if (!String.IsNullOrEmpty(root?.AbstractText))
            {
                sb.Append(new { title = root.Heading, url = root.AbstractURL, snippet = root.AbstractText });
                first = false;
            }

            if (root?.RelatedTopics != null)
            {
                foreach (var topic in root.RelatedTopics)
                {
                    if (String.IsNullOrEmpty(topic.Text)) continue;
                    if (!first) sb.Append(',');
                    sb.Append(new { title = topic.Text, url = topic.FirstURL, snippet = "" });
                    first = false;
                    if (sb.Length > 3000) break;
                }
            }

            sb.Append(']');
            return sb.Return(true);
        }
        catch (Exception ex)
        {
            return new { error = ex.Message };
        }
    }

    #endregion

    #region 内部模型

    // 搜索响应模型
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

    // 淘宝 IP 服务响应模型（国内优先）
    private class TaobaoIpData
    {
        public String? Ip { get; set; }
        public String? Country { get; set; }
        public String? Area { get; set; }
        public String? Region { get; set; }
        public String? City { get; set; }
        public String? County { get; set; }
        public String? Isp { get; set; }
    }
    private class TaobaoIpResponse
    {
        public Int32 Code { get; set; }
        public TaobaoIpData? Data { get; set; }
    }

    // ip-api.com 降级响应模型
    private class IpApiResponse
    {
        public String? Status { get; set; }
        public String? Message { get; set; }
        public String? Country { get; set; }
        public String? RegionName { get; set; }
        public String? City { get; set; }
        public String? Isp { get; set; }
        public String? Query { get; set; }
    }

    // wttr.in j1 格式响应模型
    private class WttrNameValue { public String? Value { get; set; } }
    private class WttrCurrentCondition
    {
        public String? TempC { get; set; }
        public String? TempF { get; set; }
        public String? FeelsLikeC { get; set; }
        public String? FeelsLikeF { get; set; }
        public String? Humidity { get; set; }
        public String? WindspeedKmph { get; set; }
        public String? Visibility { get; set; }
        public String? UvIndex { get; set; }
        public String? ObservationTime { get; set; }
        public List<WttrNameValue>? WeatherDesc { get; set; }
    }
    private class WttrNearestArea
    {
        public List<WttrNameValue>? AreaName { get; set; }
        public List<WttrNameValue>? Country { get; set; }
    }
    private class WttrResponse
    {
        public List<WttrCurrentCondition>? CurrentCondition { get; set; }
        public List<WttrNearestArea>? NearestArea { get; set; }
    }

    // MyMemory 翻译响应模型
    private class MyMemoryTranslation { public String? TranslatedText { get; set; } }
    private class MyMemoryResponse
    {
        public MyMemoryTranslation? ResponseData { get; set; }
        public Int32 ResponseStatus { get; set; }
        public String? ResponseDetails { get; set; }
    }

    #endregion
}
