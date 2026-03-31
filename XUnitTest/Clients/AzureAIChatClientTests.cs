#nullable enable
using System;
using System.ComponentModel;
using System.Linq;
using NewLife.AI.Clients;
using NewLife.AI.Models;
using NewLife.AI.Providers;
using Xunit;

namespace XUnitTest.Clients;

/// <summary>AzureAIChatClient 单元测试（不需要网络/ApiKey，验证 URL 构建和请求头设置）</summary>
public class AzureAIChatClientTests
{
    #region BuildUrl 单元测试

    [Fact]
    [DisplayName("BuildUrl_标准endpoint_生成正确的Azure部署URL")]
    public void BuildUrl_StandardEndpoint_GeneratesCorrectDeploymentUrl()
    {
        var client = new AzureAIChatClient("test-key", "gpt-4o", "https://myresource.openai.azure.com");
        var request = new ChatRequest
        {
            Messages = [new ChatMessage { Role = "user", Content = "hello" }],
            Model = "gpt-4o",
        };

        // 通过反射调用 protected BuildUrl
        var method = typeof(AzureAIChatClient).GetMethod("BuildUrl",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var url = method!.Invoke(client, [request]) as String;

        Assert.NotNull(url);
        Assert.Equal("https://myresource.openai.azure.com/openai/deployments/gpt-4o/chat/completions?api-version=2024-10-21", url);
    }

    [Fact]
    [DisplayName("BuildUrl_endpoint带斜杠_正确裁剪")]
    public void BuildUrl_EndpointWithTrailingSlash_TrimmedCorrectly()
    {
        var client = new AzureAIChatClient("test-key", "my-deploy", "https://myresource.openai.azure.com/");
        var request = new ChatRequest
        {
            Messages = [new ChatMessage { Role = "user", Content = "hello" }],
            Model = "my-deploy",
        };

        var method = typeof(AzureAIChatClient).GetMethod("BuildUrl",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var url = method!.Invoke(client, [request]) as String;

        Assert.NotNull(url);
        Assert.StartsWith("https://myresource.openai.azure.com/openai/deployments/my-deploy/", url);
        Assert.DoesNotContain("//openai", url);
    }

    [Fact]
    [DisplayName("SetHeaders_设置apikey头_不使用Bearer认证")]
    public void SetHeaders_SetsApiKeyHeader_NoBearerAuth()
    {
        var client = new AzureAIChatClient("my-azure-key", "gpt-4o", "https://myresource.openai.azure.com");

        var httpRequest = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, "https://myresource.openai.azure.com/openai/deployments/gpt-4o/chat/completions?api-version=2024-10-21");

        var method = typeof(AzureAIChatClient).GetMethod("SetHeaders",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method!.Invoke(client, [httpRequest, null, new AiClientOptions { ApiKey = "my-azure-key" }]);

        // 验证使用 api-key 头
        Assert.True(httpRequest.Headers.Contains("api-key"));
        Assert.Equal("my-azure-key", httpRequest.Headers.GetValues("api-key").First());

        // 验证没有 Bearer 认证
        Assert.Null(httpRequest.Headers.Authorization);
    }

    [Fact]
    [DisplayName("BuildUrl_使用请求中的model覆盖默认model")]
    public void BuildUrl_RequestModelOverridesDefault()
    {
        var client = new AzureAIChatClient("test-key", "default-deploy", "https://myresource.openai.azure.com");
        var request = new ChatRequest
        {
            Messages = [new ChatMessage { Role = "user", Content = "hello" }],
            Model = "custom-deploy",
        };

        var method = typeof(AzureAIChatClient).GetMethod("BuildUrl",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var url = method!.Invoke(client, [request]) as String;

        Assert.Contains("/deployments/custom-deploy/", url);
    }

    #endregion
}
