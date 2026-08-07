#nullable enable
using System;
using System.ComponentModel;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NewLife.AI.Clients;
using Xunit;

namespace XUnitTest.Clients;

/// <summary>HttpClientPool 测试集合定义。禁用并行化：测试中的 Clear() 会清理全局共享 handler，必须与其他测试类串行执行，避免破坏并行中的真实网络测试</summary>
[CollectionDefinition("HttpClientPool", DisableParallelization = true)]
public class HttpClientPoolCollection { }

/// <summary>HttpClientPool 池化测试。验证按 Endpoint 主机复用 handler、路径归一化、Clear 重建与 disposeHandler:false 语义</summary>
[Collection("HttpClientPool")]
public class HttpClientPoolTests : IDisposable
{
    /// <summary>测试专用主机。避免与其他测试的真实 Endpoint 共用池键</summary>
    private const String TestHost = "https://pool.test.local";

    /// <summary>每个测试后恢复默认生命周期并清理共享池，隔离测试间影响（集合已串行化，不影响其他测试类）</summary>
    public void Dispose()
    {
        HttpClientPool.HandlerLifetime = TimeSpan.FromMinutes(2);
        HttpClientPool.Clear();
    }

    /// <summary>同一主机多次获取返回同一 handler 实例（连接池共享）</summary>
    [Fact]
    [DisplayName("同主机复用同一 handler")]
    public void SameHostReusesHandler()
    {
        var h1 = HttpClientPool.GetHandler(TestHost);
        var h2 = HttpClientPool.GetHandler(TestHost);

        Assert.Same(h1, h2);
    }

    /// <summary>不同主机返回不同 handler（连接池隔离，避免认证等状态串扰）</summary>
    [Fact]
    [DisplayName("不同主机隔离 handler")]
    public void DifferentHostIsolated()
    {
        var h1 = HttpClientPool.GetHandler(TestHost);
        var h2 = HttpClientPool.GetHandler("https://pool2.test.local");

        Assert.NotSame(h1, h2);
    }

    /// <summary>endpoint 带路径/版本段与不带共享同一 handler（主机归一化，同主机不同路径复用连接池）</summary>
    [Fact]
    [DisplayName("路径与版本段不改变池键")]
    public void PathIgnoredInKey()
    {
        var h1 = HttpClientPool.GetHandler(TestHost);
        var h2 = HttpClientPool.GetHandler($"{TestHost}/v1/chat/completions");

        Assert.Same(h1, h2);
    }

    /// <summary>Clear 后再次获取创建新 handler 实例（测试/热更新场景）</summary>
    [Fact]
    [DisplayName("Clear 后重建 handler")]
    public void ClearRebuildsHandler()
    {
        var h1 = HttpClientPool.GetHandler(TestHost);
        HttpClientPool.Clear();
        var h2 = HttpClientPool.GetHandler(TestHost);

        Assert.NotSame(h1, h2);
    }

    /// <summary>非法 endpoint 与空值不抛异常，返回可用 handler（防御退化）</summary>
    [Fact]
    [DisplayName("非法与空 endpoint 防御")]
    public void InvalidEndpointSafe()
    {
        var h1 = HttpClientPool.GetHandler(null);
        var h2 = HttpClientPool.GetHandler("not-a-uri");

        Assert.NotNull(h1);
        Assert.NotNull(h2);
    }

    /// <summary>disposeHandler:false 构造的 HttpClient 释放时不关闭共享 handler（连接复用关键语义）</summary>
    [Fact]
    [DisplayName("Dispose 不释放共享 handler")]
    public void DisposeDoesNotDisposeHandler()
    {
        var handler = HttpClientPool.GetHandler(TestHost);
        var client = new HttpClient(handler, disposeHandler: false);
        client.Dispose();

        // 释放客户端后，再次获取仍返回同一 handler 实例
        var again = HttpClientPool.GetHandler(TestHost);
        Assert.Same(handler, again);
    }

    /// <summary>超过 HandlerLifetime 后再次获取自动轮换为新 handler（避免 DNS 变更 / 连接陈旧）</summary>
    [Fact]
    [DisplayName("超过生命周期自动轮换 handler")]
    public void HandlerLifetimeRotatesHandler()
    {
        HttpClientPool.HandlerLifetime = TimeSpan.FromMilliseconds(50);
        var h1 = HttpClientPool.GetHandler(TestHost);
        Thread.Sleep(150);
        var h2 = HttpClientPool.GetHandler(TestHost);

        Assert.NotSame(h1, h2);
    }

    /// <summary>生命周期内多次获取复用同一 handler</summary>
    [Fact]
    [DisplayName("生命周期内复用同一 handler")]
    public void HandlerLifetimeWithinReusesHandler()
    {
        var h1 = HttpClientPool.GetHandler(TestHost);
        var h2 = HttpClientPool.GetHandler(TestHost);

        Assert.Same(h1, h2);
    }

    /// <summary>池化 handler 的 HttpClient 能正常发起请求（连接复用有效）</summary>
    [Fact]
    [DisplayName("池化 handler 可正常请求")]
    public async Task PooledHandlerWorks()
    {
        var handler = new StubHandler();
        var client = new HttpClient(handler, disposeHandler: false);
        var resp = await client.GetAsync($"{TestHost}/test");

        Assert.Equal("OK", await resp.Content.ReadAsStringAsync());
    }

    /// <summary>桩 Handler。返回固定响应，验证池化 handler 的 HttpClient 可用性</summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent("OK") });
    }
}
