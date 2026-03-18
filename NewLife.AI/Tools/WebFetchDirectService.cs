namespace NewLife.AI.Tools;

/// <summary>直接 HTTP 网页爬取实现。含 SSRF 防护，仅允许 http/https 协议访问公网地址</summary>
/// <remarks>初始化直接网页爬取服务</remarks>
/// <param name="httpClient">HTTP 客户端；为 null 时自动创建默认实例</param>
public class WebFetchDirectService(HttpClient? httpClient = null) : IWebFetchService
{
    private readonly HttpClient _http = httpClient ?? ToolHelper.CreateDefaultHttpClient();

    /// <summary>爬取指定 URL 的网页内容并提取正文文本</summary>
    /// <param name="url">要爬取的网页地址</param>
    /// <param name="maxLength">返回的最大字符数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>成功返回网页正文；URL 无效、SSRF 风险或失败返回 null</returns>
    public async Task<WebFetchModel?> FetchAsync(String url, Int32 maxLength = 5000, CancellationToken cancellationToken = default)
    {
        if (String.IsNullOrWhiteSpace(url)) return null;

        // 仅允许 http/https，防止 SSRF 访问内网或其他协议
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return null;

        if (ToolHelper.IsSsrfRisk(uri.Host)) return null;

        try
        {
            var resp = await _http.GetAsync(uri, cancellationToken).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return null;

            var html = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            var text = ToolHelper.ExtractTextFromHtml(html);
            var truncated = false;

            if (maxLength > 0 && text.Length > maxLength)
            {
                text = text[..maxLength] + $"\n\n[已截断，原文共 {text.Length} 字符]";
                truncated = true;
            }

            return new WebFetchModel
            {
                Url = url.Trim(),
                Text = text,
                Truncated = truncated,
            };
        }
        catch
        {
            return null;
        }
    }
}
