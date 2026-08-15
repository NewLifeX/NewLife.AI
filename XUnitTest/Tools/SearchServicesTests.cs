#nullable enable
using System;
using System.ComponentModel;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using NewLife.AI.Tools;
using Xunit;
using XUnitTest.Helpers;

namespace XUnitTest.Tools;

/// <summary>搜索引擎服务单元测试。用 StubHttpMessageHandler 模拟 HTTP 响应，验证 RSS/HTML 解析逻辑，不依赖真实网络</summary>
public class SearchServicesTests
{
    #region Bing RSS

    private const String RssXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <rss version="2.0">
          <channel>
            <title>必应：星语</title>
            <item>
              <title>星语 - 新生命团队</title>
              <link>https://newlifex.com/starchat</link>
              <description>星语是新一代 AI 助手</description>
            </item>
            <item>
              <title>StarChat GitHub</title>
              <link>https://github.com/NewLifeX/StarChat</link>
              <description>开源代码仓库</description>
            </item>
          </channel>
        </rss>
        """;

    /// <summary>构建指向 stub 的 Bing RSS 服务</summary>
    private static SearchBingRssService CreateBingRss(StubHttpMessageHandler handler) => new(new HttpClient(handler));

    [Fact]
    [DisplayName("BingRss_正常响应_解析出标题链接摘要")]
    public async Task BingRss_OkResponse_ParsesItems()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(RssXml, System.Text.Encoding.UTF8, "application/xml"),
        });

        var model = await CreateBingRss(handler).SearchAsync("星语", 5);

        Assert.NotNull(model);
        Assert.Equal(2, model!.Items.Count);
        Assert.Equal("星语 - 新生命团队", model.Items[0].Title);
        Assert.Equal("https://newlifex.com/starchat", model.Items[0].Url);
        Assert.Equal("星语是新一代 AI 助手", model.Items[0].Snippet);
        // 请求地址应包含关键词与 rss 格式
        Assert.Contains("bing.com/search", handler.LastRequestUrl!);
        Assert.Contains("format=rss", handler.LastRequestUrl!);
        Assert.Contains("q=", handler.LastRequestUrl!);
    }

    [Fact]
    [DisplayName("BingRss_count限制_只返回指定条数")]
    public async Task BingRss_CountLimit_ReturnsLimitedItems()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(RssXml, System.Text.Encoding.UTF8, "application/xml"),
        });

        var model = await CreateBingRss(handler).SearchAsync("星语", 1);

        Assert.NotNull(model);
        Assert.Single(model!.Items);
    }

    [Fact]
    [DisplayName("BingRss_空结果或无item_返回null")]
    public async Task BingRss_EmptyChannel_ReturnsNull()
    {
        const String empty = """<?xml version="1.0"?><rss version="2.0"><channel><title>空</title></channel></rss>""";
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(empty, System.Text.Encoding.UTF8, "application/xml"),
        });

        var model = await CreateBingRss(handler).SearchAsync("无结果关键词", 5);

        Assert.Null(model);
    }

    [Fact]
    [DisplayName("BingRss_HTTP失败或无效XML_返回null")]
    public async Task BingRss_Failure_ReturnsNull()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        Assert.Null(await CreateBingRss(handler).SearchAsync("星语", 5));
    }

    #endregion

    #region DuckDuckGo HTML

    private const String DdgHtml = """
        <div class="result results_links results_links_deep web-result ">
          <h2 class="result__title">
            <a rel="nofollow" class="result__a" href="//duckduckgo.com/l/?uddg=https%3A%2F%2Fgithub.com%2FNewLifeX%2FNewLife.XCode&amp;rut=abc">NewLife.XCode - 数据中间件 / 超级ORM - <b>GitHub</b></a>
          </h2>
          <a class="result__snippet" href="//duckduckgo.com/l/?uddg=https%3A%2F%2Fgithub.com%2FNewLifeX%2FNewLife.XCode">DAL 抽象层 &amp; 查询 DSL</a>
        </div>
        """;

    [Fact]
    [DisplayName("DuckDuckGo_正常响应_解析标题并解码跳转链接")]
    public async Task DuckDuckGo_OkResponse_ParsesItems()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(DdgHtml, System.Text.Encoding.UTF8, "text/html"),
        });
        var svc = new SearchDuckDuckGoService(new HttpClient(handler));

        var model = await svc.SearchAsync("xcode", 5);

        Assert.NotNull(model);
        Assert.Single(model!.Items);
        // 标题应去除嵌套 <b> 标签并反转义
        Assert.Equal("NewLife.XCode - 数据中间件 / 超级ORM - GitHub", model.Items[0].Title);
        // uddg 跳转链接应解码出真实 URL，并补全 https 协议
        Assert.Equal("https://github.com/NewLifeX/NewLife.XCode", model.Items[0].Url);
        Assert.Equal("DAL 抽象层 & 查询 DSL", model.Items[0].Snippet);
    }

    [Fact]
    [DisplayName("DuckDuckGo_无结果页_返回null")]
    public async Task DuckDuckGo_NoResult_ReturnsNull()
    {
        const String empty = """<div class="no-results">No results found.</div>""";
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(empty, System.Text.Encoding.UTF8, "text/html"),
        });

        var model = await new SearchDuckDuckGoService(new HttpClient(handler)).SearchAsync("不存在的词", 5);

        Assert.Null(model);
    }

    #endregion

    #region 搜狗

    private const String SogouHtml = """
        <div class="vrwrap">
          <h3 class="vr-title"><a href="/link?url=xyz" data-md="1">星语助手</a></h3>
          <div class="text-layout">新一代 AI 对话助手，集成知识库与工具调用</div>
        </div>
        """;

    [Fact]
    [DisplayName("搜狗_正常响应_解析标题并补全相对链接")]
    public async Task Sogou_OkResponse_ParsesItems()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(SogouHtml, System.Text.Encoding.UTF8, "text/html"),
        });
        var svc = new SearchSogouService(new HttpClient(handler));

        var model = await svc.SearchAsync("星语", 5);

        Assert.NotNull(model);
        Assert.Single(model!.Items);
        Assert.Equal("星语助手", model.Items[0].Title);
        Assert.Equal("https://www.sogou.com/link?url=xyz", model.Items[0].Url);
        Assert.Equal("新一代 AI 对话助手，集成知识库与工具调用", model.Items[0].Snippet);
    }

    [Fact]
    [DisplayName("搜狗_无结果页_返回null")]
    public async Task Sogou_NoResult_ReturnsNull()
    {
        const String empty = """<div class="no-content">没有找到相关内容</div>""";
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(empty, System.Text.Encoding.UTF8, "text/html"),
        });

        var model = await new SearchSogouService(new HttpClient(handler)).SearchAsync("不存在的词", 5);

        Assert.Null(model);
    }

    #endregion
}
