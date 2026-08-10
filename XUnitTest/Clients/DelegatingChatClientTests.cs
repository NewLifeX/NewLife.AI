using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using NewLife.AI.Clients;
using NewLife.AI.Models;
using Xunit;

namespace XUnitTest.Clients;

/// <summary>委托式客户端（DelegatingChatClient）单元测试。验证默认转发、MergeChunkUsage 链路与释放语义</summary>
[DisplayName("DelegatingChatClient 单元测试")]
public class DelegatingChatClientTests
{
    // 假内层客户端：记录调用次数并返回固定响应
    private sealed class FakeInnerClient : IChatClient
    {
        public Int32 GetResponseCalls;
        public Int32 GetStreamingCalls;
        public Boolean Disposed;

        public Task<IChatResponse> GetResponseAsync(IChatRequest request, CancellationToken cancellationToken = default)
        {
            GetResponseCalls++;
            return Task.FromResult<IChatResponse>(new ChatResponse { Model = "fake" });
        }

        public async IAsyncEnumerable<IChatResponse> GetStreamingResponseAsync(IChatRequest request, CancellationToken cancellationToken = default)
        {
            GetStreamingCalls++;
            yield return new ChatResponse { Model = "fake-stream" };
            await Task.CompletedTask;
        }

        public void Dispose() => Disposed = true;
    }

    // 具体委托客户端子类（无覆盖，验证默认转发行为）
    private sealed class PassThroughDelegatingClient : DelegatingChatClient
    {
        public PassThroughDelegatingClient(IChatClient inner) : base(inner) { }
    }

    [Fact]
    [DisplayName("构造—内层客户端为 null 时抛 ArgumentNullException")]
    public void Constructor_NullInner_Throws()
        => Assert.Throws<ArgumentNullException>(() => new PassThroughDelegatingClient(null!));

    [Fact]
    [DisplayName("GetResponseAsync—默认转发给内层客户端")]
    public async Task GetResponseAsync_ForwardsToInner()
    {
        var inner = new FakeInnerClient();
        var delegating = new PassThroughDelegatingClient(inner);

        var resp = await delegating.GetResponseAsync(new ChatRequest());

        Assert.Equal(1, inner.GetResponseCalls);
        Assert.Equal("fake", resp.Model);
    }

    [Fact]
    [DisplayName("GetStreamingResponseAsync—默认转发给内层客户端")]
    public async Task GetStreamingResponseAsync_ForwardsToInner()
    {
        var inner = new FakeInnerClient();
        var delegating = new PassThroughDelegatingClient(inner);

        var count = 0;
        await foreach (var chunk in delegating.GetStreamingResponseAsync(new ChatRequest()))
        {
            count++;
            Assert.Equal("fake-stream", chunk.Model);
        }

        Assert.Equal(1, inner.GetStreamingCalls);
        Assert.Equal(1, count);
    }

    [Fact]
    [DisplayName("MergeChunkUsage—内层为普通 IChatClient 时返回入站 Usage")]
    public void MergeChunkUsage_PlainInner_ReturnsIncoming()
    {
        var delegating = new PassThroughDelegatingClient(new FakeInnerClient());

        var merged = delegating.MergeChunkUsage(null, new UsageDetails { InputTokens = 10 });

        Assert.Equal(10, merged.InputTokens);
    }

    [Fact]
    [DisplayName("MergeChunkUsage—内层也是 DelegatingChatClient 时透传到最深层")]
    public void MergeChunkUsage_NestedDelegating_Chains()
    {
        var inner = new FakeInnerClient();
        var innerDelegating = new PassThroughDelegatingClient(inner);
        var outerDelegating = new PassThroughDelegatingClient(innerDelegating);

        var merged = outerDelegating.MergeChunkUsage(null, new UsageDetails { OutputTokens = 20 });

        Assert.Equal(20, merged.OutputTokens);
    }

    [Fact]
    [DisplayName("Dispose—释放内层客户端")]
    public void Dispose_DisposesInner()
    {
        var inner = new FakeInnerClient();
        var delegating = new PassThroughDelegatingClient(inner);

        delegating.Dispose();

        Assert.True(inner.Disposed);
    }
}
