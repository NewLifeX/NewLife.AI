using System.Collections.Concurrent;
using System.Net;

namespace NewLife.AI.Clients;

/// <summary>HTTP 消息处理器池。按 Endpoint 主机分组复用 HttpMessageHandler，避免每次创建客户端实例都新建连接池导致 socket 与内存膨胀</summary>
/// <remarks>
/// 池化 HttpMessageHandler 而非 HttpClient：连接复用由 handler 内部连接池承担，HttpClient 仍按实例创建以保留各自 Timeout 等实例级配置。
/// 客户端以 disposeHandler:false 构造，Dispose 时只释放 HttpClient 对象本身，不关闭共享 handler 的连接池，调用方零改动即获得连接复用。
/// handler 超过 <see cref="HandlerLifetime"/> 后自动轮换（惰性替换），旧 handler 延迟释放，避免 DNS 变更 / 连接陈旧（借鉴 IHttpClientFactory 的 HandlerLifetime 思想）。
/// 配置热更新或测试场景可调用 <see cref="Clear"/> 清理重建。
/// </remarks>
public static class HttpClientPool
{
    /// <summary>handler 生命周期。超过后下次获取自动轮换为新 handler；0 或负值表示不轮换。默认 2 分钟（与 IHttpClientFactory 默认一致）</summary>
    public static TimeSpan HandlerLifetime { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>过期 handler 延迟释放等待时间。轮换后旧 handler 可能仍有请求在途，延迟一段时间再释放连接池</summary>
    private static readonly TimeSpan _disposeDelay = TimeSpan.FromMinutes(1);

    /// <summary>池条目。含共享 handler 及其创建时间，用于判断是否过期轮换</summary>
    private sealed class Entry
    {
        /// <summary>共享 handler</summary>
        public HttpMessageHandler Handler = null!;

        /// <summary>创建时间</summary>
        public DateTime Created = DateTime.UtcNow;
    }

    /// <summary>共享处理器缓存。键为规范化后的 Endpoint 主机（scheme://host:port）</summary>
    private static readonly ConcurrentDictionary<String, Entry> _handlers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>待释放的过期 handler 队列。延迟释放，避免销毁正在使用的连接</summary>
    private static readonly ConcurrentQueue<HttpMessageHandler> _expired = new();

    /// <summary>延迟释放定时器（一次性触发）。静态引用防止被 GC</summary>
    private static Timer? _cleanTimer;

    /// <summary>延迟释放调度锁</summary>
    private static readonly Object _sync = new();

    /// <summary>按 Endpoint 获取共享 HttpMessageHandler。同一主机在生命周期内复用同一实例；超过生命周期自动轮换为新实例，旧实例延迟释放</summary>
    /// <param name="endpoint">API 地址，如 https://api.openai.com 或 https://example.com/v1</param>
    /// <returns>共享的 HttpMessageHandler 实例</returns>
    public static HttpMessageHandler GetHandler(String? endpoint)
    {
        var key = GetKey(endpoint);
        var lifetime = HandlerLifetime;
        while (true)
        {
            var entry = _handlers.GetOrAdd(key, _ => new Entry { Handler = CreateHandler(), Created = DateTime.UtcNow });
            if (lifetime <= TimeSpan.Zero || DateTime.UtcNow - entry.Created < lifetime)
                return entry.Handler;

            // 超过生命周期：惰性替换为全新 handler，旧 handler 延迟释放后返回新实例
            var fresh = new Entry { Handler = CreateHandler(), Created = DateTime.UtcNow };
            if (_handlers.TryUpdate(key, fresh, entry))
            {
                ScheduleDispose(entry.Handler);
                return fresh.Handler;
            }
            // 并发竞争：其他线程已替换，循环重新判断
        }
    }

    /// <summary>清理全部共享处理器与待释放队列。测试或配置热更新时调用；之后再次 GetHandler 会重新创建</summary>
    public static void Clear()
    {
        foreach (var item in _handlers)
        {
            if (_handlers.TryRemove(item.Key, out var entry))
                entry.Handler.Dispose();
        }

        // 清理待释放队列中的过期 handler
        while (_expired.TryDequeue(out var handler))
        {
            try { handler.Dispose(); } catch { }
        }
    }

    /// <summary>调度过期 handler 延迟释放。等待在途请求完成后关闭连接池</summary>
    /// <param name="handler">过期 handler</param>
    private static void ScheduleDispose(HttpMessageHandler handler)
    {
        _expired.Enqueue(handler);
        lock (_sync)
        {
            if (_cleanTimer == null)
                _cleanTimer = new Timer(_ => CleanExpired(), null, Timeout.Infinite, Timeout.Infinite);

            _cleanTimer.Change(_disposeDelay, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>释放待释放队列中的过期 handler，并停用定时器</summary>
    private static void CleanExpired()
    {
        while (_expired.TryDequeue(out var handler))
        {
            try { handler.Dispose(); } catch { }
        }

        lock (_sync)
        {
            _cleanTimer?.Change(Timeout.Infinite, Timeout.Infinite);
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
