using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NewLife.AI.Clients;
using NewLife.AI.Models;
using NewLife.AI.Providers;
using Xunit;

namespace XUnitTest.Providers;

/// <summary>AI 客户端注册表单元测试</summary>
public class AiProviderTests
{
    #region 注册表基础功能

    [Fact]
    public void Default_RegistersExpectedCount()
    {
        // 34 个内置服务商描述符（不含 OllamaCloud，OllamaCloud 是 InitData 动态生成的）
        Assert.Equal(34, AiClientRegistry.Default.Descriptors.Count);
    }

    [Fact]
    public void Descriptors_ContainsExpectedCodes()
    {
        var descriptors = AiClientRegistry.Default.Descriptors;

        Assert.True(descriptors.ContainsKey("OpenAI"));
        Assert.True(descriptors.ContainsKey("Anthropic"));
        Assert.True(descriptors.ContainsKey("Gemini"));
        Assert.True(descriptors.ContainsKey("DeepSeek"));
        Assert.True(descriptors.ContainsKey("Ollama"));
    }

    [Fact]
    public void GetDescriptor_ByCode_IsCaseInsensitive()
    {
        var registry = AiClientRegistry.Default;

        Assert.Equal("OpenAI", registry.GetDescriptor("OpenAI")?.Code);
        Assert.Equal("OpenAI", registry.GetDescriptor("openai")?.Code);
        Assert.Equal("OpenAI", registry.GetDescriptor("OPENAI")?.Code);
    }

    [Fact]
    public void GetDescriptor_ByCode_ReturnsCorrectCode()
    {
        var registry = AiClientRegistry.Default;
        Assert.Equal("DeepSeek", registry.GetDescriptor("DeepSeek")?.Code);
    }

    [Fact]
    public void GetDescriptor_ReturnsNull_WhenCodeNotFound()
    {
        Assert.Null(AiClientRegistry.Default.GetDescriptor("Unknown.Provider.Type"));
    }

    [Fact]
    public void GetDescriptor_ReturnsNull_WhenCodeNullOrEmpty()
    {
        var registry = AiClientRegistry.Default;
        Assert.Null(registry.GetDescriptor((String)null!));
        Assert.Null(registry.GetDescriptor(""));
        Assert.Null(registry.GetDescriptor("   "));
    }

    [Fact]
    public void GetDescriptor_ReturnsNull_WhenEmptyRegistry()
    {
        Assert.Null(new AiClientRegistry().GetDescriptor("OpenAI"));
    }

    [Fact]
    [DisplayName("GetDescriptor 按显示名称回退查找")]
    public void GetDescriptor_ByDisplayName_ReturnDescriptor()
    {
        var registry = new AiClientRegistry();
        registry.Register(new AiClientDescriptor
        {
            Code = "TestCode",
            DisplayName = "中文显示名称",
            DefaultEndpoint = "https://test.api.com",
            Protocol = "OpenAI",
            Factory = opts => new OpenAIChatClient(opts),
        });

        var descriptor = registry.GetDescriptor("中文显示名称");
        Assert.NotNull(descriptor);
        Assert.Equal("TestCode", descriptor!.Code);
    }

    [Fact]
    [DisplayName("GetDescriptor 显示名称查找大小写不敏感")]
    public void GetDescriptor_ByDisplayName_IsCaseInsensitive()
    {
        var registry = new AiClientRegistry();
        registry.Register(new AiClientDescriptor
        {
            Code = "TestCode2",
            DisplayName = "TestDisplay",
            DefaultEndpoint = "https://test2.api.com",
            Protocol = "OpenAI",
            Factory = opts => new OpenAIChatClient(opts),
        });

        Assert.Equal("TestCode2", registry.GetDescriptor("testdisplay")?.Code);
    }

    #endregion

    #region 描述符注册

    [Fact]
    public void Register_Descriptor_CanFindByCode()
    {
        var registry = new AiClientRegistry();
        registry.Register(new AiClientDescriptor
        {
            Code = "TestProvider",
            DisplayName = "测试服务商",
            DefaultEndpoint = "https://test.api.com",
            Protocol = "OpenAI",
            Factory = opts => new OpenAIChatClient(opts),
        });

        Assert.True(registry.Descriptors.ContainsKey("TestProvider"));
        Assert.Equal("TestProvider", registry.GetDescriptor("TestProvider")?.Code);
    }

    [Fact]
    public void Register_ThrowsForNull()
    {
        Assert.Throws<ArgumentNullException>(() => new AiClientRegistry().Register((AiClientDescriptor)null!));
    }

    [Fact]
    [DisplayName("Register 返回当前实例支持链式调用")]
    public void Register_ReturnsThis_ForChaining()
    {
        var registry = new AiClientRegistry();
        var result = registry.Register(new AiClientDescriptor
        {
            Code = "Chain",
            DisplayName = "链式",
            DefaultEndpoint = "https://chain.api.com",
            Protocol = "OpenAI",
            Factory = opts => new OpenAIChatClient(opts),
        });

        Assert.Same(registry, result);
    }

    [Fact]
    public void Register_Overwrites_ExistingRegistration()
    {
        var registry = new AiClientRegistry();
        registry.Register(new AiClientDescriptor { Code = "Test", DisplayName = "v1", DefaultEndpoint = "https://a.com", Protocol = "OpenAI", Factory = opts => new OpenAIChatClient(opts) });
        registry.Register(new AiClientDescriptor { Code = "Test", DisplayName = "v2", DefaultEndpoint = "https://b.com", Protocol = "OpenAI", Factory = opts => new OpenAIChatClient(opts) });

        Assert.Equal("v2", registry.Descriptors["Test"].DisplayName);
    }

    #endregion

    #region GetDescriptor 代码查找

    [Fact]
    public void GetDescriptor_SameInstance_MultipleCallsSameCode()
    {
        var registry = AiClientRegistry.Default;
        var d1 = registry.GetDescriptor("OpenAI");
        var d2 = registry.GetDescriptor("OpenAI");

        Assert.NotNull(d1);
        Assert.Same(d1, d2);
    }

    [Fact]
    public void GetDescriptor_ReturnsNull_UnknownCode()
    {
        Assert.Null(new AiClientRegistry().GetDescriptor("Unknown"));
    }

    #endregion

    #region 服务商属性验证

    [Theory]
    [InlineData("OpenAI", "https://api.openai.com", "OpenAI")]
    [InlineData("AzureAI", "https://models.inference.ai.azure.com", "OpenAI")]
    [InlineData("DashScope", "https://dashscope.aliyuncs.com/api/v1", "DashScope")]
    [InlineData("DeepSeek", "https://api.deepseek.com", "OpenAI")]
    [InlineData("VolcEngine", "https://ark.cn-beijing.volces.com/api/v3", "OpenAI")]
    [InlineData("Zhipu", "https://open.bigmodel.cn/api/paas/v4", "OpenAI")]
    [InlineData("Moonshot", "https://api.moonshot.cn", "OpenAI")]
    [InlineData("Hunyuan", "https://api.hunyuan.cloud.tencent.com", "OpenAI")]
    [InlineData("Qianfan", "https://qianfan.baidubce.com/v2", "OpenAI")]
    [InlineData("Spark", "https://spark-api-open.xf-yun.com", "OpenAI")]
    [InlineData("Yi", "https://api.lingyiwanwu.com", "OpenAI")]
    [InlineData("MiniMax", "https://api.minimax.chat", "OpenAI")]
    [InlineData("SiliconFlow", "https://api.siliconflow.cn", "OpenAI")]
    [InlineData("XAI", "https://api.x.ai", "OpenAI")]
    [InlineData("GitHubModels", "https://models.github.ai/inference", "OpenAI")]
    [InlineData("OpenRouter", "https://openrouter.ai/api", "OpenAI")]
    [InlineData("Ollama", "http://localhost:11434", "Ollama")]
    [InlineData("MiMo", "https://api.xiaomimimo.com", "OpenAI")]
    [InlineData("TogetherAI", "https://api.together.xyz", "OpenAI")]
    [InlineData("Groq", "https://api.groq.com/openai", "OpenAI")]
    [InlineData("Mistral", "https://api.mistral.ai", "OpenAI")]
    [InlineData("Cohere", "https://api.cohere.com/compatibility", "OpenAI")]
    [InlineData("Perplexity", "https://api.perplexity.ai", "OpenAI")]
    [InlineData("Infini", "https://cloud.infini-ai.com/maas", "OpenAI")]
    [InlineData("Cerebras", "https://api.cerebras.ai", "OpenAI")]
    [InlineData("Fireworks", "https://api.fireworks.ai/inference", "OpenAI")]
    [InlineData("SambaNova", "https://api.sambanova.ai", "OpenAI")]
    [InlineData("XiaomaPower", "https://openapi.xmpower.cn", "OpenAI")]
    [InlineData("LMStudio", "http://localhost:1234", "OpenAI")]
    [InlineData("vLLM", "http://localhost:8000", "OpenAI")]
    [InlineData("OneAPI", "http://localhost:3000", "OpenAI")]
    public void Descriptor_HasCorrectEndpointAndProtocol(String code, String expectedEndpoint, String expectedProtocol)
    {
        var descriptor = AiClientRegistry.Default.GetDescriptor(code);

        Assert.NotNull(descriptor);
        Assert.Equal(code, descriptor!.Code);
        Assert.Equal(expectedEndpoint, descriptor.DefaultEndpoint);
        Assert.Equal(expectedProtocol, descriptor.Protocol);
    }

    [Fact]
    public void AnthropicDescriptor_HasCorrectProperties()
    {
        var descriptor = AiClientRegistry.Default.GetDescriptor("Anthropic");

        Assert.NotNull(descriptor);
        Assert.Equal("Anthropic", descriptor!.Code);
        Assert.Equal("https://api.anthropic.com", descriptor.DefaultEndpoint);
        Assert.Equal("AnthropicMessages", descriptor.Protocol);
    }

    [Fact]
    public void GeminiDescriptor_HasCorrectProperties()
    {
        var descriptor = AiClientRegistry.Default.GetDescriptor("Gemini");

        Assert.NotNull(descriptor);
        Assert.Equal("Gemini", descriptor!.Code);
        Assert.Equal("https://generativelanguage.googleapis.com", descriptor.DefaultEndpoint);
        Assert.Equal("Gemini", descriptor.Protocol);
    }

    #endregion

    #region 所有服务商通用校验

    [Fact]
    public void AllDescriptors_HaveNonEmptyDisplayName()
    {
        Assert.All(AiClientRegistry.Default.Descriptors.Values,
            d => Assert.False(String.IsNullOrWhiteSpace(d.DisplayName)));
    }

    [Fact]
    public void AllDescriptors_HaveNonEmptyEndpoint()
    {
        Assert.All(AiClientRegistry.Default.Descriptors.Values,
            d => Assert.False(String.IsNullOrWhiteSpace(d.DefaultEndpoint)));
    }

    [Fact]
    public void AllDescriptors_HaveValidProtocol()
    {
        var validProtocols = new HashSet<String> { "OpenAI", "AnthropicMessages", "Gemini", "DashScope", "Ollama" };
        Assert.All(AiClientRegistry.Default.Descriptors.Values,
            d => Assert.Contains(d.Protocol, validProtocols));
    }

    [Fact]
    public void AllDescriptors_CodesAreUnique()
    {
        var codes = AiClientRegistry.Default.Descriptors.Values.Select(d => d.Code).ToList();
        Assert.Equal(codes.Count, codes.Select(c => c.ToLowerInvariant()).Distinct().Count());
    }

    [Fact]
    public void AllDescriptors_EndpointsAreValidAbsoluteUris()
    {
        foreach (var d in AiClientRegistry.Default.Descriptors.Values)
        {
            Assert.True(
                Uri.TryCreate(d.DefaultEndpoint, UriKind.Absolute, out var uri),
                $"服务商 {d.Code} 的 DefaultEndpoint 不是有效 URI: {d.DefaultEndpoint}");
            Assert.True(
                uri!.Scheme == "http" || uri.Scheme == "https",
                $"服务商 {d.Code} 的协议不是 http/https: {d.DefaultEndpoint}");
        }
    }

    [Fact]
    public void CloudDescriptors_UseHttps()
    {
        var localCodes = new HashSet<String>(StringComparer.OrdinalIgnoreCase)
            { "Ollama", "LMStudio", "vLLM", "OneAPI" };

        foreach (var d in AiClientRegistry.Default.Descriptors.Values)
        {
            if (localCodes.Contains(d.Code)) continue;
            Assert.StartsWith("https://", d.DefaultEndpoint, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void LocalDescriptors_UseHttp()
    {
        var localCodes = new[] { "Ollama", "LMStudio", "vLLM", "OneAPI" };
        var registry = AiClientRegistry.Default;

        foreach (var code in localCodes)
        {
            var d = registry.GetDescriptor(code);
            Assert.NotNull(d);
            Assert.StartsWith("http://", d!.DefaultEndpoint, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Default_ContainsAllCoreProviders()
    {
        var codes = AiClientRegistry.Default.Descriptors.Keys
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var expectedCodes = new[]
        {
            "OpenAI", "DashScope", "DeepSeek", "VolcEngine", "Zhipu",
            "Moonshot", "Gemini", "Anthropic", "Ollama", "LMStudio",
        };
        Assert.All(expectedCodes, code => Assert.Contains(code, codes));
    }

    #endregion

    #region AiClientOptions 测试

    [Fact]
    public void AiClientOptions_GetEndpoint_ReturnsCustom_WhenSet()
    {
        var options = new AiClientOptions { Endpoint = "https://custom.api.com" };
        Assert.Equal("https://custom.api.com", options.GetEndpoint("https://default.api.com"));
    }

    [Fact]
    public void AiClientOptions_GetEndpoint_ReturnsDefault_WhenEmpty()
    {
        var options = new AiClientOptions();
        Assert.Equal("https://default.api.com", options.GetEndpoint("https://default.api.com"));
    }

    [Fact]
    public void AiClientOptions_GetEndpoint_ReturnsDefault_WhenWhitespace()
    {
        var options = new AiClientOptions { Endpoint = "   " };
        Assert.Equal("https://default.api.com", options.GetEndpoint("https://default.api.com"));
    }

    #endregion

    #region 服务商接口调用验证

    [Fact]
    [DisplayName("DashScope服务商_模型列表_包含qwen3.5-plus")]
    public void DashScope_HasCorrectModels()
    {
        var descriptor = AiClientRegistry.Default.GetDescriptor("DashScope");

        Assert.NotNull(descriptor);
        Assert.Equal("DashScope", descriptor!.Code);
        Assert.Equal("阿里百炼", descriptor.DisplayName);

        var models = descriptor.Models;
        Assert.NotNull(models);
        Assert.NotEmpty(models);
        var qwenPlus = models.FirstOrDefault(m => m.Model == "qwen3.5-plus");
        Assert.NotNull(qwenPlus);
        Assert.Equal("Qwen3.5 Plus", qwenPlus!.DisplayName);
    }

    [Fact]
    [DisplayName("所有OpenAI兼容服务商_协议标记为OpenAI")]
    public void AllOpenAiCompatibleDescriptors_HaveCorrectProtocol()
    {
        var openAiDescriptors = AiClientRegistry.Default.Descriptors.Values
            .Where(d => d.Protocol == "OpenAI")
            .ToList();

        Assert.True(openAiDescriptors.Count >= 20, "应有至少 20 个 OpenAI 兼容服务商");
        foreach (var d in openAiDescriptors)
            Assert.NotNull(d.Factory);
    }

    [Fact]
    [DisplayName("DashScope_QwenPlus模型_能力标记正确")]
    public void DashScope_QwenPlus_CapabilitiesCorrect()
    {
        var descriptor = AiClientRegistry.Default.GetDescriptor("DashScope")!;
        var qwenPlus = descriptor.Models!.First(m => m.Model == "qwen3.5-plus");

        // qwen3.5-plus 支持思考模式、视觉，不支持文生图，支持函数调用
        Assert.True(qwenPlus.Capabilities!.SupportThinking);
        Assert.True(qwenPlus.Capabilities.SupportVision);
        Assert.False(qwenPlus.Capabilities.SupportImageGeneration);
        Assert.True(qwenPlus.Capabilities.SupportFunctionCalling);
    }

    #endregion

    #region ChatCompletionResponse 模型测试

    [Fact]
    [DisplayName("ChatCompletionResponse.Text 返回第一个 Choice 的 Content")]
    public void ChatCompletionResponse_Text_ReturnsFirstChoiceContent()
    {
        var response = new ChatResponse
        {
            Messages =
            [
                new ChatChoice { Message = new ChatMessage { Role = "assistant", Content = "你好！" } }
            ]
        };

        Assert.Equal("你好！", response.Text);
    }

    [Fact]
    [DisplayName("ChatCompletionResponse.Text 无内容时返回 null")]
    public void ChatCompletionResponse_Text_ReturnsNullWhenEmpty()
    {
        var empty = new ChatResponse();

        Assert.Null(empty.Text);
    }

    #endregion

    #region ChatAsync 扩展方法测试

    [Fact]
    [DisplayName("ChatAsync 字符串重载直接返回模型回复文本")]
    public async Task AskAsync_ReturnsTextFromResponse()
    {
        const String expected = "我是 AI 助手！";
        var fakeClient = new FixedReplyChatClient(expected);

        var result = await fakeClient.ChatAsync("你是谁？");

        Assert.Equal(expected, result);
    }

    [Fact]
    [DisplayName("ChatAsync 消息列表重载直接返回模型回复文本")]
    public async Task AskAsync_WithMessages_ReturnsTextFromResponse()
    {
        const String expected = "收到！";
        var fakeClient = new FixedReplyChatClient(expected);

        var result = await fakeClient.ChatAsync([
                ("system", "你是一名专业的 C# 开发助手"),
                ("user", "请解释什么是依赖注入"),
            ]);

        Assert.Equal(expected, result);
    }

    #endregion

    #region 注册表 CreateClient 测试

    [Fact]
    [DisplayName("AiClientRegistry.CreateClient 按 code 创建客户端实例")]
    public void AiClientRegistry_CreateClient_ByCode_ReturnsClient()
    {
        var client = AiClientRegistry.Default.CreateClient("OpenAI", new AiClientOptions { ApiKey = "sk-test-key" });

        Assert.NotNull(client);
    }

    [Fact]
    [DisplayName("AiClientRegistry.CreateClient 传入未注册 code 抛出 ArgumentException")]
    public void AiClientRegistry_CreateClient_UnknownCode_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            AiClientRegistry.Default.CreateClient("NotExistProvider999", new AiClientOptions { ApiKey = "key" }));
    }

    #endregion

    #region Register（程序集重载）

    [Fact]
    [DisplayName("Register(Assembly) 空程序集参数抛 ArgumentNullException")]
    public void Register_Assembly_ThrowsForNull()
    {
        Assert.Throws<ArgumentNullException>(() => new AiClientRegistry().Register((Assembly)null!));
    }

    [Fact]
    [DisplayName("Register(Assembly) 返回当前实例支持链式调用")]
    public void Register_Assembly_ReturnsThis_ForChaining()
    {
        var registry = new AiClientRegistry();
        var result = registry.Register(typeof(AiClientRegistry).Assembly);

        Assert.Same(registry, result);
    }

    [Fact]
    [DisplayName("Register(Assembly) 扫描主程序集注册数量与 Default 一致")]
    public void Register_Assembly_MainAssembly_MatchesDefaultCount()
    {
        var registry = new AiClientRegistry();
        registry.Register(typeof(AiClientRegistry).Assembly);

        Assert.Equal(AiClientRegistry.Default.Descriptors.Count, registry.Descriptors.Count);
    }

    [Fact]
    [DisplayName("Register(Assembly) 扫描外部程序集中标注的服务商 Code")]
    public void Register_Assembly_WithTestAssembly_RegistersAnnotatedClient()
    {
        var registry = new AiClientRegistry();
        registry.Register(typeof(AiProviderTests).Assembly);

        var descriptor = registry.GetDescriptor("TestExternal");
        Assert.NotNull(descriptor);
        Assert.Equal("外部测试服务商", descriptor!.DisplayName);
        Assert.Equal("https://test-external.api.com", descriptor.DefaultEndpoint);
    }

    [Fact]
    [DisplayName("Register(Assembly) 扫描外部程序集中标注的服务商模型列表")]
    public void Register_Assembly_WithTestAssembly_RegistersAnnotatedModels()
    {
        var registry = new AiClientRegistry();
        registry.Register(typeof(AiProviderTests).Assembly);

        var descriptor = registry.GetDescriptor("TestExternal");
        Assert.NotNull(descriptor);
        Assert.NotEmpty(descriptor!.Models);
        Assert.Equal("test-model-v1", descriptor.Models[0].Model);
        Assert.Equal("测试模型 v1", descriptor.Models[0].DisplayName);
    }

    [Fact]
    [DisplayName("Register(Assembly) 支持多次调用合并多个程序集")]
    public void Register_Assembly_MultipleAssemblies_MergesAll()
    {
        var registry = new AiClientRegistry();
        registry.Register(typeof(AiClientRegistry).Assembly)
                .Register(typeof(AiProviderTests).Assembly);

        Assert.True(registry.Descriptors.ContainsKey("OpenAI"));
        Assert.True(registry.Descriptors.ContainsKey("TestExternal"));
    }

    [Fact]
    [DisplayName("Register(Assembly) 不含标注类的程序集注册数量为零")]
    public void Register_Assembly_NoAnnotatedClients_RegistersNothing()
    {
        // System.Runtime 程序集不含任何 [AiClient] 标注的 IChatClient 实现
        var registry = new AiClientRegistry();
        registry.Register(typeof(Object).Assembly);

        Assert.Empty(registry.Descriptors);
    }

    [Fact]
    [DisplayName("Register(Assembly) 后注册同 Code 覆盖先注册")]
    public void Register_Assembly_OverwritesExistingCode()
    {
        var registry = new AiClientRegistry();
        // 先手工注册一个同 Code 的占位描述符
        registry.Register(new AiClientDescriptor
        {
            Code = "TestExternal",
            DisplayName = "占位",
            DefaultEndpoint = "https://placeholder.com",
            Protocol = "OpenAI",
            Factory = opts => new OpenAIChatClient(opts),
        });

        // 再从测试程序集注册（应覆盖上面的占位）
        registry.Register(typeof(AiProviderTests).Assembly);

        Assert.Equal("外部测试服务商", registry.GetDescriptor("TestExternal")?.DisplayName);
    }

    [Fact]
    [DisplayName("Register(Assembly) 注册的服务商 Factory 可正常创建实例")]
    public void Register_Assembly_Factory_CreatesClientInstance()
    {
        var registry = new AiClientRegistry();
        registry.Register(typeof(AiProviderTests).Assembly);

        using var client = registry.CreateClient("TestExternal", new AiClientOptions { ApiKey = "test-key" });
        Assert.NotNull(client);
        Assert.IsType<ExternalFakeChatClient>(client);
    }

    #endregion

    #region Register（类型重载）

    [Fact]
    [DisplayName("Register(Type) 空类型参数抛 ArgumentNullException")]
    public void Register_Type_ThrowsForNull()
    {
        Assert.Throws<ArgumentNullException>(() => new AiClientRegistry().Register((Type)null!));
    }

    [Fact]
    [DisplayName("Register(Type) 抽象类型抛 ArgumentException")]
    public void Register_Type_ThrowsForAbstractType()
    {
        Assert.Throws<ArgumentException>(() => new AiClientRegistry().Register(typeof(AiClientBase)));
    }

    [Fact]
    [DisplayName("Register(Type) 未实现 IChatClient 的类型抛 ArgumentException")]
    public void Register_Type_ThrowsForNonIChatClient()
    {
        Assert.Throws<ArgumentException>(() => new AiClientRegistry().Register(typeof(String)));
    }

    [Fact]
    [DisplayName("Register(Type) 返回当前实例支持链式调用")]
    public void Register_Type_ReturnsThis_ForChaining()
    {
        var registry = new AiClientRegistry();
        var result = registry.Register(typeof(ExternalFakeChatClient));

        Assert.Same(registry, result);
    }

    [Fact]
    [DisplayName("Register(Type) 注册标注了 AiClient 的具体类型")]
    public void Register_Type_RegistersAnnotatedClient()
    {
        var registry = new AiClientRegistry();
        registry.Register(typeof(ExternalFakeChatClient));

        var descriptor = registry.GetDescriptor("TestExternal");
        Assert.NotNull(descriptor);
        Assert.Equal("外部测试服务商", descriptor!.DisplayName);
        Assert.Equal("https://test-external.api.com", descriptor.DefaultEndpoint);
    }

    [Fact]
    [DisplayName("Register(Type) 注册的描述符包含模型列表")]
    public void Register_Type_RegistersAnnotatedModels()
    {
        var registry = new AiClientRegistry();
        registry.Register(typeof(ExternalFakeChatClient));

        var descriptor = registry.GetDescriptor("TestExternal");
        Assert.NotNull(descriptor);
        Assert.NotEmpty(descriptor!.Models);
        Assert.Equal("test-model-v1", descriptor.Models[0].Model);
        Assert.Equal("测试模型 v1", descriptor.Models[0].DisplayName);
    }

    [Fact]
    [DisplayName("Register(Type) 无 AiClientAttribute 标注时静默跳过")]
    public void Register_Type_SkipsTypeWithNoAttributes()
    {
        var registry = new AiClientRegistry();
        registry.Register(typeof(FixedReplyChatClient));

        Assert.Empty(registry.Descriptors);
    }

    [Fact]
    [DisplayName("Register(Type) 注册的服务商 Factory 可正常创建实例")]
    public void Register_Type_Factory_CreatesClientInstance()
    {
        var registry = new AiClientRegistry();
        registry.Register(typeof(ExternalFakeChatClient));

        using var client = registry.CreateClient("TestExternal", new AiClientOptions { ApiKey = "test-key" });
        Assert.NotNull(client);
        Assert.IsType<ExternalFakeChatClient>(client);
    }

    [Fact]
    [DisplayName("Register(Type) 与 Register(Assembly) 注册结果等价")]
    public void Register_Type_EquivalentToAssemblyResult()
    {
        var byType = new AiClientRegistry();
        byType.Register(typeof(ExternalFakeChatClient));

        var byAssembly = new AiClientRegistry();
        byAssembly.Register(typeof(ExternalFakeChatClient).Assembly);

        var d1 = byType.GetDescriptor("TestExternal")!;
        var d2 = byAssembly.GetDescriptor("TestExternal")!;
        Assert.Equal(d1.Code, d2.Code);
        Assert.Equal(d1.DisplayName, d2.DisplayName);
        Assert.Equal(d1.DefaultEndpoint, d2.DefaultEndpoint);
        Assert.Equal(d1.Models.Length, d2.Models.Length);
    }

    #endregion

    // 测试专用：返回固定文本的假客户端
    private sealed class FixedReplyChatClient : IChatClient
    {
        private readonly String _text;

        public FixedReplyChatClient(String text) => _text = text;

        public Task<ChatResponse> GetResponseAsync(ChatRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new ChatResponse
            {
                Messages = [new ChatChoice { Message = new ChatMessage { Role = "assistant", Content = _text } }]
            });

        public IAsyncEnumerable<ChatResponse> GetStreamingResponseAsync(ChatRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public void Dispose() { }
    }
}

// 测试专用：模拟外部程序集中标注了 [AiClient] 的服务商实现
[AiClient("TestExternal", "外部测试服务商", "https://test-external.api.com")]
[AiClientModel("test-model-v1", "测试模型 v1")]
internal sealed class ExternalFakeChatClient : IChatClient
{
    public ExternalFakeChatClient(AiClientOptions options) { }

    public Task<ChatResponse> GetResponseAsync(ChatRequest request, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public IAsyncEnumerable<ChatResponse> GetStreamingResponseAsync(ChatRequest request, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public void Dispose() { }
}
