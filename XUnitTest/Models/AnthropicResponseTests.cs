using System;
using System.Collections.Generic;
using System.ComponentModel;
using NewLife.AI.Clients.Anthropic;
using NewLife.AI.Models;
using NewLife.Serialization;
using Xunit;

namespace XUnitTest.Models;

/// <summary>AnthropicResponse 模型类单元测试</summary>
[DisplayName("AnthropicResponse 单元测试")]
public class AnthropicResponseTests
{
    #region JSON 反序列化
    [Fact]
    [DisplayName("JSON 反序列化—标准 Anthropic 响应")]
    public void JsonDeserialize_StandardResponse()
    {
        var json = @"{
            ""id"": ""msg_123"",
            ""type"": ""message"",
            ""role"": ""assistant"",
            ""model"": ""claude-sonnet-4-20250514"",
            ""content"": [
                {""type"": ""text"", ""text"": ""Hello from Claude!""}
            ],
            ""stop_reason"": ""end_turn"",
            ""usage"": {
                ""input_tokens"": 10,
                ""output_tokens"": 5
            }
        }";

        var result = json.ToJsonEntity<AnthropicResponse>();

        Assert.NotNull(result);
        Assert.Equal("msg_123", result!.Id);
        Assert.Equal("message", result.Type);
        Assert.Equal("assistant", result.Role);
        Assert.Equal("claude-sonnet-4-20250514", result.Model);
        Assert.Equal("end_turn", result.StopReason);
        Assert.NotNull(result.Content);
        Assert.Single(result.Content!);
        Assert.Equal("text", result.Content![0].Type);
        Assert.Equal("Hello from Claude!", result.Content[0].Text);
        Assert.NotNull(result.Usage);
        Assert.Equal(10, result.Usage!.InputTokens);
        Assert.Equal(5, result.Usage.OutputTokens);
    }

    [Fact]
    [DisplayName("JSON 反序列化—含 thinking 内容块")]
    public void JsonDeserialize_WithThinking()
    {
        var json = @"{
            ""id"": ""msg_456"",
            ""type"": ""message"",
            ""role"": ""assistant"",
            ""model"": ""claude-sonnet-4-20250514"",
            ""content"": [
                {""type"": ""thinking"", ""thinking"": ""Let me think...""},
                {""type"": ""text"", ""text"": ""The answer is 42""}
            ],
            ""stop_reason"": ""end_turn"",
            ""usage"": {""input_tokens"": 20, ""output_tokens"": 15}
        }";

        var result = json.ToJsonEntity<AnthropicResponse>();

        Assert.NotNull(result);
        Assert.Equal(2, result!.Content!.Count);
        Assert.Equal("thinking", result.Content[0].Type);
        Assert.Equal("Let me think...", result.Content[0].Thinking);
        Assert.Equal("text", result.Content[1].Type);
        Assert.Equal("The answer is 42", result.Content[1].Text);
    }

    [Fact]
    [DisplayName("JSON 反序列化—含 tool_use 内容块")]
    public void JsonDeserialize_WithToolUse()
    {
        var json = @"{
            ""id"": ""msg_789"",
            ""type"": ""message"",
            ""role"": ""assistant"",
            ""model"": ""claude-sonnet-4-20250514"",
            ""content"": [
                {
                    ""type"": ""tool_use"",
                    ""id"": ""toolu_123"",
                    ""name"": ""get_weather"",
                    ""input"": {""city"": ""Beijing""}
                }
            ],
            ""stop_reason"": ""tool_use"",
            ""usage"": {""input_tokens"": 30, ""output_tokens"": 10}
        }";

        var result = json.ToJsonEntity<AnthropicResponse>();

        Assert.NotNull(result);
        Assert.Single(result!.Content!);
        Assert.Equal("tool_use", result.Content![0].Type);
        Assert.Equal("toolu_123", result.Content[0].Id);
        Assert.Equal("get_weather", result.Content[0].Name);
        Assert.NotNull(result.Content[0].Input);
    }
    #endregion

    #region ToChatResponse
    [Fact]
    [DisplayName("ToChatResponse—基本文本响应")]
    public void ToChatResponse_BasicText()
    {
        var resp = new AnthropicResponse
        {
            Id = "msg_123",
            Type = "message",
            Role = "assistant",
            Model = "claude-sonnet-4-20250514",
            Content =
            [
                new AnthropicContentBlock { Type = "text", Text = "Hello!" }
            ],
            StopReason = "end_turn",
            Usage = new AnthropicUsage { InputTokens = 10, OutputTokens = 5 },
        };

        var result = resp.ToChatResponse();

        Assert.Equal("msg_123", result.Id);
        Assert.NotNull(result.Messages);
        Assert.Single(result.Messages!);
        Assert.Equal(FinishReason.Stop, result.Messages![0].FinishReason);
        Assert.NotNull(result.Usage);
        Assert.Equal(10, result.Usage!.InputTokens);
        Assert.Equal(5, result.Usage.OutputTokens);
    }

    [Fact]
    [DisplayName("ToChatResponse—thinking 内容映射为 ReasoningContent")]
    public void ToChatResponse_Thinking()
    {
        var resp = new AnthropicResponse
        {
            Id = "msg_456",
            Type = "message",
            Role = "assistant",
            Content =
            [
                new AnthropicContentBlock { Type = "thinking", Thinking = "Let me analyze..." },
                new AnthropicContentBlock { Type = "text", Text = "The answer is 42" },
            ],
            StopReason = "end_turn",
        };

        var result = resp.ToChatResponse();

        var msg = result.Messages![0].Message;
        Assert.NotNull(msg);
        Assert.Equal("The answer is 42", msg!.Content?.ToString());
        Assert.Equal("Let me analyze...", msg.ReasoningContent);
    }

    [Fact]
    [DisplayName("ToChatResponse—tool_use 转换为 ToolCalls")]
    public void ToChatResponse_ToolUse()
    {
        var resp = new AnthropicResponse
        {
            Id = "msg_789",
            Type = "message",
            Role = "assistant",
            Content =
            [
                new AnthropicContentBlock
                {
                    Type = "tool_use",
                    Id = "toolu_123",
                    Name = "get_weather",
                    Input = new Dictionary<String, Object> { ["city"] = "Beijing" },
                }
            ],
            StopReason = "tool_use",
        };

        var result = resp.ToChatResponse();

        var msg = result.Messages![0].Message;
        Assert.NotNull(msg);
        Assert.NotNull(msg!.ToolCalls);
        Assert.Single(msg.ToolCalls!);
        Assert.Equal("toolu_123", msg.ToolCalls![0].Id);
        Assert.Equal("get_weather", msg.ToolCalls[0].Function?.Name);
        Assert.Contains("Beijing", msg.ToolCalls[0].Function?.Arguments);
    }

    [Fact]
    [DisplayName("MapStopReason—stop_reason 到 finish_reason 映射")]
    public void MapStopReason_Mapping()
    {
        Assert.Equal(FinishReason.Stop, AnthropicResponse.MapStopReason("end_turn"));
        Assert.Equal(FinishReason.Length, AnthropicResponse.MapStopReason("max_tokens"));
        Assert.Equal(FinishReason.ToolCalls, AnthropicResponse.MapStopReason("tool_use"));
        Assert.Null(AnthropicResponse.MapStopReason(null));
    }
    #endregion

    #region From
    [Fact]
    [DisplayName("From—从 ChatResponse 反向转换")]
    public void From_ReverseConversion()
    {
        var response = new ChatResponse
        {
            Id = "resp-1",
            Model = "claude-sonnet-4-20250514",
        };
        response.Add("Hello from Claude", null, FinishReason.Stop);
        response.Usage = new UsageDetails { InputTokens = 10, OutputTokens = 5 };

        var result = AnthropicResponse.From(response);

        Assert.Equal("message", result.Type);
        Assert.Equal("assistant", result.Role);
        Assert.NotNull(result.Content);
        Assert.Single(result.Content!);
        Assert.Equal("text", result.Content![0].Type);
        Assert.NotNull(result.Usage);
        Assert.Equal(10, result.Usage!.InputTokens);
    }
    #endregion

    #region AnthropicStreamEvent.ToChunkResponse

    [Fact]
    [DisplayName("ToChunkResponse_message_start事件_返回InputTokens用量")]
    public void ToChunkResponse_MessageStart_ReturnsInputTokensUsage()
    {
        var json = @"{""type"":""message_start"",""message"":{""usage"":{""input_tokens"":25}}}";
        var ev = json.ToJsonEntity<AnthropicStreamEvent>();
        Assert.NotNull(ev);

        var chunk = ev!.ToChunkResponse("claude-sonnet-4");

        Assert.NotNull(chunk);
        Assert.Equal("claude-sonnet-4", chunk!.Model);
        Assert.Equal("chat.completion.chunk", chunk.Object);
        Assert.NotNull(chunk.Usage);
        Assert.Equal(25, chunk.Usage!.InputTokens);
    }

    [Fact]
    [DisplayName("ToChunkResponse_content_block_delta文本增量_返回文本内容")]
    public void ToChunkResponse_ContentBlockDelta_TextDelta_ReturnsTextChunk()
    {
        var json = @"{""type"":""content_block_delta"",""index"":0,""delta"":{""type"":""text_delta"",""text"":""Hello""}}";
        var ev = json.ToJsonEntity<AnthropicStreamEvent>();
        Assert.NotNull(ev);

        var chunk = ev!.ToChunkResponse("claude-haiku");

        Assert.NotNull(chunk);
        Assert.NotNull(chunk!.Messages);
        var content = chunk.Messages![0].Delta?.Content as String;
        Assert.Equal("Hello", content);
    }

    [Fact]
    [DisplayName("ToChunkResponse_content_block_delta思考增量_返回thinking内容")]
    public void ToChunkResponse_ContentBlockDelta_ThinkingDelta_ReturnsThinkingChunk()
    {
        var json = @"{""type"":""content_block_delta"",""index"":0,""delta"":{""type"":""thinking_delta"",""thinking"":""Let me think...""}}";
        var ev = json.ToJsonEntity<AnthropicStreamEvent>();
        Assert.NotNull(ev);

        var chunk = ev!.ToChunkResponse("claude-sonnet-4");

        Assert.NotNull(chunk);
        Assert.NotNull(chunk!.Messages);
        var reasoning = chunk.Messages![0].Delta?.ReasoningContent;
        Assert.Equal("Let me think...", reasoning);
    }

    [Fact]
    [DisplayName("ToChunkResponse_message_delta_返回结束原因和OutputTokens")]
    public void ToChunkResponse_MessageDelta_ReturnsFinishReasonAndOutputTokens()
    {
        var json = @"{""type"":""message_delta"",""delta"":{""stop_reason"":""end_turn""},""usage"":{""output_tokens"":42}}";
        var ev = json.ToJsonEntity<AnthropicStreamEvent>();
        Assert.NotNull(ev);

        var chunk = ev!.ToChunkResponse("claude-sonnet-4");

        Assert.NotNull(chunk);
        Assert.NotNull(chunk!.Messages);
        Assert.Equal(FinishReason.Stop, chunk.Messages![0].FinishReason);
        Assert.NotNull(chunk.Usage);
        Assert.Equal(42, chunk.Usage!.OutputTokens);
    }

    [Fact]
    [DisplayName("ToChunkResponse_message_stop_返回null")]
    public void ToChunkResponse_MessageStop_ReturnsNull()
    {
        var json = @"{""type"":""message_stop""}";
        var ev = json.ToJsonEntity<AnthropicStreamEvent>();
        Assert.NotNull(ev);

        var chunk = ev!.ToChunkResponse("claude-sonnet-4");

        Assert.Null(chunk);
    }

    [Fact]
    [DisplayName("ToChunkResponse_未知类型_返回null")]
    public void ToChunkResponse_UnknownType_ReturnsNull()
    {
        var json = @"{""type"":""content_block_start"",""index"":0}";
        var ev = json.ToJsonEntity<AnthropicStreamEvent>();
        Assert.NotNull(ev);

        var chunk = ev!.ToChunkResponse("claude-sonnet-4");

        Assert.Null(chunk);
    }

    #endregion
}
