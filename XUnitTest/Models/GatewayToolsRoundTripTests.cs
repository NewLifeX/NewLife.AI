#nullable enable
using System;
using System.ComponentModel;
using System.Text.Json;
using NewLife.AI.Clients;
using NewLife.AI.Clients.Anthropic;
using NewLife.AI.Clients.Gemini;
using NewLife.AI.Models;
using Xunit;

namespace XUnitTest.Models;

/// <summary>网关入站工具定义透传测试。A-106：Anthropic/Gemini 请求入站后 IChatRequest.Tools 未从原生 tools 数组转换，网关统一化后工具定义静默丢失</summary>
[DisplayName("网关入站工具定义透传测试")]
public class GatewayToolsRoundTripTests
{
    /// <summary>模拟 ASP.NET Core 网关入站（snake_case + System.Text.Json），元素为 JsonElement</summary>
    private static AnthropicRequest DeserializeAnthropic(String json)
        => JsonSerializer.Deserialize<AnthropicRequest>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        })!;

    /// <summary>模拟 ASP.NET Core 网关入站（camelCase + System.Text.Json），元素为 JsonElement</summary>
    private static GeminiRequest DeserializeGemini(String json)
        => JsonSerializer.Deserialize<GeminiRequest>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    [Fact]
    [DisplayName("AnthropicRequest—入站tools数组转换为IChatRequest.Tools")]
    public void Anthropic_IChatRequestTools_ParsesNativeTools()
    {
        var json = """
            {"model":"claude-sonnet-4","messages":[{"role":"user","content":"hi"}],"tools":[{"name":"get_weather","description":"查询天气","input_schema":{"type":"object","properties":{"city":{"type":"string"}}}}]}
            """;
        var req = DeserializeAnthropic(json);

        // A-106 修复前为 null：IChatRequest.Tools 是自动属性，从未从原生 Tools 惰性转换
        var tools = ((IChatRequest)req).Tools;
        Assert.NotNull(tools);
        var tool = Assert.Single(tools!);
        Assert.Equal("function", tool.Type);
        Assert.Equal("get_weather", tool.Function?.Name);
        Assert.Equal("查询天气", tool.Function?.Description);
        Assert.NotNull(tool.Function?.Parameters);
    }

    [Fact]
    [DisplayName("AnthropicRequest—ToChatRequest保留工具定义")]
    public void Anthropic_ToChatRequest_PreservesTools()
    {
        var json = """
            {"model":"claude-sonnet-4","messages":[{"role":"user","content":"hi"}],"tools":[{"name":"get_weather","description":"查询天气","input_schema":{"type":"object"}}]}
            """;
        var req = DeserializeAnthropic(json);

        var result = req.ToChatRequest();

        // 网关统一化后工具定义不得丢失
        Assert.NotNull(result.Tools);
        var tool = Assert.Single(result.Tools!);
        Assert.Equal("get_weather", tool.Function?.Name);
        Assert.Equal("查询天气", tool.Function?.Description);
        Assert.NotNull(tool.Function?.Parameters);
    }

    [Fact]
    [DisplayName("GeminiRequest—入站tools数组转换为IChatRequest.Tools")]
    public void Gemini_IChatRequestTools_ParsesNativeTools()
    {
        var json = """
            {"model":"gemini-2.5-flash","contents":[{"role":"user","parts":[{"text":"hi"}]}],"tools":[{"functionDeclarations":[{"name":"get_weather","description":"查询天气","parameters":{"type":"object","properties":{"city":{"type":"string"}}}}]}]}
            """;
        var req = DeserializeGemini(json);

        // A-106 修复前为 null：IChatRequest.Tools 是自动属性，从未从原生 Tools 惰性转换
        var tools = ((IChatRequest)req).Tools;
        Assert.NotNull(tools);
        var tool = Assert.Single(tools!);
        Assert.Equal("function", tool.Type);
        Assert.Equal("get_weather", tool.Function?.Name);
        Assert.Equal("查询天气", tool.Function?.Description);
        Assert.NotNull(tool.Function?.Parameters);
    }

    [Fact]
    [DisplayName("GeminiRequest—ToChatRequest保留工具定义")]
    public void Gemini_ToChatRequest_PreservesTools()
    {
        var json = """
            {"model":"gemini-2.5-flash","contents":[{"role":"user","parts":[{"text":"hi"}]}],"tools":[{"functionDeclarations":[{"name":"get_weather","description":"查询天气","parameters":{"type":"object"}}]}]}
            """;
        var req = DeserializeGemini(json);

        var result = req.ToChatRequest();

        // 网关统一化后工具定义不得丢失
        Assert.NotNull(result.Tools);
        var tool = Assert.Single(result.Tools!);
        Assert.Equal("get_weather", tool.Function?.Name);
        Assert.Equal("查询天气", tool.Function?.Description);
        Assert.NotNull(tool.Function?.Parameters);
    }

    [Fact]
    [DisplayName("AnthropicRequest—无tools返回null")]
    public void Anthropic_NoTools_ReturnsNull()
    {
        var json = """{"model":"claude-sonnet-4","messages":[{"role":"user","content":"hi"}]}""";
        var req = DeserializeAnthropic(json);

        Assert.Null(((IChatRequest)req).Tools);
    }

    [Fact]
    [DisplayName("GeminiRequest—无tools返回null")]
    public void Gemini_NoTools_ReturnsNull()
    {
        var json = """{"model":"gemini-2.5-flash","contents":[{"role":"user","parts":[{"text":"hi"}]}]}""";
        var req = DeserializeGemini(json);

        Assert.Null(((IChatRequest)req).Tools);
    }
}
