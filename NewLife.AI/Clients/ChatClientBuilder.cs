using NewLife.AI.Clients.Anthropic;
using NewLife.AI.Clients.DashScope;
using NewLife.AI.Clients.Gemini;
using NewLife.AI.Clients.Ollama;
using NewLife.AI.Clients.OpenAI;
using NewLife.AI.Filters;
using NewLife.AI.Tools;
using NewLife.Log;

namespace NewLife.AI.Clients;

/// <summary>AI 对话客户端构建器。通过链式 API 组装中间件管道</summary>
/// <remarks>
/// 参考 MEAI 的 ChatClientBuilder 设计。先添加的中间件包裹在最外层，请求时先执行。
/// 使用示例：
/// <code>
/// // 方式一：从描述符工厂创建客户端并传入构建器
/// var client = new ChatClientBuilder(AiClientRegistry.Default.CreateClient("OpenAI", opts))
///     .UseTools(toolRegistry)
///     .Build();
///
/// // 方式二：从已有 IChatClient 创建
/// var client = new ChatClientBuilder(existingClient)
///     .UseTools(toolRegistry)
///     .Build();
/// </code>
/// </remarks>
public sealed class ChatClientBuilder
{
    #region 属性

    private IChatClient? _innermost;
    private readonly List<Func<IChatClient, IChatClient>> _middlewares = [];

    #endregion

    #region 构造

    /// <summary>创建空构建器，后续通过 Use*() 方法设置内层客户端（MEAI 风格）</summary>
    /// <remarks>
    /// 与 MEAI 的 ChatClientBuilder 用法一致：
    /// <code>
    /// var client = new ChatClientBuilder()
    ///     .UseOpenAI(new HttpClient { BaseAddress = new Uri("https://api.openai.com") }, apiKey: "sk-xxx")
    ///     .UseTools(myTools)
    ///     .Build();
    /// </code>
    /// </remarks>
    public ChatClientBuilder() { }

    /// <summary>从已有客户端实例创建构建器</summary>
    /// <param name="innerClient">最内层客户端（实际执行 HTTP 调用的客户端）</param>
    public ChatClientBuilder(IChatClient innerClient)
    {
        if (innerClient == null) throw new ArgumentNullException(nameof(innerClient));
        _innermost = innerClient;
    }

    /// <summary>设置最内层客户端。供扩展方法（如 UseOpenAI）内部调用，支持链式调用</summary>
    /// <param name="client">实际执行 HTTP 请求的客户端实例</param>
    /// <returns>当前构建器（支持链式调用）</returns>
    internal ChatClientBuilder SetInnerClient(IChatClient client)
    {
        _innermost = client ?? throw new ArgumentNullException(nameof(client));
        return this;
    }

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
        if (_innermost == null)
            throw new InvalidOperationException("请先调用 Use*() 方法（如 UseOpenAI、UseDashScope）设置内层客户竭后再调用 Build()");

        // 从最内层提取 Log/Tracer，向外传播给各中间件（如 ToolChatClient / FilteredChatClient）
        var log = (_innermost as ILogFeature)?.Log;
        var tracer = (_innermost as ITracerFeature)?.Tracer;

        // 倒序应用：先添加的中间件包裹在最外层（请求时先执行）
        var client = _innermost;
        for (var i = _middlewares.Count - 1; i >= 0; i--)
        {
            client = _middlewares[i](client);
            if (log != null && client is ILogFeature logFeature) logFeature.Log = log;
            if (tracer != null && client is ITracerFeature tracerFeature) tracerFeature.Tracer = tracer;
        }

        return client;
    }

    #endregion
}

/// <summary>ChatClientBuilder 扩展方法。提供常用中间件的快捷注册入口</summary>
public static class ChatClientBuilderExtensions
{
    /// <summary>添加过滤器链中间件。按注册顺序在 CompleteAsync 前后执行 IChatFilter 列表</summary>
    /// <param name="builder">构建器</param>
    /// <param name="filters">过滤器列表（顺序执行）</param>
    /// <returns>构建器（支持链式调用）</returns>
    public static ChatClientBuilder UseFilters(this ChatClientBuilder builder, params IChatFilter[] filters)
        => builder.Use(inner => new FilteredChatClient(inner, filters));

    /// <summary>添加工具中间件。按注册顺序将所有 <see cref="IToolProvider"/> 的工具注入请求并处理工具调用回路</summary>
    /// <param name="builder">构建器</param>
    /// <param name="providers">工具提供者列表（按顺序递次尝试执行工具调用）</param>
    /// <returns>构建器（支持链式调用）</returns>
    public static ChatClientBuilder UseTools(this ChatClientBuilder builder, params IToolProvider[] providers)
        => builder.Use(inner => new ToolChatClient(inner, providers));

    /// <summary>添加工具中间件，并指定工具结果最大字符数。超过此长度时自动截断</summary>
    /// <param name="builder">构建器</param>
    /// <param name="maxResultLength">工具结果最大字符数。0表示不限制</param>
    /// <param name="providers">工具提供者列表</param>
    /// <returns>构建器（支持链式调用）</returns>
    public static ChatClientBuilder UseTools(this ChatClientBuilder builder, Int32 maxResultLength, params IToolProvider[] providers)
        => builder.Use(inner => new ToolChatClient(inner, providers) { MaxResultLength = maxResultLength });

    /// <summary>添加工具审批中间件。设置后 <see cref="ToolChatClient"/> 在执行每个工具前会请求用户确认</summary>
    /// <param name="builder">构建器</param>
    /// <param name="approvalProvider">工具审批提供者</param>
    /// <param name="providers">工具提供者列表</param>
    /// <returns>构建器（支持链式调用）</returns>
    public static ChatClientBuilder UseTools(this ChatClientBuilder builder, IToolApprovalProvider approvalProvider, params IToolProvider[] providers)
        => builder.Use(inner => new ToolChatClient(inner, providers) { ApprovalProvider = approvalProvider });

    /// <summary>添加工具审批中间件，并指定工具结果最大字符数</summary>
    /// <param name="builder">构建器</param>
    /// <param name="approvalProvider">工具审批提供者</param>
    /// <param name="maxResultLength">工具结果最大字符数。0表示不限制</param>
    /// <param name="providers">工具提供者列表</param>
    /// <returns>构建器（支持链式调用）</returns>
    public static ChatClientBuilder UseTools(this ChatClientBuilder builder, IToolApprovalProvider approvalProvider, Int32 maxResultLength, params IToolProvider[] providers)
        => builder.Use(inner => new ToolChatClient(inner, providers) { ApprovalProvider = approvalProvider, MaxResultLength = maxResultLength });

    // ── MEAI 风格 Use*() 工厂方法 ─────────────────────────────────────────

    /// <summary>使用 OpenAI 兼容协议客户端作为内层客户端</summary>
    /// <param name="builder">构建器</param>
    /// <param name="apiKey">API 密钥</param>
    /// <param name="model">默认模型编码，为空时由每次请求自行指定</param>
    /// <param name="endpoint">API 地址覆盖；为空时使用内置默认地址</param>
    /// <returns>构建器（支持链式调用）</returns>
    public static ChatClientBuilder UseOpenAI(this ChatClientBuilder builder, String apiKey, String? model = null, String? endpoint = null)
    {
        var opts = new AiClientOptions { Endpoint = endpoint, ApiKey = apiKey, Model = model };
        return builder.SetInnerClient(new OpenAIChatClient(opts));
    }

    /// <summary>使用阿里百炼 DashScope 客户端作为内层客户端</summary>
    /// <param name="builder">构建器</param>
    /// <param name="apiKey">阿里云 API Key</param>
    /// <param name="model">默认模型</param>
    /// <param name="endpoint">API 地址覆盖；为空时使用默认 DashScope 地址</param>
    /// <returns>构建器（支持链式调用）</returns>
    public static ChatClientBuilder UseDashScope(this ChatClientBuilder builder, String apiKey, String? model = null, String? endpoint = null)
    {
        var opts = new AiClientOptions { Endpoint = endpoint, ApiKey = apiKey, Model = model };
        return builder.SetInnerClient(new DashScopeChatClient(opts));
    }

    /// <summary>使用 Anthropic Claude 客户端作为内层客户端</summary>
    /// <param name="builder">构建器</param>
    /// <param name="apiKey">Anthropic API Key</param>
    /// <param name="model">默认模型</param>
    /// <param name="endpoint">API 地址覆盖</param>
    /// <returns>构建器（支持链式调用）</returns>
    public static ChatClientBuilder UseAnthropic(this ChatClientBuilder builder, String apiKey, String? model = null, String? endpoint = null)
    {
        var opts = new AiClientOptions { Endpoint = endpoint, ApiKey = apiKey, Model = model };
        return builder.SetInnerClient(new AnthropicChatClient(opts));
    }

    /// <summary>使用 Google Gemini 客户端作为内层客户端</summary>
    /// <param name="builder">构建器</param>
    /// <param name="apiKey">Google API Key</param>
    /// <param name="model">默认模型</param>
    /// <param name="endpoint">API 地址覆盖</param>
    /// <returns>构建器（支持链式调用）</returns>
    public static ChatClientBuilder UseGemini(this ChatClientBuilder builder, String apiKey, String? model = null, String? endpoint = null)
    {
        var opts = new AiClientOptions { Endpoint = endpoint, ApiKey = apiKey, Model = model };
        return builder.SetInnerClient(new GeminiChatClient(opts));
    }

    /// <summary>使用 Ollama 客户端作为内层客户端</summary>
    /// <param name="builder">构建器</param>
    /// <param name="apiKey">API 密钥；本地部署可传 null 或空字符串</param>
    /// <param name="model">默认模型</param>
    /// <param name="endpoint">Ollama 地址；为空时使用默认 http://localhost:11434</param>
    /// <returns>构建器（支持链式调用）</returns>
    public static ChatClientBuilder UseOllama(this ChatClientBuilder builder, String? apiKey = null, String? model = null, String? endpoint = null)
    {
        var opts = new AiClientOptions { Endpoint = endpoint, ApiKey = apiKey, Model = model };
        return builder.SetInnerClient(new OllamaChatClient(opts));
    }

    /// <summary>使用新生命 AI 网关客户端作为内层客户端</summary>
    /// <param name="builder">构建器</param>
    /// <param name="apiKey">新生命 AI 网关 API Key</param>
    /// <param name="model">默认模型</param>
    /// <param name="endpoint">网关地址覆盖；为空时使用内置默认地址</param>
    /// <returns>构建器（支持链式调用）</returns>
    public static ChatClientBuilder UseNewLifeAI(this ChatClientBuilder builder, String apiKey, String? model = null, String? endpoint = null)
    {
        var opts = new AiClientOptions { Endpoint = endpoint, ApiKey = apiKey, Model = model };
        return builder.SetInnerClient(new NewLifeAIChatClient(opts));
    }

    /// <summary>使用任意已注册服务商码创建客户端作为内层客户端</summary>
    /// <param name="builder">构建器</param>
    /// <param name="codeOrAlias">服务商编码或别名，如 "DashScope"</param>
    /// <param name="options">连接选项</param>
    /// <returns>构建器（支持链式调用）</returns>
    public static ChatClientBuilder UseAiClient(this ChatClientBuilder builder, String codeOrAlias, AiClientOptions options)
        => builder.SetInnerClient(AiClientRegistry.Default.CreateClient(codeOrAlias, options));
}
