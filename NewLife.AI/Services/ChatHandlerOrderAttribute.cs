namespace NewLife.AI.Handlers;

/// <summary>对话处理器排序特性。标注在 <see cref="IChatHandler"/> 实现类上，声明其在调用链中的执行顺序</summary>
/// <remarks>
/// <para>Before 和 After 两个顺序槽位相互独立，分别控制 OnBefore 和 OnAfter 的执行位置。
/// 未标注该特性、或将值设为 <c>0</c>（默认）的处理器，<see cref="ChatHandlerChain"/> 将按注册序自动赋值
/// <c>1000 + 注册索引 × 100</c>，确保未标注处理器维持注册顺序并排在已标注处理器之后。</para>
/// <para><b>用法示例：</b></para>
/// <list type="bullet">
/// <item><c>[ChatHandlerOrder(50)]</c> — Before = 50，After = 50（同时设两个）</item>
/// <item><c>[ChatHandlerOrder(Before = 50)]</c> — 仅设 Before，After 使用自动值</item>
/// <item><c>[ChatHandlerOrder(Before = 120, After = 8000)]</c> — 分别设置</item>
/// <item><c>[ChatHandlerOrder(After = 9999)]</c> — 仅设 After，Before 使用自动值</item>
/// </list>
/// <para><b>预留区间约定</b>（便于外部项目在内置锚点间插入自定义处理器）：</para>
/// <list type="table">
/// <listheader><term>区间</term><description>用途</description></listheader>
/// <item><term>1 — 999</term><description>高优先级前置处理（缓存命中、安全拦截等）</description></item>
/// <item><term>1000 — 1999</term><description>自动分配兜底区（未标注处理器）</description></item>
/// <item><term>8000 — 8999</term><description>后置业务处理（配额扣减等）</description></item>
/// <item><term>9000 — 9999</term><description>持久化处理（用量入库、消息落库等）</description></item>
/// <item><term>10000 +</term><description>最末异步副作用（事件智能体触发等）</description></item>
/// </list>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class ChatHandlerOrderAttribute : Attribute
{
    /// <summary>OnBefore 阶段排序值。值越小越先执行；0 表示未指定（自动分配）</summary>
    public Int32 Before { get; init; }

    /// <summary>OnAfter 阶段排序值。值越小越先执行；0 表示未指定（自动分配）</summary>
    public Int32 After { get; init; }

    /// <summary>同时设置 Before 和 After 排序值</summary>
    /// <param name="order">排序值，同时应用于 OnBefore 和 OnAfter 阶段</param>
    public ChatHandlerOrderAttribute(Int32 order)
    {
        Before = order;
        After = order;
    }

    /// <summary>通过命名参数分别设置 Before 和 After。适用于 <c>[ChatHandlerOrder(Before = 50)]</c> 或 <c>[ChatHandlerOrder(Before = 120, After = 8000)]</c></summary>
    public ChatHandlerOrderAttribute() { }
}
