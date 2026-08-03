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

    [Fact]
    [DisplayName("FromChatRequest—o3推理模型MaxTokens映射为max_completion_tokens")]
    public void FromChatRequest_O3Model_MapsToMaxCompletionTokens()
    {
        var request = new ChatRequest { Model = "o3-mini", MaxTokens = 1000 };
        request.Messages.Add(new ChatMessage { Role = "user", Content = "hi" });

        var result = ChatCompletionRequest.FromChatRequest(request);

        Assert.Null(result.MaxTokens);
        Assert.Equal(1000, result.MaxCompletionTokens);

        // OpenAI 协议 SnakeCase 序列化：o 系列输出 max_completion_tokens，不输出 max_tokens
        using var client = new OpenAIClientBase("test-key");
        var json = client.JsonHost.Write(result, client.JsonOptions!)!;
        Assert.Contains("max_completion_tokens", json);
        Assert.DoesNotContain("\"max_tokens\"", json);
    }

    [Fact]
    [DisplayName("FromChatRequest—gpt4o模型仍使用max_tokens")]
    public void FromChatRequest_Gpt4oModel_KeepsMaxTokens()
    {
        var request = new ChatRequest { Model = "gpt-4o", MaxTokens = 1000 };
        request.Messages.Add(new ChatMessage { Role = "user", Content = "hi" });

        var result = ChatCompletionRequest.FromChatRequest(request);

        Assert.Equal(1000, result.MaxTokens);
        Assert.Null(result.MaxCompletionTokens);

        // gpt-4o 等非推理模型仍输出标准 max_tokens
        using var client = new OpenAIClientBase("test-key");
        var json = client.JsonHost.Write(result, client.JsonOptions!)!;
        Assert.Contains("\"max_tokens\":1000", json);
        Assert.DoesNotContain("max_completion_tokens", json);
    }

    [Fact]
    [DisplayName("BuildBody—o3推理模型输出max_completion_tokens")]
    public void BuildBody_O3Model_WritesMaxCompletionTokens()
    {
        var request = new ChatRequest { Model = "o3-mini", MaxTokens = 1000 };
        request.Messages.Add(new ChatMessage { Role = "user", Content = "hi" });

        var body = ChatCompletionRequest.BuildBody(request);

        Assert.False(body.ContainsKey("max_tokens"));
        Assert.Equal(1000, body["max_completion_tokens"]);
    }

    [Fact]
    [DisplayName("FromChatRequest—扩展Items透传")]
    public void FromChatRequest_Items_PassedThrough()
    {
        var request = new ChatRequest { Model = "gpt-4o" };
        request.Messages.Add(new ChatMessage { Role = "user", Content = "hi" });
        request["Foo"] = "bar";

        var result = ChatCompletionRequest.FromChatRequest(request);

        Assert.Equal("bar", result["Foo"]);
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

    [Fact]
    [DisplayName("ToChatRequest—ReasoningEffort/UserId/ConversationId 不丢失")]
    public void ToChatRequest_KeepsReasoningEffortAndIds()
    {
        var original = new ChatRequest
        {
            Model = "o3-mini",
            ReasoningEffort = "high",
            UserId = "u1",
            ConversationId = "c1",
            EnableThinking = true,
        };
        original.Messages.Add(new ChatMessage { Role = "user", Content = "hi" });

        var ccr = ChatCompletionRequest.FromChatRequest(original);
        var restored = ccr.ToChatRequest();

        Assert.Equal("high", restored.ReasoningEffort);
        Assert.Equal("u1", restored.UserId);
        Assert.Equal("c1", restored.ConversationId);
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

    [Fact]
    [DisplayName("BuildBody—跳过空assistant占位消息")]
    public void BuildBody_SkipsEmptyAssistantPlaceholder()
    {
        var request = new ChatRequest { Model = "deepseek-chat" };
        request.Messages.Add(new ChatMessage { Role = "system", Content = "你是助手" });
        request.Messages.Add(new ChatMessage { Role = "user", Content = "你好" });
        request.Messages.Add(new ChatMessage { Role = "assistant" });
        request.Messages.Add(new ChatMessage { Role = "user", Content = "继续" });

        var body = ChatCompletionRequest.BuildBody(request);

        Assert.True(body.TryGetValue("messages", out var value));
        var messages = Assert.IsAssignableFrom<IList<Object>>(value);
        Assert.Equal(3, messages.Count);
        Assert.DoesNotContain(messages, item =>
        {
            var dic = Assert.IsAssignableFrom<IDictionary<String, Object>>(item);
            return String.Equals(dic["role"] as String, "assistant", StringComparison.OrdinalIgnoreCase) &&
                !dic.ContainsKey("content") && !dic.ContainsKey("tool_calls");
        });
    }

    [Fact]
    [DisplayName("BuildBody—保留仅含工具调用的assistant消息")]
    public void BuildBody_KeepsAssistantWithToolCalls()
    {
        var request = new ChatRequest { Model = "deepseek-chat" };
        request.Messages.Add(new ChatMessage { Role = "user", Content = "帮我查天气" });
        request.Messages.Add(new ChatMessage
        {
            Role = "assistant",
            ToolCalls =
            [
                new ToolCall
                {
                    Id = "call_1",
                    Type = "function",
                    Function = new FunctionCall { Name = "get_weather", Arguments = "{\"city\":\"上海\"}" },
                }
            ]
        });

        var body = ChatCompletionRequest.BuildBody(request);

        Assert.True(body.TryGetValue("messages", out var value));
        var messages = Assert.IsAssignableFrom<IList<Object>>(value);
        Assert.Equal(2, messages.Count);

        var assistant = Assert.IsAssignableFrom<IDictionary<String, Object>>(messages[1]);
        Assert.Equal("assistant", assistant["role"] as String);
        Assert.True(assistant.ContainsKey("tool_calls"));
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
