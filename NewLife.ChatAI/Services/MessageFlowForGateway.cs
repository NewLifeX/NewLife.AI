using System.Runtime.CompilerServices;
using NewLife.AI.Clients;
using NewLife.AI.Models;
using NewLife.Log;

namespace NewLife.ChatAI.Services;

/// <summary>网关消息流。继承 <see cref="MessageFlow"/> 核心管道，专为 API 网关路径适配。
/// <list type="bullet">
///   <item>不从数据库加载历史消息（<see cref="LoadHistoryMessages"/> 始终返回空列表）</item>
///   <item><see cref="MessageFlow.Chain"/> 由 <see cref="ChatHandlerChain.BuildFor"/> 按 <see cref="ChatFlowSource.Gateway"/> 过滤，<c>EnableGatewayHandlers=false</c> 时仅保留 Core 级处理器（精简链），为 true 时保留全部处理器（完整链）</item>
///   <item>工具调用：默认透传模式（<c>EnableGatewayAutoTools=false</c>），客户端传入的工具定义原样传给 LLM，tool_calls 原样返回由客户端执行；开启后恢复 ToolChatClient 服务端自动执行（兼容模式）</item>
/// </list>
/// </summary>
public class MessageFlowForGateway : MessageFlow
{
    private readonly ChatSetting _chatSetting;

    #region 构造
    /// <summary>初始化网关消息流</summary>
    /// <param name="modelService">模型服务</param>
    /// <param name="setting">系统配置</param>
    /// <param name="tracer">追踪器</param>
    /// <param name="log">日志</param>
    /// <param name="services">服务提供者（仅用于解析 IChatFilter 链）</param>
    public MessageFlowForGateway(ModelService modelService, ChatSetting setting, ITracer? tracer, ILog? log, IServiceProvider? services = null)
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
    /// <param name="request">原始请求，用于提取 MaxTokens/Temperature/EnableThinking/ResponseFormat 等生成参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>SSE 事件流</returns>
    public virtual async IAsyncEnumerable<ChatStreamEvent> StreamGatewayAsync(IList<AiChatMessage> messages, ModelConfig modelConfig, Int32 userId, Int64 conversationId, IChatRequest? request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var flow = CreateGatewayFlowContext(messages, modelConfig, userId, conversationId, request);
        await foreach (var ev in InvokeChainAsync(flow, cancellationToken).ConfigureAwait(false))
            yield return ev;
    }

    /// <summary>网关非流式对话入口。调用 <see cref="InvokeNonStreamAsync"/> 直接获取 LLM 完整响应，无 SSE 流聚合开销</summary>
    /// <remarks>OnBefore/OnAfter Handler 链完整执行；Interceptor（洋葱）Handler 不参与非流式路径</remarks>
    /// <param name="messages">完整上下文消息列表（含系统提示词）</param>
    /// <param name="modelConfig">目标模型配置</param>
    /// <param name="userId">当前用户编号（0 表示匿名）</param>
    /// <param name="conversationId">关联会话编号（0 表示无会话）</param>
    /// <param name="request">原始请求，用于提取 MaxTokens/Temperature/EnableThinking/ResponseFormat 等生成参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>完整响应</returns>
    public virtual async Task<ChatResponse> CompletionGatewayAsync(IList<AiChatMessage> messages, ModelConfig modelConfig, Int32 userId, Int64 conversationId, IChatRequest? request, CancellationToken cancellationToken)
    {
        var flow = CreateGatewayFlowContext(messages, modelConfig, userId, conversationId, request);
        await InvokeNonStreamAsync(flow, cancellationToken).ConfigureAwait(false);

        var content = flow.ContentBuilder.ToString();
        var thinking = flow.ThinkingBuilder.ToString();
        var result = new ChatResponse { Model = modelConfig.Code, Usage = flow.Usage };
        var choice = result.Add(content, "assistant", FinishReason.Stop);
        if (!thinking.IsNullOrEmpty() && choice?.Message != null)
            choice.Message.ReasoningContent = thinking;
        return result;
    }

    /// <summary>覆盖：Gateway 默认不装配 ToolChatClient（透传模式），仅当 EnableGatewayAutoTools 开启时恢复自动工具执行。
    /// 透传模式下客户端传入的 tools 已通过 <see cref="CreateGatewayFlowContext"/> 写入 <c>ChatOptions.Tools</c>，
    /// 由原始 IChatClient 直接传给 LLM；tool_calls 由 LLM 返回后原样透传给客户端。</summary>
    /// <param name="rawClient">原始模型客户端</param>
    /// <param name="context">对话上下文</param>
    /// <returns>管道客户端</returns>
    protected override IChatClient BuildPipelineClient(IChatClient rawClient, IChatContext context)
    {
        var clientBuilder = rawClient.AsBuilder();
        foreach (var filter in ChatFilters)
            clientBuilder = clientBuilder.UseFilters(filter);

        // 透传模式：不装配 ToolChatClient，客户端传入的工具定义直接由原始客户端传给 LLM
        if (_chatSetting.EnableGatewayAutoTools)
        {
            var providers = ToolProviders;
            if (providers.Length > 0)
            {
                clientBuilder = clientBuilder.UseTools(_chatSetting.ToolMaxIterations, _chatSetting.ToolResultMaxChars, context.SelectedTools, providers);

                foreach (var p in providers)
                    foreach (var t in p.GetTools(context.SelectedTools))
                        if (t.Function?.Name != null) context.AvailableToolNames.Add(t.Function.Name);
            }
        }

        return clientBuilder.Build();
    }

    /// <summary>构建网关对话上下文</summary>
    /// <param name="messages">完整上下文消息列表</param>
    /// <param name="modelConfig">目标模型配置</param>
    /// <param name="userId">当前用户编号</param>
    /// <param name="conversationId">关联会话编号</param>
    /// <param name="request">原始请求</param>
    private MessageFlowContext CreateGatewayFlowContext(IList<AiChatMessage> messages, ModelConfig modelConfig, Int32 userId, Int64 conversationId, IChatRequest? request)
    {
        var flow = new MessageFlowContext
        {
            ContextMessages = messages,
            ModelConfig = modelConfig,
            UserId = userId,
            Conversation = new Conversation { Id = conversationId, Enable = true },
            // 标记来源为 Gateway；是否持久化由 EnableGatewayRecording 配置决定
            Source = ChatFlowSource.Gateway,
            ThinkingMode = request?.EnableThinking switch
            {
                true => ThinkingMode.Think,
                false => ThinkingMode.Fast,
                _ => ThinkingMode.Auto,
            },
            Options = new ChatOptions
            {
                MaxTokens = request?.MaxTokens > 0 ? request!.MaxTokens : null,
                Temperature = request?.Temperature,
                ResponseFormat = request?.ResponseFormat,
                Model = modelConfig.GetEffectiveModelCode(),
                EnableThinking = request?.EnableThinking switch
                {
                    true => true,
                    false => false,
                    _ => modelConfig.SupportThinking ? true : null,
                },
                UserId = userId > 0 ? userId.ToString() : null,
                ConversationId = conversationId > 0 ? conversationId.ToString() : null,
                // 透传客户端工具定义：透传模式下不装配 ToolChatClient，由原始客户端直接传给 LLM
                Tools = request?.Tools,
                ToolChoice = request?.ToolChoice,
            },
        };
        ApplyResponseStyle(flow.Options, flow.Options.UserId);
        //flow.Options.Items = flow.Items;
        return flow;
    }

    #endregion
}
