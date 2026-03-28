using System;
using System.Collections.Generic;
using System.ComponentModel;
using NewLife.AI.Agents;
using NewLife.AI.Models;
using Xunit;

namespace XUnitTest.Agents;

/// <summary>ToolCallMessage、ToolCallResultMessage 及 AgentMessageHelper 补充测试</summary>
[DisplayName("Agent 工具调用消息测试")]
public class AgentMessageExtendedTests
{
    // ── ToolCallMessage ────────────────────────────────────────────────────

    [Fact]
    [DisplayName("ToolCallMessage—Type 枚举值为 ToolCall")]
    public void ToolCallMessage_Type_IsToolCall()
    {
        var msg = new ToolCallMessage();
        Assert.Equal(AgentMessageType.ToolCall, msg.Type);
    }

    [Fact]
    [DisplayName("ToolCallMessage—属性默认值正确")]
    public void ToolCallMessage_DefaultProperties()
    {
        var msg = new ToolCallMessage();
        Assert.Equal(String.Empty, msg.ToolName);
        Assert.Null(msg.Arguments);
        Assert.Equal(String.Empty, msg.CallId);
    }

    [Fact]
    [DisplayName("ToolCallMessage—属性可读写")]
    public void ToolCallMessage_Properties_ReadWrite()
    {
        var msg = new ToolCallMessage
        {
            ToolName = "get_weather",
            Arguments = "{\"city\":\"Beijing\"}",
            CallId = "call-001",
            Source = "agent-1",
        };

        Assert.Equal("get_weather", msg.ToolName);
        Assert.Equal("{\"city\":\"Beijing\"}", msg.Arguments);
        Assert.Equal("call-001", msg.CallId);
        Assert.Equal("agent-1", msg.Source);
    }

    [Fact]
    [DisplayName("ToolCallMessage—ToChatMessage 返回 assistant 角色")]
    public void ToolCallMessage_ToChatMessage_ReturnsAssistantRole()
    {
        var msg = new ToolCallMessage { ToolName = "search", CallId = "c1", Arguments = "{}" };

        var cm = msg.ToChatMessage()!;

        Assert.Equal("assistant", cm.Role);
    }

    [Fact]
    [DisplayName("ToolCallMessage—ToChatMessage 中 ToolCalls 包含正确信息")]
    public void ToolCallMessage_ToChatMessage_ToolCallsPopulated()
    {
        var msg = new ToolCallMessage
        {
            ToolName = "calculator",
            Arguments = "{\"expression\":\"2+3\"}",
            CallId = "call-xyz",
        };

        var cm = msg.ToChatMessage()!;

        Assert.NotNull(cm.ToolCalls);
        Assert.Single(cm.ToolCalls);

        var tc = cm.ToolCalls[0];
        Assert.Equal("call-xyz", tc.Id);
        Assert.Equal("calculator", tc.Function!.Name);
        Assert.Equal("{\"expression\":\"2+3\"}", tc.Function.Arguments);
    }

    [Fact]
    [DisplayName("ToolCallMessage—Arguments 为 null 时 ToChatMessage 仍成功")]
    public void ToolCallMessage_ToChatMessage_NullArguments_Succeeds()
    {
        var msg = new ToolCallMessage { ToolName = "ping", CallId = "c2", Arguments = null };

        var cm = msg.ToChatMessage()!;

        Assert.Equal("assistant", cm.Role);
        Assert.Null(cm.ToolCalls![0].Function!.Arguments);
    }

    // ── ToolCallResultMessage ──────────────────────────────────────────────

    [Fact]
    [DisplayName("ToolCallResultMessage—Type 枚举值为 ToolCallResult")]
    public void ToolCallResultMessage_Type_IsToolCallResult()
    {
        var msg = new ToolCallResultMessage();
        Assert.Equal(AgentMessageType.ToolCallResult, msg.Type);
    }

    [Fact]
    [DisplayName("ToolCallResultMessage—属性默认值正确")]
    public void ToolCallResultMessage_DefaultProperties()
    {
        var msg = new ToolCallResultMessage();
        Assert.Equal(String.Empty, msg.CallId);
        Assert.Equal(String.Empty, msg.ToolName);
        Assert.Equal(String.Empty, msg.Result);
    }

    [Fact]
    [DisplayName("ToolCallResultMessage—属性可读写")]
    public void ToolCallResultMessage_Properties_ReadWrite()
    {
        var msg = new ToolCallResultMessage
        {
            CallId = "call-001",
            ToolName = "calculator",
            Result = "5",
            Source = "tool-agent",
        };

        Assert.Equal("call-001", msg.CallId);
        Assert.Equal("calculator", msg.ToolName);
        Assert.Equal("5", msg.Result);
        Assert.Equal("tool-agent", msg.Source);
    }

    [Fact]
    [DisplayName("ToolCallResultMessage—ToChatMessage 返回 tool 角色")]
    public void ToolCallResultMessage_ToChatMessage_ReturnsToolRole()
    {
        var msg = new ToolCallResultMessage { ToolName = "calculator", Result = "5" };

        var cm = msg.ToChatMessage()!;

        Assert.Equal("tool", cm.Role);
    }

    [Fact]
    [DisplayName("ToolCallResultMessage—ToChatMessage 中 Name 等于 ToolName")]
    public void ToolCallResultMessage_ToChatMessage_NameEqualsToolName()
    {
        var msg = new ToolCallResultMessage { ToolName = "calculator", Result = "5" };

        var cm = msg.ToChatMessage()!;

        Assert.Equal("calculator", cm.Name);
    }

    [Fact]
    [DisplayName("ToolCallResultMessage—ToChatMessage 中 Content 等于 Result")]
    public void ToolCallResultMessage_ToChatMessage_ContentEqualsResult()
    {
        var msg = new ToolCallResultMessage { ToolName = "search", Result = "[\"result1\",\"result2\"]" };

        var cm = msg.ToChatMessage()!;

        Assert.Equal("[\"result1\",\"result2\"]", cm.Content);
    }

    // ── AgentMessageHelper ─────────────────────────────────────────────────

    [Fact]
    [DisplayName("AgentMessageHelper—空列表返回空结果")]
    public void AgentMessageHelper_EmptyList_ReturnsEmpty()
    {
        var result = AgentMessageHelper.ToChatMessages([]);
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    [DisplayName("AgentMessageHelper—null 抛出 ArgumentNullException")]
    public void AgentMessageHelper_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => AgentMessageHelper.ToChatMessages(null!));
    }

    [Fact]
    [DisplayName("AgentMessageHelper—StopMessage 被过滤掉")]
    public void AgentMessageHelper_StopMessage_Filtered()
    {
        var messages = new List<AgentMessage>
        {
            new TextMessage { Content = "hello", Role = "user" },
            new StopMessage { Reason = "MaxRound" },
            new TextMessage { Content = "world", Role = "assistant" },
        };

        var result = AgentMessageHelper.ToChatMessages(messages);

        // StopMessage.ToChatMessage() returns null → filtered
        Assert.Equal(2, result.Count);
        Assert.All(result, cm => Assert.NotEqual("tool", cm.Role));
    }

    [Fact]
    [DisplayName("AgentMessageHelper—混合类型消息正确转换")]
    public void AgentMessageHelper_MixedMessages_ConvertedCorrectly()
    {
        var messages = new List<AgentMessage>
        {
            new SystemMessage { Content = "You are an assistant." },
            new TextMessage { Content = "What is 2+3?", Role = "user" },
            new ToolCallMessage { ToolName = "calculator", Arguments = "{\"expression\":\"2+3\"}", CallId = "c1" },
            new ToolCallResultMessage { ToolName = "calculator", Result = "5", CallId = "c1" },
        };

        var result = AgentMessageHelper.ToChatMessages(messages);

        Assert.Equal(4, result.Count);
        Assert.Equal("system", result[0].Role);
        Assert.Equal("user", result[1].Role);
        Assert.Equal("assistant", result[2].Role);
        Assert.Equal("tool", result[3].Role);
    }
}
