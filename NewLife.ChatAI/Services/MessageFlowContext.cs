using System.Text;
using NewLife.AI.Interfaces;

namespace NewLife.ChatAI.Services;

/// <summary>消息流处理上下文。在 Validate → Prepare → Execute → Persist → PostProcess 五段式模板间流转，
/// 统一承载 DB 实体、执行过程中的收集结果、以及跨阶段的扩展数据</summary>
/// <remarks>
/// 各 <see cref="IChatHandler"/> 通过本上下文读取/修改状态，
/// 无需互相感知。四大入口通过 <see cref="Kind"/> 区分。
/// </remarks>
public class MessageFlowContext : IChatContext
{
    #region 入口信息

    /// <summary>入口类型。区分 Stream/Regenerate/RegenerateStream/EditAndResendStream/ResumeBackground</summary>
    public FlowKind Kind { get; set; }

    /// <summary>当前用户编号</summary>
    public Int32 UserId { get; set; }

    /// <summary>技能编号（0 表示无技能）</summary>
    public Int32 SkillId { get; set; }

    #endregion

    #region 实体引用

    /// <summary>会话实体</summary>
    public Conversation Conversation { get; set; } = null!;

    /// <summary>模型配置</summary>
    public ModelConfig ModelConfig { get; set; } = null!;

    /// <summary>用户消息（StreamMessage/EditResend 场景有值；Regenerate 场景可能为 null）</summary>
    public DbChatMessage? UserMessage { get; set; }

    /// <summary>AI 回复消息（新建或复用原消息）</summary>
    public DbChatMessage AssistantMessage { get; set; } = null!;

    /// <summary>对话上下文消息列表。按时间升序排列</summary>
    public IList<DbChatMessage> HistoryMessages { get; set; } = [];

    #endregion

    #region 执行产物

    /// <summary>对话上下文消息列表。<b>Prepare</b> 阶段由 <c>BuildContextAsync</c> 填充，
    /// <see cref="IChatHandler"/> 可在此基础上追加/修改/截断，管道执行时据此发起模型调用</summary>
    public IList<AiChatMessage> ContextMessages { get; set; } = [];

    /// <summary>正文内容收集器</summary>
    public StringBuilder ContentBuilder { get; } = new();

    /// <summary>思考内容收集器</summary>
    public StringBuilder ThinkingBuilder { get; } = new();

    /// <summary>工具调用收集器</summary>
    public List<ToolCallDto> ToolCalls { get; } = [];

    /// <summary>用量统计</summary>
    public UsageDetails? Usage { get; set; }

    ///// <summary>子流程用量集合（Before 阶段顺序执行，无竞态）</summary>
    //public IDictionary<String, UsageDetails> SubFlowUsages { get; } = new Dictionary<String, UsageDetails>(StringComparer.OrdinalIgnoreCase);

    #endregion

    #region 错误与控制

    /// <summary>是否出现可恢复错误。Persist 阶段会据此调整落库策略</summary>
    public Boolean HasError { get; set; }

    /// <summary>流控信号。OnBefore 阶段任一处理器设置，调度器据此决定后续执行策略</summary>
    public ChatFlowControl FlowControl { get; set; }

    /// <summary>取消代码</summary>
    public String? CancelCode { get; set; }

    /// <summary>取消消息</summary>
    public String? CancelMessage { get; set; }

    /// <summary>延迟错误事件。SSE 推送后在 Persist 阶段落库用</summary>
    public ChatStreamEvent? DeferredError { get; set; }

    /// <summary>初始化异常。CreateFlowContext 校验失败时设置，主流程检测到则直接终止</summary>
    public ChatException? Error { get; set; }

    #endregion

    #region 扩展数据

    /// <summary>跨阶段扩展数据。供自定义 <see cref="IChatHandler"/> 在阶段间传递状态，避免继承 MessageFlowContext</summary>
    public IDictionary<String, Object?> Items { get; } = new Dictionary<String, Object?>(StringComparer.OrdinalIgnoreCase);

    /// <summary>获取或设置扩展数据项</summary>
    /// <param name="key">数据键</param>
    /// <returns>数据值，不存在时返回 null</returns>
    public Object? this[String key]
    {
        get => Items.TryGetValue(key, out var v) ? v : null;
        set => Items[key] = value;
    }

    #endregion

    #region IChatContext 新增字段（向新中间件抽象过渡，独立存储）

    /// <summary>思考模式（IChatContext 新增）</summary>
    public ThinkingMode ThinkingMode { get; set; }

    ///// <summary>系统提示词内容</summary>
    //public String? SystemPrompt { get; set; }

    ///// <summary>系统消息就绪回调</summary>
    //public Action<String>? OnSystemReady { get; set; }

    /// <summary>消息中 @ToolName 显式引用的工具名称集合</summary>
    public ISet<String> SelectedTools { get; } = new HashSet<String>(StringComparer.OrdinalIgnoreCase);

    /// <summary>本轮实际注入给模型的工具名称集合</summary>
    public ISet<String> AvailableToolNames { get; } = new HashSet<String>(StringComparer.OrdinalIgnoreCase);

    /// <summary>实际使用的最大 Token 数</summary>
    public Int32 MaxTokens { get; set; }

    /// <summary>实际使用的采样温度</summary>
    public Double? Temperature { get; set; }

    /// <summary>响应格式约束。透传至 LLM 的 response_format 参数，支持 json_object/json_schema/text</summary>
    public Object? ResponseFormat { get; set; }

    /// <summary>完成原因</summary>
    public String? FinishReason { get; set; }

    #endregion

    #region IChatContext 显式接口实现

    /// <inheritdoc />
    IConversation IChatContext.Conversation { get => Conversation; set => Conversation = (Conversation)value; }

    /// <inheritdoc />
    IModelConfig IChatContext.ModelConfig { get => ModelConfig; set => ModelConfig = (ModelConfig)value; }

    /// <inheritdoc />
    IChatMessage? IChatContext.UserMessage { get => UserMessage; set => UserMessage = (DbChatMessage?)value; }

    /// <inheritdoc />
    IChatMessage IChatContext.AssistantMessage { get => AssistantMessage; set => AssistantMessage = (DbChatMessage)value; }

    ///// <inheritdoc />
    //IList<AiChatMessage> IChatContext.ContextMessages { get => ContextMessages; set => ContextMessages = value; }

    #endregion

    #region 来源与持久化

    /// <summary>消息流来源。由各 MessageFlow 子类在构建上下文时设置，默认 Web</summary>
    public ChatFlowSource Source { get; set; } = ChatFlowSource.Web;

    /// <summary>是否将消息持久化到数据库。false 时 UserMessage/AssistantMessage.Id 保持 0，不执行 Insert/Update</summary>
    public Boolean PersistMessages { get; set; } = true;

    #endregion
}
