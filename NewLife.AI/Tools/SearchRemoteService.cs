using NewLife.Serialization;

namespace NewLife.AI.Tools;

/// <summary>远程搜索实现。通过 HTTP 调用远端 API（默认 ai.newlifex.com），作为兜底方案</summary>
public class SearchRemoteService : ISearchService
{
    private readonly HttpClient _http;
    private readonly String _baseUrl;

    /// <summary>初始化远程搜索服务</summary>
    /// <param name="baseUrl">远程服务基础 URL，默认 https://ai.newlifex.com</param>
    /// <param name="httpClient">HTTP 客户端；为 null 时自动创建默认实例</param>
    public SearchRemoteService(String baseUrl = "https://ai.newlifex.com", HttpClient? httpClient = null)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _http = httpClient ?? ToolHelper.CreateDefaultHttpClient();
    }

    /// <summary>使用搜索引擎检索互联网信息</summary>
    /// <param name="query">搜索关键词</param>
    /// <param name="count">返回结果数量</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>成功返回搜索结果；失败返回 null</returns>
    public async Task<SearchModel?> SearchAsync(String query, Int32 count = 5, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{_baseUrl}/api/search?query={Uri.EscapeDataString(query)}&count={count}";
            var resp = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            return json.ToJsonEntity<SearchModel>();
        }
        catch
        {
            return null;
        }
    }
}
