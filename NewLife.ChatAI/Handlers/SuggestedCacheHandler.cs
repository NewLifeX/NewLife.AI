using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using NewLife;
using NewLife.Caching;
using NewLife.Log;
using NewLife.Serialization;

namespace NewLife.ChatAI.Handlers;

/// <summary>推荐问题缓存处理器。同时实现 <see cref="IChatHandler"/>（事前匹配 + 事后回写）（核心阶段命中时短路回放缓存内容）</summary>
/// <remarks>
/// <para>事前 (<see cref="OnBefore"/>)：精确匹配当天缓存。命中则写入 <c>Items["SuggestedHit"]</c> 标记。</para>
/// <para>核心 (<see cref="InvokeAsync"/>)：检查标记。命中则插入 assistant 消息、流式回放缓存内容（按 StreamingSpeed 节流）；
/// 未命中则透传给下游（最终 LLM 调用）。</para>
/// <para>事后 (<see cref="OnAfter"/>)：已在推荐列表的问题回写本次回复；不在列表的问题追踪热度，1小时内被2个不同会话提问则自动晋升。</para>
/// </remarks>
/// <param name="setting">对话配置</param>
/// <param name="cacheProvider">缓存提供者，用于热门问题统计</param>
[ChatHandlerOrder(10)]
public class SuggestedCacheHandler(IChatSetting setting, ICacheProvider cacheProvider) : IChatHandler, IChatHandlerScope
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
        if (!setting.EnableSuggestedQuestionCache) return Task.CompletedTask;
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

        var streamingSpeed = setting.StreamingSpeed;

        // 1. 思考过程
        if (!sourceMsg.ThinkingContent.IsNullOrEmpty())
        {
            if (streamingSpeed > 5)
            {
                yield return new ChatStreamEvent { Type = "thinking_delta", Content = sourceMsg.ThinkingContent };
            }
            else
            {
                var (tChunkSize, tDelayMs) = GetCachedStreamingParams(streamingSpeed);
                await foreach (var chunk in ThrottleTextAsync(sourceMsg.ThinkingContent!, tChunkSize, tDelayMs, cancellationToken))
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
                    yield return new ChatStreamEvent { Type = "tool_call_done", ToolCallId = tc.Id, Name = tc.Name, Result = tc.Result, Success = tc.Status != ToolCallStatus.Error };
                }
            }
        }

        // 3. 正文内容
        if (streamingSpeed > 5)
        {
            yield return new ChatStreamEvent { Type = "content_delta", Content = sourceMsg.Content };
        }
        else
        {
            var (chunkSize, delayMs) = GetCachedStreamingParams(streamingSpeed);
            await foreach (var chunk in ThrottleTextAsync(sourceMsg.Content ?? String.Empty, chunkSize, delayMs, cancellationToken))
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
        if (sq != null)
        {
            // 已在推荐列表：回写本次生成的助手消息 Id 到缓存
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

        // 不在推荐列表：追踪热度，1小时内被2个不同会话提问则自动晋升
        if (question.Length > 200) return Task.CompletedTask;

        var conversationId = context.Conversation?.Id ?? 0;
        if (conversationId <= 0) return Task.CompletedTask;

        var key = $"ai:hotq:{question}";
        var convIds = cacheProvider.Cache.Get<List<Int64>>(key) ?? [];
        if (convIds.Contains(conversationId)) return Task.CompletedTask;

        convIds.Add(conversationId);
        cacheProvider.Cache.Set(key, convIds, TimeSpan.FromHours(1));

        if (convIds.Count >= 2)
            _ = Task.Run(() => TryPromoteQuestion(question));

        return Task.CompletedTask;
    }

    /// <summary>尝试将热门问题插入推荐问题列表（二次检查防并发重复插入）</summary>
    /// <param name="question">问题全文</param>
    private static void TryPromoteQuestion(String question)
    {
        if (SuggestedQuestion.FindCachedByQuestion(question) != null) return;

        var title = question.Length > 20 ? question[..20] : question;
        var entity = new SuggestedQuestion
        {
            Title = title,
            Question = question,
            Enable = true,
        };
        entity.Insert(); // Valid() 自动补全 Icon 与 Color
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
