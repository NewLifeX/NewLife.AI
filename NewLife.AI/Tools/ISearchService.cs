namespace NewLife.AI.Tools;

/// <summary>搜索引擎服务接口。支持多实现链式降级（Bing → Serper → DuckDuckGo → 远程兜底）</summary>
public interface ISearchService
{
    /// <summary>使用搜索引擎检索互联网信息</summary>
    /// <param name="query">搜索关键词或自然语言问题</param>
    /// <param name="count">返回结果数量，1~10 之间</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>成功返回搜索结果列表；失败或不可用返回 null</returns>
    Task<SearchModel?> SearchAsync(String query, Int32 count = 5, CancellationToken cancellationToken = default);
}

/// <summary>搜索结果</summary>
public class SearchModel
{
    /// <summary>搜索结果条目列表</summary>
    public List<SearchItem> Items { get; set; } = [];
}

/// <summary>搜索结果条目</summary>
public class SearchItem
{
    /// <summary>标题</summary>
    public String? Title { get; set; }

    /// <summary>链接地址</summary>
    public String? Url { get; set; }

    /// <summary>摘要/描述</summary>
    public String? Snippet { get; set; }
}
