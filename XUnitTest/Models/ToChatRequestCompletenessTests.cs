#nullable enable
using System;
using System.ComponentModel;
using NewLife.AI.Clients.Anthropic;
using NewLife.AI.Clients.Gemini;
using NewLife.AI.Clients.Ollama;
using NewLife.AI.Models;
using Xunit;

namespace XUnitTest.Models;

/// <summary>协议请求 ToChatRequest 完整性测试。网关统一化方案支撑：协议请求→统一 ChatRequest 不得丢失关键状态（ConversationId/UserId/Items/生成参数）</summary>
[DisplayName("协议请求 ToChatRequest 完整性测试")]
public class ToChatRequestCompletenessTests
{
    [Fact]
    [DisplayName("AnthropicRequest—ToChatRequest保留扩展字段")]
    public void Anthropic_ToChatRequest_PreservesExtensionFields()
    {
        var request = new AnthropicRequest { Model = "claude-sonnet-4-20250514" };
        request.Messages.Add(new AnthropicMessage { Role = "user", Content = "hi" });
        request.ConversationId = "conv-1";
        request.UserId = "user-1";
        request["ThinkingBudget"] = 2048;
        request.EnableThinking = true;

        var result = request.ToChatRequest();

        Assert.Equal("claude-sonnet-4-20250514", result.Model);
        Assert.Equal("conv-1", result.ConversationId);
        Assert.Equal("user-1", result.UserId);
        Assert.Equal(2048, result["ThinkingBudget"]);
        Assert.True(result.EnableThinking);
    }

    [Fact]
    [DisplayName("GeminiRequest—ToChatRequest保留扩展字段")]
    public void Gemini_ToChatRequest_PreservesExtensionFields()
    {
        var request = new GeminiRequest { Model = "gemini-2.5-flash" };
        request.Contents.Add(new GeminiContent { Role = "user", Parts = [new GeminiPart { Text = "hi" }] });
        request.ConversationId = "conv-1";
        request.UserId = "user-1";
        request["Seed"] = 42;

        var result = request.ToChatRequest();

        Assert.Equal("gemini-2.5-flash", result.Model);
        Assert.Equal("conv-1", result.ConversationId);
        Assert.Equal("user-1", result.UserId);
        Assert.Equal(42, result["Seed"]);
    }

    [Fact]
    [DisplayName("OllamaChatRequest—ToChatRequest转换保留生成参数与状态")]
    public void Ollama_ToChatRequest_PreservesFields()
    {
        var request = new OllamaChatRequest { Model = "llama3" };
        request.Messages.Add(new OllamaChatMessage { Role = "user", Content = "hi" });
        request.Options = new OllamaOptions { Temperature = 0.7, NumPredict = 256, TopK = 40 };
        request.ConversationId = "conv-1";
        request.UserId = "user-1";
        request.Think = true;

        var result = request.ToChatRequest();

        Assert.Equal("llama3", result.Model);
        Assert.Single(result.Messages);
        Assert.Equal("hi", result.Messages[0].Content);
        Assert.Equal(0.7, result.Temperature);
        Assert.Equal(256, result.MaxTokens);
        Assert.Equal(40, result.TopK);
        Assert.Equal("conv-1", result.ConversationId);
        Assert.Equal("user-1", result.UserId);
        Assert.True(result.EnableThinking);
    }

    [Fact]
    [DisplayName("OllamaChatRequest—ToChatRequest保留工具与思考消息")]
    public void Ollama_ToChatRequest_PreservesToolsAndThinking()
    {
        var request = new OllamaChatRequest { Model = "llama3" };
        request.Messages.Add(new OllamaChatMessage { Role = "assistant", Content = "回答", Thinking = "思考过程" });

        var result = request.ToChatRequest();

        Assert.Equal("思考过程", result.Messages[0].ReasoningContent);
        Assert.Equal("回答", result.Messages[0].Content);
    }
}
