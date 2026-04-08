using System;
using System.Collections.Generic;
using System.ComponentModel;
using NewLife.AI.Clients.OpenAI;
using NewLife.AI.Models;
using NewLife.Serialization;
using Xunit;

namespace XUnitTest.Models;

/// <summary>ChatCompletionResponse 模型类单元测试</summary>
[DisplayName("ChatCompletionResponse 单元测试")]
public class ChatCompletionResponseTests
{
    #region JSON 反序列化
    [Fact]
    [DisplayName("JSON 反序列化—标准 OpenAI 响应")]
    public void JsonDeserialize_StandardResponse()
    {
        var json = """
        {
            "id": "chatcmpl-123",
            "object": "chat.completion",
            "created": 1700000000,
            "model": "gpt-4o",
            "choices": [
                {
                    "index": 0,
                    "message": {
                        "role": "assistant",
                        "content": "Hello!"
                    },
                    "finish_reason": "stop"
                }
            ],
            "usage": {
                "prompt_tokens": 10,
                "completion_tokens": 5,
                "total_tokens": 15
            }
        }
        """;

        var result = json.ToJsonEntity<ChatCompletionResponse>(OpenAIChatClient.DefaultJsonOptions);

        Assert.NotNull(result);
        Assert.Equal("chatcmpl-123", result!.Id);
        Assert.Equal("chat.completion", result.Object);
        Assert.Equal(1700000000, result.Created);
        Assert.Equal("gpt-4o", result.Model);
        Assert.NotNull(result.Choices);
        Assert.Single(result.Choices!);
        Assert.Equal("assistant", result.Choices![0].Message?.Role);
        Assert.Equal("Hello!", result.Choices[0].Message?.Content?.ToString());
        Assert.Equal("stop", result.Choices[0].FinishReason);
        Assert.NotNull(result.Usage);
        Assert.Equal(10, result.Usage!.PromptTokens);
        Assert.Equal(5, result.Usage.CompletionTokens);
        Assert.Equal(15, result.Usage.TotalTokens);
    }

    [Fact]
    [DisplayName("JSON 反序列化—含工具调用")]
    public void JsonDeserialize_WithToolCalls()
    {
        var json = """
        {
            "id": "chatcmpl-456",
            "object": "chat.completion",
            "created": 1700000000,
            "model": "gpt-4o",
            "choices": [
                {
                    "index": 0,
                    "message": {
                        "role": "assistant",
                        "content": null,
                        "tool_calls": [
                            {
                                "id": "call_abc",
                                "type": "function",
                                "function": {
                                    "name": "get_weather",
                                    "arguments": "{\"city\":\"Beijing\"}"
                                }
                            }
                        ]
                    },
                    "finish_reason": "tool_calls"
                }
            ]
        }
        """;

        var result = json.ToJsonEntity<ChatCompletionResponse>();

        Assert.NotNull(result);
        Assert.NotNull(result!.Choices);
        var msg = result.Choices![0].Message;
        Assert.NotNull(msg);
        Assert.NotNull(msg!.ToolCalls);
        Assert.Single(msg.ToolCalls!);
        Assert.Equal("call_abc", msg.ToolCalls![0].Id);
        Assert.Equal("get_weather", msg.ToolCalls[0].Function?.Name);
    }

    [Fact]
    [DisplayName("JSON 反序列化—含 reasoning_content")]
    public void JsonDeserialize_WithReasoningContent()
    {
        var json = """
        {
            "id": "chatcmpl-789",
            "object": "chat.completion",
            "created": 1700000000,
            "model": "qwq-plus",
            "choices": [
                {
                    "index": 0,
                    "message": {
                        "role": "assistant",
                        "content": "答案是42",
                        "reasoning_content": "让我分析一下..."
                    },
                    "finish_reason": "stop"
                }
            ]
        }
        """;

        var result = json.ToJsonEntity<ChatCompletionResponse>();

        Assert.NotNull(result);
        var msg = result!.Choices![0].Message;
        Assert.NotNull(msg);
        Assert.Equal("答案是42", msg!.Content?.ToString());
        Assert.Equal("让我分析一下...", msg.ReasoningContent);
    }
    #endregion

    #region ToChatResponse
    [Fact]
    [DisplayName("ToChatResponse—基本字段映射")]
    public void ToChatResponse_BasicFields()
    {
        var ccr = new ChatCompletionResponse
        {
            Id = "chatcmpl-123",
            Object = "chat.completion",
            Created = 1700000000,
            Model = "gpt-4o",
            Choices =
            [
                new CompletionChoice
                {
                    Index = 0,
                    Message = new ChatMessage { Role = "assistant", Content = "Hi" },
                    FinishReason = "stop",
                }
            ],
            Usage = new CompletionUsage { PromptTokens = 10, CompletionTokens = 5, TotalTokens = 15 },
        };

        var result = ccr.ToChatResponse();

        Assert.Equal("chatcmpl-123", result.Id);
        Assert.Equal("chat.completion", result.Object);
        Assert.Equal("gpt-4o", result.Model);
        Assert.NotNull(result.Messages);
        Assert.Single(result.Messages!);
        Assert.Equal("Hi", result.Messages![0].Message?.Content?.ToString());
        Assert.Equal(FinishReason.Stop, result.Messages[0].FinishReason);
        Assert.NotNull(result.Usage);
        Assert.Equal(10, result.Usage!.InputTokens);
        Assert.Equal(5, result.Usage.OutputTokens);
        Assert.Equal(15, result.Usage.TotalTokens);
    }

    [Fact]
    [DisplayName("ToChatResponse—流式 chunk Delta 映射")]
    public void ToChatResponse_StreamChunk()
    {
        var ccr = new ChatCompletionResponse
        {
            Id = "chatcmpl-chunk",
            Object = "chat.completion.chunk",
            Model = "gpt-4o",
            Choices =
            [
                new CompletionChoice
                {
                    Index = 0,
                    Delta = new ChatMessage { Role = "assistant", Content = "He" },
                }
            ]
        };

        var result = ccr.ToChatResponse();

        Assert.Equal("chat.completion.chunk", result.Object);
        Assert.NotNull(result.Messages);
        Assert.NotNull(result.Messages![0].Delta);
        Assert.Equal("He", result.Messages[0].Delta!.Content?.ToString());
    }
    #endregion

    #region From / FromChunk
    [Fact]
    [DisplayName("From—从 ChatResponse 反向转换")]
    public void From_ReverseConversion()
    {
        var response = new ChatResponse
        {
            Id = "resp-1",
            Object = "chat.completion",
            Model = "gpt-4o",
        };
        response.Add("Hello world", null, FinishReason.Stop);
        response.Usage = new UsageDetails { InputTokens = 10, OutputTokens = 5, TotalTokens = 15 };

        var result = ChatCompletionResponse.From(response);

        Assert.Equal("resp-1", result.Id);
        Assert.Equal("gpt-4o", result.Model);
        Assert.NotNull(result.Choices);
        Assert.Single(result.Choices!);
        Assert.NotNull(result.Usage);
        Assert.Equal(10, result.Usage!.PromptTokens);
        Assert.Equal(5, result.Usage.CompletionTokens);
    }

    [Fact]
    [DisplayName("FromChunk—流式块反向转换")]
    public void FromChunk_StreamConversion()
    {
        var chunk = new ChatResponse
        {
            Id = "chunk-1",
            Object = "chat.completion.chunk",
            Model = "gpt-4o",
            Messages =
            [
                new ChatChoice { Index = 0, Delta = new ChatMessage { Role = "assistant", Content = "He" } }
            ],
        };

        var result = ChatCompletionResponse.FromChunk(chunk);

        Assert.Equal("chat.completion.chunk", result.Object);
        Assert.NotNull(result.Choices);
        Assert.NotNull(result.Choices![0].Delta);
        Assert.Null(result.Choices[0].Message);
    }
    #endregion
}
