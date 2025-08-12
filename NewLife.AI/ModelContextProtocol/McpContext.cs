namespace NewLife.AI.ModelContextProtocol;

/// <summary>MCP上下文</summary>
public class McpContext
{
    /// <summary>主机上下文</summary>
    public Object? HostContext { get; set; }

    /// <summary>服务提供者</summary>
    public IServiceProvider Services { get; set; } = null!;

    //public IDictionary<String, String> RequestHeaders { get; set; }

    //public IDictionary<String, String> ResponseHeaders { get; set; }

    /// <summary>获取请求</summary>
    public Func<String, String?> GetRequest { get; set; } = null!;

    /// <summary>设置响应</summary>
    public Action<String, String> SetResponse { get; set; } = null!;
}
