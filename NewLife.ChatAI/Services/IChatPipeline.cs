using NewLife.AI.Models;
using NewLife.Data;
using NewLife.ChatAI.Entity;
using NewLife.ChatAI.Models;
using AiChatMessage = NewLife.AI.Models.ChatMessage;
using ChatResponse = NewLife.AI.Models.ChatResponse;
using ChatStreamEvent = NewLife.AI.Models.ChatStreamEvent;

namespace NewLife.ChatAI.Services;

/// <summary>对话执行管道上下文。携带单次请求所需的用户与会话上下文</summary>
public class ChatPipelineContext : IExtend
{
    /// <summary>用户编号</summary>
    public String? UserId { get; set; }

    /// <summary>会话编号</summary>
    public String? ConversationId { get; set; }

    /// <summary>消息中 @ToolName 显式引用的工具名称集合。由 SkillService 解析消息后填充，DbToolProvider 据此过滤非系统工具</summary>
    public ISet<String> SelectedTools { get; } = new HashSet<String>(StringComparer.OrdinalIgnoreCase);

    /// <summary>请求级扩展参数。由 <see cref="SendMessageRequest.Options"/> 传入，最终通过 ChatOptions.Items 注入服务商。
    /// 支持 DashScope 专属参数，如 EnableSearch / SearchStrategy / ThinkingBudget / TopK 等</summary>
    public IDictionary<String, Object?> Items { get; set; } = new Dictionary<String, Object?>();

    /// <summary>索引器，方便访问扩展数据</summary>
    public Object? this[String key] { get => Items.TryGetValue(key, out var v) ? v : null; set => Items[key] = value; }
}

/// <summary>对话执行管道。封装能力扩展层（工具调用、技能注入）与知识进化层（记忆注入、自学习、事件智能体），对外向对话内核层提供统一执行接口</summary>
/// <remarks>
/// 典型实现在外部（DI 注册时）组装好三层能力，内核层 <see cref="ChatApplicationService"/> 通过本接口驱动执行，无需感知各层细节。
/// <code>
/// // DI 注册（ChatAIExtensions.cs）
/// services.AddSingleton&lt;IChatPipeline, ChatAIPipeline&gt;();
/// </code>
/// </remarks>
public interface IChatPipeline
{
    /// <summary>流式执行对话。依次经过能力扩展层（技能注入、工具调用）和知识进化层（记忆注入、自学习触发）</summary>
    /// <param name="contextMessages">已构建好的上下文消息列表（含历史消息；技能系统消息由管道注入）</param>
    /// <param name="modelConfig">目标模型配置</param>
    /// <param name="thinkingMode">思考模式</param>
    /// <param name="context">管道执行上下文（UserId / ConversationId / SkillId）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>统一 ChatAI 事件流（content_delta / thinking_delta / tool_call_* / message_done / error）</returns>
    IAsyncEnumerable<ChatStreamEvent> StreamAsync(
        IList<AiChatMessage> contextMessages,
        ModelConfig modelConfig,
        ThinkingMode thinkingMode,
        ChatPipelineContext context,
        CancellationToken cancellationToken);

    /// <summary>非流式执行对话。用于重新生成等非 SSE 场景</summary>
    /// <param name="contextMessages">已构建好的上下文消息列表</param>
    /// <param name="modelConfig">目标模型配置</param>
    /// <param name="context">管道执行上下文（UserId / ConversationId / SkillId）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>完整的对话响应</returns>
    Task<ChatResponse> CompleteAsync(
        IList<AiChatMessage> contextMessages,
        ModelConfig modelConfig,
        ChatPipelineContext context,
        CancellationToken cancellationToken);
}
