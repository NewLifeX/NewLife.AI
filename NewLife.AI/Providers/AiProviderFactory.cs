using NewLife.Reflection;

namespace NewLife.AI.Providers;

/// <summary>AI 服务商工厂。注册时按 Code 缓存单例实例，支持按 Code 快速查找</summary>
public class AiProviderFactory
{
    #region 属性
    private readonly Dictionary<String, IAiProvider> _providers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>按类型全名索引（FullName → 实例），用于通过 ProviderConfig.Provider 字段定位实现类</summary>
    private readonly Dictionary<String, IAiProvider> _providersByTypeName = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>已注册的服务商字典（Code → 实例，大小写不敏感）</summary>
    public Dictionary<String, IAiProvider> Providers => _providers;

    /// <summary>全局默认实例。启动时显式注册所有内置服务商</summary>
    public static AiProviderFactory Default { get; } = CreateDefault();
    #endregion

    #region 注册与扫描
    /// <summary>扫描所有已加载程序集中的 IAiProvider 实现类并注册。
    /// 使用 <see cref="AssemblyX.FindAllPlugins"/> 发现当前应用目录下全部插件程序集，
    /// 适用于运行时动态加载自定义服务商扩展的场景</summary>
    /// <returns>当前工厂实例（支持链式调用）</returns>
    public AiProviderFactory ScanAllAsemblies()
    {
        foreach (var type in AssemblyX.FindAllPlugins(typeof(IAiProvider), true))
        {
            if (!type.IsAbstract)
                RegisterType(type);
        }

        return this;
    }

    /// <summary>通过泛型参数注册服务商。实例化后按 Code 缓存</summary>
    /// <typeparam name="T">IAiProvider 实现类型，需有公共无参构造</typeparam>
    /// <returns>当前工厂实例（支持链式调用）</returns>
    public AiProviderFactory Register<T>() where T : IAiProvider, new()
    {
        var instance = new T();
        _providers[instance.Code] = instance;
        _providersByTypeName[instance.GetType().FullName!] = instance;
        return this;
    }

    /// <summary>注册已有的服务商实例。按 Code 缓存</summary>
    /// <param name="provider">服务商实例</param>
    /// <returns>当前工厂实例（支持链式调用）</returns>
    public AiProviderFactory Register(IAiProvider provider)
    {
        if (provider == null) throw new ArgumentNullException(nameof(provider));

        _providers[provider.Code] = provider;
        _providersByTypeName[provider.GetType().FullName!] = provider;
        return this;
    }

    /// <summary>注册单个 IAiProvider 实现类型（供 ScanAllAsemblies 内部调用）。实例化后按 Code 缓存</summary>
    /// <param name="type">要注册的实现类型，必须实现 <see cref="IAiProvider"/></param>
    /// <returns>当前工厂实例（支持链式调用）</returns>
    public AiProviderFactory RegisterType(Type type)
    {
        if (type == null) throw new ArgumentNullException(nameof(type));

        var instance = (IAiProvider)Activator.CreateInstance(type)!;
        _providers[instance.Code] = instance;
        _providersByTypeName[type.FullName!] = instance;
        return this;
    }
    #endregion

    #region 查找
    /// <summary>按服务商编码或类型全名获取 IAiProvider 实例（大小写不敏感）。
    /// 优先按 <see cref="IAiProvider.Code"/> 查找；未命中时再按类型全名查找，
    /// 支持 ProviderConfig.Provider 字段（如 "NewLife.AI.Providers.OllamaProvider"）作为查找键</summary>
    /// <param name="code">服务商编码（如 "OpenAI"）或类型全名（如 "NewLife.AI.Providers.OllamaProvider"）</param>
    /// <returns>服务商实例，未注册时返回 null</returns>
    public IAiProvider? GetProvider(String code)
    {
        if (code.IsNullOrWhiteSpace()) return null;

        if (_providers.TryGetValue(code, out var provider)) return provider;

        // 按类型全名回退查找，支持 ProviderConfig.Provider 字段直接定位实现类
        _providersByTypeName.TryGetValue(code, out provider);
        return provider;
    }
    #endregion

    #region 便捷方法
    /// <summary>按服务商编码创建对话客户端</summary>
    /// <param name="code">服务商编码，如 "OpenAI"、"DashScope"</param>
    /// <param name="apiKey">API 密钥</param>
    /// <param name="model">默认模型编码</param>
    /// <param name="endpoint">自定义接入点地址，为空时使用服务商默认地址</param>
    /// <returns>已绑定连接参数的 IChatClient 实例</returns>
    public IChatClient CreateClient(String code, String apiKey, String? model = null, String? endpoint = null)
    {
        var provider = GetProvider(code) ?? throw new ArgumentException($"未注册的服务商编码: {code}", nameof(code));

        return provider.CreateClient(apiKey, model, endpoint);
    }
    #endregion

    #region 静态方法
    /// <summary>创建默认工厂实例。按流行度顺序显式注册内置服务商，确保注册顺序确定且可预测</summary>
    /// <returns>已完成注册的工厂实例</returns>
    private static AiProviderFactory CreateDefault()
    {
        var factory = new AiProviderFactory();

        // 星语网关：排名第一
        factory.Register<NewLifeAiProvider>();

        // 第一梯队：国内主流 + 全球主流
        factory.Register<DashScopeProvider>();
        factory.Register<DeepSeekProvider>();
        factory.Register<OpenAiProvider>();
        factory.Register<OllamaProvider>();
        factory.Register<AnthropicProvider>();
        factory.Register<GeminiProvider>();
        factory.Register<AzureAiProvider>();
        factory.Register<VolcEngineProvider>();

        // 第二梯队：国内厂商
        factory.Register<ZhipuProvider>();
        factory.Register<MoonshotProvider>();
        factory.Register<HunyuanProvider>();
        factory.Register<QianfanProvider>();
        factory.Register<SparkProvider>();
        factory.Register<MiniMaxProvider>();
        factory.Register<SiliconFlowProvider>();
        factory.Register<MiMoProvider>();
        factory.Register<InfiniProvider>();
        factory.Register<XiaomaPowerProvider>();

        // 第三梯队：海外厂商
        factory.Register<XAiProvider>();
        factory.Register<MistralProvider>();
        factory.Register<CohereProvider>();
        factory.Register<PerplexityProvider>();
        factory.Register<GroqProvider>();
        factory.Register<CerebrasProvider>();
        factory.Register<TogetherAiProvider>();
        factory.Register<FireworksProvider>();
        factory.Register<SambaNovaProvider>();
        factory.Register<YiProvider>();

        // 第四梯队：平台 / 聚合 / 本地
        factory.Register<GitHubModelsProvider>();
        factory.Register<OpenRouterProvider>();
        factory.Register<LMStudioProvider>();
        factory.Register<VllmProvider>();
        factory.Register<OneApiProvider>();

        return factory;
    }
    #endregion
}
