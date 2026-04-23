namespace NewLife.AI.Services;

/// <summary>消息生成入口类型。用于区分四大消息流（StreamMessage/Regenerate/RegenerateStream/EditAndResendStream）以及恢复后台流</summary>
/// <remarks>
/// IContextEnricher 与 IMessageFlowPostProcessor 可根据 FlowKind 决定是否生效：
/// <list type="bullet">
/// <item>Stream：用户发送新消息，SSE 流式返回</item>
/// <item>Regenerate：非流式重新生成（用于后台任务或测试）</item>
/// <item>RegenerateStream：对已有 AI 回复进行流式重新生成</item>
/// <item>EditAndResendStream：编辑用户消息后重新发送并流式返回</item>
/// <item>ResumeBackgroundStream：断线重连后恢复已在后台继续生成的流</item>
/// </list>
/// </remarks>
public enum FlowKind
{
    /// <summary>流式发送新消息</summary>
    Stream = 1,

    /// <summary>非流式重新生成</summary>
    Regenerate = 2,

    /// <summary>流式重新生成</summary>
    RegenerateStream = 3,

    /// <summary>编辑用户消息后重新发送（流式）</summary>
    EditAndResendStream = 4,

    /// <summary>恢复后台流式生成（断线重连）</summary>
    ResumeBackgroundStream = 5,
}
