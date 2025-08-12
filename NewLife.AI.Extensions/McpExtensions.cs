using NewLife.AI.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

public static class McpExtensions
{
    /// <summary>启用MCP</summary>
    public static IEndpointRouteBuilder MapMcp<TTools>(this IEndpointRouteBuilder app, String pattern) where TTools : class
    {
        var server = new AspNetMcpServer();
        app.MapPost(pattern, server.ProcessAsync);

        return app;
    }
}
