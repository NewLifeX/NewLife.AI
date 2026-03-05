namespace NewLife.AI.Providers;

/// <summary>AI 服务商工厂。按名称注册和查找服务商实例</summary>
public class AiProviderFactory
{
    #region 属性
    private readonly Dictionary<String, IAiProvider> _providers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>已注册的服务商列表</summary>
    public IReadOnlyDictionary<String, IAiProvider> Providers => _providers;

    /// <summary>全局默认实例</summary>
    public static AiProviderFactory Default { get; } = CreateDefault();
    #endregion

    #region 注册
    /// <summary>注册服务商</summary>
    /// <param name="provider">服务商实例</param>
    /// <returns></returns>
    public AiProviderFactory Register(IAiProvider provider)
    {
        if (provider == null) throw new ArgumentNullException(nameof(provider));

        _providers[provider.Code] = provider;
        return this;
    }

    /// <summary>注册服务商</summary>
    /// <typeparam name="TProvider">服务商类型</typeparam>
    /// <returns></returns>
    public AiProviderFactory Register<TProvider>() where TProvider : IAiProvider, new()
    {
        var provider = new TProvider();
        return Register(provider);
    }
    #endregion

    #region 查找
    /// <summary>按名称获取服务商</summary>
    /// <param name="name">服务商名称</param>
    /// <returns>服务商实例，未找到返回 null</returns>
    public IAiProvider? GetProvider(String name)
    {
        if (String.IsNullOrWhiteSpace(name)) return null;

        _providers.TryGetValue(name, out var provider);
        return provider;
    }

    /// <summary>按名称获取服务商，未找到时抛出异常</summary>
    /// <param name="name">服务商名称</param>
    /// <returns>服务商实例</returns>
    public IAiProvider GetRequiredProvider(String name)
    {
        var provider = GetProvider(name);
        if (provider == null)
            throw new InvalidOperationException($"未找到名为 '{name}' 的 AI 服务商。已注册的服务商：{String.Join(", ", _providers.Keys)}");

        return provider;
    }

    /// <summary>获取所有已注册的服务商名称</summary>
    /// <returns></returns>
    public IList<String> GetProviderNames() => _providers.Keys.ToList();
    #endregion

    #region 静态方法
    /// <summary>创建包含所有内置服务商的默认工厂实例</summary>
    /// <returns></returns>
    private static AiProviderFactory CreateDefault()
    {
        var factory = new AiProviderFactory();

        // OpenAI 协议兼容服务商（按流行度降序）
        factory.Register(new OpenAiProvider());
        factory.Register(new AzureAiProvider());
        factory.Register(new DashScopeProvider());
        factory.Register(new DeepSeekProvider());
        factory.Register(new VolcEngineProvider());
        factory.Register(new ZhipuProvider());
        factory.Register(new MoonshotProvider());
        factory.Register(new HunyuanProvider());
        factory.Register(new QianfanProvider());
        factory.Register(new SparkProvider());
        factory.Register(new YiProvider());
        factory.Register(new MiniMaxProvider());
        factory.Register(new SiliconFlowProvider());
        factory.Register(new XAiProvider());
        factory.Register(new GitHubModelsProvider());
        factory.Register(new OpenRouterProvider());
        factory.Register(new OllamaProvider());
        factory.Register(new MiMoProvider());
        factory.Register(new TogetherAiProvider());
        factory.Register(new GroqProvider());
        factory.Register(new MistralProvider());
        factory.Register(new CohereProvider());
        factory.Register(new PerplexityProvider());
        factory.Register(new InfiniProvider());
        factory.Register(new CerebrasProvider());
        factory.Register(new FireworksProvider());
        factory.Register(new SambaNovaProvider());
        factory.Register(new XiaomaPowerProvider());

        // 本地推理引擎
        factory.Register(new LMStudioProvider());
        factory.Register(new VllmProvider());
        factory.Register(new OneApiProvider());

        // Anthropic 协议服务商
        factory.Register(new AnthropicProvider());

        // Gemini 协议服务商
        factory.Register(new GeminiProvider());

        return factory;
    }
    #endregion
}
