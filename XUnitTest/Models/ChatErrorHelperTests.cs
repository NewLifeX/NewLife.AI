using System;
using System.ComponentModel;
using NewLife.AI.Models;
using Xunit;

namespace XUnitTest.Models;

/// <summary>聊天错误识别与友好化帮助类测试。覆盖上下文超限模式识别、窗口数提取与文案生成</summary>
[DisplayName("ChatErrorHelper错误识别测试")]
public class ChatErrorHelperTests
{
    [Fact]
    [DisplayName("LiteLLM qwen 输入长度超限原文可识别并提取窗口")]
    public void Classify_LiteLLM_Qwen_Recognizes()
    {
        var msg = "{\"error\":{\"message\":\"litellm.BadRequestError: OpenAIException - <400> InternalError.Algo.InvalidParameter: Range of input length should be [1, 983616]No fallback model group found for original model_group=qwen3.7-plus. Fallbacks=[].\",\"type\":\"invalid_request_error\",\"param\":null,\"code\":\"400\"}}";

        Assert.True(ChatErrorHelper.IsContextLengthError(msg));
        var info = ChatErrorHelper.Classify(msg);

        Assert.Equal("CONTEXT_TOO_LONG", info.Code);
        Assert.Equal(983616, info.ContextLength);
        Assert.Contains("983,616", info.Message);
        Assert.Contains("开启新会话", info.Message);
    }

    [Theory]
    [DisplayName("主流网关上下文超限文案均可识别")]
    [InlineData("This model's maximum context length is 32768 tokens. However, you requested 40000 tokens.")]
    [InlineData("Error code: 400 - {'error': {'code': 'context_length_exceeded'}}")]
    [InlineData("prompt is too long: 30000 tokens > 200000 maximum")]
    [InlineData("Request too large for claude-3-5-sonnet")]
    [InlineData("context window is 8192, but the prompt requires 9000")]
    [InlineData("too many tokens in the messages")]
    [InlineData("输入长度超出限制")]
    [InlineData("对话内容过长，已超出上下文窗口")]
    public void Classify_ContextPatterns_Recognized(String msg)
    {
        Assert.True(ChatErrorHelper.IsContextLengthError(msg));
        var info = ChatErrorHelper.Classify(msg);

        Assert.Equal("CONTEXT_TOO_LONG", info.Code);
        Assert.Contains("上下文窗口", info.Message);
    }

    [Fact]
    [DisplayName("OpenAI maximum context length 可提取窗口数")]
    public void TryExtractContextLength_MaximumContextLength_Extracts()
    {
        var limit = ChatErrorHelper.TryExtractContextLength("This model's maximum context length is 32768 tokens.");
        Assert.Equal(32768, limit);
    }

    [Fact]
    [DisplayName("无窗口数时友好文案不带数值")]
    public void Classify_NoLimit_MessageWithoutNumber()
    {
        var info = ChatErrorHelper.Classify("prompt is too long");
        Assert.Equal("CONTEXT_TOO_LONG", info.Code);
        Assert.Null(info.ContextLength);
        Assert.DoesNotContain("tokens）", info.Message);
    }

    [Fact]
    [DisplayName("非上下文超限错误保持原始信息")]
    public void Classify_OtherError_KeepsOriginal()
    {
        var info = ChatErrorHelper.Classify("模型不存在", "STREAM_ERROR");

        Assert.Equal("STREAM_ERROR", info.Code);
        Assert.Equal("模型不存在", info.Message);
    }

    [Fact]
    [DisplayName("空消息不误判为上下文超限")]
    public void Classify_Empty_NotContextError()
    {
        Assert.False(ChatErrorHelper.IsContextLengthError(null));
        Assert.False(ChatErrorHelper.IsContextLengthError(""));

        var info = ChatErrorHelper.Classify(null);
        Assert.Equal("STREAM_ERROR", info.Code);
    }
}
