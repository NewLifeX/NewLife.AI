namespace NewLife.AI.Handlers;

/// <summary>Handler 适用范围声明接口。<see cref="IChatHandler"/> 可选择性实现本接口，
/// 声明自己支持的消息流来源和链层级，供 <see cref="ChatHandlerChain.BuildFor"/> 在构建链时自动过滤</summary>
/// <remarks>
/// <para><b>向后兼容：</b>未实现本接口的 Handler 视为支持所有来源（<see cref="ChatFlowSource.All"/>）
/// 且属于完整链层级（<see cref="ChatHandlerTier.Full"/>），行为与现有逻辑完全一致。</para>
/// <para><b>过滤规则（由 <see cref="ChatHandlerChain.BuildFor"/> 执行）：</b></para>
/// <list type="bullet">
///   <item>当前来源（<see cref="ChatFlowSource"/>）不在 <see cref="SupportedSources"/> 中 → 剔除</item>
///   <item>来源匹配且 <see cref="Tier"/> == <see cref="ChatHandlerTier.Core"/> → 始终保留</item>
///   <item>来源匹配且 <see cref="Tier"/> == <see cref="ChatHandlerTier.Full"/> 且 fullChain = false → 剔除</item>
/// </list>
/// <para><b>典型用法（仅 Web 有意义的 Handler）：</b></para>
/// <code>
/// public ChatFlowSource SupportedSources =&gt; ChatFlowSource.Web;
/// public ChatHandlerTier Tier =&gt; ChatHandlerTier.Full;
/// </code>
/// <para><b>所有渠道核心 Handler：</b></para>
/// <code>
/// public ChatFlowSource SupportedSources =&gt; ChatFlowSource.All;
/// public ChatHandlerTier Tier =&gt; ChatHandlerTier.Core;
/// </code>
/// </remarks>
public interface IChatHandlerScope
{
    /// <summary>支持的消息流来源。未在此集合中的来源将在 <see cref="ChatHandlerChain.BuildFor"/> 时自动剔除</summary>
    ChatFlowSource SupportedSources { get; }

    /// <summary>Handler 链层级。<see cref="ChatHandlerTier.Core"/> 始终保留；
    /// <see cref="ChatHandlerTier.Full"/> 仅在 fullChain 模式下保留</summary>
    ChatHandlerTier Tier { get; }
}
