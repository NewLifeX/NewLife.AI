#nullable enable
using System;
using System.ComponentModel;
using NewLife.AI.Clients;
using NewLife.AI.Providers;
using Xunit;

namespace XUnitTest.Clients;

/// <summary>OpenAiChatClient 单元测试（不需要网络/ApiKey，直接验证解析逻辑）</summary>
public class OpenAiChatClientTests
{
    private static OpenAiChatClient CreateClient() => new(new AiClientOptions { Endpoint = "https://test.local", ApiKey = "test" });

    #region ParseResponse 单元测试

    [Fact]
    [DisplayName("ParseResponse_通义千问响应_基础字段正确解析")]
    public void ParseResponse_QwenPlusResponse_BasicFieldsParsed()
    {
        var json = """{"model":"qwen-plus","id":"chatcmpl-131cb128-bba5-939a-b206-48807913b636","choices":[{"message":{"content":"我是通义千问（Qwen），阿里巴巴集团旗下的超大规模语言模型，能够回答问题、创作文字，如写故事、公文、邮件、剧本等，还能进行逻辑推理、编程，甚至表达观点和玩游戏，支持多种语言，致力于为用户提供高效、智能、友好的交互体验。","role":"assistant"},"index":0,"finish_reason":"stop"}],"created":1774532789,"object":"chat.completion","usage":{"total_tokens":77,"completion_tokens":65,"prompt_tokens":12,"prompt_tokens_details":{"cached_tokens":0}}}""";

        var client = CreateClient();
        var response = client.ParseResponse(json);

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
        var json = """{"model":"qwen-plus","id":"chatcmpl-131cb128-bba5-939a-b206-48807913b636","choices":[{"message":{"content":"我是通义千问（Qwen），阿里巴巴集团旗下的超大规模语言模型，能够回答问题、创作文字，如写故事、公文、邮件、剧本等，还能进行逻辑推理、编程，甚至表达观点和玩游戏，支持多种语言，致力于为用户提供高效、智能、友好的交互体验。","role":"assistant"},"index":0,"finish_reason":"stop"}],"created":1774532789,"object":"chat.completion","usage":{"total_tokens":77,"completion_tokens":65,"prompt_tokens":12,"prompt_tokens_details":{"cached_tokens":0}}}""";

        var client = CreateClient();
        var response = client.ParseResponse(json);

        Assert.NotNull(response.Messages);
        Assert.Single(response.Messages);

        var choice = response.Messages[0];
        Assert.Equal(0, choice.Index);
        Assert.Equal("stop", choice.FinishReason);
    }

    [Fact]
    [DisplayName("ParseResponse_通义千问响应_Message内容正确")]
    public void ParseResponse_QwenPlusResponse_MessageContentCorrect()
    {
        var json = """{"model":"qwen-plus","id":"chatcmpl-131cb128-bba5-939a-b206-48807913b636","choices":[{"message":{"content":"我是通义千问（Qwen），阿里巴巴集团旗下的超大规模语言模型，能够回答问题、创作文字，如写故事、公文、邮件、剧本等，还能进行逻辑推理、编程，甚至表达观点和玩游戏，支持多种语言，致力于为用户提供高效、智能、友好的交互体验。","role":"assistant"},"index":0,"finish_reason":"stop"}],"created":1774532789,"object":"chat.completion","usage":{"total_tokens":77,"completion_tokens":65,"prompt_tokens":12,"prompt_tokens_details":{"cached_tokens":0}}}""";

        var client = CreateClient();
        var response = client.ParseResponse(json);

        var msg = response.Messages![0].Message;
        Assert.NotNull(msg);
        Assert.Equal("assistant", msg.Role);
        Assert.Equal("我是通义千问（Qwen），阿里巴巴集团旗下的超大规模语言模型，能够回答问题、创作文字，如写故事、公文、邮件、剧本等，还能进行逻辑推理、编程，甚至表达观点和玩游戏，支持多种语言，致力于为用户提供高效、智能、友好的交互体验。", msg.Content as String);
    }

    [Fact]
    [DisplayName("ParseResponse_通义千问响应_Usage用量正确解析")]
    public void ParseResponse_QwenPlusResponse_UsageParsed()
    {
        var json = """{"model":"qwen-plus","id":"chatcmpl-131cb128-bba5-939a-b206-48807913b636","choices":[{"message":{"content":"我是通义千问（Qwen），阿里巴巴集团旗下的超大规模语言模型，能够回答问题、创作文字，如写故事、公文、邮件、剧本等，还能进行逻辑推理、编程，甚至表达观点和玩游戏，支持多种语言，致力于为用户提供高效、智能、友好的交互体验。","role":"assistant"},"index":0,"finish_reason":"stop"}],"created":1774532789,"object":"chat.completion","usage":{"total_tokens":77,"completion_tokens":65,"prompt_tokens":12,"prompt_tokens_details":{"cached_tokens":0}}}""";

        var client = CreateClient();
        var response = client.ParseResponse(json);

        Assert.NotNull(response.Usage);
        Assert.Equal(12, response.Usage.InputTokens);
        Assert.Equal(65, response.Usage.OutputTokens);
        Assert.Equal(77, response.Usage.TotalTokens);
    }

    #endregion
}
