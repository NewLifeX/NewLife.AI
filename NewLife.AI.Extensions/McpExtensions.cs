using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NewLife;
using NewLife.AI.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>MCP扩展</summary>
public static class McpExtensions
{
    /// <summary>注册 MCP 工具服务到 DI。工具方法可注入依赖（MapMcp 暴露时需能从 DI 解析工具实例）</summary>
    /// <typeparam name="TTools">工具服务类型，其公共方法将作为 MCP 工具暴露</typeparam>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合（支持链式调用）</returns>
    public static IServiceCollection AddMcp<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TTools>(this IServiceCollection services) where TTools : class
    {
        services.TryAddScoped<TTools>();
        return services;
    }

    /// <summary>启用MCP。注册 <typeparamref name="TTools"/> 的工具方法到 MCP 服务器（A-40：原实现从未调用 AddTool，工具列表恒为空）</summary>
    /// <typeparam name="TTools">工具服务类型，其公共方法将作为 MCP 工具暴露</typeparam>
    /// <param name="app">路由构建器</param>
    /// <param name="pattern">MCP 端点路径模式，默认 /mcp</param>
    /// <returns>路由构建器（支持链式调用）</returns>
    public static IEndpointRouteBuilder MapMcp<[DynamicallyAccessedMembers(ToolMemberTypes)] TTools>(this IEndpointRouteBuilder app, String pattern = "/mcp") where TTools : class
        => MapMcpCore(app, pattern, null, server => server.Manager.Register<TTools>());

    /// <summary>启用MCP。注册两个工具类型的公共方法到 MCP 服务器</summary>
    /// <typeparam name="T1">工具服务类型 1</typeparam>
    /// <typeparam name="T2">工具服务类型 2</typeparam>
    /// <param name="app">路由构建器</param>
    /// <param name="pattern">MCP 端点路径模式，默认 /mcp</param>
    /// <returns>路由构建器（支持链式调用）</returns>
    public static IEndpointRouteBuilder MapMcp<[DynamicallyAccessedMembers(ToolMemberTypes)] T1, [DynamicallyAccessedMembers(ToolMemberTypes)] T2>(this IEndpointRouteBuilder app, String pattern = "/mcp") where T1 : class where T2 : class
        => MapMcpCore(app, pattern, null, server =>
        {
            server.Manager.Register<T1>();
            server.Manager.Register<T2>();
        });

    /// <summary>启用MCP。注册多个工具类型的公共方法到 MCP 服务器</summary>
    /// <param name="app">路由构建器</param>
    /// <param name="pattern">MCP 端点路径模式</param>
    /// <param name="toolTypes">工具服务类型集合</param>
    /// <returns>路由构建器（支持链式调用）</returns>
    public static IEndpointRouteBuilder MapMcp(this IEndpointRouteBuilder app, String pattern, params Type[] toolTypes)
        => MapMcp(app, pattern, null, toolTypes);

    /// <summary>启用MCP。注册多个工具类型并配置服务器（认证/响应格式/legacy SSE）。认证令牌未显式配置时自动从配置节 Mcp:AuthToken 读取</summary>
    /// <param name="app">路由构建器</param>
    /// <param name="pattern">MCP 端点路径模式</param>
    /// <param name="configure">服务器配置委托</param>
    /// <param name="toolTypes">工具服务类型集合</param>
    /// <returns>路由构建器（支持链式调用）</returns>
    [UnconditionalSuppressMessage("Trimming", "IL2062", Justification = "Type 数组无法标注 DynamicallyAccessedMembers（IL2098）；泛型重载 MapMcp<T> 已在类型参数上声明保留要求，数组重载需调用方自行保证工具类型不被裁剪")]
    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "Type 数组无法标注 DynamicallyAccessedMembers（IL2098）；泛型重载 MapMcp<T> 已在类型参数上声明保留要求，数组重载需调用方自行保证工具类型不被裁剪")]
    public static IEndpointRouteBuilder MapMcp(this IEndpointRouteBuilder app, String pattern, Action<AspNetMcpServer>? configure, params Type[] toolTypes)
        => MapMcpCore(app, pattern, configure, server =>
        {
            foreach (var type in toolTypes)
            {
                if (type == null) continue;
                server.AddTool(type);
            }
        });

    /// <summary>注册端点。工具注册动作由调用方提供，泛型重载借此保留类型注解</summary>
    /// <param name="app">路由构建器</param>
    /// <param name="pattern">MCP 端点路径模式</param>
    /// <param name="configure">服务器配置委托</param>
    /// <param name="register">工具注册动作</param>
    /// <returns>路由构建器（支持链式调用）</returns>
    private static IEndpointRouteBuilder MapMcpCore(IEndpointRouteBuilder app, String pattern, Action<AspNetMcpServer>? configure, Action<AspNetMcpServer> register)
    {
        var server = new AspNetMcpServer();
        register(server);
        configure?.Invoke(server);

        // 认证令牌：未显式配置时从配置节读取（appsettings: Mcp:AuthToken）
        if (server.AuthToken.IsNullOrEmpty())
        {
            var config = app.ServiceProvider?.GetService<IConfiguration>();
            server.AuthToken = config?["Mcp:AuthToken"];
        }

        // 显式用 RequestDelegate 重载：泛型 Delegate 重载带 RequiresUnreferencedCode/RequiresDynamicCode，AOT 下不可用
        app.MapPost(pattern, (RequestDelegate)(context => server.ProcessAsync(context, context.RequestServices)));
        if (server.EnableLegacySse)
        {
            app.MapGet(pattern + "/sse", (RequestDelegate)(context => server.ProcessSseAsync(context, context.RequestServices)));
            app.MapPost(pattern + "/message", (RequestDelegate)(context => server.ProcessMessageAsync(context, context.RequestServices)));
        }

        return app;
    }

    /// <summary>工具类型需要保留的成员。与 <c>McpServer.AddTool(Type)</c> 的要求保持一致</summary>
    private const DynamicallyAccessedMemberTypes ToolMemberTypes = DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.NonPublicConstructors;
}

