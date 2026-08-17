using System;
using System.Collections.Generic;
using System.ComponentModel;
using NewLife.AI.Models;
using NewLife.AI.Services;
using Xunit;

namespace XUnitTest.Services;

/// <summary>会话历史管理 <see cref="ChatSessionService"/> 单元测试</summary>
[DisplayName("ChatSessionService 会话历史测试")]
public class ChatSessionServiceTests
{
    [Fact]
    [DisplayName("Append—追加消息并保留历史")]
    public void Append_AddsMessage()
    {
        var svc = new ChatSessionService();
        svc.Append("s1", new ChatMessage { Role = "user", Content = "你好" });
        svc.Append("s1", new ChatMessage { Role = "assistant", Content = "你好！" });

        var history = svc.GetHistory("s1");
        Assert.NotNull(history);
        Assert.Equal(2, history!.Count);
        Assert.Equal("你好", history[0].Content + "");
    }

    [Fact]
    [DisplayName("Append—超上限时裁剪最早记录（MaxHistory=3）")]
    public void Append_TrimsOldest()
    {
        var svc = new ChatSessionService { MaxHistory = 3 };
        for (var i = 1; i <= 5; i++)
        {
            svc.Append("s1", new ChatMessage { Role = "user", Content = $"第{i}条" });
        }

        var history = svc.GetHistory("s1");
        Assert.NotNull(history);
        Assert.Equal(3, history!.Count);
        Assert.Equal("第3条", history[0].Content + "");
        Assert.Equal("第5条", history[2].Content + "");
    }

    [Fact]
    [DisplayName("GetHistory—会话不存在返回 null")]
    public void GetHistory_NotExists_ReturnsNull()
    {
        var svc = new ChatSessionService();
        Assert.Null(svc.GetHistory("nope"));
    }

    [Fact]
    [DisplayName("GetOrCreate—不存在时创建空列表")]
    public void GetOrCreate_CreatesEmpty()
    {
        var svc = new ChatSessionService();
        var history = svc.GetOrCreate("s1");
        Assert.NotNull(history);
        Assert.Empty(history);
    }

    [Fact]
    [DisplayName("Clear—清除会话历史")]
    public void Clear_RemovesHistory()
    {
        var svc = new ChatSessionService();
        svc.Append("s1", new ChatMessage { Role = "user", Content = "你好" });
        svc.Clear("s1");

        Assert.Null(svc.GetHistory("s1"));
    }

    [Fact]
    [DisplayName("存储注入—读写委托外部 IChatSessionStore")]
    public void StoreMode_DelegatesToStore()
    {
        var store = new MemoryStore();
        var svc = new ChatSessionService(store);
        svc.Append("s1", new ChatMessage { Role = "user", Content = "你好" });

        // 外部存储收到数据
        Assert.NotNull(store.Get("s1"));
        Assert.Single(store.Get("s1")!);

        // 新服务实例从存储读取（模拟跨进程/重启）
        var svc2 = new ChatSessionService(store);
        var history = svc2.GetHistory("s1");
        Assert.NotNull(history);
        Assert.Equal("你好", history![0].Content + "");
    }

    /// <summary>测试用内存存储</summary>
    private sealed class MemoryStore : IChatSessionStore
    {
        private readonly Dictionary<String, IList<ChatMessage>> _data = new();

        public IList<ChatMessage>? Get(String sessionId)
            => _data.TryGetValue(sessionId, out var list) ? list : null;

        public void Set(String sessionId, IList<ChatMessage> messages)
            => _data[sessionId] = messages;
    }
}
