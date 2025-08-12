using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using NewLife.AI.ModelContextProtocol;

namespace NewLife.AI.Extensions;

/// <summary>AspNet托管MCP上下文</summary>
public class AspNetMcpServer : McpServer
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    #region 方法
    /// <summary>处理MCP请求</summary>
    public async Task ProcessAsync(HttpContext context, [FromServices] IServiceProvider serviceProvider)
    {
        var request = await JsonSerializer.DeserializeAsync<JsonRpcRequest>(context.Request.Body, _jsonOptions);
        if (request == null)
        {
            context.Response.StatusCode = 400; // Bad Request
            return;
        }

        var ctx = new McpContext
        {
            HostContext = context,
            Services = serviceProvider,
            GetRequest = key => context.Request.Headers.TryGetValue(key, out var value) ? value.ToString() : null,
            SetResponse = (key, value) => context.Response.Headers[key] = value,
        };
        var rs = Process(request, ctx);
        if (rs != null) await WriteSseMessageAsync(context, rs);
    }

    /// <summary>输出SSE消息</summary>
    /// <param name="data"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    private async Task WriteSseMessageAsync(HttpContext context, Object data)
    {
        var response = context.Response;
        if (!response.Headers.ContainsKey("Content-Type"))
        {
            response.ContentType = "text/event-stream";
            response.Headers.CacheControl = "no-cache,no-store";
            response.Headers.ContentEncoding = "identity";
            response.Headers.KeepAlive = "true";
        }

        var json = JsonSerializer.Serialize(data, _jsonOptions);
        var message = $"event: message\ndata: {json}\n\n";
        await response.WriteAsync(message);
        await response.Body.FlushAsync();
    }
    #endregion
}
