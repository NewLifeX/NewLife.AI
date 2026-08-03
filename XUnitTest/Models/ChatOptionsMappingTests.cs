#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using NewLife.AI.Clients.Gemini;
using NewLife.AI.Clients.Ollama;
using NewLife.AI.Models;
using Xunit;

namespace XUnitTest.Models;

/// <summary>增强对话参数跨协议映射一致性测试。验证 ChatOptions 增强参数在各协议请求 DTO 中的映射（ResponseFormat/TopK/PresencePenalty/FrequencyPenalty）</summary>
public class ChatOptionsMappingTests
{
    private static ChatRequest CreateRequest(String model, String prompt = "你好")
        => new()
        {
            Model = model,
            Messages = [new ChatMessage { Role = "user", Content = prompt }],
        };

    #region Gemini

    [Fact]
    [DisplayName("Gemini_ResponseFormat_json_object_映射responseMimeType")]
    public void Gemini_ResponseFormat_JsonObject_MapsMimeType()
    {
        var req = CreateRequest("gemini-3.5-flash");
        req.ResponseFormat = new Dictionary<String, Object> { ["type"] = "json_object" };

        var result = GeminiRequest.FromChatRequest(req);

        Assert.NotNull(result.GenerationConfig);
        Assert.Equal("application/json", result.GenerationConfig!.ResponseMimeType);
        Assert.Null(result.GenerationConfig.ResponseSchema);
    }

    [Fact]
    [DisplayName("Gemini_ResponseFormat_json_schema_映射MimeType与Schema")]
    public void Gemini_ResponseFormat_JsonSchema_MapsMimeTypeAndSchema()
    {
        var schema = new Dictionary<String, Object> { ["type"] = "object", ["properties"] = new Dictionary<String, Object> { ["name"] = new Dictionary<String, Object> { ["type"] = "string" } } };
        var req = CreateRequest("gemini-3.5-flash");
        req.ResponseFormat = new Dictionary<String, Object>
        {
            ["type"] = "json_schema",
            ["json_schema"] = new Dictionary<String, Object> { ["name"] = "result", ["schema"] = schema },
        };

        var result = GeminiRequest.FromChatRequest(req);

        Assert.Equal("application/json", result.GenerationConfig!.ResponseMimeType);
        Assert.NotNull(result.GenerationConfig.ResponseSchema);
    }

    [Fact]
    [DisplayName("Gemini_ResponseFormat_JSON字符串_兼容解析")]
    public void Gemini_ResponseFormat_JsonString_Parses()
    {
        var req = CreateRequest("gemini-3.5-flash");
        req.ResponseFormat = """{"type":"json_object"}""";

        var result = GeminiRequest.FromChatRequest(req);

        Assert.Equal("application/json", result.GenerationConfig!.ResponseMimeType);
    }

    [Fact]
    [DisplayName("Gemini_PresenceFrequency惩罚_映射到generationConfig")]
    public void Gemini_PresenceFrequencyPenalty_MapsToGenerationConfig()
    {
        var req = CreateRequest("gemini-3.5-flash");
        req.PresencePenalty = 0.5;
        req.FrequencyPenalty = -0.5;

        var result = GeminiRequest.FromChatRequest(req);

        Assert.NotNull(result.GenerationConfig);
        Assert.Equal(0.5, result.GenerationConfig!.PresencePenalty);
        Assert.Equal(-0.5, result.GenerationConfig.FrequencyPenalty);
    }

    [Fact]
    [DisplayName("Gemini_无增强参数_不生成GenerationConfig")]
    public void Gemini_NoEnhancedParams_NoGenerationConfig()
    {
        var result = GeminiRequest.FromChatRequest(CreateRequest("gemini-3.5-flash"));

        Assert.Null(result.GenerationConfig);
    }

    #endregion

    #region Ollama

    [Fact]
    [DisplayName("Ollama_TopK_映射到options.top_k")]
    public void Ollama_TopK_MapsToOptions()
    {
        var req = CreateRequest("qwen3:8b");
        req.TopK = 40;

        var result = OllamaChatRequest.FromChatRequest(req);

        Assert.NotNull(result.Options);
        Assert.Equal(40, result.Options!.TopK);
    }

    [Fact]
    [DisplayName("Ollama_无参数_不生成Options")]
    public void Ollama_NoParams_NoOptions()
    {
        var result = OllamaChatRequest.FromChatRequest(CreateRequest("qwen3:8b"));

        Assert.Null(result.Options);
    }

    #endregion
}
