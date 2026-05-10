namespace NewLife.ChatAI.Services;

/// <summary>网关专属调用链管理器（已废弃）。</summary>
/// <remarks>请改用 <see cref="ChatHandlerChain.BuildFor(System.Collections.Generic.IEnumerable{IChatHandler}, ChatFlowSource, Boolean)"/>，
/// 可按来源和链模式动态过滤，无需子类。</remarks>
[Obsolete("请改用 ChatHandlerChain.BuildFor(handlers, ChatFlowSource.Gateway, fullChain) 构建网关专属链。")]
public class GatewayChatHandlerChain : ChatHandlerChain
{
    /// <summary>创建空网关链</summary>
    public GatewayChatHandlerChain() { }

    /// <summary>从处理器集合创建网关链（通常由 DI 工厂调用）</summary>
    /// <param name="handlers">处理器集合，按注册顺序传入</param>
    public GatewayChatHandlerChain(IEnumerable<IChatHandler> handlers) : base(handlers) { }
}
