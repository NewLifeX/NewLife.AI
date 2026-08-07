#nullable enable
using System;
using System.ComponentModel;
using System.Text.Json;
using NewLife.AI.Clients.Gemini;
using NewLife.AI.Models;
using Xunit;

namespace XUnitTest.Models;

/// <summary>验证 GeminiRequest.ApplyResponseFormat 对 JsonElement 入站（网关场景）的兼容性，评估复用 CollectionHelper.ToDictionary 的可行性</summary>
[DisplayName("Gemini ResponseFormat JsonElement 兼容性验证")]
public class GeminiResponseFormatJsonElementTests
{
    [Fact]
    [DisplayName("ResponseFormat—JsonElement json_object 映射")]
    public void JsonElement_JsonObject_Maps()
    {
        // 模拟网关入站：ResponseFormat 被 System.Text.Json 反序列化为 JsonElement
        var json = """{"type":"json_object"}""";
        using var doc = JsonDocument.Parse(json);
        var req = new ChatRequest { Model = "gemini-3.5-flash" };
        req.Messages.Add(new ChatMessage { Role = "user", Content = "hi" });
        req.ResponseFormat = doc.RootElement.Clone();

        var result = GeminiRequest.FromChatRequest(req);

        Assert.NotNull(result.GenerationConfig);
        Assert.Equal("application/json", result.GenerationConfig!.ResponseMimeType);
    }

    [Fact]
    [DisplayName("ResponseFormat—JsonElement json_schema 映射 Schema")]
    public void JsonElement_JsonSchema_Maps()
    {
        // 模拟网关入站：ResponseFormat 为 JsonElement，含嵌套 json_schema
        var json = """{"type":"json_schema","json_schema":{"name":"result","schema":{"type":"object","properties":{"city":{"type":"string"}}}}}""";
        using var doc = JsonDocument.Parse(json);
        var req = new ChatRequest { Model = "gemini-3.5-flash" };
        req.Messages.Add(new ChatMessage { Role = "user", Content = "hi" });
        req.ResponseFormat = doc.RootElement.Clone();

        var result = GeminiRequest.FromChatRequest(req);

        Assert.NotNull(result.GenerationConfig);
        Assert.Equal("application/json", result.GenerationConfig!.ResponseMimeType);
        Assert.NotNull(result.GenerationConfig.ResponseSchema);
    }
}
