using System.Collections.Concurrent;
using System.Net;

namespace NewLife.AI.Clients;

/// <summary>HTTP 消息处理器池。按 Endpoint 主机分组复用 HttpMessageHandler，避免每次创建客户端实例都新建连接池导致 socket 与内存膨胀</summary>
/// <remarks>
/// 池化 HttpMessageHandler 而非 HttpClient：连接复用由 handler 内部连接池承担，HttpClient 仍按实例创建以保留各自 Timeout 等实例级配置。
/// 客户端以 disposeHandler:false 构造，Dispose 时只释放 HttpClient 对象本身，不关闭共享 handler 的连接池，调用方零改动即获得连接复用。
/// handler 为进程级生命周期，配置热更新或测试场景可调用 <see cref="Clear"/> 清理重建。
/// </remarks>
public static class HttpClientPool
{
    /// <summary>共享处理器缓存。键为规范化后的 Endpoint 主机（scheme://host:port）</summary>
    private static readonly ConcurrentDictionary<String, HttpMessageHandler> _handlers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>按 Endpoint 获取共享 HttpMessageHandler。同一主机复用同一实例，内部连接池共享；不同主机各自独立</summary>
    /// <param name="endpoint">API 地址，如 https://api.openai.com 或 https://example.com/v1</param>
    /// <returns>共享的 HttpMessageHandler 实例</returns>
    public static HttpMessageHandler GetHandler(String? endpoint)
    {
        var key = GetKey(endpoint);
        return _handlers.GetOrAdd(key, _ => CreateHandler());
    }

    /// <summary>清理全部共享处理器。测试或配置热更新时调用；之后再次 GetHandler 会重新创建</summary>
    public static void Clear()
    {
        foreach (var item in _handlers)
        {
            if (_handlers.TryRemove(item.Key, out var handler))
                handler.Dispose();
        }
    }

    /// <summary>创建默认 HttpMessageHandler（自动 GZip/Deflate 解压）</summary>
    /// <returns>新的 HttpMessageHandler 实例</returns>
    private static HttpMessageHandler CreateHandler() => new HttpClientHandler
    {
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
    };

    /// <summary>规范化 Endpoint 为池键。提取 scheme://host:port 忽略路径（同主机不同路径共享连接池）；无法解析时返回原始字符串</summary>
    /// <param name="endpoint">API 地址</param>
    /// <returns>池键</returns>
    private static String GetKey(String? endpoint)
    {
        if (String.IsNullOrWhiteSpace(endpoint)) return "";
        if (Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) && !String.IsNullOrEmpty(uri.Host))
            return uri.GetLeftPart(UriPartial.Authority);

        return endpoint!;
    }
}
