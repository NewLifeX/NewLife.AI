using Microsoft.Extensions.FileProviders;
using NewLife.AI.Services;
using NewLife.AI.Tools;
using NewLife.ChatAI.Entity;
using NewLife.ChatAI.Services;
using NewLife.Cube.Extensions;

namespace NewLife.ChatAI;

/// <summary>ChatAI 服务注册与中间件扩展方法</summary>
/// <remarks>
/// 独立部署时，直接使用 Program.cs：
///   services.AddChatAI()
///   app.UseChatAI(redirectToChat: true)
///
/// 作为子模块被其他项目引用时：
///   services.AddChatAI()
///   app.UseChatAI()   // redirectToChat 默认 false，不干扰主应用的默认路由
/// </remarks>
public static class ChatAIExtensions
{
    #region 服务注册

    /// <summary>注册 ChatAI 所需的全部服务</summary>
    /// <param name="services">服务集合</param>
    /// <returns></returns>
    public static IServiceCollection AddChatAI(this IServiceCollection services)
    {
        services.AddScoped<ChatApplicationService>();
        services.AddSingleton<SkillService>();
        services.AddSingleton<UsageService>();
        services.AddSingleton<GatewayService>();

        // 对话执行管道：将能力扩展层（工具调用、技能注入）与知识进化层（记忆注入、自学习、事件智能体）装配为统一执行入口
        // ChatApplicationService 通过 IChatPipeline 驱动执行，对各层实现细节保持透明
        // IEnumerable<IToolProvider> 由 DI 自动聚合所有注册的 IToolProvider 实现（DbToolProvider、McpClientService 等）
        services.AddSingleton<IChatPipeline, ChatAIPipeline>();

        // 工具服务注册（工具提供者实现）
        RegisterToolServices(services);

        // 原生 .NET 工具注册
        services.AddSingleton(sp =>
        {
            var registry = new ToolRegistry();
            registry.AddTools(new HolidayToolService());
            registry.AddTools(new BuiltinToolService());
            registry.AddTools(new NetworkToolService(sp));
            registry.AddTools(new CurrentUserTool());
            return registry;
        });

        services.AddSingleton<McpClientService>();
        services.AddSingleton<IToolProvider>(p => p.GetRequiredService<McpClientService>());
        services.AddSingleton<IToolProvider, DbToolProvider>();

        services.AddSingleton<BackgroundGenerationService>();
        services.AddSingleton<MemoryService>();
        services.AddHostedService<ModelDiscoveryService>();
        services.AddHostedService<NativeToolSyncService>();
        services.AddHttpClient("McpClient");

        // 消息频率限制器
        services.AddSingleton<MessageRateLimiter>();

        return services;
    }

    #endregion

    #region 中间件配置

    /// <summary>配置 ChatAI 中间件：嵌入静态资源（SPA 前端），以及可选的根路由重定向</summary>
    /// <param name="app">应用构建器</param>
    /// <param name="redirectToChat">
    /// 是否将根路由 "/" 重定向到 "/chat"。
    /// 独立部署时为 true；作为子模块嵌入时为 false（默认），不干扰主应用确定的路由前缀
    /// </param>
    /// <returns></returns>
    public static WebApplication UseChatAI(this WebApplication app, Boolean redirectToChat = false)
    {
        // 嵌入在 DLL 中的 wwwroot 文件，作为静态资源
        var env = app.Environment;
        var assembly = typeof(ChatAiStaticFilesService).Assembly;
        var embeddedProvider = new CubeEmbeddedFileProvider(assembly, "NewLife.ChatAI.wwwroot");

        if (!env.WebRootPath.IsNullOrEmpty() && Directory.Exists(env.WebRootPath) && env.WebRootFileProvider != null)
        {
            // 嵌入资源优先，再到主机的 WebRootFileProvider，覆盖 Cube 内嵌视图文件夹
            env.WebRootFileProvider = new CompositeFileProvider(
                embeddedProvider,
                env.WebRootFileProvider);
        }
        else
        {
            env.WebRootFileProvider = embeddedProvider;
        }

        app.UseStaticFiles();

        // 独立部署时，根路径自动跳转到 /chat；否则，回退到未匹配路径的 chat.html
        // 子模块模式不注册根路由，保持与主应用的路由体系兼容
        if (redirectToChat)
        {
            app.MapGet("/", () => Results.Redirect("/chat"));
            // 仅对 /chat/* 路径做 SPA 兜底，不干扰其他模块（如 Cube 后台）的路由
            app.MapFallbackToFile("/chat/{**path}", "chat.html");
        }

        return app;
    }

    #endregion

    #region 工具服务注册

    /// <summary>从 NativeTool 表读取配置并注册工具服务实现。首次启动表为空时使用硬编码默认值，
    /// 外部同名注册的接口不受影响（TryAdd 语义）</summary>
    /// <param name="services">服务集合</param>
    private static void RegisterToolServices(IServiceCollection services)
    {
        const String url = "https://ai.newlifex.com";

        // 从 NativeTool 表读取配置，首次启动表为空时使用硬编码默认值
        var toolMap = LoadToolConfigFromDb();

        var ipTool = toolMap.GetValueOrDefault("get_ip_location");
        var ipProviders = ipTool?.Providers ?? "pconline,ipapi";

        var weatherTool = toolMap.GetValueOrDefault("get_weather");
        var weatherProviders = weatherTool?.Providers ?? "nmc,wttr";

        var translateTool = toolMap.GetValueOrDefault("translate");
        var translateProviders = translateTool?.Providers ?? "mymemory";

        var searchTool = toolMap.GetValueOrDefault("web_search");
        var searchProviders = searchTool?.Providers ?? "bing,duckduckgo";
        var searchKey = searchTool?.ApiKey ?? "";
        var searchRemoteUrl = searchTool?.Endpoint ?? url;

        var fetchTool = toolMap.GetValueOrDefault("web_fetch");
        var fetchProviders = fetchTool?.Providers ?? "direct";
        var fetchRemoteUrl = fetchTool?.Endpoint ?? url;

        // IP 归属地
        foreach (var name in SplitProviders(ipProviders))
        {
            switch (name)
            {
                case "pconline": services.AddSingleton<IIpLocationService, IpLocationPconlineService>(); break;
                case "ipapi": services.AddSingleton<IIpLocationService, IpLocationIpApiService>(); break;
                case "newlife":
                    var ipRemote = ipTool?.Endpoint ?? url;
                    services.AddSingleton<IIpLocationService>(sp => new IpLocationRemoteService(ipRemote)); break;
            }
        }

        // 天气
        foreach (var name in SplitProviders(weatherProviders))
        {
            switch (name)
            {
                case "nmc": services.AddSingleton<IWeatherService, WeatherNmcService>(); break;
                case "wttr": services.AddSingleton<IWeatherService, WeatherWttrService>(); break;
                case "newlife":
                    var weatherRemote = weatherTool?.Endpoint ?? url;
                    services.AddSingleton<IWeatherService>(sp => new WeatherRemoteService(weatherRemote)); break;
            }
        }

        // 翻译
        foreach (var name in SplitProviders(translateProviders))
        {
            switch (name)
            {
                case "mymemory": services.AddSingleton<ITranslateService, TranslateMyMemoryService>(); break;
                case "newlife":
                    var translateRemote = translateTool?.Endpoint ?? url;
                    services.AddSingleton<ITranslateService>(sp => new TranslateRemoteService(translateRemote)); break;
            }
        }

        // 搜索
        foreach (var name in SplitProviders(searchProviders))
        {
            switch (name)
            {
                case "bing": services.AddSingleton<ISearchService>(sp => new SearchBingService(searchKey)); break;
                case "serper": services.AddSingleton<ISearchService>(sp => new SearchSerperService(searchKey)); break;
                case "duckduckgo": services.AddSingleton<ISearchService, SearchDuckDuckGoService>(); break;
                case "newlife": services.AddSingleton<ISearchService>(sp => new SearchRemoteService(searchRemoteUrl)); break;
            }
        }

        // 网页抓取
        foreach (var name in SplitProviders(fetchProviders))
        {
            switch (name)
            {
                case "direct": services.AddSingleton<IWebFetchService, WebFetchDirectService>(); break;
                case "newlife": services.AddSingleton<IWebFetchService>(sp => new WebFetchRemoteService(fetchRemoteUrl)); break;
            }
        }
    }

    /// <summary>从 NativeTool 表加载工具配置，首次启动为空时返回空字典（供调用方使用默认值）</summary>
    private static Dictionary<String, NativeTool> LoadToolConfigFromDb()
    {
        try
        {
            var list = NativeTool.FindAll();
            return list.ToDictionary(t => t.Name!, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            // 数据库未就绪（首次启动）时，默认返回空字典，使用硬编码默认值
            return [];
        }
    }

    /// <summary>将逗号分隔的提供者列表拆分为数组，去除空白</summary>
    private static String[] SplitProviders(String? providers) =>
        String.IsNullOrWhiteSpace(providers)
            ? []
            : providers.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    #endregion
}
