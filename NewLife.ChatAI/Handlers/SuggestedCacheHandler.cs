using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using NewLife;
using NewLife.Log;
using NewLife.Serialization;

namespace NewLife.ChatAI.Handlers;

/// <summary>推荐问题缓存处理器。同时实现 <see cref="IChatHandler"/>（事前匹配 + 事后回写）（核心阶段命中时短路回放缓存内容）</summary>
/// <remarks>
/// <para>事前 (<see cref="OnBefore"/>)：精确匹配当天缓存。命中则写入 <c>Items["SuggestedHit"]</c> 标记。</para>
/// <para>核心 (<see cref="InvokeAsync"/>)：检查标记。命中则插入 assistant 消息、流式回放缓存内容（固定节流速度）；
/// 未命中则透传给下游（最终 LLM 调用）。</para>
/// <para>事后 (<see cref="OnAfter"/>)：仅在推荐列表中的问题回写本次回复，供下次命中回放缓存；推荐问题由人工维护。</para>
/// </remarks>
[ChatHandlerOrder(10)]
public class SuggestedCacheHandler : IChatHandler, IChatHandlerScope
{
    private const String HitKey = "SuggestedHit";

    /// <inheritdoc/>
    public ChatHandlerCapabilities Capabilities => ChatHandlerCapabilities.Before | ChatHandlerCapabilities.After | ChatHandlerCapabilities.Interceptor;

    /// <inheritdoc/>
    /// <remarks>推荐问题缓存以 ConversationId 为 key，无持久化会话的渠道/网关场景无意义，仅在 Web 来源启用</remarks>
    public ChatFlowSource SupportedSources => ChatFlowSource.Web;

    /// <inheritdoc/>
    public ChatHandlerTier Tier => ChatHandlerTier.Full;

    /// <inheritdoc/>
    public Task OnBefore(IChatContext context, CancellationToken cancellationToken)
    {
        var content = context.UserMessage?.Content;
        if (content.IsNullOrEmpty()) return Task.CompletedTask;

        var cached = SuggestedQuestion.FindCachedTodayByQuestion(content);
        if (cached != null)
        {
            // 跳过后续处理器，直接回放缓存内容
            context.FlowControl = ChatFlowControl.SkipRemaining;
            context[HitKey] = cached;
            DefaultSpan.Current?.AppendTag(cached.Title!);
            // fire-and-forget：记录命中，更新热度分数，不阻塞主流程
            _ = Task.Run(() => cached.RecordHit());
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<ChatStreamEvent> InvokeAsync(IChatContext context, ChatNextDelegate next, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (context[HitKey] is not SuggestedQuestion cached)
        {
            await foreach (var ev in next(cancellationToken).ConfigureAwait(false))
                yield return ev;
            yield break;
        }

        // 命中：从关联的助手消息读取并全量回放（含工具调用）
        var sourceMsg = DbChatMessage.FindById(cached.MessageId);
        if (sourceMsg == null)
        {
            // 关联消息已丢失，降级走正常 LLM 路径
            await foreach (var ev in next(cancellationToken).ConfigureAwait(false))
                yield return ev;
            yield break;
        }

        if (context.AssistantMessage is not DbChatMessage msg)
        {
            msg = new DbChatMessage { Role = "assistant", Enable = true };
        }
        msg.ConversationId = context.Conversation.Id;
        msg.Content = sourceMsg.Content;
        msg.ThinkingContent = sourceMsg.ThinkingContent;
        msg.ToolCalls = sourceMsg.ToolCalls;
        msg.Save();
        context.AssistantMessage = msg;

        // 1. 思考过程
        if (!sourceMsg.ThinkingContent.IsNullOrEmpty())
        {
            var thinking = sourceMsg.ThinkingContent;
#if STARCHAT
            // STARCHAT：思考内容压缩存储（NLBR: 前缀），回放给前端前还原明文
            thinking = sourceMsg.GetThinking();
#endif
            if (!thinking.IsNullOrEmpty())
            {
                await foreach (var chunk in ThrottleTextAsync(thinking!, CachedChunkSize, CachedDelayMs, cancellationToken))
                    yield return new ChatStreamEvent { Type = "thinking_delta", Content = chunk };
            }
        }

        // 2. 工具调用
        if (!sourceMsg.ToolCalls.IsNullOrEmpty())
        {
            var toolCallDtos = sourceMsg.ToolCalls.ToJsonEntity<List<ToolCallDto>>();
            if (toolCallDtos != null)
            {
                foreach (var tc in toolCallDtos)
                {
                    yield return new ChatStreamEvent { Type = "tool_call_start", ToolCallId = tc.Id, Name = tc.Name, Arguments = tc.Arguments };
                    yield return new ChatStreamEvent { Type = "tool_call_done", ToolCallId = tc.Id, Name = tc.Name, Result = tc.Result };
                }
            }
        }

        // 3. 正文内容
        await foreach (var chunk in ThrottleTextAsync(sourceMsg.Content ?? String.Empty, CachedChunkSize, CachedDelayMs, cancellationToken))
            yield return new ChatStreamEvent { Type = "content_delta", Content = chunk };

        yield return new ChatStreamEvent { Type = "message_done" };
    }

    /// <inheritdoc/>
    public Task OnAfter(IChatContext context, CancellationToken cancellationToken)
    {
        if (context.HasError || context.ContentBuilder.Length == 0) return Task.CompletedTask;
        if (context[HitKey] is SuggestedQuestion) return Task.CompletedTask; // 命中场景不回写

        var question = context.UserMessage?.Content;
        if (question.IsNullOrEmpty()) return Task.CompletedTask;

        // 仅在推荐列表中的问题回写本次回复，供下次命中回放缓存（推荐问题由人工维护，不自动晋升）
        var sq = SuggestedQuestion.FindCachedByQuestion(question);
        if (sq == null) return Task.CompletedTask;

        if (context.AssistantMessage is DbChatMessage savedMsg)
        {
            sq.ConversationId = savedMsg.ConversationId;
            sq.MessageId = savedMsg.Id;
            sq.Update();
        }
        // fire-and-forget：记录命中，更新热度分数，不阻塞主流程
        _ = Task.Run(() => sq.RecordHit());
        return Task.CompletedTask;
    }

    /// <summary>缓存回放分块大小。固定速度 4 档：每块字符数</summary>
    private const Int32 CachedChunkSize = 14;

    /// <summary>缓存回放块间延迟（毫秒）。固定速度 4 档，兼顾快速输出与打字机效果</summary>
    private const Int32 CachedDelayMs = 16;

    private static async IAsyncEnumerable<String> ThrottleTextAsync(String text, Int32 chunkSize, Int32 delayMs, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (text.IsNullOrEmpty()) yield break;

        var enumerator = StringInfo.GetTextElementEnumerator(text);
        var buf = new StringBuilder(chunkSize * 4);
        var count = 0;
        while (enumerator.MoveNext())
        {
            buf.Append(enumerator.GetTextElement());
            count++;
            if (count >= chunkSize)
            {
                yield return buf.ToString();
                buf.Clear();
                count = 0;
                await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
            }
        }
        if (buf.Length > 0) yield return buf.ToString();
    }
}
