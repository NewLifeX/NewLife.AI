using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NewLife.AI.Handlers;
using NewLife.AI.Models;
using Xunit;

namespace XUnitTest.Handlers;

/// <summary>对话处理器调用链（ChatHandlerChain）单元测试。覆盖排序、能力过滤、增删替换、缓存失效与 BuildFor 过滤</summary>
[DisplayName("ChatHandlerChain 单元测试")]
public class ChatHandlerChainTests
{
    // 测试处理器：携带名称与能力标志
    private class TestHandler : IChatHandler
    {
        public TestHandler(String name, ChatHandlerCapabilities caps)
        {
            Name = name;
            Capabilities = caps;
        }

        public String Name { get; }

        public ChatHandlerCapabilities Capabilities { get; }

        public Task OnBefore(IChatContext context, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task OnAfter(IChatContext context, CancellationToken cancellationToken) => Task.CompletedTask;

        public async IAsyncEnumerable<ChatStreamEvent> InvokeAsync(IChatContext context, ChatNextDelegate next, CancellationToken cancellationToken)
        {
            await foreach (var e in next(cancellationToken))
                yield return e;
        }
    }

    // 带排序特性的处理器（Before = 50，应排前面）
    [ChatHandlerOrder(Before = 50)]
    private sealed class EarlyHandler : TestHandler
    {
        public EarlyHandler() : base("early", ChatHandlerCapabilities.Before) { }
    }

    // 实现范围声明的处理器（BuildFor 过滤用）
    private sealed class ScopedHandler : TestHandler, IChatHandlerScope
    {
        public ScopedHandler(String name, ChatFlowSource sources, ChatHandlerTier tier, ChatHandlerCapabilities caps = ChatHandlerCapabilities.Before)
            : base(name, caps)
        {
            SupportedSources = sources;
            Tier = tier;
        }

        public ChatFlowSource SupportedSources { get; }

        public ChatHandlerTier Tier { get; }
    }

    [Fact]
    [DisplayName("BeforeHandlers—标注排序值的排前面，未标注按注册序")]
    public void BeforeHandlers_SortByOrder()
    {
        var chain = new ChatHandlerChain();
        chain.Add(new TestHandler("late", ChatHandlerCapabilities.Before));
        chain.Add(new EarlyHandler());

        var before = chain.BeforeHandlers;

        Assert.Equal(2, before.Count);
        Assert.Equal("early", ((TestHandler)before[0]).Name);
        Assert.Equal("late", ((TestHandler)before[1]).Name);
    }

    [Fact]
    [DisplayName("BeforeHandlers—仅含 Before 能力的处理器，After 视图独立")]
    public void BeforeHandlers_OnlyBeforeCapability()
    {
        var chain = new ChatHandlerChain();
        chain.Add(new TestHandler("before", ChatHandlerCapabilities.Before));
        chain.Add(new TestHandler("after", ChatHandlerCapabilities.After));
        chain.Add(new TestHandler("both", ChatHandlerCapabilities.Before | ChatHandlerCapabilities.After));

        var before = chain.BeforeHandlers;
        var after = chain.AfterHandlers;

        Assert.Equal(2, before.Count);
        Assert.Equal(2, after.Count);
        Assert.All(before, h => Assert.True(h.Capabilities.HasFlag(ChatHandlerCapabilities.Before)));
        Assert.All(after, h => Assert.True(h.Capabilities.HasFlag(ChatHandlerCapabilities.After)));
    }

    [Fact]
    [DisplayName("Interceptors—保持注册顺序")]
    public void Interceptors_KeepRegistrationOrder()
    {
        var chain = new ChatHandlerChain();
        chain.Add(new TestHandler("i1", ChatHandlerCapabilities.Interceptor));
        chain.Add(new TestHandler("b", ChatHandlerCapabilities.Before));
        chain.Add(new TestHandler("i2", ChatHandlerCapabilities.Interceptor));

        var interceptors = chain.Interceptors;

        Assert.Equal(2, interceptors.Count);
        Assert.Equal("i1", ((TestHandler)interceptors[0]).Name);
        Assert.Equal("i2", ((TestHandler)interceptors[1]).Name);
    }

    [Fact]
    [DisplayName("Add/Remove/Replace—修改后视图立即更新（缓存失效）")]
    public void Modify_InvalidatesCache()
    {
        var chain = new ChatHandlerChain();
        var h1 = new TestHandler("h1", ChatHandlerCapabilities.Before);
        chain.Add(h1);
        Assert.Equal(1, chain.BeforeHandlers.Count);

        chain.Remove<TestHandler>();
        Assert.Equal(0, chain.BeforeHandlers.Count);

        chain.Add(h1);
        chain.Replace<TestHandler>(new TestHandler("h2", ChatHandlerCapabilities.Before));
        Assert.Equal(1, chain.BeforeHandlers.Count);
        Assert.Equal("h2", ((TestHandler)chain.BeforeHandlers[0]).Name);
    }

    [Fact]
    [DisplayName("Replace—目标类型不存在时追加到末尾")]
    public void Replace_NotFound_Appends()
    {
        var chain = new ChatHandlerChain();
        chain.Add(new TestHandler("h1", ChatHandlerCapabilities.Before));

        chain.Replace<EarlyHandler>(new TestHandler("new", ChatHandlerCapabilities.Before));

        Assert.Equal(2, chain.Count);
    }

    [Fact]
    [DisplayName("BuildFor—未实现 Scope 的处理器精简链下剔除")]
    public void BuildFor_NoScope_FilteredInFullOnly()
    {
        var handlers = new IChatHandler[] { new TestHandler("plain", ChatHandlerCapabilities.Before) };

        var full = ChatHandlerChain.BuildFor(handlers, ChatFlowSource.Web, fullChain: true);
        var slim = ChatHandlerChain.BuildFor(handlers, ChatFlowSource.Web, fullChain: false);

        Assert.Equal(1, full.Count);
        Assert.Equal(0, slim.Count);
    }

    [Fact]
    [DisplayName("BuildFor—来源不匹配剔除")]
    public void BuildFor_SourceMismatch_Removed()
    {
        var handlers = new IChatHandler[]
        {
            new ScopedHandler("web", ChatFlowSource.Web, ChatHandlerTier.Core),
            new ScopedHandler("channel", ChatFlowSource.Channel, ChatHandlerTier.Core),
        };

        var chain = ChatHandlerChain.BuildFor(handlers, ChatFlowSource.Web, fullChain: true);

        Assert.Equal(1, chain.Count);
        Assert.Equal("web", ((TestHandler)chain.BeforeHandlers[0]).Name);
    }

    [Fact]
    [DisplayName("BuildFor—Core 始终保留，Full 仅完整链保留")]
    public void BuildFor_TierFiltering()
    {
        var handlers = new IChatHandler[]
        {
            new ScopedHandler("core", ChatFlowSource.All, ChatHandlerTier.Core),
            new ScopedHandler("full", ChatFlowSource.All, ChatHandlerTier.Full),
        };

        var full = ChatHandlerChain.BuildFor(handlers, ChatFlowSource.Web, fullChain: true);
        var slim = ChatHandlerChain.BuildFor(handlers, ChatFlowSource.Web, fullChain: false);

        Assert.Equal(2, full.Count);
        Assert.Equal(1, slim.Count);
        Assert.Equal("core", ((TestHandler)slim.BeforeHandlers[0]).Name);
    }

    [Fact]
    [DisplayName("Count—返回已注册总数")]
    public void Count_ReturnsTotal()
    {
        var chain = new ChatHandlerChain();
        chain.Add(new TestHandler("a", ChatHandlerCapabilities.Before));
        chain.Add(new TestHandler("b", ChatHandlerCapabilities.Before));
        Assert.Equal(2, chain.Count);
    }
}
