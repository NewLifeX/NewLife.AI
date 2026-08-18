using NewLife.AI.Models;
using NewLife.Caching;

namespace NewLife.AI.Services;

/// <summary>会话历史存储接口。轻量对话服务的会话历史可插拔存储，默认内存实现；
/// 宿主可注入 MemoryCache / Redis / 数据库等带过期能力的存储</summary>
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

/// <summary>会话历史存储默认实现。基于 <see cref="MemoryCache"/>，1 小时过期自动清理，防止会话长期驻留内存</summary>
public class MemoryCacheSessionStore : IChatSessionStore
{
    /// <summary>会话历史过期秒数。1 小时无访问自动清理，防止会话长期驻留内存</summary>
    private const Int32 ExpireSeconds = 3600;

    /// <summary>读取会话历史。会话不存在时返回 null</summary>
    /// <param name="sessionId">会话编号</param>
    /// <returns>历史消息列表，不存在返回 null</returns>
    public IList<ChatMessage>? Get(String sessionId) => MemoryCache.Instance.Get<IList<ChatMessage>>(sessionId);

    /// <summary>保存会话历史（覆盖写，1 小时过期）。调用方保证消息列表已裁剪到上限</summary>
    /// <param name="sessionId">会话编号</param>
    /// <param name="messages">历史消息列表</param>
    public void Set(String sessionId, IList<ChatMessage> messages) => MemoryCache.Instance.Set(sessionId, messages, ExpireSeconds);
}
