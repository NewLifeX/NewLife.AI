using System.Collections.Concurrent;
using NewLife.AI.Models;

namespace NewLife.AI.Services;

/// <summary>轻量对话会话历史管理。按会话编号维护消息历史，支持上限裁剪与可插拔存储</summary>
/// <remarks>
/// 默认内存实现（<see cref="ConcurrentDictionary{TKey, TValue}"/>）；注入 <see cref="IChatSessionStore"/>
/// 后读写委托给外部存储（如 MemoryCache / Redis），内存模式与存储模式行为一致。
/// 单会话历史上限由 <see cref="MaxHistory"/> 控制，超出时裁剪最早的记录。
/// </remarks>
/// <remarks>实例化会话历史服务</remarks>
/// <param name="store">可选外部存储；为 null 时使用进程内内存</param>
public class ChatSessionService(IChatSessionStore? store = null)
{
    #region 属性
    /// <summary>默认历史消息上限</summary>
    public const Int32 DefaultMaxHistory = 30;

    /// <summary>会话历史上限。超过时裁剪最早的记录</summary>
    public Int32 MaxHistory { get; set; } = DefaultMaxHistory;

    private readonly ConcurrentDictionary<String, IList<ChatMessage>> _sessions = new(StringComparer.OrdinalIgnoreCase);
    #endregion

    #region 方法
    /// <summary>读取会话历史。会话不存在或编号为空时返回 null</summary>
    /// <param name="sessionId">会话编号</param>
    /// <returns>历史消息列表（只读语义，修改请用 <see cref="Append"/>）；不存在返回 null</returns>
    public IList<ChatMessage>? GetHistory(String sessionId)
    {
        if (sessionId.IsNullOrEmpty()) return null;

        if (store != null) return store.Get(sessionId);

        return _sessions.TryGetValue(sessionId, out var list) ? list : null;
    }

    /// <summary>获取会话历史，不存在时创建空列表并初始化</summary>
    /// <param name="sessionId">会话编号</param>
    /// <returns>历史消息列表（实时列表，追加消息请用 <see cref="Append"/>）</returns>
    public IList<ChatMessage> GetOrCreate(String sessionId)
    {
        if (sessionId.IsNullOrEmpty()) throw new ArgumentNullException(nameof(sessionId));

        var list = GetHistory(sessionId);
        if (list != null) return list;

        list = [];
        Set(sessionId, list);
        return list;
    }

    /// <summary>追加一条消息到会话历史，自动裁剪到 <see cref="MaxHistory"/> 上限</summary>
    /// <param name="sessionId">会话编号</param>
    /// <param name="message">消息</param>
    public void Append(String sessionId, ChatMessage message)
    {
        if (sessionId.IsNullOrEmpty()) throw new ArgumentNullException(nameof(sessionId));
        if (message == null) throw new ArgumentNullException(nameof(message));

        var history = GetOrCreate(sessionId);
        history.Add(message);

        // 裁剪到上限：移除最早的多余记录（IList 无 RemoveRange，用循环 RemoveAt）
        var overflow = history.Count - MaxHistory;
        for (var i = 0; i < overflow; i++) history.RemoveAt(0);

        Set(sessionId, history);
    }

    /// <summary>清除会话历史</summary>
    /// <param name="sessionId">会话编号</param>
    public void Clear(String sessionId)
    {
        if (sessionId.IsNullOrEmpty()) return;

        store?.Set(sessionId, []);
        _sessions.TryRemove(sessionId, out _);
    }
    #endregion

    #region 辅助
    /// <summary>写入会话历史。存储模式委托外部存储，内存模式写入字典</summary>
    private void Set(String sessionId, IList<ChatMessage> messages)
    {
        if (store != null)
            store.Set(sessionId, messages);
        else
            _sessions[sessionId] = messages;
    }
    #endregion
}
