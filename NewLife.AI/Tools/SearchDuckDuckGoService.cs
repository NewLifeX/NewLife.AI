using NewLife.Serialization;

namespace NewLife.AI.Tools;

/// <summary>DuckDuckGo 即时问答搜索实现。无需密钥，功能有限但完全免费</summary>
public class SearchDuckDuckGoService : ISearchService
{
    private readonly HttpClient _http;

    /// <summary>初始化 DuckDuckGo 搜索服务</summary>
    /// <param name="httpClient">HTTP 客户端；为 null 时自动创建默认实例</param>
    public SearchDuckDuckGoService(HttpClient? httpClient = null) => _http = httpClient ?? ToolHelper.CreateDefaultHttpClient();

    /// <summary>使用 DuckDuckGo 即时问答检索信息</summary>
    /// <param name="query">搜索关键词</param>
    /// <param name="count">返回结果数量（DuckDuckGo 即时问答不精确控制数量）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>成功返回搜索结果；失败返回 null</returns>
    public async Task<SearchModel?> SearchAsync(String query, Int32 count = 5, CancellationToken cancellationToken = default)
    {
        try
        {
            var encoded = Uri.EscapeDataString(query);
            var resp = await _http.GetAsync(
                $"https://api.duckduckgo.com/?q={encoded}&format=json&no_redirect=1&no_html=1",
                cancellationToken).ConfigureAwait(false);
            var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            var root = json.ToJsonEntity<DuckDuckGoResponse>();

            var model = new SearchModel();

            if (!String.IsNullOrEmpty(root?.AbstractText))
                model.Items.Add(new SearchItem { Title = root.Heading, Url = root.AbstractURL, Snippet = root.AbstractText });

            if (root?.RelatedTopics != null)
            {
                foreach (var topic in root.RelatedTopics)
                {
                    if (String.IsNullOrEmpty(topic.Text)) continue;
                    model.Items.Add(new SearchItem { Title = topic.Text, Url = topic.FirstURL });
                    if (model.Items.Count >= count) break;
                }
            }

            return model.Items.Count > 0 ? model : null;
        }
        catch
        {
            return null;
        }
    }

    #region 内部模型
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
