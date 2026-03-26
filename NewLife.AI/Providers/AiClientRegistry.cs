using System.Reflection;
using NewLife.AI.Clients;

namespace NewLife.AI.Providers;

/// <summary>AI 客户端注册表。描述符驱动，按编码查找服务商定义并通过工厂创建客户端实例</summary>
/// <remarks>
/// 替代旧版 <c>AiProviderFactory</c>。主要差异：
/// <list type="bullet">
/// <item>注册的是无状态 <see cref="AiClientDescriptor"/> 数据对象，而非服务商单例</item>
/// <item>每次调用 <see cref="AiClientDescriptor.Factory"/> 创建新客户端实例，天然无状态</item>
/// </list>
/// </remarks>
public class AiClientRegistry
{
    #region 属性
    private readonly Dictionary<String, AiClientDescriptor> _descriptors = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>DisplayName → Code 映射，供按显示名称回退查找</summary>
    private readonly Dictionary<String, String> _displayNames = new(StringComparer.OrdinalIgnoreCase);

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
        _displayNames[descriptor.DisplayName] = descriptor.Code;
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
        if (_displayNames.TryGetValue(code, out var mappedCode))
            _descriptors.TryGetValue(mappedCode, out descriptor);

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
    #endregion

    #region 默认实例
    private static AiClientRegistry CreateDefault()
    {
        var registry = new AiClientRegistry();
        RegisterFromAttributes(registry, typeof(AiClientRegistry).Assembly);
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
