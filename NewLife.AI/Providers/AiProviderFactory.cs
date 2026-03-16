using System.Linq.Expressions;
using NewLife.Model;
using NewLife.Reflection;

namespace NewLife.AI.Providers;

/// <summary>AI 服务商工厂。通过反射扫描并注册 IAiProvider 实现类，按 ProviderConfig 缓存实例</summary>
public class AiProviderFactory
{
    #region 属性
    // 类型工厂注册表：Type → 编译后的无参构造函数委托（null 表示无公共无参构造，需通过 DI 创建）
    private readonly Dictionary<Type, Func<IAiProvider>?> _typeFactories = [];

    // FullName 大小写不敏感索引，供 GetProvider(String) 和 GetProviderForConfig 使用
    private readonly Dictionary<String, Type> _fullNameIndex = new(StringComparer.OrdinalIgnoreCase);

    // 每行 ProviderConfig 的运行时实例缓存：configId → IAiProvider 实例
    private readonly Dictionary<Int32, IAiProvider> _instances = [];

    /// <summary>已注册的 IAiProvider 实现类型集合</summary>
    public IEnumerable<Type> RegisteredTypes => _typeFactories.Keys;

    /// <summary>全局默认实例。启动时扫描 NewLife.AI 程序集中所有 IAiProvider 实现类</summary>
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
            RegisterType(type);
        }

        return this;
    }

    /// <summary>注册单个 IAiProvider 实现类型。
    /// 有公共无参构造时用表达式树编译委托（避免运行时反射开销）；否则存 null，调用时须借助 DI 容器</summary>
    /// <param name="type">要注册的实现类型，必须实现 <see cref="IAiProvider"/></param>
    /// <returns>当前工厂实例（支持链式调用）</returns>
    public AiProviderFactory RegisterType(Type type)
    {
        if (type == null) throw new ArgumentNullException(nameof(type));

        var ctor = type.GetConstructor(Type.EmptyTypes);
        _typeFactories[type] = ctor != null
            ? Expression.Lambda<Func<IAiProvider>>(Expression.New(ctor)).Compile()
            : null;

        _fullNameIndex[type.FullName!] = type;
        return this;
    }
    #endregion

    #region 查找
    /// <summary>按实现类 FullName 创建并返回 IAiProvider 实例，未注册或 FullName 无法解析时返回 null。
    /// 支持大小写不敏感匹配</summary>
    /// <param name="typeFullName">实现类完整类名（对应 ProviderConfig.Provider 字段值）</param>
    /// <param name="serviceProvider">可选 DI 容器，用于创建有构造参数的实现类</param>
    /// <returns>新创建的服务商实例，或 null</returns>
    public IAiProvider? GetProvider(String typeFullName, IServiceProvider? serviceProvider = null)
    {
        if (typeFullName.IsNullOrWhiteSpace()) return null;
        if (!_fullNameIndex.TryGetValue(typeFullName, out var type)) return null;

        return GetProvider(type, serviceProvider);
    }

    /// <summary>按实现类 Type 创建并返回 IAiProvider 实例，未注册时返回 null</summary>
    /// <param name="type">实现类类型</param>
    /// <param name="serviceProvider">可选 DI 容器，用于创建有构造参数的实现类</param>
    /// <returns>新创建的服务商实例，或 null</returns>
    public IAiProvider? GetProvider(Type type, IServiceProvider? serviceProvider = null)
    {
        if (type == null) return null;
        if (!_typeFactories.TryGetValue(type, out var factory)) return null;

        if (serviceProvider != null)
        {
            if (serviceProvider.CreateInstance(type) is IAiProvider provider) return provider;
        }

        if (factory == null) throw new ArgumentException($"类型 {type.FullName} 没有公共无参构造函数，请提供 IServiceProvider", nameof(type));

        return factory();
    }

    /// <summary>获取或创建指定 ProviderConfig 行对应的 IAiProvider 实例。
    /// 同一 configId 首次调用后缓存，后续调用直接返回缓存实例</summary>
    /// <param name="configId">ProviderConfig 主键，用作缓存键</param>
    /// <param name="typeFullName">IAiProvider 实现类 FullName（ProviderConfig.Provider 字段值）</param>
    /// <param name="serviceProvider">可选 DI 容器，用于创建有构造参数的实现类</param>
    /// <returns>服务商实例；typeFullName 未注册或无无参构造且未提供 DI 时返回 null</returns>
    public IAiProvider? GetProviderForConfig(Int32 configId, String typeFullName, IServiceProvider? serviceProvider = null)
    {
        if (typeFullName.IsNullOrWhiteSpace()) return null;
        if (_instances.TryGetValue(configId, out var provider)) return provider;

        lock (_instances)
        {
            if (_instances.TryGetValue(configId, out provider)) return provider;

            provider = GetProvider(typeFullName, serviceProvider);
            if (provider == null) return null;

            return _instances[configId] = provider;
        }
    }

    /// <summary>使指定 ProviderConfig 的缓存实例失效。配置（地址/密钥等）变更后调用，
    /// 下次 <see cref="GetProviderForConfig"/> 时将重新创建实例</summary>
    /// <param name="configId">ProviderConfig 主键</param>
    public void InvalidateConfig(Int32 configId)
    {
        lock (_instances)
        {
            _instances.Remove(configId);
        }
    }
    #endregion

    #region 静态方法
    /// <summary>创建默认工厂实例。按流行度顺序显式注册内置服务商，确保注册顺序确定且可预测</summary>
    /// <returns>已完成注册的工厂实例</returns>
    private static AiProviderFactory CreateDefault()
    {
        var factory = new AiProviderFactory();

        // 星语网关：排名第一
        factory.RegisterType(typeof(NewLifeAiProvider));

        // 第一梯队：国内主流 + 全球主流
        factory.RegisterType(typeof(DashScopeProvider));
        factory.RegisterType(typeof(DeepSeekProvider));
        factory.RegisterType(typeof(OpenAiProvider));
        factory.RegisterType(typeof(OllamaProvider));
        factory.RegisterType(typeof(AnthropicProvider));
        factory.RegisterType(typeof(GeminiProvider));
        factory.RegisterType(typeof(AzureAiProvider));
        factory.RegisterType(typeof(VolcEngineProvider));

        // 第二梯队：国内厂商
        factory.RegisterType(typeof(ZhipuProvider));
        factory.RegisterType(typeof(MoonshotProvider));
        factory.RegisterType(typeof(HunyuanProvider));
        factory.RegisterType(typeof(QianfanProvider));
        factory.RegisterType(typeof(SparkProvider));
        factory.RegisterType(typeof(MiniMaxProvider));
        factory.RegisterType(typeof(SiliconFlowProvider));
        factory.RegisterType(typeof(MiMoProvider));
        factory.RegisterType(typeof(InfiniProvider));
        factory.RegisterType(typeof(XiaomaPowerProvider));

        // 第三梯队：海外厂商
        factory.RegisterType(typeof(XAiProvider));
        factory.RegisterType(typeof(MistralProvider));
        factory.RegisterType(typeof(CohereProvider));
        factory.RegisterType(typeof(PerplexityProvider));
        factory.RegisterType(typeof(GroqProvider));
        factory.RegisterType(typeof(CerebrasProvider));
        factory.RegisterType(typeof(TogetherAiProvider));
        factory.RegisterType(typeof(FireworksProvider));
        factory.RegisterType(typeof(SambaNovaProvider));
        factory.RegisterType(typeof(YiProvider));

        // 第四梯队：平台 / 聚合 / 本地
        factory.RegisterType(typeof(GitHubModelsProvider));
        factory.RegisterType(typeof(OpenRouterProvider));
        factory.RegisterType(typeof(LMStudioProvider));
        factory.RegisterType(typeof(VllmProvider));
        factory.RegisterType(typeof(OneApiProvider));

        return factory;
    }
    #endregion
}
