using System.Runtime.CompilerServices;
using NewLife.AI.Models;
using NewLife.AI.Services;
using NewLife.Log;

namespace NewLife.ChatAI.Handlers;

/// <summary>消息持久化处理器。事中收集流事件到 <see cref="IChatContext"/> 各 Builder 与 Usage 字段，
/// 事后由消息流入口统一写库。本 Handler 一般置于链路靠外位置（事前最先、事后最后），与 <see cref="LlmCoreHandler"/> 配合</summary>
/// <remarks>
/// <para>事中职责（透传 next 时）：
/// <list type="bullet">
///   <item><c>thinking_delta</c> → <see cref="IChatContext.ThinkingBuilder"/></item>
///   <item><c>content_delta</c> → <see cref="IChatContext.ContentBuilder"/> 并触发 ArtifactDetector</item>
///   <item><c>message_done</c> → <see cref="IChatContext.Usage"/></item>
///   <item><c>tool_call_*</c> → <see cref="IChatContext.ToolCalls"/></item>
///   <item><c>error</c> → 设置 <see cref="IChatContext.HasError"/>，停止迭代</item>
/// </list>
/// </para>
/// <para>事后职责：当前留空。Step 5 时将 MessageFlow.PersistStreamResult 的 XCode 持久化逻辑迁移过来。</para>
/// </remarks>
/// <param name="tracer">追踪器</param>
/// <param name="log">日志</param>
public class PersistMessageHandler(ITracer tracer, ILog log) : IChatHandler
{
    /// <inheritdoc/>
    public async IAsyncEnumerable<ChatStreamEvent> InvokeAsync(IChatContext context, ChatHandlerDelegate next, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var span = tracer?.NewSpan("handler:PersistMessage");

        var contentBuilder = context.ContentBuilder;
        var thinkingBuilder = context.ThinkingBuilder;
        var toolCalls = context.ToolCalls;
        var artifactDetector = new ArtifactDetector();

        var enumerator = next(cancellationToken).GetAsyncEnumerator(cancellationToken);
        try
        {
            while (true)
            {
                Boolean moved;
                ChatStreamEvent? errorEvent = null;
                try
                {
                    moved = await enumerator.MoveNextAsync().ConfigureAwait(false);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    log?.Error("流式生成失败: {0}", ex.Message);
                    context.HasError = true;
                    errorEvent = ChatStreamEvent.ErrorEvent("STREAM_ERROR", ex.Message);
                    moved = false;
                }

                if (errorEvent != null)
                {
                    yield return errorEvent;
                    break;
                }
                if (!moved) break;

                var ev = enumerator.Current;
                switch (ev.Type)
                {
                    case "thinking_delta":
                        thinkingBuilder.Append(ev.Content);
                        yield return ev;
                        break;
                    case "content_delta":
                        contentBuilder.Append(ev.Content);

                        // Artifact 检测：识别可预览代码块（html/svg/mermaid）
                        var artifactEvents = artifactDetector.Process(ev.Content!);
                        foreach (var ae in artifactEvents)
                        {
                            switch (ae.Kind)
                            {
                                case ArtifactEventKind.ArtifactStart:
                                    yield return ChatStreamEvent.ArtifactStart(ae.Language!);
                                    break;
                                case ArtifactEventKind.ArtifactDelta:
                                    yield return ChatStreamEvent.ArtifactDelta(ae.Content!);
                                    break;
                                case ArtifactEventKind.ArtifactEnd:
                                    yield return ChatStreamEvent.ArtifactEnd();
                                    break;
                            }
                        }

                        yield return ev;
                        break;
                    case "message_done":
                        context.Usage = ev.Usage;
                        yield return ev;
                        break;
                    case "error":
                        context.HasError = true;
                        yield return ev;
                        break;
                    case "tool_call_start":
                        toolCalls.Add(new ToolCallDto(ev.ToolCallId + "", ev.Name + "", ToolCallStatus.Calling, ev.Arguments));
                        yield return ev;
                        break;
                    case "tool_call_done":
                        UpdateToolCallStatus(toolCalls, ev.ToolCallId, ToolCallStatus.Done, ev.Result);
                        yield return ev;
                        break;
                    case "tool_call_error":
                        UpdateToolCallStatus(toolCalls, ev.ToolCallId, ToolCallStatus.Error, ev.Error);
                        yield return ev;
                        break;
                    default:
                        yield return ev;
                        break;
                }

                if (context.HasError) break;
            }
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
        }

        // Step 5 待迁移：MessageFlow.PersistStreamResult 中的 XCode 写库逻辑
    }

    /// <summary>更新工具调用列表中指定 id 的状态与结果</summary>
    /// <param name="collector">工具调用收集器</param>
    /// <param name="toolCallId">工具调用编号</param>
    /// <param name="status">新状态</param>
    /// <param name="value">结果或错误信息</param>
    private static void UpdateToolCallStatus(List<ToolCallDto> collector, String? toolCallId, ToolCallStatus status, String? value)
    {
        for (var i = collector.Count - 1; i >= 0; i--)
        {
            if (collector[i].Id == toolCallId)
            {
                var orig = collector[i];
                collector[i] = new ToolCallDto(orig.Id, orig.Name, status, orig.Arguments, value);
                break;
            }
        }
    }
}
