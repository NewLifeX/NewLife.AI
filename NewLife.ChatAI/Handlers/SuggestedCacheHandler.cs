using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using NewLife.Log;

namespace NewLife.ChatAI.Handlers;

/// <summary>推荐问题缓存处理器。同时实现 <see cref="IChatHandler"/>（事前匹配 + 事后回写）和
/// <see cref="IChatInterceptor"/>（核心阶段命中时短路回放缓存内容）</summary>
/// <remarks>
/// <para>事前 (<see cref="OnBefore"/>)：精确匹配当天缓存。命中则写入 <c>Items["SuggestedHit"]</c> 标记。</para>
/// <para>核心 (<see cref="InvokeAsync"/>)：检查标记。命中则插入 assistant 消息、流式回放缓存内容（按 StreamingSpeed 节流）；
/// 未命中则透传给下游（最终 LLM 调用）。</para>
/// <para>事后 (<see cref="OnAfter"/>)：未命中且生成成功时回写本次回复到缓存。</para>
/// </remarks>
/// <param name="setting">对话配置</param>
/// <param name="tracer">追踪器</param>
public class SuggestedCacheHandler(IChatSetting setting, ITracer? tracer) : IChatHandler, IChatInterceptor
{
    private const String HitKey = "SuggestedHit";

    /// <inheritdoc/>
    public Task OnBefore(IChatContext context, CancellationToken cancellationToken)
    {
        if (!setting.EnableSuggestedQuestionCache) return Task.CompletedTask;
        var content = context.UserMessage?.Content;
        if (content.IsNullOrEmpty()) return Task.CompletedTask;

        var cached = SuggestedQuestion.FindCachedTodayByQuestion(content);
        if (cached != null) context[HitKey] = cached;
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

        // 命中：直接回放
        using var span = tracer?.NewSpan("handler:SuggestedCache", cached.Question);

        if (context is MessageFlowContext flow)
        {
            var cachedMsg = new DbChatMessage
            {
                ConversationId = flow.Conversation.Id,
                Role = "assistant",
                Content = cached.Response,
                ThinkingContent = cached.ThinkingResponse,
            };
            cachedMsg.Insert();
            flow.AssistantMessage = cachedMsg;
        }

        var streamingSpeed = setting.StreamingSpeed;

        if (!cached.ThinkingResponse.IsNullOrEmpty())
        {
            if (streamingSpeed > 5)
            {
                yield return new ChatStreamEvent { Type = "thinking_delta", Content = cached.ThinkingResponse };
            }
            else
            {
                var (tChunkSize, tDelayMs) = GetCachedStreamingParams(streamingSpeed);
                await foreach (var chunk in ThrottleTextAsync(cached.ThinkingResponse!, tChunkSize, tDelayMs, cancellationToken))
                    yield return new ChatStreamEvent { Type = "thinking_delta", Content = chunk };
            }
        }

        if (streamingSpeed > 5)
        {
            yield return new ChatStreamEvent { Type = "content_delta", Content = cached.Response };
        }
        else
        {
            var (chunkSize, delayMs) = GetCachedStreamingParams(streamingSpeed);
            await foreach (var chunk in ThrottleTextAsync(cached.Response ?? String.Empty, chunkSize, delayMs, cancellationToken))
                yield return new ChatStreamEvent { Type = "content_delta", Content = chunk };
        }

        yield return new ChatStreamEvent { Type = "message_done" };
    }

    /// <inheritdoc/>
    public Task OnAfter(IChatContext context, CancellationToken cancellationToken)
    {
        if (!setting.EnableSuggestedQuestionCache) return Task.CompletedTask;
        if (context.HasError || context.ContentBuilder.Length == 0) return Task.CompletedTask;
        if (context[HitKey] is SuggestedQuestion) return Task.CompletedTask; // 命中场景不回写

        var question = context.UserMessage?.Content;
        if (question.IsNullOrEmpty()) return Task.CompletedTask;

        var sq = SuggestedQuestion.FindCachedByQuestion(question);
        if (sq == null) return Task.CompletedTask;

        sq.Response = context.ContentBuilder.ToString();
        sq.ThinkingResponse = context.ThinkingBuilder.ToString();
        sq.ModelId = context.ModelConfig.Id;
        sq.Update();

        return Task.CompletedTask;
    }

    private static (Int32 ChunkSize, Int32 DelayMs) GetCachedStreamingParams(Int32 speed) => speed switch
    {
        1 => (4, 60),
        2 => (6, 30),
        4 => (14, 16),
        5 => (24, 10),
        _ => (10, 20),
    };

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
