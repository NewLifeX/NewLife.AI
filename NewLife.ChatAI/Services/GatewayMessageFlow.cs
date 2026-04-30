using System.Runtime.CompilerServices;
using NewLife.Collections;
using NewLife.Log;
using ChatResponse = NewLife.AI.Models.ChatResponse;

namespace NewLife.ChatAI.Services;

/// <summary>网关消息流。继承 <see cref="MessageFlow"/> 核心管道，专为 API 网关路径适配。
/// <list type="bullet">
///   <item>不从数据库加载历史消息（<see cref="LoadHistoryMessages"/> 始终返回空列表）</item>
///   <item><see cref="MessageFlow.Handlers"/> 固定为空，不执行 Web UI 路径的持久化/技能/知识库等处理器</item>
///   <item><see cref="MessageFlow.ChatFilters"/> 仍由 DI 解析，延续 IChatFilter 日志/监控横切链路</item>
/// </list>
/// 如需在网关路径追加配额检查、用量记录等处理器，构造后直接向 <c>Handlers</c> 赋值即可。
/// </summary>
public class GatewayMessageFlow : MessageFlow
{
    #region 构造

    /// <summary>初始化网关消息流</summary>
    /// <param name="modelService">模型服务</param>
    /// <param name="setting">系统配置</param>
    /// <param name="tracer">追踪器</param>
    /// <param name="log">日志</param>
    /// <param name="services">服务提供者（仅用于解析 IChatFilter 链）</param>
    public GatewayMessageFlow(ModelService modelService, ChatSetting setting, ITracer? tracer, ILog? log, IServiceProvider? services = null)
        : base(modelService, null, setting, tracer, log, services)
    {
        // 构造函数体在 base 构造之后执行，此时 Handlers 已由 base 通过 services 解析完毕
        // 网关路径不执行 DI 全局注册的 Web UI IChatHandler 链（技能/知识库/持久化等）
        // IChatFilter 链（日志、监控等）由 base 通过 services 解析并保留
        Handlers = [];
    }

    #endregion

    #region 方法

    /// <summary>覆盖：网关路径不从数据库加载历史消息，直接由调用方构建完整消息列表传入</summary>
    /// <param name="conversationId">会话编号（网关场景忽略）</param>
    /// <param name="maxRounds">最大轮数（网关场景忽略）</param>
    /// <returns>空列表</returns>
    protected override IList<DbChatMessage> LoadHistoryMessages(Int64 conversationId, Int32 maxRounds) => [];

    /// <summary>网关流式对话入口。绕过数据库操作，直接使用调用方已构建好的 <paramref name="messages"/> 发起 LLM 调用。
    /// 经过 <see cref="MessageFlow.Handlers"/> 三段式链路（OnBefore → <see cref="MessageFlow.CoreStreamAsync"/> → OnAfter），
    /// 以及 <see cref="MessageFlow.ChatFilters"/> 过滤器链。
    /// </summary>
    /// <param name="messages">完整上下文消息列表（含系统提示词）</param>
    /// <param name="modelConfig">目标模型配置</param>
    /// <param name="userId">当前用户编号（0 表示匿名）</param>
    /// <param name="conversationId">关联会话编号（0 表示无会话）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>SSE 事件流</returns>
    public virtual async IAsyncEnumerable<ChatStreamEvent> StreamGatewayAsync(
        IList<AiChatMessage> messages,
        ModelConfig modelConfig,
        Int32 userId,
        Int64 conversationId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var flow = new MessageFlowContext
        {
            ContextMessages = messages,
            ModelConfig = modelConfig,
            UserId = userId,
            Conversation = new Conversation { Id = conversationId },
        };

        await foreach (var ev in InvokeChainAsync(flow, cancellationToken).ConfigureAwait(false))
            yield return ev;
    }

    /// <summary>网关非流式对话入口。内部调用 <see cref="StreamGatewayAsync"/> 聚合所有 SSE 事件为单次响应，
    /// 同样经过 <see cref="MessageFlow.Handlers"/> 三段式链路及 <see cref="MessageFlow.ChatFilters"/> 过滤器链。
    /// </summary>
    /// <param name="messages">完整上下文消息列表（含系统提示词）</param>
    /// <param name="modelConfig">目标模型配置</param>
    /// <param name="userId">当前用户编号（0 表示匿名）</param>
    /// <param name="conversationId">关联会话编号（0 表示无会话）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>聚合后的完整响应</returns>
    public virtual async Task<ChatResponse> CompletionGatewayAsync(
        IList<AiChatMessage> messages,
        ModelConfig modelConfig,
        Int32 userId,
        Int64 conversationId,
        CancellationToken cancellationToken)
    {
        var contentSb = Pool.StringBuilder.Get();
        var thinkingSb = Pool.StringBuilder.Get();
        UsageDetails? lastUsage = null;

        await foreach (var ev in StreamGatewayAsync(messages, modelConfig, userId, conversationId, cancellationToken).ConfigureAwait(false))
        {
            switch (ev.Type)
            {
                case "content_delta" when !ev.Content.IsNullOrEmpty():
                    contentSb.Append(ev.Content);
                    break;
                case "thinking_delta" when !ev.Content.IsNullOrEmpty():
                    thinkingSb.Append(ev.Content);
                    break;
                case "message_done":
                    if (ev.Usage != null) lastUsage = ev.Usage;
                    break;
            }
        }

        var content = contentSb.Return(true);
        var thinking = thinkingSb.Return(true);
        var result = new ChatResponse { Model = modelConfig.Code, Usage = lastUsage };
        var choice = result.Add(content, "assistant", FinishReason.Stop);
        if (!thinking.IsNullOrEmpty() && choice?.Message != null)
            choice.Message.ReasoningContent = thinking;
        return result;
    }

    #endregion
}
