using System;
using System.ComponentModel;
using NewLife.AI.Clients;
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

    #endregion
}
