using System.Net;
using NewLife;
using NewLife.Data;
using NewLife.Http;
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
        Server ??= new HttpServer()
        {
            Port = Port,
            ServiceProvider = this,

            Log = Log,
            Tracer = Tracer,
        };
        Server.Map("/", Process);
        Server.Start();
    }

    /// <summary>处理MCP请求</summary>
    public void Process(IHttpContext context)
    {
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

        using var rs = response.Build();
        context.Connection!.Send(rs);
    }
    #endregion
}
