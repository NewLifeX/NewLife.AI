using NewLife.AI.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>MCP扩展</summary>
public static class McpExtensions
{
    /// <summary>启用MCP。注册 <typeparamref name="TTools"/> 的工具方法到 MCP 服务器（A-40：原实现从未调用 AddTool，工具列表恒为空）</summary>
    /// <typeparam name="TTools">工具服务类型，其公共方法将作为 MCP 工具暴露</typeparam>
    /// <param name="app">路由构建器</param>
    /// <param name="pattern">MCP 端点路径模式</param>
    /// <returns>路由构建器（支持链式调用）</returns>
    public static IEndpointRouteBuilder MapMcp<TTools>(this IEndpointRouteBuilder app, String pattern) where TTools : class
    {
        var server = new AspNetMcpServer();
        server.AddTool<TTools>(server);
        app.MapPost(pattern, server.ProcessAsync);

        return app;
    }
}
