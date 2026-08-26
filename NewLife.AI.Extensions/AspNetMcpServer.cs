using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Microsoft.AspNetCore.Mvc;
using NewLife.AI.ModelContextProtocol;
using NewLife.Security;
using NewLife.Serialization;

namespace NewLife.AI.Extensions;

/// <summary>AspNet托管MCP上下文。完整 Streamable HTTP（JSON/SSE 内容协商）+ 可选 legacy SSE + 可选 Bearer 认证</summary>
/// <remarks>
/// 通过 <see cref="McpExtensions.MapMcp{T}"/> 注册到 ASP.NET Core 管线。默认 Streamable HTTP（POST 单端点，
/// 按 Accept 头返回 application/json 或 text/event-stream）；配置 <see cref="EnableLegacySse"/> 后额外提供
/// GET /sse + POST /message 旧式端点。配置 <see cref="AuthToken"/>（或 appsettings 的 Mcp:AuthToken）后所有端点
/// 需携带 Authorization: Bearer 令牌。
/// </remarks>
public class AspNetMcpServer : McpServer
{
    /// <summary>MCP 请求反序列化选项。camelCase + 大小写不敏感，兼容规范字段 jsonrpc（全小写）</summary>
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerOptions.Default)
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    #region 属性
    /// <summary>响应格式。Auto 按 Accept 头协商，Json 强制 JSON，Sse 强制 SSE</summary>
    public McpResponseFormat ResponseFormat { get; set; } = McpResponseFormat.Auto;

    /// <summary>启用 legacy SSE 端点（GET /sse + POST /message）。默认 false，Streamable HTTP 客户端无需启用</summary>
    public Boolean EnableLegacySse { get; set; }

    /// <summary>可选 Bearer 令牌。配置后所有 MCP 请求需带 Authorization: Bearer &lt;token&gt;；未配置则开放</summary>
    public String? AuthToken { get; set; }

    /// <summary>legacy SSE 会话队列。sessionId → 待推送消息通道</summary>
    private readonly ConcurrentDictionary<String, Channel<String>> _sseQueues = new();

    /// <summary>请求体大小上限，防止超大 body 打爆内存（DoS，对齐 A-36）。MCP 请求 JSON 通常 &lt; 1MB</summary>
    private const Int32 MaxBodyBytes = 1024 * 1024;
    #endregion

    #region Streamable HTTP 主端点
    /// <summary>处理MCP请求（Streamable HTTP 主端点，POST）</summary>
    /// <param name="context">HTTP 上下文</param>
    /// <param name="serviceProvider">DI 服务提供者，用于解析工具实例</param>
    public async Task ProcessAsync(HttpContext context, [FromServices] IServiceProvider serviceProvider)
    {
        if (context.Request.ContentLength > MaxBodyBytes)
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            return;
        }
        if (!CheckAuth(context)) return;

        var request = await JsonSerializer.DeserializeAsync<JsonRpcRequest>(context.Request.Body, _jsonOptions);
        if (request == null)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var ctx = CreateContext(context, serviceProvider);
        var rs = Process(request, ctx);
        if (rs == null) return;

        // 内容协商：显式指定格式优先；否则 Accept 不接受 text/event-stream 时用 JSON，默认 SSE（对齐 Streamable HTTP）
        if (ResponseFormat == McpResponseFormat.Json)
            await WriteJsonAsync(context, rs);
        else if (ResponseFormat == McpResponseFormat.Sse)
            await WriteSseAsync(context, rs);
        else if (context.Request.Headers.Accept.ToString().Contains("text/event-stream", StringComparison.OrdinalIgnoreCase))
            await WriteSseAsync(context, rs);
        else
            await WriteJsonAsync(context, rs);
    }
    #endregion

    #region legacy SSE 端点
    /// <summary>legacy SSE 事件流端点（GET /sse）。客户端建立长连接，接收服务器推送消息</summary>
    /// <param name="context">HTTP 上下文</param>
    /// <param name="serviceProvider">DI 服务提供者</param>
    public async Task ProcessSseAsync(HttpContext context, [FromServices] IServiceProvider serviceProvider)
    {
        if (!CheckAuth(context)) return;

        var sessionId = context.Request.Query["sessionId"].ToString();
        if (sessionId.IsNullOrEmpty())
        {
            sessionId = Rand.NextString(16);
            context.Response.Headers["Mcp-Session-Id"] = sessionId;
        }

        var queue = _sseQueues.GetOrAdd(sessionId, _ => Channel.CreateUnbounded<String>());

        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache,no-store";

        // legacy SSE 规范：先推送 endpoint 事件，告知客户端消息提交端点
        var path = context.Request.Path.ToString().TrimEnd('/');
        var endpointUrl = $"{context.Request.Scheme}://{context.Request.Host}{context.Request.PathBase}{path}/message?sessionId={sessionId}";
        await context.Response.WriteAsync($"event: endpoint\ndata: {endpointUrl}\n\n");
        await context.Response.Body.FlushAsync();

        try
        {
            await foreach (var msg in queue.Reader.ReadAllAsync(context.RequestAborted))
            {
                await context.Response.WriteAsync(msg);
                await context.Response.Body.FlushAsync();
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            _sseQueues.TryRemove(sessionId, out _);
        }
    }

    /// <summary>legacy SSE 消息端点（POST /message）。处理请求并把响应推送到对应会话的事件流</summary>
    /// <param name="context">HTTP 上下文</param>
    /// <param name="serviceProvider">DI 服务提供者</param>
    public async Task ProcessMessageAsync(HttpContext context, [FromServices] IServiceProvider serviceProvider)
    {
        if (context.Request.ContentLength > MaxBodyBytes)
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            return;
        }
        if (!CheckAuth(context)) return;

        var request = await JsonSerializer.DeserializeAsync<JsonRpcRequest>(context.Request.Body, _jsonOptions);
        if (request == null)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var sessionId = context.Request.Query["sessionId"].ToString();
        if (sessionId.IsNullOrEmpty()) sessionId = context.Request.Headers["Mcp-Session-Id"].ToString();

        var ctx = CreateContext(context, serviceProvider);
        var rs = Process(request, ctx);
        if (rs != null)
        {
            var queue = _sseQueues.GetOrAdd(sessionId, _ => Channel.CreateUnbounded<String>());
            var json = rs.ToJson(false, true, true);
            await queue.Writer.WriteAsync($"event: message\ndata: {json}\n\n");
        }

        // legacy SSE：POST 立即返回 202，响应经 GET 事件流推送
        context.Response.StatusCode = StatusCodes.Status202Accepted;
    }
    #endregion

    #region 辅助
    /// <summary>创建 MCP 上下文</summary>
    /// <param name="context">HTTP 上下文</param>
    /// <param name="serviceProvider">DI 服务提供者</param>
    /// <returns>MCP 上下文</returns>
    private static McpContext CreateContext(HttpContext context, IServiceProvider serviceProvider) => new()
    {
        HostContext = context,
        Services = serviceProvider,
        GetRequest = key => context.Request.Headers[key].ToString(),
        SetResponse = (key, value) => context.Response.Headers[key] = value,
    };

    /// <summary>校验 Bearer 令牌。未配置 AuthToken 时直接放行</summary>
    /// <param name="context">HTTP 上下文</param>
    /// <returns>true 表示通过；false 表示已写 401 响应</returns>
    private Boolean CheckAuth(HttpContext context)
    {
        if (AuthToken.IsNullOrEmpty()) return true;

        var auth = context.Request.Headers.Authorization.ToString();
        if (auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) &&
            auth.AsSpan(7).Trim().SequenceEqual(AuthToken))
            return true;

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers.WWWAuthenticate = "Bearer";
        return false;
    }

    /// <summary>输出SSE消息（Streamable HTTP / legacy SSE 通用格式）</summary>
    /// <param name="context">HTTP 上下文</param>
    /// <param name="data">响应数据</param>
    private static async Task WriteSseAsync(HttpContext context, Object data)
    {
        var response = context.Response;
        if (!response.HasStarted)
        {
            response.ContentType = "text/event-stream";
            response.Headers.CacheControl = "no-cache,no-store";
            response.Headers.ContentEncoding = "identity";
            response.Headers.KeepAlive = "true";
        }

        var json = data.ToJson(false, true, true);
        var message = $"event: message\ndata: {json}\n\n";
        await response.WriteAsync(message);
        await response.Body.FlushAsync();
    }

    /// <summary>输出JSON消息（Streamable HTTP 的 JSON 响应模式）</summary>
    /// <param name="context">HTTP 上下文</param>
    /// <param name="data">响应数据</param>
    private static async Task WriteJsonAsync(HttpContext context, Object data)
    {
        var response = context.Response;
        if (!response.HasStarted)
        {
            response.ContentType = "application/json";
            response.Headers.CacheControl = "no-cache,no-store";
        }

        await response.WriteAsync(data.ToJson(false, true, true));
    }
    #endregion
}

