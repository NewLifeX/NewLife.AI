using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using NewLife.AI.Clients;
using Xunit;

namespace XUnitTest.Clients;

/// <summary>内置服务商注册契约测试。验证 BuiltinChatClient.cs 声明的 40+ 服务商全部注册、Code 与模型标识无歧义</summary>
[DisplayName("内置服务商注册契约测试")]
public class BuiltinProviderTests
{
    /// <summary>BuiltinChatClient.cs 中 [AiClient] 声明的服务商编码清单（注册完整性护栏）</summary>
    private static readonly String[] BuiltinCodes =
    [
        "VolcEngine", "Zhipu", "Moonshot", "Hunyuan", "Qianfan", "Spark", "MiniMax", "SiliconFlow",
        "MiMo", "Infini", "XiaomaPower", "XAI", "GitHubModels", "OpenRouter", "Mistral", "Cohere",
        "Perplexity", "Groq", "Cerebras", "TogetherAI", "Fireworks", "SambaNova", "Yi", "LMStudio",
        "vLLM", "OneAPI", "HuggingFace", "NvidiaNIM", "DeepInfra", "Hyperbolic", "NovitaAI", "AI21",
        "Stepfun", "Baichuan", "SenseNova", "Doubao", "CloudflareAI",
    ];

    [Fact]
    [DisplayName("内置服务商—全部已注册且 Code 唯一")]
    public void BuiltinProviders_AllRegistered()
    {
        var registry = AiClientRegistry.Default;

        foreach (var code in BuiltinCodes)
        {
            var descriptor = registry.GetDescriptor(code);
            Assert.NotNull(descriptor);
            Assert.Equal(code, descriptor!.Code);
        }

        // 注册表内部 Code 唯一（大小写不敏感字典天然保证，此处做回归护栏）
        var codes = registry.Descriptors.Values.Select(d => d.Code).ToList();
        Assert.Equal(codes.Count, codes.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    [DisplayName("内置服务商—各描述符内部模型标识不重复")]
    public void BuiltinProviders_NoDuplicateModelsWithinDescriptor()
    {
        var registry = AiClientRegistry.Default;

        foreach (var d in registry.Descriptors.Values)
        {
            var models = d.Models.Select(m => m.Model).ToList();
            Assert.Equal(models.Count, models.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }
    }

    [Fact]
    [DisplayName("内置服务商—注册模型的能力元数据完整")]
    public void BuiltinProviders_ModelsHaveCapabilities()
    {
        var registry = AiClientRegistry.Default;

        foreach (var code in BuiltinCodes)
        {
            var descriptor = registry.GetDescriptor(code);
            Assert.NotNull(descriptor);
            foreach (var m in descriptor!.Models)
            {
                Assert.False(String.IsNullOrWhiteSpace(m.Model), $"{code} 模型标识为空");
                Assert.False(String.IsNullOrWhiteSpace(m.DisplayName), $"{code}/{m.Model} 显示名为空");
                Assert.NotNull(m.Capabilities);
            }
        }
    }
}
