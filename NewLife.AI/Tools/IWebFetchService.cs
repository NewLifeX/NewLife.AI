namespace NewLife.AI.Tools;

/// <summary>网页爬取服务接口。支持多实现链式降级（直接爬取 → 远程兜底）</summary>
public interface IWebFetchService
{
    /// <summary>爬取指定 URL 的网页内容并提取正文文本</summary>
    /// <param name="url">要爬取的网页地址，必须是完整的 http/https URL</param>
    /// <param name="maxLength">返回的最大字符数，防止超长内容占用过多 token</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>成功返回网页正文；失败或不可用返回 null</returns>
    Task<WebFetchModel?> FetchAsync(String url, Int32 maxLength = 5000, CancellationToken cancellationToken = default);
}

/// <summary>网页爬取结果</summary>
public class WebFetchModel
{
    /// <summary>爬取的 URL 地址</summary>
    public String? Url { get; set; }

    /// <summary>提取的纯文本正文</summary>
    public String? Text { get; set; }

    /// <summary>是否已截断</summary>
    public Boolean Truncated { get; set; }
}
