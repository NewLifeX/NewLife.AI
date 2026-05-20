using System.Runtime.CompilerServices;
using NewLife.AI.Clients;
using NewLife.Collections;
using NewLife.Log;
using ChatResponse = NewLife.AI.Models.ChatResponse;

namespace NewLife.ChatAI.Services;

/// <summary>网关消息流。继承 <see cref="MessageFlow"/> 核心管道，专为 API 网关路径适配。
/// <list type="bullet">
///   <item>不从数据库加载历史消息（<see cref="LoadHistoryMessages"/> 始终返回空列表）</item>
///   <item><see cref="MessageFlow.Chain"/> 由 <see cref="ChatHandlerChain.BuildFor"/> 按 <see cref="ChatFlowSource.Gateway"/> 过滤，<c>EnableGatewayHandlers=false</c> 时仅保留 Core 级处理器（精简链），为 true 时保留全部处理器（完整链）</item>
///   <item>上下文 <see cref="MessageFlowContext.PersistMessages"/> 默认 false，由 <c>EnableGatewayRecording</c> 控制</item>
/// </list>
/// </summary>
public class GatewayMessageFlow : MessageFlow
{
    #region 构造

    private readonly ChatSetting _chatSetting;

    /// <summary>初始化网关消息流</summary>
    /// <param name="modelService">模型服务</param>
    /// <param name="setting">系统配置</param>
    /// <param name="tracer">追踪器</param>
    /// <param name="log">日志</param>
    /// <param name="services">服务提供者（仅用于解析 IChatFilter 链）</param>
    public GatewayMessageFlow(ModelService modelService, ChatSetting setting, ITracer? tracer, ILog? log, IServiceProvider? services = null)
        : base(modelService, null, setting, tracer, log, services)
    {
        _chatSetting = setting;
        // 按来源和链模式从全量 Handler 集合中过滤：
        //   EnableGatewayHandlers=false → 精简链（Core 级处理器：配额、用量等）
        //   EnableGatewayHandlers=true  → 完整链（含知识进化、记忆图谱等高级能力）
        var allHandlers = services?.GetServices<IChatHandler>() ?? [];
        Chain = ChatHandlerChain.BuildFor(allHandlers, ChatFlowSource.Gateway, setting.EnableGatewayHandlers);
    }

    #endregion

    #region 方法

    /// <summary>覆盖：网关路径不从数据库加载历史消息，直接由调用方构建完整消息列表传入</summary>
    /// <param name="conversationId">会话编号（网关场景忽略）</param>
    /// <param name="maxRounds">最大轮数（网关场景忽略）</param>
    /// <returns>空列表</returns>
    protected override IList<DbChatMessage> LoadHistoryMessages(Int64 conversationId, Int32 maxRounds) => [];

    /// <summary>网关流式对话入口。绕过数据库操作，直接使用调用方已构建好的 <paramref name="messages"/> 发起 LLM 调用。
    /// 经过 <see cref="ChatHandlerChain"/> 三段式链路（OnBefore → <see cref="MessageFlow.CoreStreamAsync"/> → OnAfter），
    /// 以及 <see cref="MessageFlow.ChatFilters"/> 过滤器链。
    /// </summary>
    /// <param name="messages">完整上下文消息列表（含系统提示词）</param>
    /// <param name="modelConfig">目标模型配置</param>
    /// <param name="userId">当前用户编号（0 表示匿名）</param>
    /// <param name="conversationId">关联会话编号（0 表示无会话）</param>
    /// <param name="request">原始请求，用于提取 MaxTokens/Temperature/EnableThinking 等生成参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>SSE 事件流</returns>
    public virtual async IAsyncEnumerable<ChatStreamEvent> StreamGatewayAsync(
        IList<AiChatMessage> messages,
        ModelConfig modelConfig,
        Int32 userId,
        Int64 conversationId,
        IChatRequest? request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var flow = new MessageFlowContext
        {
            ContextMessages = messages,
            ModelConfig = modelConfig,
            UserId = userId,
            Conversation = new Conversation { Id = conversationId, Enable = true },
            // 标记来源为 Gateway；是否持久化由 EnableGatewayRecording 配置决定
            Source = ChatFlowSource.Gateway,
            PersistMessages = _chatSetting.EnableGatewayRecording,
            MaxTokens = request?.MaxTokens ?? 0,
            Temperature = request?.Temperature,
            ThinkingMode = request?.EnableThinking switch
            {
                true => ThinkingMode.Think,
                false => ThinkingMode.Fast,
                _ => ThinkingMode.Auto,
            },
        };

        await foreach (var ev in InvokeChainAsync(flow, cancellationToken).ConfigureAwait(false))
            yield return ev;
    }

    /// <summary>网关非流式对话入口。内部调用 <see cref="StreamGatewayAsync"/> 聚合所有 SSE 事件为单次响应，
    /// 同样经过 <see cref="ChatHandlerChain"/> 三段式链路及 <see cref="MessageFlow.ChatFilters"/> 过滤器链。
    /// </summary>
    /// <param name="messages">完整上下文消息列表（含系统提示词）</param>
    /// <param name="modelConfig">目标模型配置</param>
    /// <param name="userId">当前用户编号（0 表示匿名）</param>
    /// <param name="conversationId">关联会话编号（0 表示无会话）</param>
    /// <param name="request">原始请求，用于提取 MaxTokens/Temperature/EnableThinking 等生成参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>聚合后的完整响应</returns>
    public virtual async Task<ChatResponse> CompletionGatewayAsync(
        IList<AiChatMessage> messages,
        ModelConfig modelConfig,
        Int32 userId,
        Int64 conversationId,
        IChatRequest? request,
        CancellationToken cancellationToken)
    {
        var contentSb = Pool.StringBuilder.Get();
        var thinkingSb = Pool.StringBuilder.Get();
        UsageDetails? lastUsage = null;

        await foreach (var ev in StreamGatewayAsync(messages, modelConfig, userId, conversationId, request, cancellationToken).ConfigureAwait(false))
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
