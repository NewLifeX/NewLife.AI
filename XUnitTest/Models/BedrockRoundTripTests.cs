#nullable enable
using System;
using System.ComponentModel;
using NewLife.AI.Clients.Bedrock;
using NewLife.AI.Models;
using Xunit;

namespace XUnitTest.Models;

/// <summary>BedrockRequest ToChatRequest 往返一致性测试（P3：恢复 ToolCalls/ToolCallId，与 FromChatRequest 对称）</summary>
[DisplayName("BedrockRequest 往返一致性测试")]
public class BedrockRoundTripTests
{
    [Fact]
    [DisplayName("ToChatRequest—工具调用往返恢复ToolCalls")]
    public void ToChatRequest_RoundTripToolCall_RestoresToolCalls()
    {
        var original = new ChatRequest { Model = "claude-sonnet" };
        original.Messages.Add(new ChatMessage { Role = "user", Content = "查天气" });
        original.Messages.Add(new ChatMessage
        {
            Role = "assistant",
            Content = "",
            ToolCalls =
            [
                new ToolCall
                {
                    Id = "use_1",
                    Type = "function",
                    Function = new FunctionCall { Name = "get_weather", Arguments = """{"city":"北京"}""" }
                }
            ],
        });

        var wire = BedrockRequest.FromChatRequest(original);
        var restored = wire.ToChatRequest();

        // 第 2 条为 assistant 工具调用消息，应恢复 ToolCalls
        var msg = restored.Messages[1];
        Assert.NotNull(msg.ToolCalls);
        Assert.Single(msg.ToolCalls!);
        Assert.Equal("use_1", msg.ToolCalls![0].Id);
        Assert.Equal("get_weather", msg.ToolCalls![0].Function!.Name);
        Assert.Contains("北京", msg.ToolCalls![0].Function!.Arguments!);
    }

    [Fact]
    [DisplayName("ToChatRequest—工具结果往返恢复ToolCallId与内容")]
    public void ToChatRequest_RoundTripToolResult_RestoresToolCallId()
    {
        var original = new ChatRequest { Model = "claude-sonnet" };
        original.Messages.Add(new ChatMessage { Role = "user", Content = "查天气" });
        original.Messages.Add(new ChatMessage
        {
            Role = "tool",
            ToolCallId = "use_1",
            Content = """{"temp":26}""",
        });

        var wire = BedrockRequest.FromChatRequest(original);
        var restored = wire.ToChatRequest();

        // 第 2 条为工具结果消息，应恢复 ToolCallId 与内容
        var msg = restored.Messages[1];
        Assert.Equal("use_1", msg.ToolCallId);
        Assert.Contains("26", msg.Content as String ?? "");
    }
}
