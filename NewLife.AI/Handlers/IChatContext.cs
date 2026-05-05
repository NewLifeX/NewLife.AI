using System.Text;
using NewLife.AI.Handlers;
using NewLife.AI.Interfaces;
using NewLife.AI.Models;
using NewLife.Data;

namespace NewLife.AI.Services;

/// <summary>对话处理上下文。在 <see cref="IChatHandler"/> 链各节点之间流转，
/// 统一承载会话/模型/消息实体、对话上下文、收集器、用量与扩展数据</summary>
/// <remarks>
/// <para>本接口合并了原 <c>IMessageFlowContext</c>（消息流上下文）与 <c>ChatPipelineContext</c>（管道上下文）的全部字段，
/// 让所有处理器在同一上下文上协作，无需在两个对象之间手工同步状态。</para>
/// <para>稀少入口参数通过 <see cref="IExtend.Items"/> 字典携带，例如：
/// <c>Items["SkillName"]</c>（技能名称）、<c>Items["NewUserContent"]</c>（编辑重发的新内容）。</para>
/// </remarks>
public interface IChatContext : IExtend
{
    #region 入口信息

    /// <summary>入口类型。区分 Stream/Regenerate/RegenerateStream/EditAndResendStream/ResumeBackground</summary>
    FlowKind Kind { get; set; }

    /// <summary>当前用户编号</summary>
    Int32 UserId { get; set; }

    /// <summary>会话技能编号（0 表示无技能；处理器内部可在执行过程中重写）</summary>
    Int32 SkillId { get; set; }

    /// <summary>思考模式</summary>
    ThinkingMode ThinkingMode { get; set; }

    #endregion

    #region 实体引用

    /// <summary>会话实体（接口形式）</summary>
    IConversation Conversation { get; set; }

    /// <summary>模型配置（接口形式）</summary>
    IModelConfig ModelConfig { get; set; }

    /// <summary>用户消息（StreamMessage/EditResend 场景有值；Regenerate 场景可能为 null）</summary>
    IChatMessage? UserMessage { get; set; }

    /// <summary>AI 回复消息（新建或复用原消息）</summary>
    IChatMessage AssistantMessage { get; set; }

    #endregion

    #region 对话内容

    /// <summary>对话上下文消息列表。事前处理器可追加/修改/截断；核心处理器据此发起模型调用</summary>
    IList<ChatMessage> ContextMessages { get; set; }

    ///// <summary>系统提示词内容。由 SystemPrompt 处理器填充，供持久化与诊断使用</summary>
    //String? SystemPrompt { get; set; }

    ///// <summary>系统消息就绪回调。核心处理器收到第一个流式 chunk 时触发，参数为完整的系统消息文本</summary>
    //Action<String>? OnSystemReady { get; set; }

    #endregion

    #region 工具与技能解析结果

    /// <summary>消息中 @ToolName 显式引用的工具名称集合。由 SystemPrompt 处理器解析消息后填充，工具提供者据此过滤非系统工具</summary>
    ISet<String> SelectedTools { get; }

    /// <summary>本轮实际注入给模型的工具名称集合。由核心处理器在构建工具提供者后填充</summary>
    ISet<String> AvailableToolNames { get; }

    #endregion

    #region 模型调用参数

    /// <summary>实际使用的最大 Token 数。由核心处理器在构建 ChatOptions 后填充</summary>
    Int32 MaxTokens { get; set; }

    /// <summary>实际使用的采样温度。由核心处理器在构建 ChatOptions 后填充</summary>
    Double? Temperature { get; set; }

    /// <summary>完成原因。由核心处理器在流式/非流式结束后填充</summary>
    String? FinishReason { get; set; }

    #endregion

    #region 流式收集结果

    /// <summary>正文内容收集器。核心处理器流式接收时累积，事后处理器据此持久化</summary>
    StringBuilder ContentBuilder { get; }

    /// <summary>思考内容收集器（推理模式）</summary>
    StringBuilder ThinkingBuilder { get; }

    /// <summary>工具调用收集器</summary>
    List<ToolCallDto> ToolCalls { get; }

    /// <summary>用量统计。message_done 事件时填充</summary>
    UsageDetails? Usage { get; set; }

    /// <summary>子流程用量集合。Before 阶段中的同步子流程（如三明治状态机分类）在此写入；
    /// <see cref="UsageRecordHandler"/> 在 OnAfter 阶段将每条记录独立落库，Source 取字典 Key</summary>
    IDictionary<String, UsageDetails> SubFlowUsages { get; }

    #endregion

    #region 错误与控制

    /// <summary>是否出现可恢复错误。事后处理器可据此调整持久化策略</summary>
    Boolean HasError { get; set; }

    /// <summary>是否取消执行（事前短路）。由 OnBefore 阶段任一处理器置 true，MessageFlow 将跳过后续 OnBefore 与整个核心阶段，但仍按注册倒序执行已经过的所有处理器（含本处理器）的 OnAfter，便于资源回收/扣减回滚</summary>
    Boolean Cancel { get; set; }

    /// <summary>取消代码。短路时填写，便于客户端识别（如 quota_exceeded、content_blocked 等）</summary>
    String? CancelCode { get; set; }

    /// <summary>取消消息。短路时填写，将作为错误事件文本回写给客户端</summary>
    String? CancelMessage { get; set; }

    #endregion
}
