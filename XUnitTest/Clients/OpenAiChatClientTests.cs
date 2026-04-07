#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using NewLife.AI.Clients;
using NewLife.AI.Clients.OpenAI;
using NewLife.AI.Models;
using NewLife.Serialization;
using Xunit;

namespace XUnitTest.Clients;

/// <summary>OpenAiChatClient 单元测试（不需要网络/ApiKey，直接验证解析逻辑）</summary>
public class OpenAiChatClientTests
{
    private const String QwenJson = """{"model":"qwen-plus","id":"chatcmpl-131cb128-bba5-939a-b206-48807913b636","choices":[{"message":{"content":"我是通义千问（Qwen），阿里巴巴集团旗下的超大规模语言模型，能够回答问题、创作文字，如写故事、公文、邮件、剧本等，还能进行逻辑推理、编程，甚至表达观点和玩游戏，支持多种语言，致力于为用户提供高效、智能、友好的交互体验。","role":"assistant"},"index":0,"finish_reason":"stop"}],"created":1774532789,"object":"chat.completion","usage":{"total_tokens":77,"completion_tokens":65,"prompt_tokens":12,"prompt_tokens_details":{"cached_tokens":0}}}""";

    #region ParseResponse 单元测试

    [Fact]
    [DisplayName("ParseResponse_通义千问响应_基础字段正确解析")]
    public void ParseResponse_QwenPlusResponse_BasicFieldsParsed()
    {
        var response = QwenJson.ToJsonEntity<ChatCompletionResponse>()!.ToChatResponse();

        Assert.NotNull(response);
        Assert.Equal("chatcmpl-131cb128-bba5-939a-b206-48807913b636", response.Id);
        Assert.Equal("chat.completion", response.Object);
        Assert.Equal("qwen-plus", response.Model);
        Assert.Equal(1774532789L, response.Created.ToUnixTimeSeconds());
    }

    [Fact]
    [DisplayName("ParseResponse_通义千问响应_Choices解析正确")]
    public void ParseResponse_QwenPlusResponse_ChoicesParsed()
    {
        var response = QwenJson.ToJsonEntity<ChatCompletionResponse>()!.ToChatResponse();

        Assert.NotNull(response.Messages);
        Assert.Single(response.Messages);

        var choice = response.Messages[0];
        Assert.Equal(0, choice.Index);
        Assert.Equal(FinishReason.Stop, choice.FinishReason);
    }

    [Fact]
    [DisplayName("ParseResponse_通义千问响应_Message内容正确")]
    public void ParseResponse_QwenPlusResponse_MessageContentCorrect()
    {
        var response = QwenJson.ToJsonEntity<ChatCompletionResponse>()!.ToChatResponse();

        var msg = response.Messages![0].Message;
        Assert.NotNull(msg);
        Assert.Equal("assistant", msg.Role);
        Assert.Equal("我是通义千问（Qwen），阿里巴巴集团旗下的超大规模语言模型，能够回答问题、创作文字，如写故事、公文、邮件、剧本等，还能进行逻辑推理、编程，甚至表达观点和玩游戏，支持多种语言，致力于为用户提供高效、智能、友好的交互体验。", msg.Content as String);
    }

    [Fact]
    [DisplayName("ParseResponse_通义千问响应_Usage用量正确解析")]
    public void ParseResponse_QwenPlusResponse_UsageParsed()
    {
        var response = QwenJson.ToJsonEntity<ChatCompletionResponse>()!.ToChatResponse();

        Assert.NotNull(response.Usage);
        Assert.Equal(12, response.Usage.InputTokens);
        Assert.Equal(65, response.Usage.OutputTokens);
        Assert.Equal(77, response.Usage.TotalTokens);
    }

    #endregion

    #region BuildBody 序列化验证

    [Fact]
    [DisplayName("BuildRequest_非流式请求_生产路径不含stream_options")]
    public void BuildRequest_NonStreamRequest_NoStreamOptionsField()
    {
        var request = new ChatRequest
        {
            Model = "qwen-max",
            Messages = [new ChatMessage { Role = "user", Content = "hi" }],
        };
        // Stream 默认为 false，序列化后不应写入 stream 和 stream_options
        var body = ChatCompletionRequest.FromChatRequest(request);

        var options = new AiClientOptions();
        var client = new OpenAIChatClient(options);

        // 与生产代码 AiClientBase.PostAsync 完全一致：传入 JsonHost.Options
        //var json = client.JsonHost.Write(body, client.JsonHost.Options);
        var json = client.JsonHost.Write(body, false, false, false);
        var dic = JsonParser.Decode(json);

        Assert.False(dic.ContainsKey("stream_options"), $"非流式请求不应包含 stream_options, json={json}");
        Assert.False(dic.ContainsKey("stream"), $"非流式请求不应包含 stream, json={json}");
    }

    [Fact]
    [DisplayName("BuildBody_非流式请求_不含stream_options字段")]
    public void BuildBody_NonStreamRequest_NoStreamOptionsField()
    {
        var request = new ChatRequest
        {
            Model = "qwen-max",
            Messages = [new ChatMessage { Role = "user", Content = "hi" }],
        };
        // Stream 默认为 false，BuildBody 不应写入 stream 和 stream_options
        var body = ChatCompletionRequest.BuildBody(request);

        Assert.False(body.ContainsKey("stream_options"), "非流式请求不应包含 stream_options");
        Assert.False(body.ContainsKey("stream"), "非流式请求不应包含 stream");
    }

    [Fact]
    [DisplayName("BuildBody_流式请求_包含stream和stream_options字段")]
    public void BuildBody_StreamRequest_HasStreamOptionsField()
    {
        var request = new ChatRequest
        {
            Model = "qwen-plus",
            Messages = [new ChatMessage { Role = "user", Content = "hi" }],
            Stream = true,
        };
        var body = ChatCompletionRequest.BuildBody(request);

        Assert.True(body.ContainsKey("stream"), "流式请求应包含 stream 字段");
        Assert.True(body.ContainsKey("stream_options"), "流式请求应包含 stream_options");
        var streamOptions = body["stream_options"] as IDictionary<String, Object>;
        Assert.NotNull(streamOptions);
        Assert.True(streamOptions.ContainsKey("include_usage"));
    }

    #endregion
}
