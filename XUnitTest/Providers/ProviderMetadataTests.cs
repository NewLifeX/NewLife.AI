#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using NewLife.AI.Clients;
using Xunit;

namespace XUnitTest.Providers;

/// <summary>服务商注册元数据校验测试。验证全部内置服务商描述符的元数据完整性（endpoint/路径/模型/工厂），确保注册质量</summary>
public class ProviderMetadataTests
{
    #region 描述符元数据

    [Fact]
    [DisplayName("全部描述符_Code与DisplayName非空")]
    public void AllDescriptors_HaveCodeAndDisplayName()
    {
        foreach (var d in AiClientRegistry.Default.Descriptors.Values)
        {
            Assert.False(String.IsNullOrWhiteSpace(d.Code), $"Code 为空: {d.DisplayName}");
            Assert.False(String.IsNullOrWhiteSpace(d.DisplayName), $"DisplayName 为空: {d.Code}");
        }
    }

    [Fact]
    [DisplayName("全部描述符_默认端点合法且为http/https")]
    public void AllDescriptors_HaveValidEndpoint()
    {
        foreach (var d in AiClientRegistry.Default.Descriptors.Values)
        {
            // AzureAI 等端点模板含 {resource} 占位符（用户按资源名替换），先替换为合法主机再校验
            var sample = System.Text.RegularExpressions.Regex.Replace(d.DefaultEndpoint, @"\{[^}]*\}", "sample");
            Assert.True(Uri.TryCreate(sample, UriKind.Absolute, out var uri),
                $"Endpoint 无效: {d.Code} => {d.DefaultEndpoint}");
            Assert.True(uri!.Scheme == "http" || uri.Scheme == "https",
                $"Endpoint 协议不支持: {d.Code} => {uri.Scheme}");
        }
    }

    [Fact]
    [DisplayName("全部描述符_注册模型标识与显示名非空")]
    public void AllDescriptors_RegisteredModelsHaveValidMetadata()
    {
        foreach (var d in AiClientRegistry.Default.Descriptors.Values)
        {
            foreach (var m in d.Models)
            {
                Assert.False(String.IsNullOrWhiteSpace(m.Model), $"模型标识为空: {d.Code}");
                Assert.False(String.IsNullOrWhiteSpace(m.DisplayName), $"模型显示名为空: {d.Code}/{m.Model}");
            }
        }
    }

    [Fact]
    [DisplayName("全部描述符_工厂可创建客户端")]
    public void AllDescriptors_FactoryCreatesClient()
    {
        foreach (var d in AiClientRegistry.Default.Descriptors.Values)
        {
            var model = d.Models.FirstOrDefault()?.Model;
            var client = d.Factory(new AiClientOptions { ApiKey = "test-key", Model = model });
            try
            {
                Assert.NotNull(client);
                if (client is AiClientBase ab)
                    Assert.False(String.IsNullOrWhiteSpace(ab.Name), $"客户端名称为空: {d.Code}");
            }
            finally
            {
                client?.Dispose();
            }
        }
    }

    #endregion

    #region 关键服务商 ChatPath 校验

    [Theory]
    [DisplayName("已知服务商_ChatPath正确覆盖")]
    [InlineData("VolcEngine", "/chat/completions")]
    [InlineData("Zhipu", "/chat/completions")]
    [InlineData("Qianfan", "/chat/completions")]
    [InlineData("SenseNova", "/chat/completions")]
    public void KnownProvider_ChatPathIsCorrect(String code, String expectedPath)
    {
        var descriptor = AiClientRegistry.Default.GetDescriptor(code);
        Assert.NotNull(descriptor);

        var client = descriptor!.Factory(new AiClientOptions { ApiKey = "test-key" });
        try
        {
            var ab = Assert.IsAssignableFrom<AiClientBase>(client);
            Assert.Equal(expectedPath, ab.ChatPath);
        }
        finally
        {
            client.Dispose();
        }
    }

    [Fact]
    [DisplayName("关键服务商_默认端点正确")]
    public void KeyProviders_DefaultEndpointIsCorrect()
    {
        var registry = AiClientRegistry.Default;

        Assert.Equal("https://api.openai.com", registry.GetDescriptor("OpenAI")?.DefaultEndpoint);
        Assert.Equal("https://api.deepseek.com", registry.GetDescriptor("DeepSeek")?.DefaultEndpoint);
        Assert.Equal("https://api.anthropic.com", registry.GetDescriptor("Anthropic")?.DefaultEndpoint);
        Assert.Equal("https://ai.newlifex.com", registry.GetDescriptor("NewLifeAI")?.DefaultEndpoint);
        Assert.Equal("https://dashscope.aliyuncs.com/api/v1", registry.GetDescriptor("DashScope")?.DefaultEndpoint);
    }

    #endregion
}
