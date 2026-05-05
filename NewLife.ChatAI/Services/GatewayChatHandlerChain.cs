namespace NewLife.ChatAI.Services;

/// <summary>网关专属调用链管理器。继承 <see cref="ChatHandlerChain"/>，专为 API 网关路径提供差异化的 Handler 子集。
/// <para>网关路径不执行 Web UI 专属的技能激活、标题生成、知识召回、消息持久化等处理器，
/// 仅执行配额校验和用量记录等轻量处理器。</para>
/// <para>通过 DI 工厂在 <c>AddChatAI()</c> / <c>AddStarChat()</c> 中显式选取处理器构建，
/// 与主链路 <see cref="ChatHandlerChain"/> 共享同一批处理器单例（无重复实例化）。</para>
/// </summary>
public class GatewayChatHandlerChain : ChatHandlerChain
{
    /// <summary>创建空网关链</summary>
    public GatewayChatHandlerChain() { }

    /// <summary>从处理器集合创建网关链（通常由 DI 工厂调用）</summary>
    /// <param name="handlers">处理器集合，按注册顺序传入</param>
    public GatewayChatHandlerChain(IEnumerable<IChatHandler> handlers) : base(handlers) { }
}
