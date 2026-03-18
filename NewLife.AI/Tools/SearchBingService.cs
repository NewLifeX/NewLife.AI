using NewLife.Serialization;

namespace NewLife.AI.Tools;

/// <summary>Bing 搜索实现。需要 Azure Cognitive Services 密钥，国内可用</summary>
/// <remarks>初始化 Bing 搜索服务</remarks>
/// <param name="apiKey">Azure Cognitive Services 密钥；为空时不可用</param>
/// <param name="httpClient">HTTP 客户端；为 null 时自动创建默认实例</param>
public class SearchBingService(String? apiKey, HttpClient? httpClient = null) : ISearchService
{
    private readonly HttpClient _http = httpClient ?? ToolHelper.CreateDefaultHttpClient();

    /// <summary>使用 Bing 搜索引擎检索互联网信息</summary>
    /// <param name="query">搜索关键词</param>
    /// <param name="count">返回结果数量</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>成功返回搜索结果；无密钥或失败返回 null</returns>
    public async Task<SearchModel?> SearchAsync(String query, Int32 count = 5, CancellationToken cancellationToken = default)
    {
        if (String.IsNullOrEmpty(apiKey)) return null;

        try
        {
            var encoded = Uri.EscapeDataString(query);
            using var req = new HttpRequestMessage(HttpMethod.Get,
                $"https://api.bing.microsoft.com/v7.0/search?q={encoded}&count={count}&mkt=zh-CN");
            req.Headers.Add("Ocp-Apim-Subscription-Key", apiKey);

            var resp = await _http.SendAsync(req, cancellationToken).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            var root = json.ToJsonEntity<BingSearchResponse>();
            var items = root?.WebPages?.Value;
            if (items == null || items.Count == 0) return null;

            var model = new SearchModel();
            foreach (var item in items)
            {
                model.Items.Add(new SearchItem { Title = item.Name, Url = item.Url, Snippet = item.Snippet });
            }
            return model;
        }
        catch
        {
            return null;
        }
    }

    #region 内部模型
    private class BingWebItem { public String? Name { get; set; } public String? Url { get; set; } public String? Snippet { get; set; } }
    private class BingWebPages { public List<BingWebItem>? Value { get; set; } }
    private class BingSearchResponse { public BingWebPages? WebPages { get; set; } }
    #endregion
}
