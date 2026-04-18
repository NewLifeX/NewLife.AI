using System.ComponentModel;
using NewLife.Model;

namespace NewLife.AI.Tools;

/// <summary>网络工具服务。提供网页抓取、搜索、IP 归属地查询、实时天气、文本翻译等能力，通过 ToolRegistry 注册后供 AI 模型调用</summary>
/// <remarks>
/// 通过 IServiceProvider 在内部解析各服务接口集合，遍历尝试每个实现直到获取有效结果。
/// newlife 远程兜底实现建议注册在最后，作为最终降级方案。
/// </remarks>
/// <remarks>初始化网络工具服务，从 IServiceProvider 内部解析各服务集合</remarks>
/// <param name="serviceProvider">依赖注入服务提供者</param>
public class NetworkToolService(IServiceProvider serviceProvider)
{
    private static IEnumerable<T> Resolve<T>(IServiceProvider sp) => sp.GetService<IEnumerable<T>>() ?? [];

    #region 网页工具

    /// <summary>爬取指定 URL 的网页内容并提取正文文本。适用于读取文章、文档或任何公开网页</summary>
    /// <param name="url">要爬取的网页地址，必须是完整的 http/https URL</param>
    /// <param name="maxLength">返回的最大字符数，防止超长内容占用过多 token。默认 5000</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ToolDescription("web_fetch", Triggers = "读取网页,抓取网页,总结网页,读取链接内容,打开这个链接")]
    [DisplayName("网页抓取")]
    [Description("爬取指定 URL 的网页内容并提取正文文本。适用于读取文章、文档或任何公开网页")]
    public async Task<Object> WebFetchAsync(
        [Description("要爬取的网页地址，必须是完整的 http/https URL")] String url,
        [Description("返回的最大字符数，防止超长内容占用过多 token。默认 5000")] Int32 maxLength = 5000,
        CancellationToken cancellationToken = default)
    {
        if (String.IsNullOrWhiteSpace(url))
            return new { error = "url is required" };

        var _fetchServices = Resolve<IWebFetchService>(serviceProvider);
        foreach (var svc in _fetchServices)
        {
            var result = await svc.FetchAsync(url, maxLength, cancellationToken).ConfigureAwait(false);
            if (result != null) return result;
        }

        return new { error = "all web fetch providers failed" };
    }

    /// <summary>使用搜索引擎检索互联网信息，返回标题、链接和摘要列表。适用于查找最新资讯、事实核查等场景</summary>
    /// <param name="query">搜索关键词或自然语言问题</param>
    /// <param name="count">返回结果数量，1~10 之间。默认 5</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ToolDescription("web_search", Triggers = "帮我搜索,搜索一下,联网搜索,上网搜索,查一下资料")]
    [DisplayName("网络搜索")]
    [Description("使用搜索引擎检索互联网信息，返回标题、链接和摘要列表。适用于查找最新资讯、事实核查等场景")]
    public async Task<Object> WebSearchAsync(
        [Description("搜索关键词或自然语言问题")] String query,
        [Description("返回结果数量，1~10 之间。默认 5")] Int32 count = 5,
        CancellationToken cancellationToken = default)
    {
        if (String.IsNullOrWhiteSpace(query))
            return new { error = "query is required" };

        count = Math.Max(1, Math.Min(count, 10));

        var _searchServices = Resolve<ISearchService>(serviceProvider);
        foreach (var svc in _searchServices)
        {
            var result = await svc.SearchAsync(query, count, cancellationToken).ConfigureAwait(false);
            if (result != null && result.Items.Count > 0) return result;
        }

        return new { error = "all search providers failed" };
    }

    #endregion

    #region IP 归属地

    /// <summary>
    /// 查询 IP 地址的归属地信息（国家、省份、城市、运营商）。
    /// 按注册顺序遍历所有 IP 查询服务直到获取有效结果。
    /// 不传入 ip 时查询本机当前公网 IP。
    /// 若需查询 Web 访问者 IP，请由调用方从 HTTP 请求头中提取后传入此参数
    /// </summary>
    /// <param name="ip">要查询的 IPv4/IPv6 地址；留空则自动查询本机当前公网 IP</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ToolDescription("get_ip_location", Triggers = "ip归属地,ip地址,ip归属地,这个ip在哪,ip地理位置")]
    [DisplayName("IP归属地")]
    [Description("查询 IP 地址的归属地信息（国家、省份、城市、运营商）。不传入 ip 时查询本机当前公网 IP")]
    public async Task<Object> GetIpLocationAsync(
        [Description("要查询的 IPv4/IPv6 地址；留空则自动查询本机当前公网 IP")] String? ip = null,
        CancellationToken cancellationToken = default)
    {
        var _ipServices = Resolve<IIpLocationService>(serviceProvider);
        foreach (var svc in _ipServices)
        {
            var result = await svc.GetLocationAsync(ip, cancellationToken).ConfigureAwait(false);
            if (result != null) return result;
        }

        return new { error = "all IP location providers failed" };
    }

    #endregion

    #region 天气工具

    /// <summary>获取指定城市的实时天气信息，包括温度、湿度、风速、天气描述等。无需 API 密钥</summary>
    /// <param name="city">城市名称，支持中英文，如 Shanghai、上海、New York</param>
    /// <param name="unit">温度单位：C（摄氏度，默认）或 F（华氏度）</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ToolDescription("get_weather", Triggers = "今天天气,明天天气,天气怎么样,天气预报,查询天气")]
    [DisplayName("天气查询")]
    [Description("获取指定城市的实时天气信息，包括温度、湿度、风速、天气描述等。无需 API 密鑰")]
    public async Task<Object> GetWeatherAsync(
        [Description("城市名称，支持中英文，如 Shanghai、上海、New York")] String city,
        [Description("温度单位：C（摄氏度，默认）或 F（华氏度）")] String unit = "C",
        CancellationToken cancellationToken = default)
    {
        if (String.IsNullOrWhiteSpace(city))
            return new { error = "city is required" };

        var _weatherServices = Resolve<IWeatherService>(serviceProvider);
        foreach (var svc in _weatherServices)
        {
            var result = await svc.GetWeatherAsync(city, unit, cancellationToken).ConfigureAwait(false);
            if (result != null) return result;
        }

        return new { error = "all weather providers failed" };
    }

    #endregion

    #region 翻译工具

    /// <summary>将文本翻译为目标语言。支持 60+ 种语言，无需 API 密钥（每天免费额度 5000 词）</summary>
    /// <param name="text">要翻译的文本内容</param>
    /// <param name="targetLang">目标语言代码，如 zh（中文）、en（英文）、ja（日文）、fr（法文）、de（德文）、ko（韩文）</param>
    /// <param name="sourceLang">源语言代码，默认 auto（自动检测）</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ToolDescription("translate", Triggers = "帮我翻译,翻译这段,翻译成中文,翻译成英文,translate this")]
    [DisplayName("文本翻译")]
    [Description("将文本翻译为目标语言。支持 60+ 种语言，无需 API 密鑰（每天免费额度 5000 词）")]
    public async Task<Object> TranslateAsync(
        [Description("要翻译的文本内容")] String text,
        [Description("目标语言代码，如 zh（中文）、en（英文）、ja（日文）、fr（法文）、de（德文）、ko（韩文）")] String targetLang = "zh",
        [Description("源语言代码，默认 auto（自动检测）")] String sourceLang = "auto",
        CancellationToken cancellationToken = default)
    {
        if (String.IsNullOrEmpty(text))
            return new { error = "text is required" };

        var _translateServices = Resolve<ITranslateService>(serviceProvider);
        foreach (var svc in _translateServices)
        {
            var result = await svc.TranslateAsync(text, targetLang, sourceLang, cancellationToken).ConfigureAwait(false);
            if (result != null) return result;
        }

        return new { error = "all translate providers failed" };
    }

    #endregion
}
