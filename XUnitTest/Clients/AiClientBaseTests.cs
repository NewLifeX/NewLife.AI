using System;
using System.ComponentModel;
using System.Net.Http;
using System.Threading.Tasks;
using NewLife.AI.Clients;
using NewLife.AI.Clients.OpenAI;
using Xunit;

namespace XUnitTest.Clients;

/// <summary>AiClientBase 辅助方法单元测试</summary>
public class AiClientBaseTests
{
    #region CombineApiUrl

    [Fact]
    [DisplayName("CombineApiUrl_端点无版本段_直接拼接路径")]
    public void CombineApiUrl_NoVersionInEndpoint_AppendsPathDirectly()
    {
        var result = AiClientBase.CombineApiUrl("https://api.openai.com", "/v1/chat/completions");
        Assert.Equal("https://api.openai.com/v1/chat/completions", result);
    }

    [Fact]
    [DisplayName("CombineApiUrl_端点含v1后缀且路径以v1开头_去重版本段")]
    public void CombineApiUrl_EndpointHasV1AndPathHasV1_DeduplicatesVersion()
    {
        var result = AiClientBase.CombineApiUrl("https://example.com/v1", "/v1/chat/completions");
        Assert.Equal("https://example.com/v1/chat/completions", result);
    }

    [Fact]
    [DisplayName("CombineApiUrl_端点含v2用户版本且路径以v1开头_保留用户版本")]
    public void CombineApiUrl_EndpointHasV2AndPathHasV1_KeepsUserVersion()
    {
        var result = AiClientBase.CombineApiUrl("https://example.com/v2", "/v1/chat/completions");
        Assert.Equal("https://example.com/v2/chat/completions", result);
    }

    [Fact]
    [DisplayName("CombineApiUrl_端点含v4用户版本且路径以v1开头_保留v4版本")]
    public void CombineApiUrl_EndpointHasV4AndPathHasV1_KeepsV4Version()
    {
        var result = AiClientBase.CombineApiUrl("https://example.com/v4", "/v1/images/edits");
        Assert.Equal("https://example.com/v4/images/edits", result);
    }

    [Fact]
    [DisplayName("CombineApiUrl_端点含版本但路径无版本前缀_直接拼接")]
    public void CombineApiUrl_EndpointHasVersionButPathHasNoVersion_AppendsDirectly()
    {
        var result = AiClientBase.CombineApiUrl("https://example.com/v1", "/chat/completions");
        Assert.Equal("https://example.com/v1/chat/completions", result);
    }

    [Fact]
    [DisplayName("CombineApiUrl_端点含尾部斜杠_自动TrimEnd后拼接")]
    public void CombineApiUrl_EndpointHasTrailingSlash_TrimmedBeforeCombine()
    {
        var result = AiClientBase.CombineApiUrl("https://api.openai.com/", "/v1/models");
        Assert.Equal("https://api.openai.com/v1/models", result);
    }

    [Fact]
    [DisplayName("CombineApiUrl_DashScope兼容端点含v1_路径v1去重")]
    public void CombineApiUrl_DashScopeCompatibleEndpointWithV1_DeduplicatesVersion()
    {
        var result = AiClientBase.CombineApiUrl("https://dashscope.aliyuncs.com/compatible-mode/v1", "/v1/models");
        Assert.Equal("https://dashscope.aliyuncs.com/compatible-mode/v1/models", result);
    }

    [Fact]
    [DisplayName("CombineApiUrl_端点含v1beta后缀且路径以v1beta开头_去重版本段（Gemini）")]
    public void CombineApiUrl_EndpointHasV1BetaAndPathHasV1Beta_DeduplicatesVersion()
    {
        var result = AiClientBase.CombineApiUrl("https://generativelanguage.googleapis.com/v1beta", "/v1beta/models/gemini-2.5-flash:generateContent");
        Assert.Equal("https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent", result);
    }

    #endregion

    #region HttpClient 并发
    [Fact]
    [DisplayName("HttpClient_并发首访_返回同一实例（双检锁防双创建）")]
    public async Task HttpClient_ConcurrentFirstAccess_SameInstance()
    {
        using var client = new OpenAIChatClient(new AiClientOptions
        {
            Endpoint = "https://stub.local",
            ApiKey = "test-key",
            Model = "gpt-4o",
        });

        // 并发首次访问：修复前无锁会双创建泄漏一个连接池，修复后双检锁保证单实例
        const Int32 n = 16;
        var tasks = new Task<HttpClient>[n];
        for (var i = 0; i < n; i++)
            tasks[i] = Task.Run(() => client.HttpClient);

        var instances = await Task.WhenAll(tasks);

        var first = instances[0];
        for (var i = 1; i < instances.Length; i++)
            Assert.Same(first, instances[i]);
    }
    #endregion
}
