using System.Text;
using NewLife.Serialization;

namespace NewLife.AI.Tools;

/// <summary>Serper.dev 搜索实现。基于 Google 搜索结果，需要 serper.dev 密钥</summary>
public class SearchSerperService : ISearchService
{
    private readonly HttpClient _http;
    private readonly String? _apiKey;

    /// <summary>初始化 Serper 搜索服务</summary>
    /// <param name="apiKey">serper.dev 密钥；为空时不可用</param>
    /// <param name="httpClient">HTTP 客户端；为 null 时自动创建默认实例</param>
    public SearchSerperService(String? apiKey, HttpClient? httpClient = null)
    {
        _apiKey = apiKey;
        _http = httpClient ?? ToolHelper.CreateDefaultHttpClient();
    }

    /// <summary>使用 Serper 搜索引擎检索互联网信息</summary>
    /// <param name="query">搜索关键词</param>
    /// <param name="count">返回结果数量</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>成功返回搜索结果；无密钥或失败返回 null</returns>
    public async Task<SearchModel?> SearchAsync(String query, Int32 count = 5, CancellationToken cancellationToken = default)
    {
        if (String.IsNullOrEmpty(_apiKey)) return null;

        try
        {
            var body = new { q = query, num = count, hl = "zh-cn" };
            using var req = new HttpRequestMessage(HttpMethod.Post, "https://google.serper.dev/search");
            req.Headers.Add("X-API-KEY", _apiKey);
            req.Content = new StringContent(body.ToJson(), Encoding.UTF8, "application/json");

            var resp = await _http.SendAsync(req, cancellationToken).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            var root = json.ToJsonEntity<SerperSearchResponse>();
            var items = root?.Organic;
            if (items == null || items.Count == 0) return null;

            var model = new SearchModel();
            foreach (var item in items)
            {
                model.Items.Add(new SearchItem { Title = item.Title, Url = item.Link, Snippet = item.Snippet });
            }
            return model;
        }
        catch
        {
            return null;
        }
    }

    #region 内部模型
    private class SerperItem { public String? Title { get; set; } public String? Link { get; set; } public String? Snippet { get; set; } }
    private class SerperSearchResponse { public List<SerperItem>? Organic { get; set; } }
    #endregion
}
