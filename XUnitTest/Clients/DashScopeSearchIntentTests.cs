#nullable enable
using System;
using System.ComponentModel;
using System.Net.Http;
using System.Threading.Tasks;
using NewLife.AI.Clients;
using NewLife.AI.Clients.DashScope;
using NewLife.AI.Models;
using Xunit;
using XUnitTest.Helpers;

namespace XUnitTest.Clients;

/// <summary>DashScope 联网搜索意图检测测试。验证检测结果不污染共享 request.Items（P1：跨请求永久强制联网搜索，行为+费用影响）</summary>
public class DashScopeSearchIntentTests
{
    /// <summary>构建指向 stub 地址的 DashScope 客户端（原生协议）</summary>
    private static DashScopeChatClient CreateClient(StubHttpMessageHandler handler)
    {
        var client = new DashScopeChatClient(new AiClientOptions
        {
            Endpoint = "https://stub.local",
            ApiKey = "test-key",
            Model = "qwen-plus",
        });
        client.HttpClient = new HttpClient(handler);
        return client;
    }

    private const String OkJson = """{"output":{"choices":[{"message":{"role":"assistant","content":"好的"},"finish_reason":"stop"}]},"usage":{"input_tokens":1,"output_tokens":1,"total_tokens":2},"request_id":"req_1"}""";

    /// <summary>断言请求体包含 enable_search 参数（大小写不敏感，兼容序列化差异）</summary>
    private static void AssertBodyHasEnableSearch(StubHttpMessageHandler handler)
        => Assert.Contains("enable_search", handler.LastRequestBody!, StringComparison.OrdinalIgnoreCase);

    [Fact]
    [DisplayName("搜索关键词触发检测_请求体含enable_search_且不污染Items")]
    public async Task SearchKeyword_DetectionApplied_ItemsNotPolluted()
    {
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json(OkJson));
        using var client = CreateClient(handler);

        var request = new ChatRequest
        {
            Model = "qwen-plus",
            Messages = [new ChatMessage { Role = "user", Content = "查一下今天的天气" }],
        };

        var response = await client.GetResponseAsync(request);

        Assert.NotNull(response);
        // 检测生效：原生请求体 parameters 含 enable_search=true
        AssertBodyHasEnableSearch(handler);
        // 核心回归：检测结果不得写入共享 Items，防止跨请求永久污染
        Assert.Null(request["EnableSearch"]);
        Assert.Null(request["EnableWebExtractor"]);
        Assert.Null(request["EnableSource"]);
    }

    [Fact]
    [DisplayName("URL触发爬取检测_原生路径不落Items")]
    public async Task Url_Detection_NotPolluteItems()
    {
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json(OkJson));
        using var client = CreateClient(handler);

        var request = new ChatRequest
        {
            Model = "qwen-plus",
            Messages = [new ChatMessage { Role = "user", Content = "请抓取 https://example.com/page 的内容" }],
        };

        await client.GetResponseAsync(request);

        Assert.Null(request["EnableSearch"]);
        Assert.Null(request["EnableWebExtractor"]);
        Assert.Null(request["EnableSource"]);
    }

    [Fact]
    [DisplayName("显式设置EnableSearch_尊重调用方_不自动检测覆盖")]
    public async Task ExplicitEnableSearch_Respected_NoAutoDetect()
    {
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json(OkJson));
        using var client = CreateClient(handler);

        var request = new ChatRequest
        {
            Model = "qwen-plus",
            Messages = [new ChatMessage { Role = "user", Content = "你好" }],
        };
        request["EnableSearch"] = true;

        var response = await client.GetResponseAsync(request);

        Assert.NotNull(response);
        // 显式设置时检测跳过，请求体仍带 enable_search（来自显式设置）
        AssertBodyHasEnableSearch(handler);
        Assert.True((Boolean)request["EnableSearch"]!);
    }
}
