using Microsoft.Extensions.FileProviders;
using NewLife.ChatAI.Services;
using NewLife.Cube;
using NewLife.Cube.Extensions;

namespace NewLife.ChatAI;

/// <summary>ChatAI 依赖注入与中间件扩展方法</summary>
/// <remarks>
/// 独立运行时（直接使用 Program.cs）：
///   services.AddChatAI()
///   app.UseChatAI(redirectToChat: true)
///
/// 作为子模块被其他项目引入时：
///   services.AddChatAI()
///   app.UseChatAI()   // redirectToChat 默认 false，由宿主决定默认路由
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
        services.AddSingleton<McpClientService>();
        services.AddSingleton<ToolCallService>();
        services.AddSingleton<BackgroundGenerationService>();
        services.AddHttpClient("McpClient");

        return services;
    }

    #endregion

    #region 中间件配置

    /// <summary>启用 ChatAI 中间件：嵌入静态资源、SPA 回退，以及可选的根路径重定向</summary>
    /// <param name="app">应用构建器</param>
    /// <param name="redirectToChat">
    /// 是否将根路径 "/" 重定向到 "/chat"。
    /// 独立运行时传 true；作为子模块嵌入时传 false（默认），由宿主明确告知用户访问路径。
    /// </param>
    /// <returns></returns>
    public static WebApplication UseChatAI(this WebApplication app, Boolean redirectToChat = false)
    {
        // 将嵌入在 DLL 中的 wwwroot 文件挂载为静态资源
        var env = app.Environment;
        var assembly = typeof(ChatAiStaticFilesService).Assembly;
        var embeddedProvider = new CubeEmbeddedFileProvider(assembly, "NewLife.ChatAI.wwwroot");

        if (!env.WebRootPath.IsNullOrEmpty() && Directory.Exists(env.WebRootPath) && env.WebRootFileProvider != null)
        {
            // 嵌入资源优先，再叠加宿主 WebRootFileProvider（如 Cube 的视图文件）
            env.WebRootFileProvider = new CompositeFileProvider(
                embeddedProvider,
                env.WebRootFileProvider);
        }
        else
        {
            env.WebRootFileProvider = embeddedProvider;
        }

        app.UseStaticFiles();

        // 独立运行时：将根路径自动跳转到 /chat，并兜底所有未匹配路由到 chat.html
        // 子模块模式不注册这两条路由，避免干扰宿主应用的路由体系
        if (redirectToChat)
        {
            app.MapGet("/", () => Results.Redirect("/chat"));
            app.MapFallbackToFile("chat.html");
        }

        return app;
    }

    #endregion
}
