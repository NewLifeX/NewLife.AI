using NewLife.AI.Filters;
using NewLife.AI.Models;
using NewLife.AI.Tools;
using NewLife.Log;

namespace NewLife.AI.Providers;

/// <summary>AI 对话客户端构建器。通过链式 API 组装中间件管道</summary>
/// <remarks>
/// 参考 MEAI 的 ChatClientBuilder 设计。先添加的中间件包裹在最外层，请求时先执行。
/// 使用示例：
/// <code>
/// // 方式一：从 IAiProvider 创建
/// var client = new ChatClientBuilder(provider, options)
///     .UseLogging(log)
///     .UseTracing(tracer)
///     .UseUsageTracking((usage, model) => Console.WriteLine($"{model}: {usage.TotalTokens} tokens"))
///     .Build();
///
/// // 方式二：从已有 IChatClient 创建
/// var client = new ChatClientBuilder(existingClient)
///     .UseLogging()
///     .Build();
/// </code>
/// </remarks>
public sealed class ChatClientBuilder
{
    #region 属性

    private readonly IChatClient _innermost;
    private readonly List<Func<IChatClient, IChatClient>> _middlewares = [];

    #endregion

    #region 构造

    /// <summary>从已有客户端实例创建构建器</summary>
    /// <param name="innerClient">最内层客户端（实际执行 HTTP 调用的客户端）</param>
    public ChatClientBuilder(IChatClient innerClient)
    {
        if (innerClient == null) throw new ArgumentNullException(nameof(innerClient));
        _innermost = innerClient;
    }

    /// <summary>从服务商工厂创建构建器，自动调用 CreateClient 生成绑定参数的客户端</summary>
    /// <param name="provider">AI 服务商</param>
    /// <param name="options">连接选项（Endpoint、ApiKey 等）</param>
    public ChatClientBuilder(IAiProvider provider, AiProviderOptions options)
        : this(provider.CreateClient(options)) { }

    #endregion

    #region 方法

    /// <summary>添加一个中间件工厂到管道。先添加的中间件包裹在外层，请求时先执行</summary>
    /// <param name="middleware">接受内层客户端、返回新客户端的工厂函数</param>
    /// <returns>当前构建器（支持链式调用）</returns>
    public ChatClientBuilder Use(Func<IChatClient, IChatClient> middleware)
    {
        if (middleware == null) throw new ArgumentNullException(nameof(middleware));
        _middlewares.Add(middleware);
        return this;
    }

    /// <summary>构建并返回组装完成的客户端管道</summary>
    /// <returns>最外层 IChatClient，调用时按中间件添加顺序依次执行</returns>
    public IChatClient Build()
    {
        // 倒序应用：先添加的中间件包裹在最外层（请求时先执行）
        var client = _innermost;
        for (var i = _middlewares.Count - 1; i >= 0; i--)
            client = _middlewares[i](client);
        return client;
    }

    #endregion
}

/// <summary>ChatClientBuilder 扩展方法。提供常用中间件的快捷注册入口</summary>
public static class ChatClientBuilderExtensions
{
    /// <summary>添加日志中间件。记录每次请求的模型、耗时与 Token 用量</summary>
    /// <param name="builder">构建器</param>
    /// <param name="log">日志实例，为 null 时使用 XTrace</param>
    /// <returns>构建器（支持链式调用）</returns>
    public static ChatClientBuilder UseLogging(this ChatClientBuilder builder, ILog? log = null)
        => builder.Use(inner => new LoggingChatClient(inner, log));

    /// <summary>添加分布式追踪中间件（NewLife ITracer）。为每次对话创建 Span，记录耗时与 Token</summary>
    /// <param name="builder">构建器</param>
    /// <param name="tracer">追踪器，为 null 时不追踪</param>
    /// <returns>构建器（支持链式调用）</returns>
    public static ChatClientBuilder UseTracing(this ChatClientBuilder builder, ITracer? tracer = null)
        => builder.Use(inner => new TracingChatClient(inner, tracer));

    /// <summary>添加用量追踪中间件。对话完成后触发回调以记录 Token 消耗</summary>
    /// <param name="builder">构建器</param>
    /// <param name="onUsage">用量回调，参数为（用量统计, 模型编码）；通常用于接入轻量聚合统计</param>
    /// <returns>构建器（支持链式调用）</returns>
    public static ChatClientBuilder UseUsageTracking(this ChatClientBuilder builder, Action<ChatUsage, String?> onUsage)
        => builder.Use(inner => new UsageTrackingChatClient(inner, onUsage));

    /// <summary>添加过滤器链中间件。按注册顺序在 CompleteAsync 前后执行 IChatFilter 列表</summary>
    /// <param name="builder">构建器</param>
    /// <param name="filters">过滤器列表（顺序执行）</param>
    /// <returns>构建器（支持链式调用）</returns>
    public static ChatClientBuilder UseFilters(this ChatClientBuilder builder, params IChatFilter[] filters)
        => builder.Use(inner => new FilteredChatClient(inner, filters));

    /// <summary>添加原生工具中间件。自动将 <see cref="ToolRegistry"/> 中的工具注入请求，并处理工具调用回路</summary>
    /// <param name="builder">构建器</param>
    /// <param name="registry">工具注册表</param>
    /// <returns>构建器（支持链式调用）</returns>
    public static ChatClientBuilder UseNativeTools(this ChatClientBuilder builder, ToolRegistry registry)
        => builder.Use(inner => new NativeToolChatClient(inner, registry));
}
