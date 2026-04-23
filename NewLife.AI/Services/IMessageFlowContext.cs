using NewLife.AI.Interfaces;
using NewLife.AI.Models;
using NewLife.Data;

namespace NewLife.AI.Services;

/// <summary>消息流上下文契约。NewLife.AI 通用编排层通过此接口操作消息流上下文，
/// 与具体实现（ChatAI/StarChat 的 MessageFlowContext 派生类）解耦</summary>
/// <remarks>
/// <para>本接口暴露 <see cref="IContextEnricher"/> 与 <see cref="IMessageFlowPostProcessor"/> 在跨边界使用时所需的最小成员集；
/// 派生项目内部仍可通过具体类型访问完整字段（如 ContentBuilder/ThinkingBuilder/ToolCalls 等）。</para>
/// <para>实体引用统一通过 I 前缀接口（IConversation/IChatMessage/IModelConfig）暴露，
/// 避免 NewLife.AI 直接耦合具体实体类型。</para>
/// <para>稀少入口参数通过 <see cref="IExtend"/> 的 Items 字典携带，勿挂到接口属性：
/// <c>Items["SkillName"]</c>（技能名称）、<c>Items["NewUserContent"]</c>（编辑重发时的新用户消息内容）。</para>
/// </remarks>
public interface IMessageFlowContext : IExtend
{
    /// <summary>入口类型。区分 Stream/Regenerate/RegenerateStream/EditAndResendStream/ResumeBackground</summary>
    FlowKind Kind { get; }

    /// <summary>当前用户编号</summary>
    Int32 UserId { get; }

    /// <summary>技能编号（0 表示无技能）</summary>
    Int32 SkillId { get; }

    /// <summary>会话实体（接口形式）</summary>
    IConversation Conversation { get; }

    /// <summary>模型配置（接口形式）</summary>
    IModelConfig ModelConfig { get; }

    /// <summary>用户消息（StreamMessage/EditResend 场景有值；Regenerate 场景可能为 null）</summary>
    IChatMessage? UserMessage { get; }

    /// <summary>AI 回复消息（新建或复用原消息）</summary>
    IChatMessage AssistantMessage { get; }

    /// <summary>对话上下文消息列表。Prepare 阶段由 BuildContextAsync 填充，
    /// IContextEnricher 可在此基础上追加/修改/截断，管道执行时据此发起模型调用</summary>
    IList<ChatMessage> ContextMessages { get; set; }

    /// <summary>是否出现可恢复错误。Persist 阶段会据此调整落库策略</summary>
    Boolean HasError { get; }
}
