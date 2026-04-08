using System;
using System.Collections.Generic;
using System.ComponentModel;
using NewLife.AI.Clients.OpenAI;
using NewLife.AI.Models;
using NewLife.Serialization;
using Xunit;

namespace XUnitTest.Models;

/// <summary>ChatCompletionRequest 模型类单元测试</summary>
[DisplayName("ChatCompletionRequest 单元测试")]
public class ChatCompletionRequestTests
{
    #region FromChatRequest
    [Fact]
    [DisplayName("FromChatRequest—基本字段映射正确")]
    public void FromChatRequest_BasicFields()
    {
        var request = new ChatRequest
        {
            Model = "gpt-4o",
            Stream = false,
            Temperature = 0.7,
            TopP = 0.9,
            MaxTokens = 1024,
            User = "test-user",
        };
        request.Messages.Add(new ChatMessage { Role = "user", Content = "Hello" });

        var result = ChatCompletionRequest.FromChatRequest(request);

        Assert.Equal("gpt-4o", result.Model);
        Assert.False(result.Stream);
        Assert.Equal(0.7, result.Temperature);
        Assert.Equal(0.9, result.TopP);
        Assert.Equal(1024, result.MaxTokens);
        Assert.Equal("test-user", result.User);
        Assert.Single(result.Messages);
        Assert.Equal("user", result.Messages[0].Role);
        Assert.Equal("Hello", result.Messages[0].Content);
    }

    [Fact]
    [DisplayName("FromChatRequest—流式模式自动添加 stream_options")]
    public void FromChatRequest_StreamOptions()
    {
        var request = new ChatRequest { Model = "gpt-4o", Stream = true };
        request.Messages.Add(new ChatMessage { Role = "user", Content = "Hi" });

        var result = ChatCompletionRequest.FromChatRequest(request);

        Assert.True(result.Stream);
        Assert.NotNull(result.StreamOptions);
        Assert.True((Boolean)result.StreamOptions!["include_usage"]);
    }

    [Fact]
    [DisplayName("FromChatRequest—工具定义保留")]
    public void FromChatRequest_Tools()
    {
        var request = new ChatRequest { Model = "gpt-4o" };
        request.Messages.Add(new ChatMessage { Role = "user", Content = "天气" });
        request.Tools =
        [
            new ChatTool
            {
                Type = "function",
                Function = new FunctionDefinition
                {
                    Name = "get_weather",
                    Description = "获取天气",
                }
            }
        ];

        var result = ChatCompletionRequest.FromChatRequest(request);

        Assert.NotNull(result.Tools);
        Assert.Single(result.Tools);
        Assert.Equal("get_weather", result.Tools[0].Function?.Name);
    }

    [Fact]
    [DisplayName("FromChatRequest—多模态 Contents 转换为 Content")]
    public void FromChatRequest_MultimodalContents()
    {
        var request = new ChatRequest { Model = "gpt-4o" };
        var msg = new ChatMessage { Role = "user" };
        msg.Contents = new List<AIContent>
        {
            new TextContent("描述这张图片"),
            new ImageContent { Uri = "https://example.com/image.jpg" },
        };
        request.Messages.Add(msg);

        var result = ChatCompletionRequest.FromChatRequest(request);

        Assert.Single(result.Messages);
        // Contents 被转换为多模态数组
        Assert.IsAssignableFrom<IList<Object>>(result.Messages[0].Content);
    }

    [Fact]
    [DisplayName("FromChatRequest—ToolCalls 消息保留")]
    public void FromChatRequest_ToolCallsPreserved()
    {
        var request = new ChatRequest { Model = "gpt-4o" };
        var msg = new ChatMessage
        {
            Role = "assistant",
            ToolCalls =
            [
                new ToolCall
                {
                    Id = "call_123",
                    Type = "function",
                    Function = new FunctionCall { Name = "get_weather", Arguments = "{\"city\":\"Beijing\"}" },
                }
            ]
        };
        request.Messages.Add(msg);

        var result = ChatCompletionRequest.FromChatRequest(request);

        Assert.Single(result.Messages);
        Assert.NotNull(result.Messages[0].ToolCalls);
        Assert.Single(result.Messages[0].ToolCalls!);
        Assert.Equal("call_123", result.Messages[0].ToolCalls![0].Id);
    }

    [Fact]
    [DisplayName("FromChatRequest—EnableThinking 传递")]
    public void FromChatRequest_EnableThinking()
    {
        var request = new ChatRequest { Model = "qwen3", EnableThinking = true };
        request.Messages.Add(new ChatMessage { Role = "user", Content = "思考" });

        var result = ChatCompletionRequest.FromChatRequest(request);

        Assert.True(result.EnableThinking);
    }
    #endregion

    #region ToChatRequest
    [Fact]
    [DisplayName("ToChatRequest—往返转换字段不丢失")]
    public void ToChatRequest_RoundTrip()
    {
        var original = new ChatRequest
        {
            Model = "gpt-4o",
            Stream = true,
            Temperature = 0.5,
            TopP = 0.8,
            MaxTokens = 2048,
            User = "user-1",
            EnableThinking = false,
        };
        original.Messages.Add(new ChatMessage { Role = "system", Content = "你是助手" });
        original.Messages.Add(new ChatMessage { Role = "user", Content = "你好" });

        var ccr = ChatCompletionRequest.FromChatRequest(original);
        var restored = ccr.ToChatRequest();

        Assert.Equal(original.Model, restored.Model);
        Assert.Equal(original.Stream, restored.Stream);
        Assert.Equal(original.Temperature, restored.Temperature);
        Assert.Equal(original.MaxTokens, restored.MaxTokens);
        Assert.Equal(original.User, restored.User);
        Assert.Equal(original.EnableThinking, restored.EnableThinking);
        Assert.Equal(2, restored.Messages.Count);
    }
    #endregion

    #region BuildContent
    [Fact]
    [DisplayName("BuildContent—单一文本返回字符串")]
    public void BuildContent_SingleText_ReturnsString()
    {
        var contents = new List<AIContent> { new TextContent("hello") };
        var result = ChatCompletionRequest.BuildContent(contents);

        Assert.IsType<String>(result);
        Assert.Equal("hello", result);
    }

    [Fact]
    [DisplayName("BuildContent—多模态返回数组")]
    public void BuildContent_Multimodal_ReturnsList()
    {
        var contents = new List<AIContent>
        {
            new TextContent("描述图片"),
            new ImageContent { Uri = "https://example.com/a.jpg" },
        };
        var result = ChatCompletionRequest.BuildContent(contents);

        Assert.IsAssignableFrom<IList<Object>>(result);
        var list = (IList<Object>)result;
        Assert.Equal(2, list.Count);
    }
    #endregion

    #region JSON 序列化
    [Fact]
    [DisplayName("JSON 反序列化—snake_case 字段正确映射")]
    public void JsonDeserialize_SnakeCaseFields()
    {
        var json = """
        {
            "model": "gpt-4o",
            "messages": [{"role": "user", "content": "Hi"}],
            "temperature": 0.7,
            "top_p": 0.9,
            "max_tokens": 100,
            "stream": true,
            "presence_penalty": 0.5,
            "frequency_penalty": 0.3,
            "enable_thinking": true
        }
        """;

        var result = json.ToJsonEntity<ChatCompletionRequest>(OpenAIChatClient.DefaultJsonOptions);

        Assert.NotNull(result);
        Assert.Equal("gpt-4o", result!.Model);
        Assert.Equal(0.7, result.Temperature);
        Assert.Equal(0.9, result.TopP);
        Assert.Equal(100, result.MaxTokens);
        Assert.True(result.Stream);
        Assert.Equal(0.5, result.PresencePenalty);
        Assert.Equal(0.3, result.FrequencyPenalty);
        Assert.True(result.EnableThinking);
    }
    #endregion
}
