using System.Reflection;

namespace NewLife.AI.Providers;

/// <summary>AI 客户端注册表。描述符驱动，按编码查找服务商定义并通过工厂创建客户端实例</summary>
/// <remarks>
/// 替代旧版 <c>AiProviderFactory</c>。主要差异：
/// <list type="bullet">
/// <item>注册的是无状态 <see cref="AiClientDescriptor"/> 数据对象，而非服务商单例</item>
/// <item>每次调用 <see cref="AiClientDescriptor.Factory"/> 创建新客户端实例，天然无状态</item>
/// <item>内置 FullName→Code 别名映射，兼容数据库中存储的旧版类型全名</item>
/// </list>
/// </remarks>
public class AiClientRegistry
{
    #region 属性
    private readonly Dictionary<String, AiClientDescriptor> _descriptors = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>FullName → Code 别名映射，用于兼容数据库中存储的旧版类型全名</summary>
    private readonly Dictionary<String, String> _aliases = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>已注册的描述符字典（Code → 描述符，大小写不敏感）</summary>
    public IReadOnlyDictionary<String, AiClientDescriptor> Descriptors => _descriptors;

    /// <summary>全局默认实例。包含所有内置服务商描述符</summary>
    public static AiClientRegistry Default { get; } = CreateDefault();
    #endregion

    #region 注册
    /// <summary>注册一个服务商描述符</summary>
    /// <param name="descriptor">描述符实例</param>
    /// <returns>当前实例（支持链式调用）</returns>
    public AiClientRegistry Register(AiClientDescriptor descriptor)
    {
        if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
        _descriptors[descriptor.Code] = descriptor;
        return this;
    }

    /// <summary>注册 FullName → Code 别名，用于兼容旧版类型全名的数据库字段</summary>
    /// <param name="fullName">旧版类型全名，如 NewLife.AI.Providers.OpenAiProvider</param>
    /// <param name="code">对应的新版编码，如 OpenAI</param>
    /// <returns>当前实例（支持链式调用）</returns>
    public AiClientRegistry RegisterAlias(String fullName, String code)
    {
        if (fullName == null) throw new ArgumentNullException(nameof(fullName));
        if (code == null) throw new ArgumentNullException(nameof(code));
        _aliases[fullName] = code;
        return this;
    }
    #endregion

    #region 查找
    /// <summary>按服务商编码或类型全名获取描述符（大小写不敏感）</summary>
    /// <remarks>优先按 Code 查找；未命中时按 FullName 别名回退，支持旧版 ProviderConfig.Provider 字段</remarks>
    /// <param name="codeOrAlias">服务商编码（如 "OpenAI"）或类型全名（如 "NewLife.AI.Providers.OpenAiProvider"）</param>
    /// <returns>描述符，未注册时返回 null</returns>
    public AiClientDescriptor? GetDescriptor(String codeOrAlias)
    {
        if (String.IsNullOrWhiteSpace(codeOrAlias)) return null;

        if (_descriptors.TryGetValue(codeOrAlias, out var descriptor)) return descriptor;

        // 按类型全名别名回退查找
        if (_aliases.TryGetValue(codeOrAlias, out var code))
            _descriptors.TryGetValue(code, out descriptor);

        return descriptor;
    }

    /// <summary>按服务商编码或类型全名创建客户端实例</summary>
    /// <param name="codeOrAlias">服务商编码或类型全名</param>
    /// <param name="options">连接选项（ApiKey、Model、Endpoint 等）</param>
    /// <returns>已绑定连接参数的客户端实例</returns>
    /// <exception cref="ArgumentException">编码未注册时抛出</exception>
    public IChatClient CreateClient(String codeOrAlias, AiClientOptions options)
    {
        var descriptor = GetDescriptor(codeOrAlias)
            ?? throw new ArgumentException($"未注册的服务商编码: {codeOrAlias}", nameof(codeOrAlias));
        return descriptor.Factory(options);
    }
    #endregion

    #region 默认实例
    private static AiClientRegistry CreateDefault()
    {
        var registry = new AiClientRegistry();

        // 注册内置服务商并建立别名
        RegisterFromAttributes(registry, typeof(AiClientRegistry).Assembly);
        RegisterAliases(registry);

        return registry;
    }

    private static void RegisterFromAttributes(AiClientRegistry registry, Assembly assembly)
    {
        // 收集所有标注了 [AiClient] 的 IChatClient 具体类型
        var entries = new List<(Int32 order, AiClientAttribute attr, Type type)>();
        foreach (var type in assembly.GetTypes())
        {
            if (type.IsAbstract || type.IsInterface) continue;
            if (!typeof(IChatClient).IsAssignableFrom(type)) continue;

            var clientAttrs = Attribute.GetCustomAttributes(type, typeof(AiClientAttribute), false)
                                       .Cast<AiClientAttribute>()
                                       .ToArray();
            if (clientAttrs.Length == 0) continue;

            foreach (var attr in clientAttrs)
                entries.Add((attr.Order, attr, type));
        }

        // 按 Order 排序，保证注册顺序可预期
        foreach (var (_, attr, type) in entries.OrderBy(e => e.order).ThenBy(e => e.attr.Code))
            registry.Register(BuildDescriptor(attr, type));
    }

    private static AiClientDescriptor BuildDescriptor(AiClientAttribute attr, Type type)
    {
        // 收集该类上所有 [AiClientModel]，按 Code 归属过滤
        var allModels = Attribute.GetCustomAttributes(type, typeof(AiClientModelAttribute), false)
                                 .Cast<AiClientModelAttribute>()
                                 .ToArray();
        var isSingle = Attribute.GetCustomAttributes(type, typeof(AiClientAttribute), false).Length == 1;

        var models = allModels
            .Where(m => isSingle ? (m.Code == null || m.Code == attr.Code) : m.Code == attr.Code)
            .Select(m => new AiModelInfo(m.Model, m.DisplayName,
                new AiProviderCapabilities(m.Thinking, m.Vision, m.ImageGeneration, m.FunctionCalling)))
            .ToArray();

        // 找接受 AiClientOptions 为第一参数的构造函数
        var ctor = type.GetConstructors()
            .FirstOrDefault(c => {
                var ps = c.GetParameters();
                return ps.Length >= 1 && ps[0].ParameterType == typeof(AiClientOptions);
            });
        var ctorParamCount = ctor?.GetParameters().Length ?? 0;

        // 缓存 ChatPath 属性（仅在需要时查找，避免每次 Factory 调用反射）
        var chatPathProp = !String.IsNullOrEmpty(attr.ChatPath)
            ? type.GetProperty("ChatPath")
            : null;
        var chatPath       = attr.ChatPath;
        var defaultEndpoint = attr.DefaultEndpoint;

        return new AiClientDescriptor
        {
            Code            = attr.Code,
            DisplayName     = attr.DisplayName,
            Description     = attr.Description,
            DefaultEndpoint = defaultEndpoint,
            Protocol        = attr.Protocol,
            Models          = models,
            Factory         = opts =>
            {
                var filled = FillEndpoint(opts, defaultEndpoint);
                Object? instance;
                if (ctor != null)
                {
                    var args = new Object[ctorParamCount];
                    args[0] = filled;
                    instance = ctor.Invoke(args);
                }
                else
                    instance = Activator.CreateInstance(type, filled);

                chatPathProp?.SetValue(instance, chatPath);
                return (IChatClient)instance!;
            },
        };
    }
    private static void RegisterAliases(AiClientRegistry registry)
    {
        // 旧版 Provider 类型全名 → 新版 Code 映射（兼容数据库中存储的旧值）
        registry.RegisterAlias("NewLife.AI.Providers.NewLifeAiProvider", "NewLifeAI");
        registry.RegisterAlias("NewLife.AI.Providers.DashScopeProvider", "DashScope");
        registry.RegisterAlias("NewLife.AI.Providers.DeepSeekProvider", "DeepSeek");
        registry.RegisterAlias("NewLife.AI.Providers.OpenAiProvider", "OpenAI");
        registry.RegisterAlias("NewLife.AI.Providers.OllamaProvider", "Ollama");
        registry.RegisterAlias("NewLife.AI.Providers.AnthropicProvider", "Anthropic");
        registry.RegisterAlias("NewLife.AI.Providers.GeminiProvider", "Gemini");
        registry.RegisterAlias("NewLife.AI.Providers.AzureAiProvider", "AzureAI");
        registry.RegisterAlias("NewLife.AI.Providers.VolcEngineProvider", "VolcEngine");
        registry.RegisterAlias("NewLife.AI.Providers.ZhipuProvider", "Zhipu");
        registry.RegisterAlias("NewLife.AI.Providers.MoonshotProvider", "Moonshot");
        registry.RegisterAlias("NewLife.AI.Providers.HunyuanProvider", "Hunyuan");
        registry.RegisterAlias("NewLife.AI.Providers.QianfanProvider", "Qianfan");
        registry.RegisterAlias("NewLife.AI.Providers.SparkProvider", "Spark");
        registry.RegisterAlias("NewLife.AI.Providers.MiniMaxProvider", "MiniMax");
        registry.RegisterAlias("NewLife.AI.Providers.SiliconFlowProvider", "SiliconFlow");
        registry.RegisterAlias("NewLife.AI.Providers.MiMoProvider", "MiMo");
        registry.RegisterAlias("NewLife.AI.Providers.InfiniProvider", "Infini");
        registry.RegisterAlias("NewLife.AI.Providers.XiaomaPowerProvider", "XiaomaPower");
        registry.RegisterAlias("NewLife.AI.Providers.XAiProvider", "XAI");
        registry.RegisterAlias("NewLife.AI.Providers.MistralProvider", "Mistral");
        registry.RegisterAlias("NewLife.AI.Providers.CohereProvider", "Cohere");
        registry.RegisterAlias("NewLife.AI.Providers.PerplexityProvider", "Perplexity");
        registry.RegisterAlias("NewLife.AI.Providers.GroqProvider", "Groq");
        registry.RegisterAlias("NewLife.AI.Providers.CerebrasProvider", "Cerebras");
        registry.RegisterAlias("NewLife.AI.Providers.TogetherAiProvider", "TogetherAI");
        registry.RegisterAlias("NewLife.AI.Providers.FireworksProvider", "Fireworks");
        registry.RegisterAlias("NewLife.AI.Providers.SambaNovaProvider", "SambaNova");
        registry.RegisterAlias("NewLife.AI.Providers.YiProvider", "Yi");
        registry.RegisterAlias("NewLife.AI.Providers.GitHubModelsProvider", "GitHubModels");
        registry.RegisterAlias("NewLife.AI.Providers.OpenRouterProvider", "OpenRouter");
        registry.RegisterAlias("NewLife.AI.Providers.LMStudioProvider", "LMStudio");
        registry.RegisterAlias("NewLife.AI.Providers.VllmProvider", "vLLM");
        registry.RegisterAlias("NewLife.AI.Providers.OneApiProvider", "OneAPI");
    }

    /// <summary>确保 options 含有有效 Endpoint。若已有则原样返回；为空则返回填入 defaultEndpoint 的新实例</summary>
    private static AiClientOptions FillEndpoint(AiClientOptions opts, String defaultEndpoint)
    {
        if (!String.IsNullOrEmpty(opts.Endpoint)) return opts;
        return new AiClientOptions
        {
            Endpoint = defaultEndpoint,
            ApiKey = opts.ApiKey,
            Model = opts.Model,
            Organization = opts.Organization,
            Protocol = opts.Protocol,
        };
    }
    #endregion
}
