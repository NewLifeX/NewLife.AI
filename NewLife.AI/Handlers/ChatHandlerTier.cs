namespace NewLife.AI.Handlers;

/// <summary>Handler 链层级。配合 <see cref="IChatHandlerScope"/> 标识处理器属于精简链（Core）还是完整链（Full）</summary>
/// <remarks>
/// 当渠道或网关通过配置选择"精简链"模式（<c>FullChain = false</c>）时，
/// <see cref="ChatHandlerChain.BuildFor"/> 只保留 <see cref="Core"/> 层级的处理器（如配额、用量记录），
/// 其余 <see cref="Full"/> 层级处理器（如知识召回、记忆注入）将被剔除。
/// </remarks>
public enum ChatHandlerTier
{
    /// <summary>精简链核心处理器。无论 <c>fullChain</c> 参数为何值，始终保留。
    /// 适用于配额校验、用量记录、安全审计等所有渠道必须执行的能力</summary>
    Core = 0,

    /// <summary>完整链处理器。仅当 <c>fullChain = true</c> 时保留。
    /// 适用于技能注入、知识召回、记忆增强、痛觉预警等需要完整上下文的能力</summary>
    Full = 1,
}
