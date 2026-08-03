using System;
using System.Collections.Generic;
using System.ComponentModel;
using NewLife.AI.Clients.Anthropic;
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
    [DisplayName("FromChatRequest—多条system消息合并")]
    public void FromChatRequest_MultipleSystemMessages_Merged()
    {
        var request = new ChatRequest { Model = "claude-sonnet-4-20250514" };
        request.Messages.Add(new ChatMessage { Role = "system", Content = "规则一" });
        request.Messages.Add(new ChatMessage { Role = "system", Content = "规则二" });
        request.Messages.Add(new ChatMessage { Role = "user", Content = "你好" });

        var result = AnthropicRequest.FromChatRequest(request);

        // 多条 system 消息按顺序合并，不覆盖
        Assert.Equal("规则一\n\n规则二", result.System);
        Assert.Single(result.Messages!);
        Assert.Equal("user", result.Messages![0].Role);
    }

    [Fact]
    [DisplayName("FromChatRequest—ImageContent 转换为 image 内容块")]
    public void FromChatRequest_ImageContent_BuildsImageBlock()
    {
        var request = new ChatRequest { Model = "claude-sonnet-4-6" };
        var msg = new ChatMessage { Role = "user" };
        msg.Contents =
        [
            new TextContent("描述图片"),
            new ImageContent { Data = [1, 2, 3], MediaType = "image/png" },
        ];
        request.Messages.Add(msg);

        var result = AnthropicRequest.FromChatRequest(request);

        var content = Assert.IsType<List<Object>>(result.Messages![0].Content);
        Assert.Equal(2, content.Count);
        var textBlock = Assert.IsType<Dictionary<String, Object>>(content[0]);
        Assert.Equal("text", textBlock["type"]);
        var imageBlock = Assert.IsType<Dictionary<String, Object>>(content[1]);
        Assert.Equal("image", imageBlock["type"]);
        var source = Assert.IsType<Dictionary<String, Object>>(imageBlock["source"]);
        Assert.Equal("base64", source["type"]);
        Assert.Equal("image/png", source["media_type"]);
        Assert.Equal(Convert.ToBase64String([1, 2, 3]), source["data"]);
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
    [DisplayName("FromChatRequest—EnableThinking=true 映射 thinking 配置")]
    public void FromChatRequest_Thinking()
    {
        var request = new ChatRequest
        {
            Model = "claude-sonnet-4-20250514",
            EnableThinking = true,
        };
        request.Messages.Add(new ChatMessage { Role = "user", Content = "思考" });

        var result = AnthropicRequest.FromChatRequest(request);

        Assert.Equal("claude-sonnet-4-20250514", result.Model);
        Assert.Single(result.Messages);
        Assert.NotNull(result.Thinking);
        Assert.Equal("enabled", result.Thinking!.Type);
        Assert.True(result.Thinking.BudgetTokens > 0, "思考预算应大于 0");
    }

    [Fact]
    [DisplayName("FromChatRequest—EnableThinking=false 映射 thinking 关闭")]
    public void FromChatRequest_ThinkingDisabled()
    {
        var request = new ChatRequest
        {
            Model = "claude-sonnet-4-20250514",
            EnableThinking = false,
        };
        request.Messages.Add(new ChatMessage { Role = "user", Content = "思考" });

        var result = AnthropicRequest.FromChatRequest(request);

        Assert.NotNull(result.Thinking);
        Assert.Equal("disabled", result.Thinking!.Type);
    }

    [Fact]
    [DisplayName("FromChatRequest—ThinkingBudget 透传思考预算")]
    public void FromChatRequest_ThinkingBudget()
    {
        var request = new ChatRequest
        {
            Model = "claude-sonnet-4-20250514",
            EnableThinking = true,
        };
        request["ThinkingBudget"] = 2048;
        request.Messages.Add(new ChatMessage { Role = "user", Content = "思考" });

        var result = AnthropicRequest.FromChatRequest(request);

        Assert.Equal(2048, result.Thinking!.BudgetTokens);
    }

    [Fact]
    [DisplayName("FromChatRequest—思考预算超过 max_tokens 时自动提升 max_tokens")]
    public void FromChatRequest_ThinkingBudget_ExceedsMaxTokens()
    {
        var request = new ChatRequest
        {
            Model = "claude-sonnet-4-20250514",
            EnableThinking = true,
            MaxTokens = 512,
        };
        request["ThinkingBudget"] = 1024;
        request.Messages.Add(new ChatMessage { Role = "user", Content = "思考" });

        var result = AnthropicRequest.FromChatRequest(request);

        Assert.Equal(1024, result.Thinking!.BudgetTokens);
        Assert.True(result.MaxTokens > 1024, "max_tokens 应大于思考预算");
    }

    [Fact]
    [DisplayName("FromChatRequest—assistant思考块+签名回传thinking块")]
    public void FromChatRequest_ThinkingReplay_WithSignature()
    {
        var request = new ChatRequest { Model = "claude-sonnet-4-6" };
        var assistant = new ChatMessage
        {
            Role = "assistant",
            Content = "答案是 42",
            ReasoningContent = "让我分析一下",
        };
        assistant["Signature"] = "sig_abc123";
        request.Messages.Add(assistant);
        request.Messages.Add(new ChatMessage { Role = "user", Content = "继续" });

        var result = AnthropicRequest.FromChatRequest(request);

        var content = result.Messages![0].Content as IList<Object>;
        Assert.NotNull(content);
        Assert.Equal(2, content!.Count);
        var thinking = content[0] as IDictionary<String, Object>;
        Assert.NotNull(thinking);
        Assert.Equal("thinking", thinking["type"]);
        Assert.Equal("让我分析一下", thinking["thinking"]);
        Assert.Equal("sig_abc123", thinking["signature"]);
        var text = content[1] as IDictionary<String, Object>;
        Assert.NotNull(text);
        Assert.Equal("text", text["type"]);
    }

    [Fact]
    [DisplayName("FromChatRequest—assistant redacted_thinking 数据回传")]
    public void FromChatRequest_RedactedThinking_Replay()
    {
        var request = new ChatRequest { Model = "claude-sonnet-4-6" };
        var assistant = new ChatMessage { Role = "assistant", Content = "继续分析" };
        assistant["RedactedThinking"] = new List<String> { "redacted_data_1" };
        request.Messages.Add(assistant);

        var result = AnthropicRequest.FromChatRequest(request);

        var content = result.Messages![0].Content as IList<Object>;
        Assert.NotNull(content);
        Assert.Equal(2, content!.Count);
        var redacted = content[0] as IDictionary<String, Object>;
        Assert.NotNull(redacted);
        Assert.Equal("redacted_thinking", redacted["type"]);
        Assert.Equal("redacted_data_1", redacted["data"]);
    }

    [Fact]
    [DisplayName("FromChatRequest—工具轮次思考块位于text与tool_use之前")]
    public void FromChatRequest_ToolTurn_ThinkingBlockFirst()
    {
        var request = new ChatRequest { Model = "claude-sonnet-4-6" };
        var assistant = new ChatMessage
        {
            Role = "assistant",
            Content = "我来查询天气",
            ReasoningContent = "需要调用 get_weather",
            ToolCalls =
            [
                new ToolCall
                {
                    Id = "call_1",
                    Type = "function",
                    Function = new FunctionCall { Name = "get_weather", Arguments = "{\"city\":\"Beijing\"}" },
                }
            ],
        };
        assistant["Signature"] = "sig_tool";
        request.Messages.Add(assistant);

        var result = AnthropicRequest.FromChatRequest(request);

        var content = result.Messages![0].Content as IList<Object>;
        Assert.NotNull(content);
        Assert.Equal(3, content!.Count); // thinking + text + tool_use
        var first = content[0] as IDictionary<String, Object>;
        Assert.NotNull(first);
        Assert.Equal("thinking", first["type"]);
        Assert.Equal("sig_tool", first["signature"]);
        var second = content[1] as IDictionary<String, Object>;
        Assert.NotNull(second);
        Assert.Equal("text", second["type"]);
        var third = content[2] as IDictionary<String, Object>;
        Assert.NotNull(third);
        Assert.Equal("tool_use", third["type"]);
    }

    [Fact]
    [DisplayName("FromChatRequest—ThinkingMode=adaptive 映射 adaptive + effort")]
    public void FromChatRequest_Adaptive_WithEffort()
    {
        var request = new ChatRequest
        {
            Model = "claude-opus-4-6",
            EnableThinking = true,
            ReasoningEffort = "high",
        };
        request["ThinkingMode"] = "adaptive";
        request["ThinkingDisplay"] = "summarized";
        request.Messages.Add(new ChatMessage { Role = "user", Content = "分析" });

        var result = AnthropicRequest.FromChatRequest(request);

        Assert.NotNull(result.Thinking);
        Assert.Equal("adaptive", result.Thinking!.Type);
        Assert.Equal("summarized", result.Thinking.Display);
        Assert.NotNull(result.OutputConfig);
        Assert.Equal("high", result.OutputConfig!.Effort);
    }

    [Fact]
    [DisplayName("FromChatRequest—思考开启时剥离temperature/top_k并收敛top_p")]
    public void FromChatRequest_ThinkingEnabled_StripsSamplingParams()
    {
        var request = new ChatRequest
        {
            Model = "claude-sonnet-4-6",
            EnableThinking = true,
            Temperature = 0.7,
            TopP = 0.5,
            TopK = 20,
        };
        request.Messages.Add(new ChatMessage { Role = "user", Content = "思考" });

        var result = AnthropicRequest.FromChatRequest(request);

        Assert.Null(result.Temperature);
        Assert.Null(result.TopK);
        Assert.Equal(0.95, result.TopP!.Value); // 收敛到下限 0.95
    }

    [Fact]
    [DisplayName("FromChatRequest—思考关闭时保留采样参数")]
    public void FromChatRequest_ThinkingDisabled_KeepsSamplingParams()
    {
        var request = new ChatRequest
        {
            Model = "claude-sonnet-4-6",
            EnableThinking = false,
            Temperature = 0.7,
            TopK = 20,
        };
        request.Messages.Add(new ChatMessage { Role = "user", Content = "快速" });

        var result = AnthropicRequest.FromChatRequest(request);

        Assert.Equal(0.7, result.Temperature);
        Assert.Equal(20, result.TopK);
    }

    [Fact]
    [DisplayName("FromChatRequest—ThinkingBudget低于1024时clamp到1024")]
    public void FromChatRequest_ThinkingBudget_BelowMinimum_Clamped()
    {
        var request = new ChatRequest
        {
            Model = "claude-sonnet-4-6",
            EnableThinking = true,
        };
        request["ThinkingBudget"] = 512;
        request.Messages.Add(new ChatMessage { Role = "user", Content = "思考" });

        var result = AnthropicRequest.FromChatRequest(request);

        Assert.Equal(1024, result.Thinking!.BudgetTokens);
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
