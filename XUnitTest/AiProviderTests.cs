using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NewLife.AI.Models;
using NewLife.AI.Providers;
using Xunit;

namespace XUnitTest;

/// <summary>AI 服务提供商单元测试</summary>
public class AiProviderTests
{
    // 通过 RegisteredTypes 枚举所有已注册服务商实例（每次调用工厂委托，返回新实例）
    private static IEnumerable<IAiProvider> AllProviders(AiProviderFactory factory)
        => factory.RegisteredTypes.Select(t => factory.GetProvider(t)!);

    #region 工厂基础功能

    [Fact]
    public void Default_RegistersExpectedCount()
    {
        // 33 个内置服务商
        Assert.Equal(33, AiProviderFactory.Default.RegisteredTypes.Count());
    }

    [Fact]
    public void RegisteredTypes_ContainsExpectedTypes()
    {
        var types = AiProviderFactory.Default.RegisteredTypes.ToHashSet();

        Assert.Contains(typeof(OpenAiProvider), types);
        Assert.Contains(typeof(AnthropicProvider), types);
        Assert.Contains(typeof(GeminiProvider), types);
        Assert.Contains(typeof(DeepSeekProvider), types);
        Assert.Contains(typeof(OllamaProvider), types);
    }

    [Fact]
    public void GetProvider_ByFullName_IsCaseInsensitive()
    {
        var factory = AiProviderFactory.Default;
        var fullName = typeof(OpenAiProvider).FullName!;

        // 每次通过委托创建新实例，大小写不敏感均能命中正确类型
        Assert.IsType<OpenAiProvider>(factory.GetProvider(fullName));
        Assert.IsType<OpenAiProvider>(factory.GetProvider(fullName.ToLowerInvariant()));
        Assert.IsType<OpenAiProvider>(factory.GetProvider(fullName.ToUpperInvariant()));
    }

    [Fact]
    public void GetProvider_ByType_ReturnsNewInstance()
    {
        var factory = AiProviderFactory.Default;
        Assert.IsType<DeepSeekProvider>(factory.GetProvider(typeof(DeepSeekProvider)));
    }

    [Fact]
    public void GetProvider_ReturnsNull_WhenStringNotFound()
    {
        Assert.Null(AiProviderFactory.Default.GetProvider("Unknown.Provider.Type"));
    }

    [Fact]
    public void GetProvider_ReturnsNull_WhenStringNullOrEmpty()
    {
        var factory = AiProviderFactory.Default;
        Assert.Null(factory.GetProvider((String)null!));
        Assert.Null(factory.GetProvider(""));
        Assert.Null(factory.GetProvider("   "));
    }

    [Fact]
    public void GetProvider_ReturnsNull_WhenTypeNull()
    {
        Assert.Null(AiProviderFactory.Default.GetProvider((Type)null!));
    }

    [Fact]
    public void GetProvider_ReturnsNull_WhenTypeNotRegistered()
    {
        Assert.Null(new AiProviderFactory().GetProvider(typeof(OpenAiProvider)));
    }

    #endregion

    #region 扫描与注册

    [Fact]
    public void RegisterType_CanFindByType()
    {
        var factory = new AiProviderFactory();
        factory.RegisterType(typeof(DashScopeProvider));

        Assert.Contains(typeof(DashScopeProvider), factory.RegisteredTypes);
        Assert.IsType<DashScopeProvider>(factory.GetProvider(typeof(DashScopeProvider)));
    }

    [Fact]
    public void RegisterType_CanFindByFullName()
    {
        var factory = new AiProviderFactory();
        factory.RegisterType(typeof(DashScopeProvider));

        Assert.IsType<DashScopeProvider>(factory.GetProvider(typeof(DashScopeProvider).FullName!));
    }

    [Fact]
    public void RegisterType_ThrowsForNull()
    {
        Assert.Throws<ArgumentNullException>(() => new AiProviderFactory().RegisterType(null!));
    }

    [Fact]
    public void RegisterType_Overwrites_ExistingRegistration()
    {
        var factory = new AiProviderFactory();
        factory.RegisterType(typeof(OpenAiProvider));
        factory.RegisterType(typeof(OpenAiProvider)); // 重复注册不报错，幂等

        Assert.Single(factory.RegisteredTypes.Where(t => t == typeof(OpenAiProvider)));
    }

    [Fact]
    public void RegisteredTypes_AllImplementIAiProvider()
    {
        Assert.All(AiProviderFactory.Default.RegisteredTypes,
            t => Assert.True(typeof(IAiProvider).IsAssignableFrom(t)));
    }

    [Fact]
    public void RegisteredTypes_AllTypesCanCreateInstance()
    {
        var factory = AiProviderFactory.Default;
        Assert.All(factory.RegisteredTypes, t => Assert.NotNull(factory.GetProvider(t)));
    }

    [Fact]
    public void RegisteredTypes_CanFindByProviderCode()
    {
        var factory = AiProviderFactory.Default;
        var openAiType = factory.RegisteredTypes
            .FirstOrDefault(t => factory.GetProvider(t)?.Code.Equals("OpenAI", StringComparison.OrdinalIgnoreCase) == true);

        Assert.Equal(typeof(OpenAiProvider), openAiType);
    }

    #endregion

    #region 实例缓存（GetProviderForConfig / InvalidateConfig）

    [Fact]
    public void GetProviderForConfig_CreatesInstance_FirstCall()
    {
        var factory = new AiProviderFactory();
        factory.RegisterType(typeof(OpenAiProvider));

        var instance = factory.GetProviderForConfig(1, typeof(OpenAiProvider).FullName!);
        Assert.IsType<OpenAiProvider>(instance);
    }

    [Fact]
    public void GetProviderForConfig_ReturnsCached_SameConfigId()
    {
        var factory = new AiProviderFactory();
        factory.RegisterType(typeof(OpenAiProvider));
        var fullName = typeof(OpenAiProvider).FullName!;

        var i1 = factory.GetProviderForConfig(42, fullName);
        var i2 = factory.GetProviderForConfig(42, fullName);

        Assert.NotNull(i1);
        Assert.Same(i1, i2);
    }

    [Fact]
    public void GetProviderForConfig_DifferentConfigIds_ReturnDifferentInstances()
    {
        var factory = new AiProviderFactory();
        factory.RegisterType(typeof(OpenAiProvider));
        var fullName = typeof(OpenAiProvider).FullName!;

        var i1 = factory.GetProviderForConfig(10, fullName);
        var i2 = factory.GetProviderForConfig(20, fullName);

        Assert.NotNull(i1);
        Assert.NotNull(i2);
        Assert.NotSame(i1, i2);
    }

    [Fact]
    public void GetProviderForConfig_ReturnsNull_UnknownType()
    {
        Assert.Null(new AiProviderFactory().GetProviderForConfig(1, "Unknown.Provider.Type"));
    }

    [Fact]
    public void GetProviderForConfig_ReturnsNull_NullOrEmpty()
    {
        var factory = new AiProviderFactory();
        Assert.Null(factory.GetProviderForConfig(1, ""));
        Assert.Null(factory.GetProviderForConfig(1, null!));
    }

    [Fact]
    public void InvalidateConfig_ForcesInstanceRecreation()
    {
        var factory = new AiProviderFactory();
        factory.RegisterType(typeof(OpenAiProvider));
        var fullName = typeof(OpenAiProvider).FullName!;

        var i1 = factory.GetProviderForConfig(99, fullName);
        factory.InvalidateConfig(99);
        var i2 = factory.GetProviderForConfig(99, fullName);

        Assert.NotNull(i1);
        Assert.NotNull(i2);
        Assert.NotSame(i1, i2);
    }

    [Fact]
    public void InvalidateConfig_NonExistentId_DoesNotThrow()
    {
        Assert.Null(Record.Exception(() => new AiProviderFactory().InvalidateConfig(999)));
    }

    #endregion

    #region OpenAI 协议服务商属性

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
    public void OpenAiCompatibleProvider_HasCorrectProperties(Type providerType, String expectedCode, String expectedEndpoint, String expectedProtocol)
    {
        var provider = (IAiProvider)Activator.CreateInstance(providerType)!;

        Assert.Equal(expectedCode, provider.Code);
        Assert.Equal(expectedEndpoint, provider.DefaultEndpoint);
        Assert.Equal(expectedProtocol, provider.ApiProtocol);
    }

    #endregion

    #region Anthropic 协议服务商属性

    [Fact]
    public void AnthropicProvider_HasCorrectProperties()
    {
        var provider = new AnthropicProvider();

        Assert.Equal("Anthropic", provider.Code);
        Assert.Equal("https://api.anthropic.com", provider.DefaultEndpoint);
        Assert.Equal("AnthropicMessages", provider.ApiProtocol);
    }

    #endregion

    #region Gemini 协议服务商属性

    [Fact]
    public void GeminiProvider_HasCorrectProperties()
    {
        var provider = new GeminiProvider();

        Assert.Equal("Gemini", provider.Code);
        Assert.Equal("https://generativelanguage.googleapis.com", provider.DefaultEndpoint);
        Assert.Equal("Gemini", provider.ApiProtocol);
    }

    #endregion

    #region 所有服务商通用校验

    [Fact]
    public void AllProviders_HaveNonEmptyName()
    {
        Assert.All(AllProviders(AiProviderFactory.Default),
            p => Assert.False(String.IsNullOrWhiteSpace(p.Name)));
    }

    [Fact]
    public void AllProviders_HaveNonEmptyEndpoint()
    {
        Assert.All(AllProviders(AiProviderFactory.Default),
            p => Assert.False(String.IsNullOrWhiteSpace(p.DefaultEndpoint)));
    }

    [Fact]
    public void AllProviders_HaveValidProtocol()
    {
        var validProtocols = new HashSet<String> { "ChatCompletions", "AnthropicMessages", "Gemini" };
        Assert.All(AllProviders(AiProviderFactory.Default),
            p => Assert.Contains(p.ApiProtocol, validProtocols));
    }

    [Fact]
    public void AllProviders_CodesAreUnique()
    {
        var codes = AllProviders(AiProviderFactory.Default).Select(p => p.Code).ToList();
        Assert.Equal(codes.Count, codes.Select(c => c.ToLowerInvariant()).Distinct().Count());
    }

    [Fact]
    public void AllProviders_TypesAreUnique()
    {
        var types = AiProviderFactory.Default.RegisteredTypes.ToList();
        Assert.Equal(types.Count, types.Distinct().Count());
    }

    [Fact]
    public void AllProviders_EndpointsAreValidAbsoluteUris()
    {
        foreach (var p in AllProviders(AiProviderFactory.Default))
        {
            Assert.True(
                Uri.TryCreate(p.DefaultEndpoint, UriKind.Absolute, out var uri),
                $"服务商 {p.Code} 的 DefaultEndpoint 不是有效 URI: {p.DefaultEndpoint}");
            Assert.True(
                uri!.Scheme == "http" || uri.Scheme == "https",
                $"服务商 {p.Code} 的协议不是 http/https: {p.DefaultEndpoint}");
        }
    }

    [Fact]
    public void CloudProviders_UseHttps()
    {
        var localCodes = new HashSet<String>(StringComparer.OrdinalIgnoreCase)
            { "Ollama", "LMStudio", "vLLM", "OneAPI" };

        foreach (var p in AllProviders(AiProviderFactory.Default))
        {
            if (localCodes.Contains(p.Code)) continue;
            Assert.StartsWith("https://", p.DefaultEndpoint, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void LocalProviders_UseHttp()
    {
        var localCodes = new[] { "Ollama", "LMStudio", "vLLM", "OneAPI" };
        var factory = AiProviderFactory.Default;

        foreach (var code in localCodes)
        {
            var p = AllProviders(factory)
                .FirstOrDefault(x => x.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(p);
            Assert.StartsWith("http://", p.DefaultEndpoint, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Default_ContainsAllCoreProviders()
    {
        var codes = AllProviders(AiProviderFactory.Default)
            .Select(p => p.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var expectedCodes = new[]
        {
            "OpenAI", "DashScope", "DeepSeek", "VolcEngine", "Zhipu",
            "Moonshot", "Gemini", "Anthropic", "Ollama", "LMStudio"
        };
        Assert.All(expectedCodes, code => Assert.Contains(code, codes));
    }

    #endregion

    #region AiProviderOptions 测试

    [Fact]
    public void AiProviderOptions_GetEndpoint_ReturnsCustom_WhenSet()
    {
        var options = new AiProviderOptions { Endpoint = "https://custom.api.com" };
        Assert.Equal("https://custom.api.com", options.GetEndpoint("https://default.api.com"));
    }

    [Fact]
    public void AiProviderOptions_GetEndpoint_ReturnsDefault_WhenEmpty()
    {
        var options = new AiProviderOptions();
        Assert.Equal("https://default.api.com", options.GetEndpoint("https://default.api.com"));
    }

    [Fact]
    public void AiProviderOptions_GetEndpoint_ReturnsDefault_WhenWhitespace()
    {
        var options = new AiProviderOptions { Endpoint = "   " };
        Assert.Equal("https://default.api.com", options.GetEndpoint("https://default.api.com"));
    }

    #endregion

    #region 服务商接口调用验证

    [Fact]
    [DisplayName("DashScope服务商_构建请求_模型和消息正确")]
    public void DashScopeProvider_BuildsCorrectRequest()
    {
        var provider = new DashScopeProvider();

        Assert.Equal("DashScope", provider.Code);
        Assert.Equal("阿里百炼", provider.Name);

        // 验证支持的模型列表包含 qwen-plus
        var models = provider.Models;
        Assert.NotEmpty(models);
        var qwenPlus = models.FirstOrDefault(m => m.Model == "qwen-plus");
        Assert.NotNull(qwenPlus);
        Assert.Equal("Qwen Plus", qwenPlus.DisplayName);
    }

    [Fact]
    [DisplayName("所有OpenAI兼容服务商_请求结构一致")]
    public void AllOpenAiCompatibleProviders_AcceptSameRequestFormat()
    {
        var factory = AiProviderFactory.Default;
        var openAiProviders = AllProviders(factory)
            .Where(p => p.ApiProtocol == "ChatCompletions")
            .ToList();

        Assert.True(openAiProviders.Count >= 20, "应有至少 20 个 OpenAI 兼容服务商");

        // 验证所有 OpenAI 兼容服务商都继承自 OpenAiProvider
        foreach (var p in openAiProviders)
        {
            Assert.IsAssignableFrom<OpenAiProvider>(p);
            // 验证模型列表不为 null
            Assert.NotNull(p.Models);
        }
    }

    [Fact]
    [DisplayName("DashScope_QwenPlus模型_能力标记正确")]
    public void DashScope_QwenPlus_CapabilitiesCorrect()
    {
        var provider = new DashScopeProvider();
        var qwenPlus = provider.Models.First(m => m.Model == "qwen-plus");

        // qwen-plus 自 2025 年起已支持思考模式，支持视觉，不支持文生图，支持函数调用
        Assert.True(qwenPlus.Capabilities.SupportThinking);
        Assert.True(qwenPlus.Capabilities.SupportVision);
        Assert.False(qwenPlus.Capabilities.SupportImageGeneration);
        Assert.True(qwenPlus.Capabilities.SupportFunctionCalling);
    }

    #endregion
}
