using NewLife.Serialization;

namespace NewLife.AI.Tools;

/// <summary>远程网页爬取实现。通过 HTTP 调用远端 API（默认 ai.newlifex.com），作为兜底方案</summary>
public class WebFetchRemoteService : IWebFetchService
{
    private readonly HttpClient _http;
    private readonly String _baseUrl;

    /// <summary>初始化远程网页爬取服务</summary>
    /// <param name="baseUrl">远程服务基础 URL，默认 https://ai.newlifex.com</param>
    /// <param name="httpClient">HTTP 客户端；为 null 时自动创建默认实例</param>
    public WebFetchRemoteService(String baseUrl = "https://ai.newlifex.com", HttpClient? httpClient = null)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _http = httpClient ?? ToolHelper.CreateDefaultHttpClient();
    }

    /// <summary>爬取指定 URL 的网页内容并提取正文文本</summary>
    /// <param name="url">要爬取的网页地址</param>
    /// <param name="maxLength">返回的最大字符数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>成功返回网页正文；失败返回 null</returns>
    public async Task<WebFetchModel?> FetchAsync(String url, Int32 maxLength = 5000, CancellationToken cancellationToken = default)
    {
        try
        {
            var apiUrl = $"{_baseUrl}/api/fetch?url={Uri.EscapeDataString(url)}&maxLength={maxLength}";
            var resp = await _http.GetAsync(apiUrl, cancellationToken).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            return json.ToJsonEntity<WebFetchModel>();
        }
        catch
        {
            return null;
        }
    }
}
