using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NewLife.AI.Filters;
using NewLife.AI.Models;
using NewLife.AI.Providers;
using Xunit;

namespace XUnitTest;

[DisplayName("过滤器管道测试")]
public class FilterTests
{
    // ── 测试用假 IChatClient ──────────────────────────────────────────────────

    /// <summary>固定返回指定文本的假客户端</summary>
    private sealed class FakeClient : IChatClient
    {
        private readonly String _reply;

        public FakeClient(String reply = "ok") => _reply = reply;

        public Task<ChatResponse> GetResponseAsync(ChatRequest request, CancellationToken cancellationToken = default)
        {
            var resp = new ChatResponse
            {
                Messages = [new ChatChoice
                {
                    Message = new ChatMessage { Role = "assistant", Content = _reply }
                }]
            };
            return Task.FromResult(resp);
        }

        public async IAsyncEnumerable<ChatResponse> GetStreamingResponseAsync(
            ChatRequest request,
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
    }

    // ── 测试 ──────────────────────────────────────────────────────────────────

    [Fact]
    [DisplayName("无过滤器时直接转发给内层客户端")]
    public async Task FilteredClient_NoFilters_PassesThrough()
    {
        var client = new FilteredChatClient(new FakeClient("hello"));
        IList<ChatMessage> messages = [new ChatMessage { Role = "user", Content = "hi" }];
        var resp = await client.GetResponseAsync(messages);

        Assert.Equal("hello", resp.Messages![0].Message!.Content?.ToString());
    }

    [Fact]
    [DisplayName("单个过滤器—before/after 均被调用")]
    public async Task FilteredClient_SingleFilter_CallsBeforeAndAfter()
    {
        var filter = new RecordingFilter("f1");
        var client = new FilteredChatClient(new FakeClient(), [filter]);
        await client.GetResponseAsync((IList<ChatMessage>)[]);

        Assert.Equal(["before-f1", "after-f1"], filter.Calls);
    }

    [Fact]
    [DisplayName("多个过滤器—按注册顺序洋葱圈执行")]
    public async Task FilteredClient_MultipleFilters_OnionOrder()
    {
        var f1 = new RecordingFilter("f1");
        var f2 = new RecordingFilter("f2");
        var client = new FilteredChatClient(new FakeClient(), [f1, f2]);
        await client.GetResponseAsync((IList<ChatMessage>)[]);

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
        await client.GetResponseAsync(messages);

        // RequestModifyingFilter 在 before 阶段修改了 Request.User
        Assert.Equal("modified-by-filter", filter.CapturedUser);
    }

    [Fact]
    [DisplayName("ChatClientBuilder.UseFilters 扩展方法正常注入")]
    public async Task Builder_UseFilters_InjectsFilteredChatClient()
    {
        var filter = new RecordingFilter("builderFilter");
        var client = new ChatClientBuilder(new FakeClient())
            .UseFilters(filter)
            .Build();

        await client.GetResponseAsync((IList<ChatMessage>)[]);

        Assert.Contains("before-builderFilter", filter.Calls);
        Assert.Contains("after-builderFilter", filter.Calls);
    }
}
