#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using NewLife.AI.Clients.DashScope;
using Xunit;

namespace XUnitTest.Clients;

/// <summary>DashScopeRealtimeClient 单条消息多 JSON 事件切分测试（A-71：整条解析失败被静默丢弃 → 大括号配对切分）</summary>
[DisplayName("DashScope 实时多事件切分测试")]
public class DashScopeRealtimeSplitTests
{
    [Fact]
    [DisplayName("SplitJsonObjects—单事件原样返回")]
    public void SingleEvent_Unchanged()
    {
        var json = """{"type":"session.created","session":{"id":"s1"}}""";

        var parts = DashScopeRealtimeClient.SplitJsonObjects(json).ToList();

        Assert.Single(parts);
        Assert.Equal(json, parts[0]);
    }

    [Fact]
    [DisplayName("SplitJsonObjects—多事件拼接正确切分")]
    public void MultipleEvents_Split()
    {
        var json = """{"type":"session.created","session":{"id":"s1"}}{"type":"response.created","response":{"id":"r1"}}""";

        var parts = DashScopeRealtimeClient.SplitJsonObjects(json).ToList();

        Assert.Equal(2, parts.Count);
        Assert.Equal("""{"type":"session.created","session":{"id":"s1"}}""", parts[0]);
        Assert.Equal("""{"type":"response.created","response":{"id":"r1"}}""", parts[1]);
    }

    [Fact]
    [DisplayName("SplitJsonObjects—字符串值内大括号不参与配对")]
    public void BracesInsideString_Ignored()
    {
        // delta 内容含 { } 不应破坏切分
        var json = """{"type":"response.text.delta","delta":"含{花括号}文本"}{"type":"response.done"}""";

        var parts = DashScopeRealtimeClient.SplitJsonObjects(json).ToList();

        Assert.Equal(2, parts.Count);
        Assert.Equal("""{"type":"response.done"}""", parts[1]);
    }

    [Fact]
    [DisplayName("SplitJsonObjects—转义引号不结束字符串")]
    public void EscapedQuote_Handled()
    {
        var json = """{"type":"response.text.delta","delta":"say \"hi\" {x}"}""";

        var parts = DashScopeRealtimeClient.SplitJsonObjects(json).ToList();

        Assert.Single(parts);
        Assert.Equal(json, parts[0]);
    }

    [Fact]
    [DisplayName("SplitJsonObjects—多事件逐个Parse成功")]
    public void MultipleEvents_ParseEach()
    {
        var json = """{"type":"session.created","session":{"id":"s1"}}{"type":"response.audio.delta","delta":"AAA"}""";

        var events = DashScopeRealtimeClient.SplitJsonObjects(json)
            .Select(e => RealtimeEvent.Parse(e))
            .Where(e => e != null)
            .ToList();

        Assert.Equal(2, events.Count);
        Assert.Equal("session.created", events[0]!.Type);
        Assert.Equal("s1", events[0]!.SessionId);
        Assert.Equal("response.audio.delta", events[1]!.Type);
        Assert.Equal("AAA", events[1]!.AudioDelta);
    }

    [Fact]
    [DisplayName("SplitJsonObjects—空文本返回空序列")]
    public void Empty_ReturnsEmpty()
    {
        Assert.Empty(DashScopeRealtimeClient.SplitJsonObjects(""));
        Assert.Empty(DashScopeRealtimeClient.SplitJsonObjects("   "));
    }

    [Fact]
    [DisplayName("RealtimeEvent.Parse—缺失type事件不抛异常")]
    public void Parse_MissingType_NoThrow()
    {
        // 服务端推送缺少 type 的事件（异常/协议变体），解析不应抛异常中断接收循环（NullableDictionary 缺失键返回 null）
        var json = """{"event_id":"e1","session":{"id":"s1"}}""";

        var evt = RealtimeEvent.Parse(json);

        Assert.NotNull(evt);
        Assert.Equal("", evt.Type);
        Assert.Equal("e1", evt.EventId);
        Assert.Equal("s1", evt.SessionId);
    }

    [Fact]
    [DisplayName("RealtimeEvent.Parse—缺失可选键事件不中断")]
    public void Parse_MissingOptionalKeys_NoThrow()
    {
        // 仅含 type 的最小事件，session/response/delta 均缺失，不应抛 KeyNotFoundException
        var json = """{"type":"response.done"}""";

        var evt = RealtimeEvent.Parse(json);

        Assert.NotNull(evt);
        Assert.Equal("response.done", evt.Type);
        Assert.Null(evt.SessionId);
        Assert.Null(evt.ResponseId);
        Assert.Equal(0, evt.ItemIndex);
    }
}
