using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using NewLife.AI.Tools;
using NewLife.ChatAI.Filters;
using NewLife.ChatAI.Handlers;
using NewLife.ChatAI.Tools;
using NewLife.Cube.Extensions;
using NewLife.Serialization;

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
        services.AddScoped<IMessageFlow, MessageFlowForWeb>();
        services.AddSingleton<IChatSetting>(_ => ChatSetting.Current);
        services.AddSingleton(_ => ChatSetting.Current);
        services.AddSingleton<SkillService>();
        services.AddSingleton<UsageService>();
        services.AddSingleton<ModelService>();
        services.AddSingleton<GatewayService>();
        services.AddSingleton<MessageFlowForGateway>();

        // 数据库查询工具
        services.AddSingleton<DbSchemaService>();
        services.AddSingleton<DbQueryToolService>();

        // IChatHandler 三段式调用链（OnBefore 正序、核心 LLM 在 MessageFlow.InvokeLlmAsync、OnAfter 正序）
        // OnBefore 与 OnAfter 均按注册顺序正序执行，顺序意义：见 Doc/L2-IChatHandler架构.md
        services.AddSingleton<IChatHandler, ContextRoundsHandler>();    // 0. OnBefore 会话轮数上限检查，超限则拒绝
        services.AddSingleton<IChatHandler, SuggestedCacheHandler>();   // 1. OnBefore 命中缓存时 Interceptor 短路 LLM
        services.AddSingleton<IChatHandler, SkillActivationHandler>();  // 2. OnBefore 技能解析与注入 / OnAfter 技能计数
        services.AddSingleton<IChatHandler, ToolContextHandler>();      // 2.5 OnBefore 工具仓位填充与选择 / 超仓工具目录注入
        services.AddSingleton<IChatHandler, TitleGenerationHandler>();  // 3. OnBefore 异步生成标题（与 LLM 并行）
        services.AddSingleton<IChatHandler, LearningHandler>();         // 4. OnBefore 注入记忆 / OnAfter 自学习分析（火焰即忘）
        services.AddSingleton<IChatHandler, UsageRecordHandler>();      // 5. OnAfter 用量入库
        services.AddSingleton<IChatHandler, PersistMessageHandler>();   // 6. OnAfter 最后落库消息/会话

        // Web UI 主调用链：收集全部已注册的 IChatHandler，按 [ChatHandlerOrder] 特性构建有序视图
        // TryAdd 语义：上层项目已注册时不重复注册
        services.TryAddSingleton(sp => new ChatHandlerChain(sp.GetServices<IChatHandler>()));

        // 工具服务注册（工具提供者实现）
        RegisterToolServices(services);

        // 原生 .NET 工具注册（通过配置器模式，支持外部项目追加工具）
        services.ConfigureToolRegistry((sp, registry) =>
        {
            registry.AddTools<HolidayToolService>();
            registry.AddTools<BuiltinToolService>();
            registry.AddTools<NetworkToolService>();
            registry.AddTools<CurrentUserTool>();
            registry.AddTools<WidgetToolService>();
            registry.AddTools<ChartToolService>();
            registry.AddTools<MapAnnotationToolService>();
            registry.AddTools<TimelineToolService>();
            registry.AddTools<MindmapToolService>();
            registry.AddTools<KanbanToolService>();
            registry.AddTools<DbQueryToolService>();
            registry.AddTools<BuildPptToolService>();
            registry.AddTools<BuildExcelToolService>();
            registry.AddTools<BuildDocToolService>();
        });

        services.TryAddSingleton(sp =>
        {
            var registry = new ToolRegistry { ServiceProvider = sp };
            foreach (var cfg in sp.GetServices<ToolRegistryConfigurator>())
            {
                cfg.Configure(sp, registry);
            }
            return registry;
        });

        services.AddSingleton<McpClientService>();
        services.AddSingleton<IToolProvider>(p => p.GetRequiredService<McpClientService>());
        services.AddSingleton<IToolProvider, DbToolProvider>();

        services.AddSingleton<BackgroundGenerationService>();
        services.AddSingleton<MemoryService>();
        services.AddSingleton<ConversationAnalysisService>();
        services.AddHttpClient("McpClient");

        // 消息频率限制器
        services.AddSingleton<MessageRateLimiter>();

        // 注册网关 JSON 输入格式化器，根据 Action 标记属性选择 snake_case / camelCase 反序列化
        services.Configure<MvcOptions>(options =>
        {
            var defaultJsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            };
            SystemJson.Apply(defaultJsonOptions, true);
            options.InputFormatters.Insert(0, new GatewayJsonInputFormatter(defaultJsonOptions));
        });

        services.AddHostedService<DataPreloadService>();

        return services;
    }

    /// <summary>向 ToolRegistry 追加注册自定义工具。可多次调用，所有配置器按注册顺序执行</summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">配置动作，接收服务提供者和 ToolRegistry 实例</param>
    /// <returns></returns>
    public static IServiceCollection ConfigureToolRegistry(this IServiceCollection services, Action<IServiceProvider, ToolRegistry> configure)
    {
        services.AddSingleton(new ToolRegistryConfigurator(configure));
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
        var assembly = typeof(ChatAIExtensions).Assembly;
        var embeddedProvider = new CubeEmbeddedFileProvider(assembly, "NewLife.ChatAI.wwwroot");

        if (!env.WebRootPath.IsNullOrEmpty() && Directory.Exists(env.WebRootPath) && env.WebRootFileProvider != null)
        {
            // 嵌入资源优先，再到主机的 WebRootFileProvider，覆盖 Cube 内嵌视图文件夹
            env.WebRootFileProvider = new CompositeFileProvider(
                env.WebRootFileProvider,
                embeddedProvider);
        }
        else
        {
            env.WebRootFileProvider = embeddedProvider;
        }

        app.UseStaticFiles();

        // OG meta 标签注入中间件：拦截 SPA HTML 响应，注入动态 meta 标签
        // 让 IM 工具（微信/钉钉/飞书等）爬取时显示正确的标题和图标
        app.Use(async (context, next) =>
        {
            var path = context.Request.Path.Value;
            if (path != null && IsSpaRoute(path))
            {
                var originalBody = context.Response.Body;
                using var memStream = new MemoryStream();
                context.Response.Body = memStream;

                try
                {
                    await next();

                    // 仅修改 200 HTML 响应
                    if (context.Response.StatusCode == 200 &&
                        context.Response.ContentType != null &&
                        context.Response.ContentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase))
                    {
                        memStream.Seek(0, SeekOrigin.Begin);
                        var html = await new StreamReader(memStream).ReadToEndAsync();

                        // 从 ChatSetting 读取站点配置
                        var setting = context.RequestServices.GetRequiredService<ChatSetting>();
                        var siteTitle = !String.IsNullOrEmpty(setting.SiteTitle) ? setting.SiteTitle : "智能助手";
                        var appName = !String.IsNullOrEmpty(setting.Name) ? setting.Name : "星语";
                        var logoUrl = !String.IsNullOrEmpty(setting.LogoUrl) ? setting.LogoUrl : "/logo.svg";

                        // 构建当前请求绝对 URL（用于 og:url）
                        var req = context.Request;
                        var baseUrl = $"{req.Scheme}://{req.Host}{req.PathBase}{req.Path}";
                        // 如果 logoUrl 是相对路径，转为绝对 URL
                        var absLogoUrl = logoUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                            ? logoUrl
                            : $"{req.Scheme}://{req.Host}{req.PathBase}{logoUrl}";

                        var metaTags = BuildMetaTags(siteTitle, appName, absLogoUrl, baseUrl);

                        if (html.Contains("</head>"))
                        {
                            html = html.Replace("</head>", metaTags + "</head>");
                        }

                        context.Response.Body = originalBody;
                        await context.Response.WriteAsync(html);
                    }
                    else
                    {
                        memStream.Seek(0, SeekOrigin.Begin);
                        await memStream.CopyToAsync(originalBody);
                    }
                }
                finally
                {
                    if (context.Response.Body == memStream)
                        context.Response.Body = originalBody;
                }
            }
            else
            {
                await next();
            }
        });

        // 独立部署时，根路径自动跳转到 /chat；否则，回退到未匹配路径的 chat.html
        // 子模块模式不注册根路由，保持与主应用的路由体系兼容
        if (redirectToChat)
        {
            app.MapGet("/", () => Results.Redirect("/chat"));
            // 仅对 /chat/* 与 /share/* 路径做 SPA 兜底，不干扰其他模块（如 Cube 后台）的路由
            app.MapFallbackToFile("/chat/{**path}", "chat.html");
            app.MapFallbackToFile("/share/{**path}", "chat.html");
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
        // 从 NativeTool 表读取配置，首次启动表为空时使用硬编码默认值
        var toolMap = LoadToolConfigFromDb();

        var ipTool = toolMap.GetValueOrDefault("get_ip_location");
        var ipProviders = ipTool?.Providers ?? "pconline";

        var weatherTool = toolMap.GetValueOrDefault("get_weather");
        var weatherProviders = weatherTool?.Providers ?? "nmc,wttr";

        var translateTool = toolMap.GetValueOrDefault("translate");
        var translateProviders = translateTool?.Providers ?? "mymemory";

        var searchTool = toolMap.GetValueOrDefault("web_search");
        var searchProviders = searchTool?.Providers ?? "bing,duckduckgo";
        var searchKey = searchTool?.ApiKey ?? "";

        var fetchTool = toolMap.GetValueOrDefault("web_fetch");
        var fetchProviders = fetchTool?.Providers ?? "direct";

        // IP 归属地
        foreach (var name in SplitProviders(ipProviders))
        {
            switch (name)
            {
                case "pconline": services.AddSingleton<IIpLocationService, IpLocationPconlineService>(); break;
            }
        }

        // 天气
        foreach (var name in SplitProviders(weatherProviders))
        {
            switch (name)
            {
                case "nmc": services.AddSingleton<IWeatherService, WeatherNmcService>(); break;
                case "wttr": services.AddSingleton<IWeatherService, WeatherWttrService>(); break;
            }
        }

        // 翻译
        foreach (var name in SplitProviders(translateProviders))
        {
            switch (name)
            {
                case "mymemory": services.AddSingleton<ITranslateService, TranslateMyMemoryService>(); break;
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
            }
        }

        // 网页抓取
        foreach (var name in SplitProviders(fetchProviders))
        {
            switch (name)
            {
                case "direct": services.AddSingleton<IWebFetchService, WebFetchDirectService>(); break;
            }
        }
    }

    /// <summary>从 NativeTool 表加载工具配置，首次启动为空时返回空字典（供调用方使用默认值）</summary>
    private static Dictionary<String, NativeTool> LoadToolConfigFromDb()
    {
        try
        {
            var list = NativeTool.FindAllWithCache();
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

    #region 辅助

    /// <summary>判断路径是否属于 SPA 前端路由</summary>
    private static Boolean IsSpaRoute(String path)
    {
        if (path == "/" || path == "/chat" || path == "/share") return true;

        // 带子路径的前缀匹配
        if (path.StartsWith("/chat/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/share/", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    /// <summary>构建 OG meta 标签 HTML 片段</summary>
    /// <param name="title">页面标题</param>
    /// <param name="siteName">站点名称</param>
    /// <param name="imageUrl">图片绝对 URL</param>
    /// <param name="url">页面绝对 URL</param>
    /// <returns>要在 &lt;/head&gt; 前插入的 HTML 片段</returns>
    private static String BuildMetaTags(String title, String siteName, String imageUrl, String url)
    {
        var encodedTitle = System.Net.WebUtility.HtmlEncode(title);
        var encodedSiteName = System.Net.WebUtility.HtmlEncode(siteName);
        var encodedImage = System.Net.WebUtility.HtmlEncode(imageUrl);
        var encodedUrl = System.Net.WebUtility.HtmlEncode(url);

        return $@"
    <meta property=""og:title"" content=""{encodedTitle}"" />
    <meta property=""og:site_name"" content=""{encodedSiteName}"" />
    <meta property=""og:description"" content=""{encodedTitle} - 智能AI对话助手"" />
    <meta property=""og:type"" content=""website"" />
    <meta property=""og:url"" content=""{encodedUrl}"" />
    <meta property=""og:image"" content=""{encodedImage}"" />
    <meta name=""twitter:card"" content=""summary_large_image"" />
    <meta name=""twitter:title"" content=""{encodedTitle}"" />
    <meta name=""twitter:description"" content=""{encodedTitle} - 智能AI对话助手"" />
    <meta name=""twitter:image"" content=""{encodedImage}"" />
";
    }

    #endregion
}
