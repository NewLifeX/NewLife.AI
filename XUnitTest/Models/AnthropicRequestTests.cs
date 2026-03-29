using System;
using System.Collections.Generic;
using System.ComponentModel;
using NewLife.AI.Models;
using NewLife.Serialization;
using Xunit;

namespace XUnitTest.Models;

/// <summary>AnthropicRequest 模型类单元测试</summary>
[DisplayName("AnthropicRequest 单元测试")]
public class AnthropicRequestTests
{
    #region FromChatRequest
    [Fact]
    [DisplayName("FromChatRequest—基本字段映射")]
    public void FromChatRequest_Basic()
    {
        var request = new ChatRequest { Model = "claude-sonnet-4-20250514", MaxTokens = 1024 };
        request.Messages.Add(new ChatMessage { Role = "user", Content = "Hello" });

        var result = AnthropicRequest.FromChatRequest(request);

        Assert.Equal("claude-sonnet-4-20250514", result.Model);
        Assert.Equal(1024, result.MaxTokens);
        Assert.NotNull(result.Messages);
        Assert.Single(result.Messages!);
        Assert.Equal("user", result.Messages![0].Role);
    }

    [Fact]
    [DisplayName("FromChatRequest—system 消息分离")]
    public void FromChatRequest_SystemSeparation()
    {
        var request = new ChatRequest { Model = "claude-sonnet-4-20250514" };
        request.Messages.Add(new ChatMessage { Role = "system", Content = "你是一个助手" });
        request.Messages.Add(new ChatMessage { Role = "user", Content = "你好" });

        var result = AnthropicRequest.FromChatRequest(request);

        // system 消息应分离到 System 属性
        Assert.NotNull(result.System);
        // Messages 中不含 system 角色
        Assert.Single(result.Messages!);
        Assert.Equal("user", result.Messages![0].Role);
    }

    [Fact]
    [DisplayName("FromChatRequest—tool_result 消息转换")]
    public void FromChatRequest_ToolResult()
    {
        var request = new ChatRequest { Model = "claude-sonnet-4-20250514" };
        request.Messages.Add(new ChatMessage
        {
            Role = "tool",
            ToolCallId = "call_123",
            Content = "{\"result\": \"sunny\"}",
        });

        var result = AnthropicRequest.FromChatRequest(request);

        Assert.NotNull(result.Messages);
        Assert.Single(result.Messages!);
        Assert.Equal("user", result.Messages![0].Role);
    }

    [Fact]
    [DisplayName("FromChatRequest—工具调用消息的 arguments 转为对象")]
    public void FromChatRequest_ToolCallArguments()
    {
        var request = new ChatRequest { Model = "claude-sonnet-4-20250514" };
        request.Messages.Add(new ChatMessage
        {
            Role = "assistant",
            ToolCalls =
            [
                new ToolCall
                {
                    Id = "call_abc",
                    Type = "function",
                    Function = new FunctionCall
                    {
                        Name = "get_weather",
                        Arguments = "{\"city\":\"Beijing\"}"
                    }
                }
            ]
        });

        var result = AnthropicRequest.FromChatRequest(request);

        Assert.NotNull(result.Messages);
        Assert.Equal("assistant", result.Messages![0].Role);
    }

    [Fact]
    [DisplayName("FromChatRequest—EnableThinking 不影响 Request 本体")]
    public void FromChatRequest_Thinking()
    {
        var request = new ChatRequest
        {
            Model = "claude-sonnet-4-20250514",
            EnableThinking = true,
        };
        request.Messages.Add(new ChatMessage { Role = "user", Content = "思考" });

        var result = AnthropicRequest.FromChatRequest(request);

        // EnableThinking 由 AnthropicChatClient.BuildRequest 处理，不在 FromChatRequest 中体现
        Assert.Equal("claude-sonnet-4-20250514", result.Model);
        Assert.Single(result.Messages);
    }
    #endregion

    #region ToChatRequest
    [Fact]
    [DisplayName("ToChatRequest—往返转换")]
    public void ToChatRequest_RoundTrip()
    {
        var original = new ChatRequest { Model = "claude-sonnet-4-20250514", MaxTokens = 500 };
        original.Messages.Add(new ChatMessage { Role = "user", Content = "测试" });

        var anthropic = AnthropicRequest.FromChatRequest(original);
        var restored = anthropic.ToChatRequest();

        Assert.Equal("claude-sonnet-4-20250514", restored.Model);
        Assert.Equal("user", restored.Messages[0].Role);
        Assert.Equal("测试", restored.Messages[0].Content?.ToString());
    }
    #endregion
}
