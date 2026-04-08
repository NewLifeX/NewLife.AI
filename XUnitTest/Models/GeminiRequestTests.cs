using System;
using System.Collections.Generic;
using System.ComponentModel;
using NewLife.AI.Clients;
using NewLife.AI.Clients.Gemini;
using NewLife.AI.Clients.OpenAI;
using NewLife.AI.Models;
using NewLife.Serialization;
using Xunit;

namespace XUnitTest.Models;

/// <summary>GeminiRequest 模型类单元测试</summary>
[DisplayName("GeminiRequest 单元测试")]
public class GeminiRequestTests
{
    #region FromChatRequest
    [Fact]
    [DisplayName("FromChatRequest—基本消息转换")]
    public void FromChatRequest_Basic()
    {
        var request = new ChatRequest { Model = "gemini-2.5-flash" };
        request.Messages.Add(new ChatMessage { Role = "user", Content = "Hello" });

        var result = GeminiRequest.FromChatRequest(request);

        Assert.NotNull(result.Contents);
        Assert.Single(result.Contents!);
        Assert.Equal("user", result.Contents![0].Role);
        Assert.NotNull(result.Contents[0].Parts);
        Assert.Single(result.Contents[0].Parts!);
        Assert.Equal("Hello", result.Contents[0].Parts![0].Text);
    }

    [Fact]
    [DisplayName("FromChatRequest—system 消息分离为 systemInstruction")]
    public void FromChatRequest_SystemMessageSeparation()
    {
        var request = new ChatRequest { Model = "gemini-2.5-flash" };
        request.Messages.Add(new ChatMessage { Role = "system", Content = "你是助手" });
        request.Messages.Add(new ChatMessage { Role = "user", Content = "你好" });

        var result = GeminiRequest.FromChatRequest(request);

        // system 消息应该分离到 SystemInstruction
        Assert.NotNull(result.SystemInstruction);
        // 非 system 消息在 Contents 中
        Assert.NotNull(result.Contents);
        Assert.Single(result.Contents!);
        Assert.Equal("user", result.Contents![0].Role);
    }

    [Fact]
    [DisplayName("FromChatRequest—assistant 角色映射为 model")]
    public void FromChatRequest_RoleMapping()
    {
        var request = new ChatRequest { Model = "gemini-2.5-flash" };
        request.Messages.Add(new ChatMessage { Role = "user", Content = "你好" });
        request.Messages.Add(new ChatMessage { Role = "assistant", Content = "你好！" });

        var result = GeminiRequest.FromChatRequest(request);

        Assert.Equal(2, result.Contents!.Count);
        Assert.Equal("user", result.Contents[0].Role);
        Assert.Equal("model", result.Contents[1].Role);
    }

    [Fact]
    [DisplayName("FromChatRequest—GenerationConfig 正确设置")]
    public void FromChatRequest_GenerationConfig()
    {
        var request = new ChatRequest
        {
            Model = "gemini-2.5-flash",
            Temperature = 0.7,
            TopP = 0.9,
            TopK = 40,
            MaxTokens = 1024,
        };
        request.Messages.Add(new ChatMessage { Role = "user", Content = "Hi" });

        var result = GeminiRequest.FromChatRequest(request);

        Assert.NotNull(result.GenerationConfig);
        Assert.Equal(0.7, result.GenerationConfig!.Temperature);
        Assert.Equal(0.9, result.GenerationConfig.TopP);
        Assert.Equal(40, result.GenerationConfig.TopK);
        Assert.Equal(1024, result.GenerationConfig.MaxOutputTokens);
    }

    [Fact]
    [DisplayName("FromChatRequest—工具调用消息转换")]
    public void FromChatRequest_ToolCalls()
    {
        var request = new ChatRequest { Model = "gemini-2.5-flash" };
        request.Messages.Add(new ChatMessage { Role = "user", Content = "天气" });
        request.Tools =
        [
            new ChatTool
            {
                Type = "function",
                Function = new FunctionDefinition
                {
                    Name = "get_weather",
                    Description = "获取天气信息",
                }
            }
        ];

        var result = GeminiRequest.FromChatRequest(request);

        Assert.NotNull(result.Tools);
    }
    #endregion

    #region ToChatRequest
    [Fact]
    [DisplayName("ToChatRequest—往返转换")]
    public void ToChatRequest_RoundTrip()
    {
        var original = new ChatRequest { Model = "gemini-2.5-flash", Temperature = 0.5 };
        original.Messages.Add(new ChatMessage { Role = "user", Content = "测试" });

        var gemini = GeminiRequest.FromChatRequest(original);
        var restored = gemini.ToChatRequest();

        Assert.Equal("user", restored.Messages[0].Role);
        Assert.Equal("测试", restored.Messages[0].Content?.ToString());
    }

    [Fact]
    [DisplayName("JSON序列化—Model字段正确输出")]
    public void JsonSerialization_ModelField()
    {
        var request = new ChatRequest { Model = "qwen3.5-flash" };
        request.Messages.Add(new ChatMessage { Role = "user", Content = "hi" });

        var gemini = GeminiRequest.FromChatRequest(request);
        Assert.Equal("qwen3.5-flash", gemini.Model);

        var client = new OpenAIChatClient(new AiClientOptions());
        var json = client.JsonHost.Write(gemini, client.JsonOptions)!;
        Assert.Contains("\"model\"", json);
        Assert.Contains("qwen3.5-flash", json);
    }

    [Fact]
    [DisplayName("JSON序列化—Stream字段写入请求体（网关流式识别）")]
    public void JsonSerialization_StreamField()
    {
        // GeminiRequest.Stream 必须参与序列化：NewLifeAI 网关依赖此字段判断是否返回 SSE 事件流
        // 去掉 [IgnoreDataMember] 后，"stream":true 应出现在请求体中
        var request = new ChatRequest { Model = "gemini-2.5-flash", Stream = true };
        request.Messages.Add(new ChatMessage { Role = "user", Content = "hi" });

        var gemini = GeminiRequest.FromChatRequest(request);
        Assert.True(gemini.Stream);

        var client = new GeminiChatClient(new AiClientOptions());
        var json = client.JsonHost.Write(gemini, GeminiChatClient.DefaultJsonOptions)!;
        Assert.Contains("\"stream\"", json);
        Assert.Contains("true", json);
    }
    #endregion
}
