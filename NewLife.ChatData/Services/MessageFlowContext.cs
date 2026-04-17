using System.Text;
using NewLife.AI.Models;
using NewLife.AI.Services;
using NewLife.ChatData.Entity;
using NewLife.ChatData.Models;
using ChatMessage = NewLife.ChatData.Entity.ChatMessage;

namespace NewLife.ChatData.Services;

/// <summary>消息流处理上下文。在 Validate → Prepare → Execute → Persist → PostProcess 五段式模板间流转，
/// 统一承载 DB 实体、执行过程中的收集结果、以及跨阶段的扩展数据</summary>
/// <remarks>
/// 各 <see cref="IContextEnricher"/> 与 <see cref="IMessageFlowPostProcessor"/> 通过本上下文读取/修改状态，
/// 无需互相感知。四大入口通过 <see cref="Kind"/> 区分。
/// </remarks>
public class MessageFlowContext
{
    #region 入口信息

    /// <summary>入口类型。区分 Stream/Regenerate/RegenerateStream/EditAndResendStream/ResumeBackground</summary>
    public FlowKind Kind { get; set; }

    /// <summary>当前用户编号</summary>
    public Int32 UserId { get; set; }

    /// <summary>技能编号（0 表示无技能）</summary>
    public Int32 SkillId { get; set; }

    /// <summary>技能名称（PrepareContext 后填充）</summary>
    public String? SkillName { get; set; }

    /// <summary>编辑重发时的新用户消息内容（仅 EditAndResendStream 使用）</summary>
    public String? NewUserContent { get; set; }

    /// <summary>原始消息编号（Regenerate/RegenerateStream/EditAndResendStream 场景使用）</summary>
    public Int64 OriginalMessageId { get; set; }

    #endregion

    #region 实体引用

    /// <summary>会话实体</summary>
    public Conversation Conversation { get; set; } = null!;

    /// <summary>模型配置</summary>
    public ModelConfig ModelConfig { get; set; } = null!;

    /// <summary>用户消息（StreamMessage/EditResend 场景有值；Regenerate 场景可能为 null）</summary>
    public ChatMessage? UserMessage { get; set; }

    /// <summary>AI 回复消息（新建或复用原消息）</summary>
    public ChatMessage AssistantMessage { get; set; } = null!;

    #endregion

    #region 执行产物

    /// <summary>管道执行上下文。由主流程初始化，传递给 <see cref="IChatPipeline"/></summary>
    public ChatPipelineContext PipelineContext { get; set; } = null!;

    /// <summary>对话上下文消息列表。<b>Prepare</b> 阶段由 <c>BuildContextAsync</c> 填充，
    /// <see cref="IContextEnricher"/> 可在此基础上追加/修改/截断，管道执行时据此发起模型调用</summary>
    public IList<NewLife.AI.Models.ChatMessage> ContextMessages { get; set; } = [];

    /// <summary>正文内容收集器</summary>
    public StringBuilder ContentBuilder { get; } = new();

    /// <summary>思考内容收集器</summary>
    public StringBuilder ThinkingBuilder { get; } = new();

    /// <summary>工具调用收集器</summary>
    public List<ToolCallDto> ToolCalls { get; } = [];

    /// <summary>用量统计</summary>
    public UsageDetails? Usage { get; set; }

    #endregion

    #region 错误与控制

    /// <summary>是否出现可恢复错误。Persist 阶段会据此调整落库策略</summary>
    public Boolean HasError { get; set; }

    /// <summary>延迟错误事件。SSE 推送后在 Persist 阶段落库用</summary>
    public ChatStreamEvent? DeferredError { get; set; }

    /// <summary>初始化异常。CreateFlowContext 校验失败时设置，主流程检测到则直接终止</summary>
    public ChatException? Error { get; set; }

    #endregion

    #region 扩展数据

    /// <summary>跨阶段扩展数据。供自定义 <see cref="IContextEnricher"/>/<see cref="IMessageFlowPostProcessor"/> 在阶段间传递状态，避免继承 MessageFlowContext</summary>
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
}
