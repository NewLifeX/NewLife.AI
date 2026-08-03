using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NewLife.AI.Clients;
using NewLife.AI.Filters;
using NewLife.AI.Models;
using Xunit;

namespace XUnitTest.Filters;

[DisplayName("过滤器管道测试")]
public class FilterTests
{
    // ── 测试用假 IChatClient ──────────────────────────────────────────────────

    /// <summary>固定返回指定文本的假客户端</summary>
    private sealed class FakeClient : IChatClient
    {
        private readonly String _reply;

        public FakeClient(String reply = "ok") => _reply = reply;

        public Task<IChatResponse> GetResponseAsync(IChatRequest request, CancellationToken cancellationToken = default)
        {
            var resp = new ChatResponse
            {
                Messages = [new ChatChoice
                {
                    Message = new ChatMessage { Role = "assistant", Content = _reply }
                }]
            };
            return Task.FromResult<IChatResponse>(resp);
        }

        public async IAsyncEnumerable<IChatResponse> GetStreamingResponseAsync(
            IChatRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return await GetResponseAsync(request, cancellationToken).ConfigureAwait(false);
        }

        public void Dispose() { }
    }

    // ── 测试用过滤器 ──────────────────────────────────────────────────────────

    private sealed class RecordingFilter : IChatFilter
    {
        public readonly List<String> Calls = [];

        public String Label { get; }

        public RecordingFilter(String label) => Label = label;

        public async Task OnChatAsync(ChatFilterContext context, Func<ChatFilterContext, CancellationToken, Task> next, CancellationToken ct)
        {
            Calls.Add($"before-{Label}");
            await next(context, ct).ConfigureAwait(false);
            Calls.Add($"after-{Label}");
        }

        Task IChatFilter.OnStreamCompletedAsync(ChatFilterContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class RequestModifyingFilter : IChatFilter
    {
        public String? CapturedUser { get; private set; }

        public async Task OnChatAsync(ChatFilterContext context, Func<ChatFilterContext, CancellationToken, Task> next, CancellationToken ct)
        {
            // before：修改请求
            context.Request.User = "modified-by-filter";
            await next(context, ct).ConfigureAwait(false);
            CapturedUser = context.Request.User;
        }

        Task IChatFilter.OnStreamCompletedAsync(ChatFilterContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    /// <summary>流式返回思考+正文+结束原因的假客户端</summary>
    private sealed class StreamingFakeClient : IChatClient
    {
        public Task<IChatResponse> GetResponseAsync(IChatRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult<IChatResponse>(new ChatResponse());

        public async IAsyncEnumerable<IChatResponse> GetStreamingResponseAsync(
            IChatRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return new ChatResponse
            {
                Messages = [new ChatChoice { Delta = new ChatMessage { Role = "assistant", ReasoningContent = "先分析" } }]
            };
            yield return new ChatResponse
            {
                Messages = [new ChatChoice { Delta = new ChatMessage { Role = "assistant", Content = "答案是 42" }, FinishReason = FinishReason.Stop }]
            };
        }

        public void Dispose() { }
    }

    /// <summary>捕获 OnStreamCompletedAsync 响应的过滤器</summary>
    private sealed class CaptureResponseFilter : IChatFilter
    {
        public IChatResponse? Response;

        public async Task OnChatAsync(ChatFilterContext context, Func<ChatFilterContext, CancellationToken, Task> next, CancellationToken ct)
            => await next(context, ct).ConfigureAwait(false);

        Task IChatFilter.OnStreamCompletedAsync(ChatFilterContext context, CancellationToken cancellationToken)
        {
            Response = context.Response;
            return Task.CompletedTask;
        }
    }

    // ── 测试 ──────────────────────────────────────────────────────────────────

    [Fact]
    [DisplayName("无过滤器时直接转发给内层客户端")]
    public async Task FilteredClient_NoFilters_PassesThrough()
    {
        var client = new FilteredChatClient(new FakeClient("hello"));
        IList<ChatMessage> messages = [new ChatMessage { Role = "user", Content = "hi" }];
        var resp = await client.GetResponseAsync(messages, cancellationToken: default);

        Assert.Equal("hello", resp.Messages![0].Message!.Content?.ToString());
    }

    [Fact]
    [DisplayName("单个过滤器—before/after 均被调用")]
    public async Task FilteredClient_SingleFilter_CallsBeforeAndAfter()
    {
        var filter = new RecordingFilter("f1");
        var client = new FilteredChatClient(new FakeClient(), [filter]);
        await client.GetResponseAsync((IList<ChatMessage>)[], cancellationToken: default);

        Assert.Equal(["before-f1", "after-f1"], filter.Calls);
    }

    [Fact]
    [DisplayName("多个过滤器—按注册顺序洋葱圈执行")]
    public async Task FilteredClient_MultipleFilters_OnionOrder()
    {
        var f1 = new RecordingFilter("f1");
        var f2 = new RecordingFilter("f2");
        var client = new FilteredChatClient(new FakeClient(), [f1, f2]);
        await client.GetResponseAsync((IList<ChatMessage>)[], cancellationToken: default);

        // 洋葱圈：f1-before → f2-before → (inner) → f2-after → f1-after
        Assert.Equal(["before-f1"], f1.Calls.GetRange(0, 1));
        Assert.Equal(["after-f1"], f1.Calls.GetRange(1, 1));
        Assert.Equal(["before-f2"], f2.Calls.GetRange(0, 1));
        Assert.Equal(["after-f2"], f2.Calls.GetRange(1, 1));
        // f1 记录 2 条（before + after），f2 同
        Assert.Equal(2, f1.Calls.Count);
        Assert.Equal(2, f2.Calls.Count);
    }

    [Fact]
    [DisplayName("过滤器可修改 Request.User")]
    public async Task FilteredClient_Filter_CanModifyRequest()
    {
        var filter = new RequestModifyingFilter();
        var client = new FilteredChatClient(new FakeClient(), [filter]);

        IList<ChatMessage> messages = [];
        await client.GetResponseAsync(messages, cancellationToken: default);

        // RequestModifyingFilter 在 before 阶段修改了 Request.User
        Assert.Equal("modified-by-filter", filter.CapturedUser);
    }

    [Fact]
    [DisplayName("流式聚合—OnStreamCompleted 响应含思考与结束原因")]
    public async Task FilteredClient_Stream_CompletedResponseHasReasoning()
    {
        var filter = new CaptureResponseFilter();
        var client = new FilteredChatClient(new StreamingFakeClient(), [filter]);

        await foreach (var _ in client.GetStreamingResponseAsync((IList<ChatMessage>)[], cancellationToken: CancellationToken.None))
        {
        }

        // OnStreamCompletedAsync 以"火焰即忘"方式异步触发，轮询等待
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (filter.Response == null && DateTime.UtcNow < deadline)
            await Task.Delay(10);

        Assert.NotNull(filter.Response);
        Assert.Equal("答案是 42", filter.Response!.Text);
        Assert.Equal("先分析", filter.Response.Messages![0].Message!.ReasoningContent);
        Assert.Equal(FinishReason.Stop, filter.Response.Messages[0].FinishReason);
    }

    [Fact]
    [DisplayName("ChatClientBuilder.UseFilters 扩展方法正常注入")]
    public async Task Builder_UseFilters_InjectsFilteredChatClient()
    {
        var filter = new RecordingFilter("builderFilter");
        var client = new ChatClientBuilder(new FakeClient())
            .UseFilters(filter)
            .Build();

        await client.GetResponseAsync((IList<ChatMessage>)[], cancellationToken: default);

        Assert.Contains("before-builderFilter", filter.Calls);
        Assert.Contains("after-builderFilter", filter.Calls);
    }
}
