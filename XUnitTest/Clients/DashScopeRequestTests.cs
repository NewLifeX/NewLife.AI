using System;
using System.Collections.Generic;
using System.ComponentModel;
using NewLife.AI.Clients.DashScope;
using NewLife.AI.Models;
using Xunit;

namespace XUnitTest.Clients;

/// <summary>DashScopeRequest 请求体转换测试</summary>
[DisplayName("DashScopeRequest 请求体转换测试")]
public class DashScopeRequestTests
{
    [Fact]
    [DisplayName("BuildMessages_跳过空白助手占位消息")]
    public void BuildMessages_SkipBlankAssistantPlaceholder()
    {
        var messages = new List<ChatMessage>
        {
            new() { Role = "system", Content = "你是助手" },
            new() { Role = "user", Content = "你好" },
            new() { Role = "assistant", Content = null },
        };

        var result = DashScopeRequest.BuildMessages(messages, false);

        Assert.Equal(2, result.Count);
        Assert.Equal("system", result[0].Role);
        Assert.Equal("user", result[1].Role);
    }

    [Fact]
    [DisplayName("BuildMessages_工具调用助手消息自动补齐空Content")]
    public void BuildMessages_AssistantToolCall_FillsEmptyContent()
    {
        var messages = new List<ChatMessage>
        {
            new()
            {
                Role = "assistant",
                ToolCalls =
                [
                    new ToolCall
                    {
                        Id = "call_001",
                        Type = "function",
                        Function = new FunctionCall { Name = "get_weather", Arguments = "{\"city\":\"Beijing\"}" }
                    }
                ]
            }
        };

        var result = DashScopeRequest.BuildMessages(messages, false);

        Assert.Single(result);
        Assert.Equal("assistant", result[0].Role);
        Assert.Equal(String.Empty, Assert.IsType<String>(result[0].Content));
        Assert.NotNull(result[0].ToolCalls);
        Assert.Single(result[0].ToolCalls!);
    }

    [Fact]
    [DisplayName("FromChatRequest_工具调用轮次不会生成空Content字段")]
    public void FromChatRequest_ToolRoundTrip_DoesNotLeaveNullContent()
    {
        var request = new ChatRequest
        {
            Model = "glm-5.1",
            Messages =
            [
                new ChatMessage { Role = "user", Content = "北京天气怎么样？" },
                new ChatMessage
                {
                    Role = "assistant",
                    ToolCalls =
                    [
                        new ToolCall
                        {
                            Id = "call_001",
                            Type = "function",
                            Function = new FunctionCall { Name = "get_weather", Arguments = "{\"city\":\"Beijing\"}" }
                        }
                    ]
                },
                new ChatMessage
                {
                    Role = "tool",
                    ToolCallId = "call_001",
                    Name = "get_weather",
                    Content = "{\"temperature\":25}"
                }
            ]
        };

        var result = DashScopeRequest.FromChatRequest(request, false);

        Assert.Equal(3, result.Input.Messages.Count);
        Assert.Equal(String.Empty, Assert.IsType<String>(result.Input.Messages[1].Content));
        Assert.Equal("{\"temperature\":25}", Assert.IsType<String>(result.Input.Messages[2].Content));
    }
}