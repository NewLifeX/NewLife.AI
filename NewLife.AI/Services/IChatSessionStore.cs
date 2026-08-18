using NewLife.AI.Models;
using NewLife.Caching;

namespace NewLife.AI.Services;

/// <summary>会话历史存储接口。轻量对话服务的会话历史可插拔存储，默认实现 <see cref="CacheSessionStore"/>（基于缓存提供者）；
/// 宿主可注入 MemoryCache / Redis 等带过期能力的缓存存储</summary>
public interface IChatSessionStore
{
    /// <summary>读取会话历史。会话不存在时返回 null</summary>
    /// <param name="sessionId">会话编号</param>
    /// <returns>历史消息列表，不存在返回 null</returns>
    IList<ChatMessage>? Get(String sessionId);

    /// <summary>保存会话历史（覆盖写）。调用方保证消息列表已裁剪到上限</summary>
    /// <param name="sessionId">会话编号</param>
    /// <param name="messages">历史消息列表</param>
    void Set(String sessionId, IList<ChatMessage> messages);
}

/// <summary>会话历史存储默认实现。基于缓存提供者 <see cref="ICacheProvider"/> 的 <see cref="ICache"/>，1 小时过期自动清理，防止会话长期驻留内存</summary>
/// <remarks>
/// 单机部署默认使用 <see cref="CacheProvider"/>（内存缓存）；分布式部署注入 Redis 缓存提供者（NewLife.Redis 的 RedisCacheProvider）后
/// 自动切换为 Redis，会话历史跨节点共享，无需改动调用方代码。
/// </remarks>
/// <remarks>实例化会话历史缓存存储</remarks>
/// <param name="cacheProvider">缓存提供者；为 null 时使用全局默认内存缓存 <see cref="MemoryCache.Instance"/></param>
public class CacheSessionStore(ICacheProvider? cacheProvider = null) : IChatSessionStore
{
    /// <summary>会话历史过期秒数。1 小时无访问自动清理，防止会话长期驻留内存</summary>
    private const Int32 ExpireSeconds = 3600;

    private readonly ICache _cache = cacheProvider?.Cache ?? MemoryCache.Instance;

    /// <summary>读取会话历史。会话不存在时返回 null</summary>
    /// <param name="sessionId">会话编号</param>
    /// <returns>历史消息列表，不存在返回 null</returns>
    public IList<ChatMessage>? Get(String sessionId) => _cache.Get<IList<ChatMessage>>(sessionId);

    /// <summary>保存会话历史（覆盖写，1 小时过期）。调用方保证消息列表已裁剪到上限</summary>
    /// <param name="sessionId">会话编号</param>
    /// <param name="messages">历史消息列表</param>
    public void Set(String sessionId, IList<ChatMessage> messages) => _cache.Set(sessionId, messages, ExpireSeconds);
}
