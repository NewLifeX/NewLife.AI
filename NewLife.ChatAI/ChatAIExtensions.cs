using Microsoft.Extensions.FileProviders;
using NewLife.AI.Services;
using NewLife.AI.Tools;
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
            registry.AddTools(new BuiltinToolService());
            registry.AddTools(new NetworkToolService(sp));
            registry.AddTools(new CurrentUserTool(sp));
            return registry;
        });

        services.AddSingleton<IToolProvider, DbToolProvider>();

        services.AddSingleton<BackgroundGenerationService>();
        services.AddHostedService<ModelDiscoveryService>();
        services.AddHttpClient("McpClient");

        // 技能与记忆服务
        services.AddSingleton<SkillService>();
        services.AddSingleton<MemoryService>();
        services.AddScoped<ConversationAnalysisService>();

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

        // IP 归属地
        services.AddSingleton<IIpLocationService, IpLocationPconlineService>();
        services.AddSingleton<IIpLocationService, IpLocationIpApiService>();
        services.AddSingleton<IIpLocationService>(sp => new IpLocationRemoteService(url));

        // 天气
        services.AddSingleton<IWeatherService, WeatherNmcService>();
        services.AddSingleton<IWeatherService, WeatherWttrService>();
        services.AddSingleton<IWeatherService>(sp => new WeatherRemoteService(url));

        // 翻译
        services.AddSingleton<ITranslateService, TranslateMyMemoryService>();
        services.AddSingleton<ITranslateService>(sp => new TranslateRemoteService(url));

        // 搜索
        services.AddSingleton<ISearchService, SearchDuckDuckGoService>();
        services.AddSingleton<ISearchService>(sp => new SearchRemoteService(url));

        // 网页抓取
        services.AddSingleton<IWebFetchService, WebFetchDirectService>();
        services.AddSingleton<IWebFetchService>(sp => new WebFetchRemoteService(url));
    }
    #endregion
}
