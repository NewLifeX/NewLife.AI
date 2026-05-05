using System.Reflection;

namespace NewLife.AI.Handlers;

/// <summary>对话处理器调用链管理器。以单例形式托管 <see cref="IChatHandler"/> 列表，
/// 并根据 <see cref="ChatHandlerOrderAttribute"/> 特性构建排好序的 BeforeHandlers / AfterHandlers / Interceptors 视图。</summary>
/// <remarks>
/// <para><b>排序规则：</b></para>
/// <list type="bullet">
/// <item>标注了 <see cref="ChatHandlerOrderAttribute"/> 且对应槽值 &gt; 0 的处理器，按标注值升序排列。</item>
/// <item>未标注或槽值为 0 的处理器，按 <c>1000 + 注册索引 × 100</c> 自动赋值（注册索引为全局注册序）。</item>
/// <item>排序值相同时，按注册序稳定排列（先注册者排前）。</item>
/// </list>
/// <para><b>线程安全：</b>构建缓存后只读，<see cref="Add"/>/<see cref="Remove{T}"/>/<see cref="Replace{T}"/> 均通过锁保护，
/// 修改后自动使缓存失效，下次访问时重建。通常在应用启动时一次性构建完成，热路径不受锁影响。</para>
/// </remarks>
public class ChatHandlerChain
{
    #region 字段

    private readonly List<IChatHandler> _handlers = [];
    private readonly Object _lock = new();

    // 懒缓存：修改后置 null，下次访问时重建
    private IReadOnlyList<IChatHandler>? _beforeHandlers;
    private IReadOnlyList<IChatHandler>? _afterHandlers;
    private IReadOnlyList<IChatHandler>? _interceptors;

    #endregion

    #region 属性

    /// <summary>已按 BeforeOrder 排序的 OnBefore 处理器列表（仅含 <see cref="ChatHandlerCapabilities.Before"/> 能力）。首次访问时构建并缓存</summary>
    public IReadOnlyList<IChatHandler> BeforeHandlers => _beforeHandlers ??= BuildSorted(ChatHandlerCapabilities.Before, isBefore: true);

    /// <summary>已按 AfterOrder 排序的 OnAfter 处理器列表（仅含 <see cref="ChatHandlerCapabilities.After"/> 能力）。首次访问时构建并缓存</summary>
    public IReadOnlyList<IChatHandler> AfterHandlers => _afterHandlers ??= BuildSorted(ChatHandlerCapabilities.After, isBefore: false);

    /// <summary>已按注册序排列的 Interceptor 处理器列表（仅含 <see cref="ChatHandlerCapabilities.Interceptor"/> 能力）。首次访问时构建并缓存。
    /// 洋葱链按此列表 <b>倒序</b> 包裹，第 0 个处于最外层</summary>
    public IReadOnlyList<IChatHandler> Interceptors => _interceptors ??= BuildInterceptors();

    /// <summary>已注册的处理器总数</summary>
    public Int32 Count
    {
        get { lock (_lock) return _handlers.Count; }
    }

    #endregion

    #region 构造

    /// <summary>创建空链</summary>
    public ChatHandlerChain() { }

    /// <summary>从处理器集合创建链（通常由 DI 工厂调用）</summary>
    /// <param name="handlers">处理器集合，按注册顺序传入</param>
    public ChatHandlerChain(IEnumerable<IChatHandler> handlers)
    {
        foreach (var h in handlers)
            _handlers.Add(h);
    }

    #endregion

    #region 修改方法

    /// <summary>追加一个处理器到链末尾</summary>
    /// <param name="handler">处理器实例</param>
    public void Add(IChatHandler handler)
    {
        lock (_lock)
        {
            _handlers.Add(handler);
            Invalidate();
        }
    }

    /// <summary>移除链中所有 <typeparamref name="THandler"/> 类型的处理器</summary>
    /// <typeparam name="THandler">要移除的处理器类型</typeparam>
    public void Remove<THandler>() where THandler : IChatHandler
    {
        lock (_lock)
        {
            var removed = false;
            for (var i = _handlers.Count - 1; i >= 0; i--)
            {
                if (_handlers[i] is THandler)
                {
                    _handlers.RemoveAt(i);
                    removed = true;
                }
            }
            if (removed) Invalidate();
        }
    }

    /// <summary>将链中第一个 <typeparamref name="TOld"/> 类型的处理器替换为 <paramref name="newHandler"/>。
    /// 若链中不存在 <typeparamref name="TOld"/>，则将 <paramref name="newHandler"/> 追加到末尾</summary>
    /// <typeparam name="TOld">要替换的处理器类型</typeparam>
    /// <param name="newHandler">新处理器实例</param>
    public void Replace<TOld>(IChatHandler newHandler) where TOld : IChatHandler
    {
        lock (_lock)
        {
            for (var i = 0; i < _handlers.Count; i++)
            {
                if (_handlers[i] is TOld)
                {
                    _handlers[i] = newHandler;
                    Invalidate();
                    return;
                }
            }
            // 未找到则追加
            _handlers.Add(newHandler);
            Invalidate();
        }
    }

    #endregion

    #region 内部构建

    /// <summary>使三个缓存视图全部失效，下次访问时重建</summary>
    private void Invalidate()
    {
        _beforeHandlers = null;
        _afterHandlers = null;
        _interceptors = null;
    }

    /// <summary>按能力过滤并排序，返回只读列表</summary>
    /// <param name="capability">目标能力标志（Before 或 After）</param>
    /// <param name="isBefore">true = 读取 Before 槽值；false = 读取 After 槽值</param>
    /// <returns>已排序的处理器只读列表</returns>
    private IReadOnlyList<IChatHandler> BuildSorted(ChatHandlerCapabilities capability, Boolean isBefore)
    {
        List<IChatHandler> snapshot;
        lock (_lock) snapshot = [.. _handlers];

        if (snapshot.Count == 0) return [];

        // 为每个处理器计算有效排序值
        var indexed = new List<(IChatHandler Handler, Int32 EffOrder, Int32 RegIdx)>(snapshot.Count);
        for (var i = 0; i < snapshot.Count; i++)
        {
            var h = snapshot[i];
            if (!h.Capabilities.HasFlag(capability)) continue;

            var raw = ReadOrder(h, isBefore);
            var eff = raw > 0 ? raw : 1000 + i * 100;
            indexed.Add((h, eff, i));
        }

        // 按有效排序值升序，值相同时按注册序稳定排列
        indexed.Sort(static (a, b) =>
        {
            var cmp = a.EffOrder.CompareTo(b.EffOrder);
            return cmp != 0 ? cmp : a.RegIdx.CompareTo(b.RegIdx);
        });

        return indexed.Select(static x => x.Handler).ToList().AsReadOnly();
    }

    /// <summary>按 Interceptor 能力过滤，保持注册顺序（洋葱链由调用方倒序包裹）</summary>
    /// <returns>已过滤的处理器只读列表</returns>
    private IReadOnlyList<IChatHandler> BuildInterceptors()
    {
        List<IChatHandler> snapshot;
        lock (_lock) snapshot = [.. _handlers];

        return snapshot.Where(h => h.Capabilities.HasFlag(ChatHandlerCapabilities.Interceptor)).ToList().AsReadOnly();
    }

    /// <summary>从处理器类型的 <see cref="ChatHandlerOrderAttribute"/> 读取排序值</summary>
    /// <param name="handler">处理器实例</param>
    /// <param name="isBefore">true = 读 Before 值；false = 读 After 值</param>
    /// <returns>原始排序值；未标注时返回 0</returns>
    private static Int32 ReadOrder(IChatHandler handler, Boolean isBefore)
    {
        var attr = handler.GetType().GetCustomAttribute<ChatHandlerOrderAttribute>(inherit: true);
        if (attr == null) return 0;
        return isBefore ? attr.Before : attr.After;
    }

    #endregion
}
