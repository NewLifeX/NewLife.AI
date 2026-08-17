using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NewLife.AI.Clients;
using NewLife.AI.Models;
using NewLife.AI.Services;
using Xunit;

namespace XUnitTest.Services;

/// <summary>轻量 AI 对话编排服务 <see cref="AiChatService"/> 单元测试</summary>
[DisplayName("AiChatService 轻量对话编排测试")]
public class AiChatServiceTests
{
    // ── 辅助：可编程假客户端 ────────────────────────────────────────────────

    /// <summary>可编程假客户端。支持流式/非流式、捕获请求消息、抛出异常</summary>
    private sealed class CapturingChatClient : IChatClient
    {
        /// <summary>收到的请求列表（含工具循环内每轮请求）</summary>
        public List<IChatRequest> Requests { get; } = [];

        /// <summary>流式分块队列。为空时用 <see cref="NonStreamResponse"/> 单块</summary>
        public Queue<IChatResponse> StreamChunks { get; } = new();

        /// <summary>非流式响应工厂；为 null 时返回空响应</summary>
        public Func<IChatRequest, IChatResponse>? NonStreamResponse { get; set; }

        /// <summary>抛出异常（流式与非流式均触发）</summary>
        public Exception? Throw { get; set; }

        public Task<IChatResponse> GetResponseAsync(IChatRequest request, CancellationToken cancellationToken = default)
        {
            if (Throw != null) throw Throw;
            Requests.Add(request);
            return Task.FromResult(NonStreamResponse?.Invoke(request) ?? new ChatResponse());
        }

        public async IAsyncEnumerable<IChatResponse> GetStreamingResponseAsync(
            IChatRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (Throw != null) throw Throw;
            Requests.Add(request);

            if (StreamChunks.Count > 0)
            {
                while (StreamChunks.Count > 0)
                {
                    yield return StreamChunks.Dequeue();
                }
            }
            else
            {
                yield return NonStreamResponse?.Invoke(request) ?? new ChatResponse();
            }
        }

        public void Dispose() { }
    }

    /// <summary>构造文本增量块</summary>
    private static IChatResponse TextChunk(String text) => new ChatResponse
    {
        Messages = [new ChatChoice { Delta = new ChatMessage { Content = text } }],
    };

    /// <summary>构造思考增量块</summary>
    private static IChatResponse ThinkingChunk(String text) => new ChatResponse
    {
        Messages = [new ChatChoice { Delta = new ChatMessage { ReasoningContent = text } }],
    };

    /// <summary>构造工具事件块</summary>
    private static IChatResponse ToolChunk(String type, String id, String name, String value) => new ChatResponse
    {
        ToolCallEvents = [new ToolCallEventInfo(type, id, name, value)],
    };

    /// <summary>执行流式对话并收集全部事件</summary>
    private static async Task<List<ChatStreamEvent>> RunAsync(AiChatService ai, String sessionId, String message, String systemPrompt = "你是助手")
    {
        var req = new AiChatRequest { SessionId = sessionId, Message = message, Stream = true };
        var list = new List<ChatStreamEvent>();
        await foreach (var ev in ai.ChatAsync(req, systemPrompt))
        {
            list.Add(ev);
        }
        return list;
    }

    // ── 基础事件序列 ───────────────────────────────────────────────────────

    [Fact]
    [DisplayName("流式对话—文本增量产出 message_start/content_delta/message_done 序列")]
    public async Task ChatAsync_Stream_EmitsTextEvents()
    {
        var client = new CapturingChatClient();
        client.StreamChunks.Enqueue(TextChunk("你"));
        client.StreamChunks.Enqueue(TextChunk("好"));

        var ai = new AiChatService(client);
        var events = await RunAsync(ai, null, "你好");

        Assert.Equal("message_start", events[0].Type);
        Assert.Contains(events, e => e.Type == "message_start");
        Assert.Contains(events, e => e.Type == "content_delta" && e.Content == "你");
        Assert.Contains(events, e => e.Type == "content_delta" && e.Content == "好");
        Assert.Equal("message_done", events[^1].Type);
    }

    [Fact]
    [DisplayName("流式对话—思考增量产出 thinking_delta 事件")]
    public async Task ChatAsync_Stream_EmitsThinkingEvent()
    {
        var client = new CapturingChatClient();
        client.StreamChunks.Enqueue(ThinkingChunk("我先想想"));
        client.StreamChunks.Enqueue(TextChunk("结论"));

        var ai = new AiChatService(client);
        var events = await RunAsync(ai, null, "分析一下");

        Assert.Contains(events, e => e.Type == "thinking_delta" && e.Content == "我先想想");
        Assert.Contains(events, e => e.Type == "content_delta" && e.Content == "结论");
    }

    [Fact]
    [DisplayName("非流式对话—单块返回完整文本与完成原因")]
    public async Task ChatAsync_NonStream_EmitsFullText()
    {
        var client = new CapturingChatClient
        {
            NonStreamResponse = _ => { var r = new ChatResponse(); r.Add("完整回复", finishReason: FinishReason.Stop); return r; },
        };

        var ai = new AiChatService(client);
        var req = new AiChatRequest { Message = "问题", Stream = false };
        var events = new List<ChatStreamEvent>();
        await foreach (var ev in ai.ChatAsync(req, "你是助手"))
        {
            events.Add(ev);
        }

        Assert.Contains(events, e => e.Type == "content_delta" && e.Content == "完整回复");
        Assert.Equal("message_done", events[^1].Type);
        // 非流式路径与流式一致，message_done 携带真实 finish_reason
        Assert.Equal("stop", events[^1].FinishReason);
    }

    // ── 工具事件 ──────────────────────────────────────────────────────────

    [Fact]
    [DisplayName("流式对话—工具事件映射为 tool_call_start/done（done 补工具名）")]
    public async Task ChatAsync_ToolEvents_ProjectedToStreamEvents()
    {
        var client = new CapturingChatClient();
        client.StreamChunks.Enqueue(ToolChunk("start", "t1", "query_data", "{\"id\":1}"));
        client.StreamChunks.Enqueue(ToolChunk("done", "t1", "query_data", "{\"ok\":true}"));
        client.StreamChunks.Enqueue(TextChunk("已查询"));

        var ai = new AiChatService(client);
        var events = await RunAsync(ai, null, "查一下");

        var start = events.First(e => e.Type == "tool_call_start");
        Assert.Equal("t1", start.ToolCallId);
        Assert.Equal("query_data", start.Name);
        Assert.Equal("{\"id\":1}", start.Arguments);

        var done = events.First(e => e.Type == "tool_call_done");
        Assert.Equal("t1", done.ToolCallId);
        Assert.Equal("query_data", done.Name);   // done 补工具名，供投影器还原
        Assert.Equal("{\"ok\":true}", done.Result);
    }

    // ── 错误与兜底 ────────────────────────────────────────────────────────

    [Fact]
    [DisplayName("对话异常—产出 error 事件（STREAM_ERROR）")]
    public async Task ChatAsync_Error_EmitsErrorEvent()
    {
        var client = new CapturingChatClient { Throw = new InvalidOperationException("模型调用失败") };

        var ai = new AiChatService(client);
        var events = await RunAsync(ai, null, "你好");

        var error = events.FirstOrDefault(e => e.Type == "error");
        Assert.NotNull(error);
        Assert.Equal("STREAM_ERROR", error!.Code);
        Assert.Contains("模型调用失败", error.Message);
    }

    [Fact]
    [DisplayName("上下文超限—映射为 CONTEXT_TOO_LONG 友好错误")]
    public async Task ChatAsync_ContextTooLong_FriendlyError()
    {
        var client = new CapturingChatClient { Throw = new InvalidOperationException("context_length_exceeded") };

        var ai = new AiChatService(client);
        var events = await RunAsync(ai, null, "超长问题");

        var error = events.FirstOrDefault(e => e.Type == "error");
        Assert.NotNull(error);
        Assert.Equal("CONTEXT_TOO_LONG", error!.Code);
    }

    [Fact]
    [DisplayName("空响应—产出兜底提示而非静默结束")]
    public async Task ChatAsync_EmptyResponse_FallbackNote()
    {
        var client = new CapturingChatClient();
        client.StreamChunks.Enqueue(new ChatResponse());  // 空块

        var ai = new AiChatService(client);
        var events = await RunAsync(ai, null, "你好");

        var fallback = events.FirstOrDefault(e => e.Type == "content_delta" && (e.Content ?? "").Contains("AI 未返回有效结果"));
        Assert.NotNull(fallback);
        Assert.Equal("message_done", events[^1].Type);
    }

    // ── 会话历史 ──────────────────────────────────────────────────────────

    [Fact]
    [DisplayName("会话历史—同一会话第二轮请求携带首轮 user+assistant")]
    public async Task ChatAsync_WithSession_PersistsHistory()
    {
        var client = new CapturingChatClient();
        client.StreamChunks.Enqueue(TextChunk("你好回复"));

        var ai = new AiChatService(client);

        await RunAsync(ai, "s1", "第一条");
        var second = await RunAsync(ai, "s1", "第二条");

        Assert.Equal("message_done", second[^1].Type);

        // 第二次请求的消息应包含：system + user(第一条) + assistant(你好回复) + user(第二条)
        var lastRequest = client.Requests[^1];
        var roles = lastRequest.Messages.Select(m => m.Role).ToList();
        var contents = lastRequest.Messages.Select(m => m.Content + "").ToList();

        Assert.Contains("system", roles);
        Assert.Contains("第一条", contents);
        Assert.Contains("你好回复", contents);
        Assert.Equal("第二条", contents[^1]);
        Assert.Equal(4, lastRequest.Messages.Count);
    }

    [Fact]
    [DisplayName("会话历史—无会话编号时不保留历史（每轮仅 system+user）")]
    public async Task ChatAsync_NoSession_NoHistory()
    {
        var client = new CapturingChatClient();
        client.StreamChunks.Enqueue(TextChunk("回复"));

        var ai = new AiChatService(client);

        await RunAsync(ai, null, "第一问");
        await RunAsync(ai, null, "第二问");

        // 最后一次请求不应包含第一问的历史
        var lastRequest = client.Requests[^1];
        Assert.Equal(2, lastRequest.Messages.Count);
        Assert.Equal("system", lastRequest.Messages[0].Role);
        Assert.Equal("第二问", lastRequest.Messages[1].Content + "");
    }

    [Fact]
    [DisplayName("会话历史—超上限时裁剪最早记录（MaxHistory=2）")]
    public async Task ChatAsync_SessionHistory_Trimmed()
    {
        var client = new CapturingChatClient();
        client.StreamChunks.Enqueue(TextChunk("回复"));

        var sessions = new ChatSessionService { MaxHistory = 2 };
        var ai = new AiChatService(client, sessions);

        for (var i = 1; i <= 5; i++)
        {
            await RunAsync(ai, "s1", $"第{i}问");
        }

        // 最后请求的历史仅保留最近 2 条（不含当前问题）：第4问 + 第4问回复
        var lastRequest = client.Requests[^1];
        var contents = lastRequest.Messages.Select(m => m.Content + "").ToList();
        Assert.Equal(4, lastRequest.Messages.Count);  // system + 历史2 + 当前1
        Assert.DoesNotContain("第1问", contents);
        Assert.DoesNotContain("第2问", contents);
        Assert.Contains("第3问", contents);
        Assert.Contains("第4问", contents);
        Assert.Equal("第5问", contents[^1]);
    }

    // ── 参数校验 ──────────────────────────────────────────────────────────

    [Fact]
    [DisplayName("参数校验—消息为空时抛 ArgumentNullException")]
    public async Task ChatAsync_EmptyMessage_Throws()
    {
        var ai = new AiChatService(new CapturingChatClient());
        var req = new AiChatRequest { Message = "" };

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await foreach (var _ in ai.ChatAsync(req, "你是助手")) { }
        });
    }
}
