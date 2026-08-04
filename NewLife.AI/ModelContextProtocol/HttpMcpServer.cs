using System.Net;
using NewLife;
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

    /// <summary>处理MCP请求</summary>
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

        var request = context.Request.Body?.ToStr().ToJsonEntity<JsonRpcRequest>();
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
        if (rs != null) WriteSseMessage(context, rs);
    }

    /// <summary>输出SSE消息</summary>
    /// <param name="data"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    private void WriteSseMessage(IHttpContext context, Object data)
    {
        var response = context.Response;
        if (!response.Headers.ContainsKey("Content-Type"))
        {
            response.ContentType = "text/event-stream";
            response.Headers["CacheControl"] = "no-cache,no-store";
            response.Headers["ContentEncoding"] = "identity";
            response.Headers["KeepAlive"] = "true";
        }

        var json = data.ToJson();
        var message = $"event: message\ndata: {json}\n\n";

        // A-35：Connection 在不同传输上下文可能为 null，判空避免 NRE
        if (context.Connection == null)
        {
            XTrace.WriteLine("[HttpMcpServer] 连接为空，无法发送 MCP 响应");
            return;
        }

        using var rs = response.Build();
        context.Connection.Send(rs);
    }
    #endregion
}
