using System.Net;
using NewLife;
using NewLife.Collections;
using NewLife.Data;
using NewLife.Http;
using NewLife.Log;
using NewLife.Serialization;

namespace NewLife.AI.ModelContextProtocol;

/// <summary>Http托管MCP服务器</summary>
public class HttpMcpServer : McpServer
{
    #region 属性
    /// <summary>端口</summary>
    public Int32 Port { get; set; } = 8080;

    /// <summary>Http服务器</summary>
    public HttpServer Server { get; set; } = null!;

    /// <summary>响应格式。Auto 表示按请求 Accept 头协商，Json 强制 JSON，Sse 强制 SSE</summary>
    public McpResponseFormat ResponseFormat { get; set; } = McpResponseFormat.Auto;
    #endregion

    #region 方法
    /// <summary>启动MCP服务器</summary>
    public void Start()
    {
        var server = Server;
        server ??= new HttpServer()
        {
            Port = Port,

            Log = Log,
            Tracer = Tracer,
        };

        server.ServiceProvider = this;
        server.Log ??= Log;
        server.Tracer ??= Tracer;

        server.Map("/", ProcessRequest);
        server.Start();

        // 端口 0 时由系统分配，同步回实际端口
        Port = server.Port;

        Server = server;
    }

    /// <summary>释放资源。释放内部 HttpServer</summary>
    public override void Dispose()
    {
        base.Dispose();

        Server.TryDispose();
        Server = null!;
    }

    /// <summary>处理MCP请求</summary>
    /// <param name="context">HTTP 上下文</param>
    public void ProcessRequest(IHttpContext context)
    {
        // A-36：限制请求体大小，防止超大 body 打爆内存（DoS）。MCP 请求 JSON 通常 < 1MB
        const Int32 maxBodyBytes = 1024 * 1024;
        var body = context.Request.Body;
        if (body != null && body.Length > maxBodyBytes)
        {
            context.Response.StatusCode = HttpStatusCode.RequestEntityTooLarge;
            return;
        }

        // Streamable HTTP：客户端向端点发 POST；GET 用于 legacy SSE 事件流（独立服务器暂不提供长连接）
        if (!context.Request.Method.EqualIgnoreCase("POST"))
        {
            context.Response.StatusCode = HttpStatusCode.MethodNotAllowed;
            return;
        }

        var request = DecodeBody(context)?.ToJsonEntity<JsonRpcRequest>();
        if (request == null)
        {
            context.Response.StatusCode = HttpStatusCode.BadRequest;
            return;
        }

        var ctx = new McpContext
        {
            HostContext = context,
            Services = this,
            GetRequest = key => context.Request.Headers.TryGetValue(key, out var value) ? value.ToString() : null,
            SetResponse = (key, value) => context.Response.Headers[key] = value,
        };
        var rs = Process(request, ctx);
        if (rs == null) return;

        // 内容协商：显式指定格式优先；否则客户端 Accept 不接受 text/event-stream 时用 JSON，默认 SSE（对齐 Streamable HTTP）
        if (ResponseFormat == McpResponseFormat.Json)
            WriteJsonMessage(context, rs);
        else if (ResponseFormat == McpResponseFormat.Sse)
            WriteSseMessage(context, rs);
        else if (context.Request.Headers.TryGetValue("Accept", out var accept) && !accept.IsNullOrEmpty() && !accept.Contains("text/event-stream"))
            WriteJsonMessage(context, rs);
        else
            WriteSseMessage(context, rs);
    }

    /// <summary>读取并解码请求体文本。兼容 Transfer-Encoding: chunked（官方 MCP 客户端 JsonContent 默认 chunked）</summary>
    /// <remarks>
    /// NewLife.Http 的 HttpBase.Parse 只认 Content-Length，不解析 chunked，
    /// 导致 Body 保留原始块格式（形如 "2e\r\n{json}\r\n0\r\n\r\n"），JSON 解析必然失败。
    /// 这里手动解码作为过渡规避；根治需上游 NewLife.Core 的 HttpBase/HttpSession 支持 chunked。
    /// 注意：仅对单包完整到达的请求可靠，跨 TCP 段的超大请求体仍可能丢失，超大 MCP 消息请用 Kestrel 宿主。
    /// </remarks>
    /// <param name="context">HTTP 上下文</param>
    /// <returns>解码后的请求体文本；无主体或解码失败时返回 null</returns>
    private static String? DecodeBody(IHttpContext context)
    {
        var body = context.Request.Body;
        if (body == null) return null;

        var text = body.ToStr();
        if (text.IsNullOrEmpty()) return null;

        // 非 chunked，直接返回
        if (!context.Request.Headers.TryGetValue("Transfer-Encoding", out var te) || !te.EqualIgnoreCase("chunked"))
            return text;

        // chunked 原始格式：块大小行\r\n数据\r\n……0\r\n\r\n
        var sb = Pool.StringBuilder.Get();
        var p = 0;
        while (p < text.Length)
        {
            var eol = text.IndexOf('\n', p);
            if (eol < 0) break;

            var sizeLine = text[p..eol].TrimEnd('\r').Trim();
            if (sizeLine.IsNullOrEmpty())
            {
                p = eol + 1;
                continue;
            }

            // 块大小可能带扩展：1a;ext=1
            var semi = sizeLine.IndexOf(';');
            if (semi > 0) sizeLine = sizeLine[..semi];

            // chunked 块大小为十六进制
            var size = -1;
            try { size = Convert.ToInt32(sizeLine, 16); }
            catch { return null; }
            if (size < 0) return null; // 非法块大小

            if (size == 0) break; // 结束块

            var start = eol + 1;
            if (start + size > text.Length) return null; // 数据不完整
            sb.Append(text, start, size);

            p = start + size + 2; // 跳过数据后的 CRLF
        }

        return sb.Return(true);
    }

    /// <summary>输出SSE消息</summary>
    /// <param name="data">响应数据</param>
    /// <param name="context">HTTP 上下文</param>
    private void WriteSseMessage(IHttpContext context, Object data)    {
        var response = context.Response;
        if (!response.Headers.ContainsKey("Content-Type"))
        {
            response.ContentType = "text/event-stream";
            response.Headers["CacheControl"] = "no-cache,no-store";
            response.Headers["ContentEncoding"] = "identity";
            response.Headers["KeepAlive"] = "true";
        }

        var json = data.ToJson(false, true, true);
        var message = $"event: message\ndata: {json}\n\n";

        // A-35：Connection 在不同传输上下文可能为 null，判空避免 NRE
        if (context.Connection == null)
        {
            XTrace.WriteLine("[HttpMcpServer] 连接为空，无法发送 MCP 响应");
            return;
        }

        response.SetResult(message, "text/event-stream");
        using var rs = response.Build();
        context.Connection.Send(rs);
    }

    /// <summary>输出JSON消息（Streamable HTTP 的 JSON 响应模式）</summary>
    /// <param name="data">响应数据</param>
    /// <param name="context">HTTP 上下文</param>
    private void WriteJsonMessage(IHttpContext context, Object data)
    {
        var response = context.Response;
        if (!response.Headers.ContainsKey("Content-Type"))
        {
            response.ContentType = "application/json";
            response.Headers["CacheControl"] = "no-cache,no-store";
        }

        // A-35：Connection 在不同传输上下文可能为 null，判空避免 NRE
        if (context.Connection == null)
        {
            XTrace.WriteLine("[HttpMcpServer] 连接为空，无法发送 MCP 响应");
            return;
        }

        response.SetResult(data.ToJson(false, true, true), "application/json");
        using var rs = response.Build();
        context.Connection.Send(rs);
    }
    #endregion
}

/// <summary>MCP HTTP 响应格式</summary>
public enum McpResponseFormat
{
    /// <summary>按请求 Accept 头自动协商（默认）</summary>
    Auto = 0,

    /// <summary>强制 JSON 响应</summary>
    Json = 1,

    /// <summary>强制 SSE 响应</summary>
    Sse = 2,
}
