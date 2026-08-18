using System;
using System.Collections.Generic;
using System.ComponentModel;
using NewLife.AI.Models;
using NewLife.AI.Services;
using NewLife.Caching;
using Xunit;

namespace XUnitTest.Services;

/// <summary>会话历史存储默认实现 <see cref="CacheSessionStore"/> 单元测试</summary>
[DisplayName("CacheSessionStore 会话存储测试")]
public class CacheSessionStoreTests
{
    [Fact]
    [DisplayName("Set/Get—读写会话历史")]
    public void Set_Get_RoundTrip()
    {
        var store = new CacheSessionStore();
        var messages = new List<ChatMessage>
        {
            new() { Role = "user", Content = "你好" },
            new() { Role = "assistant", Content = "你好！" },
        };

        store.Set("s1", messages);

        var history = store.Get("s1");
        Assert.NotNull(history);
        Assert.Equal(2, history!.Count);
        Assert.Equal("你好", history[0].Content + "");
        Assert.Equal("你好！", history[1].Content + "");
    }

    [Fact]
    [DisplayName("Get—会话不存在返回 null")]
    public void Get_NotExists_ReturnsNull()
    {
        var store = new CacheSessionStore();
        Assert.Null(store.Get("nope"));
    }

    [Fact]
    [DisplayName("Set—覆盖写同会话历史")]
    public void Set_Overwrites()
    {
        var store = new CacheSessionStore();
        store.Set("s2", new List<ChatMessage> { new() { Role = "user", Content = "第一轮" } });
        store.Set("s2", new List<ChatMessage> { new() { Role = "user", Content = "第二轮" } });

        var history = store.Get("s2");
        Assert.NotNull(history);
        Assert.Single(history!);
        Assert.Equal("第二轮", history![0].Content + "");
    }

    [Fact]
    [DisplayName("注入 ChatSessionService—读写委托默认存储")]
    public void ChatSessionService_DelegatesToDefaultStore()
    {
        var store = new CacheSessionStore();
        var svc = new ChatSessionService(store);
        svc.Append("s3", new ChatMessage { Role = "user", Content = "你好" });

        // 默认存储收到数据
        Assert.NotNull(store.Get("s3"));
        Assert.Single(store.Get("s3")!);

        // 新服务实例从默认存储读取（模拟跨进程/重启）
        var svc2 = new ChatSessionService(store);
        var history = svc2.GetHistory("s3");
        Assert.NotNull(history);
        Assert.Equal("你好", history![0].Content + "");
    }

    [Fact]
    [DisplayName("注入 ICacheProvider—读写委托提供者缓存")]
    public void InjectProvider_DelegatesToProviderCache()
    {
        var provider = new CacheProvider();
        var store = new CacheSessionStore(provider);
        store.Set("s4", new List<ChatMessage> { new() { Role = "user", Content = "你好" } });

        // 数据写入提供者缓存
        var history = provider.Cache.Get<IList<ChatMessage>>("s4");
        Assert.NotNull(history);
        Assert.Single(history!);
        Assert.Equal("你好", history![0].Content + "");
    }

    [Fact]
    [DisplayName("共享缓存后端—跨实例数据共享（模拟分布式）")]
    public void SharedProvider_CrossInstanceShare()
    {
        var provider = new CacheProvider();
        var store1 = new CacheSessionStore(provider);
        var store2 = new CacheSessionStore(provider);

        store1.Set("s5", new List<ChatMessage> { new() { Role = "user", Content = "你好" } });

        // 另一实例从同一后端读取（模拟多节点共享 Redis/缓存）
        var history = store2.Get("s5");
        Assert.NotNull(history);
        Assert.Equal("你好", history![0].Content + "");
    }
}
