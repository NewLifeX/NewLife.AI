using System;
using System.ComponentModel;
using System.Net.Http;
using System.Threading.Tasks;
using NewLife.AI.Clients;
using NewLife.AI.Clients.OpenAI;
using NewLife.AI.Models;
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

    #region SerializeBody 内部键剔除

    [Theory]
    [DisplayName("IsInternalItemKey_双规则_识别内部保留键")]
    [InlineData("IChatContext", true)]
    [InlineData("MaxInputTokens", true)]
    [InlineData("__GatewayAppKey", true)]
    [InlineData("__X", true)]
    [InlineData("Foo", false)]
    [InlineData("enable_search", false)]
    [InlineData("seed", false)]
    public void IsInternalItemKey_DualRule_DetectsInternalKeys(String key, Boolean expected)
        => Assert.Equal(expected, AiClientBase.IsInternalItemKey(key));

    [Fact]
    [DisplayName("SerializeBody_内部键剔除_协议扩展键保留且原字典恢复")]
    public void SerializeBody_InternalKeysStripped_ProtocolKeysKeptAndItemsRestored()
    {
        // 模拟管道：内部键与协议扩展键混在共享 Items 中（与 MessageFlow 写入 IChatContext/MaxInputTokens 同源）。
        // 曾因未剔除，SystemJson 的 ExtendableConverter 把内部键扁平化到 JSON 顶层，Azure（经 liteLLM）报 Unknown parameter: 'IChatContext'
        var options = new ChatOptions();
        options["IChatContext"] = "ctx-marker";
        options["MaxInputTokens"] = 100;
        options["__X"] = 1;
        options["Foo"] = "bar";

        var request = ChatRequest.Create([new ChatMessage { Role = "user", Content = "hi" }], options, stream: true);
        var wire = ChatCompletionRequest.FromChatRequest(request);

        using var client = new OpenAIChatClient("test-key");
        var json = AiClientBase.SerializeBody(wire, client.JsonHost, client.JsonOptions);

        // 内部键不得上 wire
        Assert.DoesNotContain("IChatContext", json);
        Assert.DoesNotContain("MaxInputTokens", json);
        Assert.DoesNotContain("__X", json);

        // 协议扩展键仍正常透传
        Assert.Contains("\"Foo\":\"bar\"", json);

        // 序列化后恢复原字典（PostAsync 重试共用同一 body，内部键不能丢）
        Assert.Equal("ctx-marker", wire["IChatContext"]);
        Assert.Equal(100, (Int32)wire["MaxInputTokens"]!);
        Assert.Equal(1, (Int32)wire["__X"]!);
        Assert.Equal("bar", wire["Foo"]);
    }

    [Fact]
    [DisplayName("SerializeBody_字符串_原样返回")]
    public void SerializeBody_String_PassesThrough()
    {
        using var client = new OpenAIChatClient("test-key");
        var json = AiClientBase.SerializeBody("raw-body", client.JsonHost, client.JsonOptions);
        Assert.Equal("raw-body", json);
    }
    #endregion
}
