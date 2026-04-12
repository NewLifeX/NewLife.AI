using System.Reflection;

namespace NewLife.AI.Clients;

/// <summary>AI 客户端注册表。描述符驱动，按编码查找服务商定义并通过工厂创建客户端实例</summary>
/// <remarks>
/// <list type="bullet">
/// <item>注册的是无状态 <see cref="AiClientDescriptor"/> 数据对象，而非服务商单例</item>
/// <item>每次调用 <see cref="AiClientDescriptor.Factory"/> 创建新客户端实例，天然无状态</item>
/// </list>
/// </remarks>
public class AiClientRegistry
{
    #region 属性
    private readonly Dictionary<String, AiClientDescriptor> _descriptors = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<String, AiClientDescriptor> _displayNames = new(StringComparer.OrdinalIgnoreCase);

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
        if (!descriptor.DisplayName.IsNullOrEmpty())
            _displayNames[descriptor.DisplayName] = descriptor;

        return this;
    }

    /// <summary>扫描指定程序集中所有标注了 <see cref="AiClientAttribute"/> 的 <see cref="IChatClient"/> 实现类，自动注册服务商描述符</summary>
    /// <remarks>
    /// 可多次调用以注册来自不同程序集的服务商，同一 Code 后注册者覆盖先注册者。<br/>
    /// 支持链式调用：<c>registry.Register(asm1).Register(asm2)</c>
    /// </remarks>
    /// <param name="assembly">要扫描的目标程序集</param>
    /// <returns>当前实例（支持链式调用）</returns>
    public AiClientRegistry Register(Assembly assembly)
    {
        if (assembly == null) throw new ArgumentNullException(nameof(assembly));

        // 收集所有标注了 [AiClient] 的 IChatClient 具体类型
        var entries = new List<(Int32 order, AiClientAttribute attr, Type type)>();
        foreach (var type in assembly.GetTypes())
        {
            if (type.IsAbstract || type.IsInterface) continue;
            if (!typeof(IChatClient).IsAssignableFrom(type)) continue;

            var clientAttrs = type.GetCustomAttributes<AiClientAttribute>(false).ToArray();
            if (clientAttrs.Length == 0) continue;

            foreach (var attr in clientAttrs)
                entries.Add((attr.Order, attr, type));
        }

        // 按 Order 排序，保证注册顺序可预期
        foreach (var (_, attr, type) in entries.OrderBy(e => e.order).ThenBy(e => e.attr.Code))
            Register(BuildDescriptor(attr, type));

        return this;
    }

    /// <summary>注册一个 <see cref="IChatClient"/> 实现类型上标注的所有 <see cref="AiClientAttribute"/> 为服务商描述符</summary>
    public AiClientRegistry Register<T>() where T : IChatClient => Register(typeof(T));

    /// <summary>将指定 <see cref="IChatClient"/> 实现类型上标注的所有 <see cref="AiClientAttribute"/> 注册为服务商描述符</summary>
    /// <remarks>
    /// 适用于精确注册某一具体实现类，而无需扫描整个程序集。<br/>
    /// 类型上无任何 <see cref="AiClientAttribute"/> 时静默跳过，不抛出异常。
    /// </remarks>
    /// <param name="type">标注了 <see cref="AiClientAttribute"/> 的 <see cref="IChatClient"/> 具体实现类型</param>
    /// <returns>当前实例（支持链式调用）</returns>
    /// <exception cref="ArgumentNullException">type 为 null 时抛出</exception>
    /// <exception cref="ArgumentException">type 为抽象类或接口，或未实现 <see cref="IChatClient"/> 时抛出</exception>
    public AiClientRegistry Register(Type type)
    {
        if (type == null) throw new ArgumentNullException(nameof(type));
        if (type.IsAbstract || type.IsInterface)
            throw new ArgumentException($"类型 {type.FullName} 不能是抽象类或接口", nameof(type));
        if (!typeof(IChatClient).IsAssignableFrom(type))
            throw new ArgumentException($"类型 {type.FullName} 必须实现 IChatClient 接口", nameof(type));

        var clientAttrs = type.GetCustomAttributes<AiClientAttribute>(false).ToArray();
        if (clientAttrs.Length == 0) return this;

        foreach (var (_, attr, t) in clientAttrs
            .Select(attr => (attr.Order, attr, type))
            .OrderBy(e => e.Order).ThenBy(e => e.attr.Code))
            Register(BuildDescriptor(attr, t));

        return this;
    }
    #endregion

    #region 查找
    /// <summary>按服务商编码或显示名称获取描述符（大小写不敏感）</summary>
    /// <remarks>优先按 Code 查找；未命中时按 DisplayName 回退，方便用人类可读名称查询</remarks>
    /// <param name="code">服务商编码（如 "OpenAI"）或显示名称（如 "深度求索"）</param>
    /// <returns>描述符，未注册时返回 null</returns>
    public AiClientDescriptor? GetDescriptor(String code)
    {
        if (String.IsNullOrWhiteSpace(code)) return null;

        if (_descriptors.TryGetValue(code, out var descriptor)) return descriptor;

        // 按显示名称回退查找
        if (_displayNames.TryGetValue(code, out descriptor)) return descriptor;

        return descriptor;
    }

    /// <summary>按服务商编码或显示名称创建客户端实例</summary>
    /// <param name="code">服务商编码（如 "OpenAI"）或显示名称（如 "深度求索"）</param>
    /// <param name="options">连接选项（ApiKey、Model、Endpoint 等）</param>
    /// <returns>已绑定连接参数的客户端实例</returns>
    /// <exception cref="ArgumentException">编码未注册时抛出</exception>
    public IChatClient CreateClient(String code, AiClientOptions options)
    {
        var descriptor = GetDescriptor(code)
            ?? throw new ArgumentException($"未注册的服务商编码: {code}", nameof(code));
        return descriptor.Factory(options);
    }

    /// <summary>以 API 密钥和可选模型快速创建客户端实例</summary>
    /// <param name="code">服务商编码（如 "DashScope"）或显示名称</param>
    /// <param name="apiKey">API 密钥</param>
    /// <param name="model">默认模型编码，为空时由每次请求指定</param>
    /// <param name="endpoint">API 地址覆盖；为空时使用服务商默认地址</param>
    /// <returns>已绑定连接参数的客户端实例</returns>
    public IChatClient CreateClient(String code, String apiKey, String? model = null, String? endpoint = null)
        => CreateClient(code, new AiClientOptions { ApiKey = apiKey, Model = model, Endpoint = endpoint });
    #endregion

    #region 默认实例
    private static AiClientRegistry CreateDefault()
    {
        var registry = new AiClientRegistry();
        registry.Register(typeof(AiClientRegistry).Assembly);
        return registry;
    }

    private static AiClientDescriptor BuildDescriptor(AiClientAttribute attr, Type type)
    {
        // 收集该类上所有 [AiClientModel]，按 Code 归属过滤
        var allModels = type.GetCustomAttributes<AiClientModelAttribute>(false).ToArray();
        var isSingle = type.GetCustomAttributes<AiClientAttribute>(false).Count() == 1;

        var models = allModels
            .Where(m => isSingle ? (m.Code == null || m.Code == attr.Code) : m.Code == attr.Code)
            .Select(m => new AiModelInfo(m.Model, m.DisplayName,
                new AiProviderCapabilities(m.Thinking, m.FunctionCalling, m.Vision, m.Audio, m.ImageGeneration, m.VideoGeneration, m.ContextLength)))
            .ToArray();

        // 找接受 AiClientOptions 为第一参数的构造函数
        var ctor = type.GetConstructors()
            .FirstOrDefault(c =>
            {
                var ps = c.GetParameters();
                return ps.Length >= 1 && ps[0].ParameterType == typeof(AiClientOptions);
            });
        var ctorParamCount = ctor?.GetParameters().Length ?? 0;

        var chatPath = attr.ChatPath;
        var defaultEndpoint = attr.DefaultEndpoint;

        return new AiClientDescriptor
        {
            Code = attr.Code,
            DisplayName = attr.DisplayName,
            Description = attr.Description,
            DefaultEndpoint = defaultEndpoint,
            Protocol = attr.Protocol,
            Models = models,
            Factory = opts =>
            {
                if (String.IsNullOrEmpty(opts.Endpoint)) opts.Endpoint = defaultEndpoint;
                Object? instance;
                if (ctor != null)
                {
                    var args = new Object[ctorParamCount];
                    args[0] = opts;
                    instance = ctor.Invoke(args);
                }
                else
                    instance = Activator.CreateInstance(type, opts);

                if (instance is AiClientBase clientBase)
                {
                    clientBase.Name = attr.DisplayName;
                    if (!String.IsNullOrEmpty(chatPath)) clientBase.ChatPath = chatPath;
                }
                return (IChatClient)instance!;
            },
        };
    }
    #endregion
}
