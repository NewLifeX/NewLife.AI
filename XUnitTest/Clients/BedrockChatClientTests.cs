#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using NewLife.AI.Clients;
using NewLife.AI.Clients.Bedrock;
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
        var response = method!.Invoke(client, [ConverseResponseJson, request]) as IChatResponse;

        Assert.NotNull(response);
        Assert.Equal("anthropic.claude-v2", response.Model);
        Assert.NotNull(response.Messages);
        Assert.Single(response.Messages);

        var choice = response.Messages[0];
        Assert.Equal(FinishReason.Stop, choice.FinishReason);
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
        var response = method!.Invoke(client, [ConverseResponseJson, request]) as IChatResponse;

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
        var response = method!.Invoke(client, [ToolUseResponseJson, request]) as IChatResponse;

        Assert.NotNull(response);
        var choice = response.Messages![0];
        Assert.Equal(FinishReason.ToolCalls, choice.FinishReason);

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
        var body = method!.Invoke(client, [request]) as BedrockRequest;

        Assert.NotNull(body);
        Assert.NotNull(body.System);
        Assert.NotNull(body.Messages);

        // system 应为顶级列表
        Assert.Single(body.System);

        // messages 不应包含 system 角色
        Assert.Single(body.Messages); // 仅 user 消息
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
        var body = method!.Invoke(client, [request]) as BedrockRequest;

        Assert.NotNull(body);
        Assert.NotNull(body.InferenceConfig);
        Assert.Equal(1024, body.InferenceConfig.MaxTokens);
        Assert.Equal(0.7, body.InferenceConfig.Temperature);
        Assert.Equal(0.9, body.InferenceConfig.TopP);
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
        var result = BedrockResponse.MapStopReason(input);
        Assert.Equal(FinishReasonHelper.Parse(expected), result);
    }

    #endregion

    #region BedrockStreamEvent.ToChunkResponse 单元测试

    [Fact]
    [DisplayName("ToChunkResponse_messageStart事件_返回空增量")]
    public void ToChunkResponse_MessageStart_ReturnsEmptyDelta()
    {
        var json = @"{""messageStart"":{""message"":{""role"":""assistant"",""content"":[]}}}";
        var streamEvent = json.ToJsonEntity<BedrockStreamEvent>();
        Assert.NotNull(streamEvent);

        var chunk = streamEvent!.ToChunkResponse("anthropic.claude-v2");

        Assert.NotNull(chunk);
        Assert.Equal("anthropic.claude-v2", chunk!.Model);
        Assert.Equal("chat.completion.chunk", chunk.Object);
    }

    [Fact]
    [DisplayName("ToChunkResponse_contentBlockDelta文本增量_返回文本内容")]
    public void ToChunkResponse_ContentBlockDelta_TextDelta_ReturnsTextChunk()
    {
        var json = @"{""contentBlockDelta"":{""delta"":{""text"":""Hello Bedrock""}}}";
        var streamEvent = json.ToJsonEntity<BedrockStreamEvent>();
        Assert.NotNull(streamEvent);

        var chunk = streamEvent!.ToChunkResponse("test-model");

        Assert.NotNull(chunk);
        Assert.NotNull(chunk!.Messages);
        var content = chunk.Messages![0].Delta?.Content as String;
        Assert.Equal("Hello Bedrock", content);
    }

    [Fact]
    [DisplayName("ToChunkResponse_contentBlockDelta推理增量_返回thinking内容")]
    public void ToChunkResponse_ContentBlockDelta_ReasoningDelta_ReturnsThinkingChunk()
    {
        var json = @"{""contentBlockDelta"":{""delta"":{""reasoningContent"":{""reasoningText"":""Thinking deeply...""}}}}";
        var streamEvent = json.ToJsonEntity<BedrockStreamEvent>();
        Assert.NotNull(streamEvent);

        var chunk = streamEvent!.ToChunkResponse("test-model");

        Assert.NotNull(chunk);
        Assert.NotNull(chunk!.Messages);
        var reasoning = chunk.Messages![0].Delta?.ReasoningContent;
        Assert.Equal("Thinking deeply...", reasoning);
    }

    [Fact]
    [DisplayName("ToChunkResponse_messageStop_返回结束原因")]
    public void ToChunkResponse_MessageStop_ReturnsFinishReason()
    {
        var json = @"{""messageStop"":{""stopReason"":""end_turn""}}";
        var streamEvent = json.ToJsonEntity<BedrockStreamEvent>();
        Assert.NotNull(streamEvent);

        var chunk = streamEvent!.ToChunkResponse("test-model");

        Assert.NotNull(chunk);
        Assert.NotNull(chunk!.Messages);
        Assert.Equal(FinishReason.Stop, chunk.Messages![0].FinishReason);
    }

    [Fact]
    [DisplayName("ToChunkResponse_metadata_返回用量统计")]
    public void ToChunkResponse_Metadata_ReturnsUsage()
    {
        var json = @"{""metadata"":{""usage"":{""inputTokens"":100,""outputTokens"":50}}}";
        var streamEvent = json.ToJsonEntity<BedrockStreamEvent>();
        Assert.NotNull(streamEvent);

        var chunk = streamEvent!.ToChunkResponse("test-model");

        Assert.NotNull(chunk);
        Assert.NotNull(chunk!.Usage);
        Assert.Equal(100, chunk.Usage!.InputTokens);
        Assert.Equal(50, chunk.Usage.OutputTokens);
    }

    [Fact]
    [DisplayName("ParseChunk_通过反射调用_文本增量解析正确")]
    public void ParseChunk_ViaReflection_TextDeltaParsed()
    {
        var client = new BedrockChatClient("AKID", "SECRET", "test-model", "us-east-1");
        var request = new ChatRequest
        {
            Messages = [new ChatMessage { Role = "user", Content = "hello" }],
            Model = "test-model",
        };

        var method = typeof(BedrockChatClient).GetMethod("ParseChunk",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var json = @"{""contentBlockDelta"":{""delta"":{""text"":""Hi there""}}}";
        var chunk = method!.Invoke(client, [json, request, null]) as ChatResponse;

        Assert.NotNull(chunk);
        var content = chunk!.Messages![0].Delta?.Content as String;
        Assert.Equal("Hi there", content);
    }

    [Fact]
    [DisplayName("ParseChunk_通过反射调用_messageStop解析结束原因")]
    public void ParseChunk_ViaReflection_MessageStopParsed()
    {
        var client = new BedrockChatClient("AKID", "SECRET", "test-model", "us-east-1");
        var request = new ChatRequest
        {
            Messages = [new ChatMessage { Role = "user", Content = "hello" }],
            Model = "test-model",
        };

        var method = typeof(BedrockChatClient).GetMethod("ParseChunk",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var json = @"{""messageStop"":{""stopReason"":""max_tokens""}}";
        var chunk = method!.Invoke(client, [json, request, null]) as ChatResponse;

        Assert.NotNull(chunk);
        Assert.Equal(FinishReason.Length, chunk!.Messages![0].FinishReason);
    }

    #endregion
}
