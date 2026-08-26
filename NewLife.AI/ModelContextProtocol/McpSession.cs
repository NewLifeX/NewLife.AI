using NewLife.Data;
using NewLife.Messaging;
using NewLife.Remoting;

namespace NewLife.AI.ModelContextProtocol;

/// <summary>MCP 会话。为 ApiHandler 提供非网络会话上下文（工具调用参数绑定需要非空 IApiSession）</summary>
/// <remarks>
/// MCP 是请求-响应式协议，无长连接会话。此类为每次工具调用提供最小会话实现，
/// 使 <see cref="ApiHandler.Prepare"/> 能完成参数绑定（此前传 null 导致 Prepare 首行 NRE，工具调用必然失败）。
/// </remarks>
internal class McpSession : IApiSession
{
    #region 属性
    /// <summary>主机</summary>
    public IApiHost Host { get; }

    /// <summary>最后活跃时间</summary>
    public DateTime LastActive { get; set; } = DateTime.Now;

    /// <summary>所有服务器所有会话，包含自己</summary>
    public IApiSession[] AllSessions => [this];

    /// <summary>令牌。取自 MCP 会话头 Mcp-Session-Id</summary>
    public String? Token { get; set; }

    private readonly Dictionary<String, Object?> _items = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>扩展数据项字典</summary>
    public IDictionary<String, Object?> Items => _items;

    /// <summary>获取/设置 会话数据</summary>
    /// <param name="key">键</param>
    /// <returns>值</returns>
    public Object? this[String key]
    {
        get => _items.TryGetValue(key, out var value) ? value : null;
        set => _items[key] = value;
    }
    #endregion

    #region 构造
    /// <summary>实例化</summary>
    /// <param name="host">MCP 服务器（ApiHost）</param>
    /// <param name="sessionId">会话编号，取自 Mcp-Session-Id 请求头</param>
    public McpSession(IApiHost host, String? sessionId = null)
    {
        Host = host;
        Token = sessionId;

        // Prepare 从会话解析 Encoder，无则回退 Host.Encoder；预置可省一次反射回退
        this["Encoder"] = host.Encoder;
    }
    #endregion

    #region 方法
    /// <summary>单向远程调用。MCP 无此场景，直接返回 0</summary>
    /// <param name="action">服务操作</param>
    /// <param name="args">参数</param>
    /// <param name="flag">标识</param>
    /// <returns>固定 0</returns>
    public Int32 InvokeOneWay(String action, Object? args = null, Byte flag = 0) => 0;

    /// <summary>发送消息。MCP 无长连接推送，直接返回 0</summary>
    /// <param name="msg">消息</param>
    /// <returns>固定 0</returns>
    public Int32 SendMessage(IMessage msg) => 0;
    #endregion
}
