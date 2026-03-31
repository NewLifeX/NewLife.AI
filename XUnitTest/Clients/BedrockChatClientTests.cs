#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using NewLife.AI.Clients;
using NewLife.AI.Models;
using NewLife.Serialization;
using Xunit;

namespace XUnitTest.Clients;

/// <summary>BedrockChatClient 单元测试（不需要网络/ApiKey，验证请求构建和响应解析）</summary>
public class BedrockChatClientTests
{
    #region ParseResponse 单元测试

    private const String ConverseResponseJson = """{"output":{"message":{"role":"assistant","content":[{"text":"Hello! How can I help you today?"}]}},"stopReason":"end_turn","usage":{"inputTokens":10,"outputTokens":15,"totalTokens":25}}""";

    private const String ToolUseResponseJson = """{"output":{"message":{"role":"assistant","content":[{"text":"Let me check the weather."},{"toolUse":{"toolUseId":"call_123","name":"get_weather","input":{"city":"Beijing"}}}]}},"stopReason":"tool_use","usage":{"inputTokens":20,"outputTokens":30,"totalTokens":50}}""";

    [Fact]
    [DisplayName("ParseResponse_基本对话响应_文本内容正确解析")]
    public void ParseResponse_BasicConverseResponse_TextContentParsed()
    {
        var client = new BedrockChatClient("AKID", "SECRET", "anthropic.claude-v2", "us-east-1");
        var request = new ChatRequest
        {
            Messages = [new ChatMessage { Role = "user", Content = "hello" }],
            Model = "anthropic.claude-v2",
        };

        // 通过反射调用 protected ParseResponse
        var method = typeof(BedrockChatClient).GetMethod("ParseResponse",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var response = method!.Invoke(client, [ConverseResponseJson, request]) as ChatResponse;

        Assert.NotNull(response);
        Assert.Equal("anthropic.claude-v2", response.Model);
        Assert.NotNull(response.Messages);
        Assert.Single(response.Messages);

        var choice = response.Messages[0];
        Assert.Equal("stop", choice.FinishReason);
        Assert.NotNull(choice.Message);
        Assert.Equal("assistant", choice.Message.Role);
        Assert.Equal("Hello! How can I help you today?", choice.Message.Content as String);
    }

    [Fact]
    [DisplayName("ParseResponse_基本对话响应_Usage正确解析")]
    public void ParseResponse_BasicConverseResponse_UsageParsed()
    {
        var client = new BedrockChatClient("AKID", "SECRET", "test-model", "us-east-1");
        var request = new ChatRequest
        {
            Messages = [new ChatMessage { Role = "user", Content = "hello" }],
            Model = "test-model",
        };

        var method = typeof(BedrockChatClient).GetMethod("ParseResponse",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var response = method!.Invoke(client, [ConverseResponseJson, request]) as ChatResponse;

        Assert.NotNull(response?.Usage);
        Assert.Equal(10, response.Usage.InputTokens);
        Assert.Equal(15, response.Usage.OutputTokens);
    }

    [Fact]
    [DisplayName("ParseResponse_工具调用响应_ToolCalls正确解析")]
    public void ParseResponse_ToolUseResponse_ToolCallsParsed()
    {
        var client = new BedrockChatClient("AKID", "SECRET", "test-model", "us-east-1");
        var request = new ChatRequest
        {
            Messages = [new ChatMessage { Role = "user", Content = "what's the weather?" }],
            Model = "test-model",
        };

        var method = typeof(BedrockChatClient).GetMethod("ParseResponse",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var response = method!.Invoke(client, [ToolUseResponseJson, request]) as ChatResponse;

        Assert.NotNull(response);
        var choice = response.Messages![0];
        Assert.Equal("tool_calls", choice.FinishReason);

        var msg = choice.Message!;
        Assert.NotNull(msg.ToolCalls);
        Assert.Single(msg.ToolCalls);

        var tc = msg.ToolCalls[0];
        Assert.Equal("call_123", tc.Id);
        Assert.Equal("function", tc.Type);
        Assert.Equal("get_weather", tc.Function!.Name);
        Assert.Contains("Beijing", tc.Function.Arguments);
    }

    #endregion

    #region BuildUrl 单元测试

    [Fact]
    [DisplayName("BuildUrl_标准请求_生成正确的Converse API URL")]
    public void BuildUrl_StandardRequest_GeneratesCorrectConverseUrl()
    {
        var client = new BedrockChatClient("AKID", "SECRET", "anthropic.claude-v2", "us-east-1");
        var request = new ChatRequest
        {
            Messages = [new ChatMessage { Role = "user", Content = "hello" }],
            Model = "anthropic.claude-v2",
        };

        var method = typeof(BedrockChatClient).GetMethod("BuildUrl",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var url = method!.Invoke(client, [request]) as String;

        Assert.NotNull(url);
        Assert.Equal("https://bedrock-runtime.us-east-1.amazonaws.com/model/anthropic.claude-v2/converse", url);
    }

    [Fact]
    [DisplayName("BuildUrl_不同region_URL包含正确区域")]
    public void BuildUrl_DifferentRegion_UrlContainsCorrectRegion()
    {
        var client = new BedrockChatClient("AKID", "SECRET", "test-model", "eu-west-1");
        var request = new ChatRequest
        {
            Messages = [new ChatMessage { Role = "user", Content = "hello" }],
            Model = "test-model",
        };

        var method = typeof(BedrockChatClient).GetMethod("BuildUrl",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var url = method!.Invoke(client, [request]) as String;

        Assert.Contains("eu-west-1", url);
    }

    #endregion

    #region BuildRequest 单元测试

    [Fact]
    [DisplayName("BuildRequest_系统消息_正确放置到顶级system字段")]
    public void BuildRequest_SystemMessage_PlacedInTopLevelSystemField()
    {
        var client = new BedrockChatClient("AKID", "SECRET", "test-model", "us-east-1");
        var request = new ChatRequest
        {
            Messages =
            [
                new ChatMessage { Role = "system", Content = "You are helpful." },
                new ChatMessage { Role = "user", Content = "hello" }
            ],
            Model = "test-model",
        };

        var method = typeof(BedrockChatClient).GetMethod("BuildRequest",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var body = method!.Invoke(client, [request]) as IDictionary<String, Object>;

        Assert.NotNull(body);
        Assert.True(body.ContainsKey("system"));
        Assert.True(body.ContainsKey("messages"));

        // system 应为顶级列表
        var system = body["system"] as IList<Object>;
        Assert.NotNull(system);
        Assert.Single(system);

        // messages 不应包含 system 角色
        var messages = body["messages"] as IList<Object>;
        Assert.NotNull(messages);
        Assert.Single(messages); // 仅 user 消息
    }

    [Fact]
    [DisplayName("BuildRequest_推理配置_正确设置inferenceConfig")]
    public void BuildRequest_InferenceConfig_CorrectlySet()
    {
        var client = new BedrockChatClient("AKID", "SECRET", "test-model", "us-east-1");
        var request = new ChatRequest
        {
            Messages = [new ChatMessage { Role = "user", Content = "hello" }],
            Model = "test-model",
            MaxTokens = 1024,
            Temperature = 0.7,
            TopP = 0.9,
        };

        var method = typeof(BedrockChatClient).GetMethod("BuildRequest",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var body = method!.Invoke(client, [request]) as IDictionary<String, Object>;

        Assert.NotNull(body);
        Assert.True(body.ContainsKey("inferenceConfig"));

        var config = body["inferenceConfig"] as IDictionary<String, Object>;
        Assert.NotNull(config);
        Assert.Equal(1024, config["maxTokens"]);
        Assert.Equal(0.7, config["temperature"]);
        Assert.Equal(0.9, config["topP"]);
    }

    #endregion

    #region MapStopReason 单元测试

    [Theory]
    [DisplayName("MapStopReason_Bedrock停止原因_正确映射")]
    [InlineData("end_turn", "stop")]
    [InlineData("stop_sequence", "stop")]
    [InlineData("max_tokens", "length")]
    [InlineData("tool_use", "tool_calls")]
    [InlineData("content_filtered", "content_filter")]
    [InlineData(null, null)]
    public void MapStopReason_BedrockReasons_MappedCorrectly(String? input, String? expected)
    {
        var result = BedrockChatClient.MapStopReason(input);
        Assert.Equal(expected, result);
    }

    #endregion
}
