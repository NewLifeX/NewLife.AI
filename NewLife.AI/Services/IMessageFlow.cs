using NewLife.AI.Models;

namespace NewLife.AI.Services;

/// <summary>消息生成流程契约。统一对外暴露 4 大入口（流式发送 / 重生成 / 流式重生成 / 编辑重发），
/// 以及辅助操作（停止生成 / 自动生成标题）。派生项目实现具体的实体绑定与持久化</summary>
/// <remarks>
/// <para>四段式入口由 <see cref="FlowKind"/> 区分，所有方法默认绑定到当前用户上下文。</para>
/// <para>实现类应保证：</para>
/// <list type="bullet">
/// <item><description><b>流式方法</b>：以 <see cref="ChatStreamEvent"/> 序列形式回传，包含开始 / 增量 / 完成 / 错误事件</description></item>
/// <item><description><b>非流式重生成</b>：返回最终 <see cref="MessageDto"/>；失败可返回 null 或抛出异常</description></item>
/// <item><description><b>停止生成</b>：通过取消令牌或后台任务标记，幂等</description></item>
/// </list>
/// </remarks>
public interface IMessageFlow
{
    /// <summary>流式发送新消息。用户发起新轮对话的主入口</summary>
    /// <param name="conversationId">会话编号</param>
    /// <param name="request">消息请求</param>
    /// <param name="userId">当前用户编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>流式事件序列</returns>
    IAsyncEnumerable<ChatStreamEvent> StreamMessageAsync(Int64 conversationId, SendMessageRequest request, Int32 userId, CancellationToken cancellationToken);

    /// <summary>非流式重生成。一次性返回完整结果</summary>
    /// <param name="messageId">待重生成的助手消息编号</param>
    /// <param name="userId">当前用户编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>新生成的消息 DTO，失败返回 null</returns>
    Task<MessageDto?> RegenerateMessageAsync(Int64 messageId, Int32 userId, CancellationToken cancellationToken);

    /// <summary>流式重生成助手消息</summary>
    /// <param name="messageId">待重生成的助手消息编号</param>
    /// <param name="userId">当前用户编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>流式事件序列</returns>
    IAsyncEnumerable<ChatStreamEvent> RegenerateStreamAsync(Int64 messageId, Int32 userId, CancellationToken cancellationToken);

    /// <summary>编辑用户消息后流式重发</summary>
    /// <param name="messageId">待编辑的用户消息编号</param>
    /// <param name="newContent">新内容</param>
    /// <param name="userId">当前用户编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>流式事件序列</returns>
    IAsyncEnumerable<ChatStreamEvent> EditAndResendStreamAsync(Int64 messageId, String newContent, Int32 userId, CancellationToken cancellationToken);

    /// <summary>停止指定消息的生成</summary>
    /// <param name="messageId">消息编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步任务</returns>
    Task StopGenerateAsync(Int64 messageId, CancellationToken cancellationToken);

    /// <summary>根据用户消息自动生成会话标题</summary>
    /// <param name="conversationId">会话编号</param>
    /// <param name="userMessage">用户消息内容</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>生成的标题，失败返回 null</returns>
    Task<String?> GenerateTitleAsync(Int64 conversationId, String userMessage, CancellationToken cancellationToken);
}
