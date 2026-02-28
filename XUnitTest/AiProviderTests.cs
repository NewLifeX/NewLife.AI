using System;
using System.Collections.Generic;
using System.Linq;
using NewLife.AI.Providers;
using Xunit;

namespace XUnitTest;

/// <summary>AI 服务提供商单元测试</summary>
public class AiProviderTests
{
    #region 工厂测试
    [Fact]
    public void DefaultFactoryRegistersAllProviders()
    {
        var factory = AiProviderFactory.Default;
        var names = factory.GetProviderNames();

        // 当前共 33 个内置服务商
        Assert.Equal(33, names.Count);
    }

    [Fact]
    public void GetProviderByNameIsCaseInsensitive()
    {
        var factory = AiProviderFactory.Default;

        var p1 = factory.GetProvider("openai");
        var p2 = factory.GetProvider("OpenAI");
        var p3 = factory.GetProvider("OPENAI");

        Assert.NotNull(p1);
        Assert.Same(p1, p2);
        Assert.Same(p1, p3);
    }

    [Fact]
    public void GetProviderReturnsNullForUnknown()
    {
        var factory = AiProviderFactory.Default;

        Assert.Null(factory.GetProvider("NotExist"));
        Assert.Null(factory.GetProvider(""));
        Assert.Null(factory.GetProvider(null));
    }

    [Fact]
    public void GetRequiredProviderThrowsForUnknown()
    {
        var factory = AiProviderFactory.Default;

        var ex = Assert.Throws<InvalidOperationException>(() => factory.GetRequiredProvider("NotExist"));
        Assert.Contains("NotExist", ex.Message);
    }

    [Fact]
    public void CustomProviderCanBeRegistered()
    {
        var factory = new AiProviderFactory();
        var count = factory.GetProviderNames().Count;

        factory.Register(new OpenAiProvider());
        Assert.Equal(count + 1, factory.GetProviderNames().Count);
        Assert.NotNull(factory.GetProvider("OpenAI"));
    }
    #endregion

    #region OpenAI 协议服务商
    [Theory]
    [InlineData(typeof(OpenAiProvider), "OpenAI", "https://api.openai.com", "ChatCompletions")]
    [InlineData(typeof(AzureAiProvider), "AzureAI", "https://models.inference.ai.azure.com", "ChatCompletions")]
    [InlineData(typeof(DashScopeProvider), "DashScope", "https://dashscope.aliyuncs.com/compatible-mode", "ChatCompletions")]
    [InlineData(typeof(DeepSeekProvider), "DeepSeek", "https://api.deepseek.com", "ChatCompletions")]
    [InlineData(typeof(VolcEngineProvider), "VolcEngine", "https://ark.cn-beijing.volces.com/api/v3", "ChatCompletions")]
    [InlineData(typeof(ZhipuProvider), "Zhipu", "https://open.bigmodel.cn/api/paas/v4", "ChatCompletions")]
    [InlineData(typeof(MoonshotProvider), "Moonshot", "https://api.moonshot.cn", "ChatCompletions")]
    [InlineData(typeof(HunyuanProvider), "Hunyuan", "https://api.hunyuan.cloud.tencent.com", "ChatCompletions")]
    [InlineData(typeof(QianfanProvider), "Qianfan", "https://qianfan.baidubce.com/v2", "ChatCompletions")]
    [InlineData(typeof(SparkProvider), "Spark", "https://spark-api-open.xf-yun.com", "ChatCompletions")]
    [InlineData(typeof(YiProvider), "Yi", "https://api.lingyiwanwu.com", "ChatCompletions")]
    [InlineData(typeof(MiniMaxProvider), "MiniMax", "https://api.minimax.chat", "ChatCompletions")]
    [InlineData(typeof(SiliconFlowProvider), "SiliconFlow", "https://api.siliconflow.cn", "ChatCompletions")]
    [InlineData(typeof(XAiProvider), "XAI", "https://api.x.ai", "ChatCompletions")]
    [InlineData(typeof(GitHubModelsProvider), "GitHubModels", "https://models.github.ai/inference", "ChatCompletions")]
    [InlineData(typeof(OpenRouterProvider), "OpenRouter", "https://openrouter.ai/api", "ChatCompletions")]
    [InlineData(typeof(OllamaProvider), "Ollama", "http://localhost:11434", "ChatCompletions")]
    [InlineData(typeof(MiMoProvider), "MiMo", "https://api.xiaomimimo.com", "ChatCompletions")]
    [InlineData(typeof(TogetherAiProvider), "TogetherAI", "https://api.together.xyz", "ChatCompletions")]
    [InlineData(typeof(GroqProvider), "Groq", "https://api.groq.com/openai", "ChatCompletions")]
    [InlineData(typeof(MistralProvider), "Mistral", "https://api.mistral.ai", "ChatCompletions")]
    [InlineData(typeof(CohereProvider), "Cohere", "https://api.cohere.com/compatibility", "ChatCompletions")]
    [InlineData(typeof(PerplexityProvider), "Perplexity", "https://api.perplexity.ai", "ChatCompletions")]
    [InlineData(typeof(InfiniProvider), "Infini", "https://cloud.infini-ai.com/maas", "ChatCompletions")]
    [InlineData(typeof(CerebrasProvider), "Cerebras", "https://api.cerebras.ai", "ChatCompletions")]
    [InlineData(typeof(FireworksProvider), "Fireworks", "https://api.fireworks.ai/inference", "ChatCompletions")]
    [InlineData(typeof(SambaNovaProvider), "SambaNova", "https://api.sambanova.ai", "ChatCompletions")]
    [InlineData(typeof(XiaomaPowerProvider), "XiaomaPower", "https://openapi.xmpower.cn", "ChatCompletions")]
    [InlineData(typeof(LMStudioProvider), "LMStudio", "http://localhost:1234", "ChatCompletions")]
    [InlineData(typeof(VllmProvider), "vLLM", "http://localhost:8000", "ChatCompletions")]
    [InlineData(typeof(OneApiProvider), "OneAPI", "http://localhost:3000", "ChatCompletions")]
    public void OpenAiCompatibleProviderHasCorrectProperties(Type providerType, String expectedName, String expectedEndpoint, String expectedProtocol)
    {
        var provider = (IAiProvider)Activator.CreateInstance(providerType);

        Assert.Equal(expectedName, provider.Name);
        Assert.Equal(expectedEndpoint, provider.DefaultEndpoint);
        Assert.Equal(expectedProtocol, provider.ApiProtocol);
    }
    #endregion

    #region Anthropic 协议服务商
    [Fact]
    public void AnthropicProviderHasCorrectProperties()
    {
        var provider = new AnthropicProvider();

        Assert.Equal("Anthropic", provider.Name);
        Assert.Equal("https://api.anthropic.com", provider.DefaultEndpoint);
        Assert.Equal("AnthropicMessages", provider.ApiProtocol);
    }
    #endregion

    #region Gemini 协议服务商
    [Fact]
    public void GeminiProviderHasCorrectProperties()
    {
        var provider = new GeminiProvider();

        Assert.Equal("Gemini", provider.Name);
        Assert.Equal("https://generativelanguage.googleapis.com", provider.DefaultEndpoint);
        Assert.Equal("Gemini", provider.ApiProtocol);
    }
    #endregion

    #region 所有服务商通用校验
    [Fact]
    public void AllProvidersHaveNonEmptyName()
    {
        var factory = AiProviderFactory.Default;

        foreach (var name in factory.GetProviderNames())
        {
            var provider = factory.GetProvider(name);
            Assert.NotNull(provider);
            Assert.False(String.IsNullOrWhiteSpace(provider.Name));
        }
    }

    [Fact]
    public void AllProvidersHaveNonEmptyEndpoint()
    {
        var factory = AiProviderFactory.Default;

        foreach (var name in factory.GetProviderNames())
        {
            var provider = factory.GetProvider(name);
            Assert.NotNull(provider);
            Assert.False(String.IsNullOrWhiteSpace(provider.DefaultEndpoint));
        }
    }

    [Fact]
    public void AllProvidersHaveValidProtocol()
    {
        var validProtocols = new HashSet<String> { "ChatCompletions", "AnthropicMessages", "Gemini" };
        var factory = AiProviderFactory.Default;

        foreach (var name in factory.GetProviderNames())
        {
            var provider = factory.GetProvider(name);
            Assert.NotNull(provider);
            Assert.Contains(provider.ApiProtocol, validProtocols);
        }
    }

    [Fact]
    public void AllProviderNamesAreUnique()
    {
        var factory = AiProviderFactory.Default;
        var names = factory.GetProviderNames();

        // 名称不区分大小写时也唯一
        var distinctNames = names.Select(n => n.ToLowerInvariant()).Distinct().ToList();
        Assert.Equal(names.Count, distinctNames.Count);
    }

    [Fact]
    public void AllEndpointsAreValidUris()
    {
        var factory = AiProviderFactory.Default;

        foreach (var name in factory.GetProviderNames())
        {
            var provider = factory.GetProvider(name);
            Assert.NotNull(provider);
            Assert.True(Uri.TryCreate(provider.DefaultEndpoint, UriKind.Absolute, out var uri), $"服务商 {name} 的 DefaultEndpoint 不是有效 URI: {provider.DefaultEndpoint}");
            Assert.True(uri.Scheme == "http" || uri.Scheme == "https", $"服务商 {name} 的 DefaultEndpoint 协议不是 http/https: {provider.DefaultEndpoint}");
        }
    }

    [Fact]
    public void CloudProvidersUseHttps()
    {
        var localProviders = new HashSet<String>(StringComparer.OrdinalIgnoreCase) { "Ollama", "LMStudio", "vLLM", "OneAPI" };
        var factory = AiProviderFactory.Default;

        foreach (var name in factory.GetProviderNames())
        {
            if (localProviders.Contains(name)) continue;

            var provider = factory.GetProvider(name);
            Assert.NotNull(provider);
            Assert.StartsWith("https://", provider.DefaultEndpoint, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void LocalProvidersUseHttp()
    {
        var localProviders = new[] { "Ollama", "LMStudio", "vLLM", "OneAPI" };

        var factory = AiProviderFactory.Default;
        foreach (var name in localProviders)
        {
            var provider = factory.GetProvider(name);
            Assert.NotNull(provider);
            Assert.StartsWith("http://", provider.DefaultEndpoint, StringComparison.OrdinalIgnoreCase);
        }
    }
    #endregion

    #region AiProviderOptions 测试
    [Fact]
    public void GetEndpointReturnsCustomWhenSet()
    {
        var options = new AiProviderOptions { Endpoint = "https://custom.api.com" };

        Assert.Equal("https://custom.api.com", options.GetEndpoint("https://default.api.com"));
    }

    [Fact]
    public void GetEndpointReturnsDefaultWhenEmpty()
    {
        var options = new AiProviderOptions();
        Assert.Equal("https://default.api.com", options.GetEndpoint("https://default.api.com"));

        options.Endpoint = "";
        Assert.Equal("https://default.api.com", options.GetEndpoint("https://default.api.com"));

        options.Endpoint = "   ";
        Assert.Equal("https://default.api.com", options.GetEndpoint("https://default.api.com"));
    }
    #endregion

    #region 工厂注册顺序测试
    [Fact]
    public void FactoryRegistrationOrderMatchesExpected()
    {
        var factory = AiProviderFactory.Default;
        var names = factory.GetProviderNames();

        // 前几个应该是主流服务商
        Assert.Equal("OpenAI", names[0]);
        Assert.Equal("AzureAI", names[1]);
        Assert.Equal("DashScope", names[2]);
        Assert.Equal("DeepSeek", names[3]);

        // 最后应该是 Anthropic 和 Gemini
        Assert.Equal("Gemini", names[names.Count - 1]);
        Assert.Equal("Anthropic", names[names.Count - 2]);
    }
    #endregion
}
