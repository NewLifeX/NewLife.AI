namespace NewLife.AI.Handlers;

/// <summary>对话消息流来源。标识当前 <see cref="IChatContext"/> 是从哪条路径进入 <see cref="IChatHandler"/> 链的</summary>
/// <remarks>
/// 配合 <see cref="IChatHandlerScope"/> 接口使用：Handler 通过 <see cref="IChatHandlerScope.SupportedSources"/>
/// 声明自己支持哪些来源，<see cref="ChatHandlerChain.BuildFor"/> 在构建链时自动剔除不适用的 Handler。
/// </remarks>
[Flags]
public enum ChatFlowSource
{
    /// <summary>无来源（占位，不应出现在运行时）</summary>
    None = 0,

    /// <summary>Web 主对话（<c>MessagesController</c> → <c>MessageService</c>）</summary>
    Web = 1,

    /// <summary>API 网关（<c>GatewayController</c> → <c>GatewayMessageFlow</c>）</summary>
    Gateway = 2,

    /// <summary>IM/OA 渠道（钉钉/企微/飞书/Webhook → <c>ChannelMessageFlow</c>）</summary>
    Channel = 4,

    /// <summary>所有来源（Web | Gateway | Channel）</summary>
    All = Web | Gateway | Channel,
}
