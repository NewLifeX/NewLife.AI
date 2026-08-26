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
    public static IServiceCollection AddMcp<TTools>(this IServiceCollection services) where TTools : class
    {
        services.TryAddScoped<TTools>();
        return services;
    }

    /// <summary>启用MCP。注册 <typeparamref name="TTools"/> 的工具方法到 MCP 服务器（A-40：原实现从未调用 AddTool，工具列表恒为空）</summary>
    /// <typeparam name="TTools">工具服务类型，其公共方法将作为 MCP 工具暴露</typeparam>
    /// <param name="app">路由构建器</param>
    /// <param name="pattern">MCP 端点路径模式，默认 /mcp</param>
    /// <returns>路由构建器（支持链式调用）</returns>
    public static IEndpointRouteBuilder MapMcp<TTools>(this IEndpointRouteBuilder app, String pattern = "/mcp") where TTools : class
        => MapMcp(app, pattern, typeof(TTools));

    /// <summary>启用MCP。注册两个工具类型的公共方法到 MCP 服务器</summary>
    /// <typeparam name="T1">工具服务类型 1</typeparam>
    /// <typeparam name="T2">工具服务类型 2</typeparam>
    /// <param name="app">路由构建器</param>
    /// <param name="pattern">MCP 端点路径模式，默认 /mcp</param>
    /// <returns>路由构建器（支持链式调用）</returns>
    public static IEndpointRouteBuilder MapMcp<T1, T2>(this IEndpointRouteBuilder app, String pattern = "/mcp") where T1 : class where T2 : class
        => MapMcp(app, pattern, typeof(T1), typeof(T2));

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
    public static IEndpointRouteBuilder MapMcp(this IEndpointRouteBuilder app, String pattern, Action<AspNetMcpServer>? configure, params Type[] toolTypes)
    {
        var server = new AspNetMcpServer();
        foreach (var type in toolTypes)
        {
            if (type == null) continue;
            server.AddTool(type);
        }
        configure?.Invoke(server);

        // 认证令牌：未显式配置时从配置节读取（appsettings: Mcp:AuthToken）
        if (server.AuthToken.IsNullOrEmpty())
        {
            var config = app.ServiceProvider?.GetService<IConfiguration>();
            server.AuthToken = config?["Mcp:AuthToken"];
        }

        app.MapPost(pattern, server.ProcessAsync);
        if (server.EnableLegacySse)
        {
            app.MapGet(pattern + "/sse", server.ProcessSseAsync);
            app.MapPost(pattern + "/message", server.ProcessMessageAsync);
        }

        return app;
    }
}

