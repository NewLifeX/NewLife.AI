#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using NewLife.AI.Clients.DashScope;
using Xunit;

namespace XUnitTest.Clients;

/// <summary>TTS WebSocket 路径多事件解析测试（A-108：JsonParser.Decode 对多顶层对象静默返回第一个，后续事件丢失）</summary>
[DisplayName("DashScope TTS 多事件解析测试")]
public class DashScopeTtsEventParseTests
{
    [Fact]
    [DisplayName("ParseJsonEvents—单事件正常解析")]
    public void SingleEvent_Parses()
    {
        var json = """{"type":"session.created","session":{"id":"s1"}}""";

        var events = DashScopeChatClient.ParseJsonEvents(json);

        Assert.Single(events);
        Assert.Equal("session.created", events[0]["type"] as String);
    }

    [Fact]
    [DisplayName("ParseJsonEvents—多事件拼接全部解析（A-108修复）")]
    public void MultipleEvents_AllParsed()
    {
        // A-108 修复前：JsonParser.Decode 静默返回第一个，response.done 丢失
        var json = """{"type":"response.audio.delta","delta":"AAA"}{"type":"response.done","response":{"usage":{"characters":10}}}""";

        var events = DashScopeChatClient.ParseJsonEvents(json);

        Assert.Equal(2, events.Count);
        Assert.Equal("response.audio.delta", events[0]["type"] as String);
        Assert.Equal("response.done", events[1]["type"] as String);
    }

    [Fact]
    [DisplayName("ParseJsonEvents—字符串内大括号不干扰切分")]
    public void BracesInString_Handled()
    {
        var json = """{"type":"response.text.delta","delta":"含{花括号}"}{"type":"response.done"}""";

        var events = DashScopeChatClient.ParseJsonEvents(json);

        Assert.Equal(2, events.Count);
        Assert.Equal("response.done", events[1]["type"] as String);
    }

    [Fact]
    [DisplayName("ParseJsonEvents—三个事件连续拼接")]
    public void ThreeEvents_AllParsed()
    {
        var json = """{"type":"a"}{"type":"b"}{"type":"c"}""";

        var events = DashScopeChatClient.ParseJsonEvents(json);

        Assert.Equal(new[] { "a", "b", "c" }, events.Select(e => e["type"] as String).ToArray());
    }

    [Fact]
    [DisplayName("ParseJsonEvents—空文本返回空列表")]
    public void Empty_ReturnsEmpty()
    {
        Assert.Empty(DashScopeChatClient.ParseJsonEvents(""));
        Assert.Empty(DashScopeChatClient.ParseJsonEvents("   "));
    }

    [Fact]
    [DisplayName("ParseJsonEvents—无法配对的残缺尾部被丢弃")]
    public void TruncatedTail_Dropped()
    {
        // 第一个完整 + 尾部残缺 {（未配对）
        var json = """{"type":"a"}""";
        var events = DashScopeChatClient.ParseJsonEvents(json + "{" );

        Assert.Single(events);
        Assert.Equal("a", events[0]["type"] as String);
    }
}
