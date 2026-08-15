using System.Xml.Linq;

namespace NewLife.AI.Tools;

/// <summary>Bing RSS 搜索实现。使用必应 RSS 输出端点，无需密钥，国内可用</summary>
/// <remarks>
/// 通过 <c>https://www.bing.com/search?q=xxx&amp;format=rss</c> 获取结构化 RSS 结果，
/// 不依赖 Azure 付费密钥，适合作为免费搜索主通道。
/// 注意：Bing RSS 版权声明限定个人非商业用途呈现，商业化部署请改用 Bing Web Search API 或 HTML 解析。
/// </remarks>
/// <remarks>初始化 Bing RSS 搜索服务</remarks>
/// <param name="httpClient">HTTP 客户端；为 null 时自动创建默认实例</param>
public class SearchBingRssService(HttpClient? httpClient = null) : ISearchService
{
    private readonly HttpClient _http = httpClient ?? ToolHelper.CreateDefaultHttpClient();

    /// <summary>使用必应 RSS 输出检索互联网信息</summary>
    /// <param name="query">搜索关键词</param>
    /// <param name="count">返回结果数量</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>成功返回搜索结果；失败返回 null</returns>
    public async Task<SearchModel?> SearchAsync(String query, Int32 count = 5, CancellationToken cancellationToken = default)
    {
        try
        {
            // 独立 8 秒超时，避免单提供者不可用时拖慢整个降级链
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(8000);

            var encoded = Uri.EscapeDataString(query);
            using var req = new HttpRequestMessage(HttpMethod.Get,
                $"https://www.bing.com/search?q={encoded}&format=rss&count={count}&mkt=zh-CN");

            var resp = await _http.SendAsync(req, cts.Token).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return null;

            var xml = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            var doc = XDocument.Parse(xml);

            var model = new SearchModel();
            foreach (var item in doc.Descendants("item").Take(count))
            {
                var title = item.Element("title")?.Value;
                var link = item.Element("link")?.Value;
                var desc = item.Element("description")?.Value;
                if (String.IsNullOrEmpty(title) && String.IsNullOrEmpty(desc)) continue;

                model.Items.Add(new SearchItem { Title = title, Url = link, Snippet = desc });
            }

            return model.Items.Count > 0 ? model : null;
        }
        catch
        {
            return null;
        }
    }
}
