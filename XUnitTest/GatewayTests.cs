using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using NewLife.AI.Models;
using NewLife.AI.Providers;
using NewLife.ChatAI.Services;
using Xunit;

namespace XUnitTest;

/// <summary>API 网关单元测试</summary>
public class GatewayTests
{
    #region 重试延迟测试
    [Fact]
    public void GetRetryDelayReturnsIncreasingValues()
    {
        var d0 = GatewayService.GetRetryDelay(0);
        var d1 = GatewayService.GetRetryDelay(1);
        var d2 = GatewayService.GetRetryDelay(2);
        var d3 = GatewayService.GetRetryDelay(3);

        // 基础延迟：1s, 2s, 4s, 8s + 0~250ms 抖动
        Assert.InRange(d0, 1000, 1250);
        Assert.InRange(d1, 2000, 2250);
        Assert.InRange(d2, 4000, 4250);
        Assert.InRange(d3, 8000, 8250);
    }

    [Fact]
    public void GetRetryDelayCapsAtMaximum()
    {
        // 第10次重试：2^10 = 1024s >> 30s，应被限制在 30s
        var delay = GatewayService.GetRetryDelay(10);

        Assert.InRange(delay, 30000, 30250);
    }

    [Fact]
    public void GetRetryDelayIncludesJitter()
    {
        // 多次调用同一 retryIndex，由于随机抖动不应总是相同值
        var delays = Enumerable.Range(0, 100).Select(_ => GatewayService.GetRetryDelay(0)).ToList();
        var distinct = delays.Distinct().Count();

        // 大概率出现多种不同值（允许极小概率全相同）
        Assert.True(distinct > 1, "重试延迟应包含随机抖动");
    }
    #endregion

    #region 429 检测测试
    [Fact]
    public void Is429DetectsStatusCodeProperty()
    {
        var ex = new HttpRequestException("Too Many Requests", null, HttpStatusCode.TooManyRequests);

        Assert.True(GatewayService.Is429(ex));
    }

    [Fact]
    public void Is429DetectsMessageFallback()
    {
        var ex = new HttpRequestException("AI 服务商 OpenAI 返回错误 429: rate limit exceeded");

        Assert.True(GatewayService.Is429(ex));
    }

    [Fact]
    public void Is429ReturnsFalseForOtherErrors()
    {
        var ex = new HttpRequestException("Internal Server Error", null, HttpStatusCode.InternalServerError);

        Assert.False(GatewayService.Is429(ex));
    }

    [Fact]
    public void Is429ReturnsFalseForGenericError()
    {
        var ex = new HttpRequestException("Connection refused");

        Assert.False(GatewayService.Is429(ex));
    }
    #endregion

    #region 模型路由测试
    [Fact]
    public void ProviderFactoryResolvesAllBuiltInProviders()
    {
        var factory = AiProviderFactory.Default;

        // 验证核心服务商都能被正确查找
        var providers = new[] { "OpenAI", "DashScope", "DeepSeek", "Anthropic", "Gemini" };
        foreach (var name in providers)
        {
            var provider = factory.GetProvider(name);
            Assert.NotNull(provider);
            Assert.Equal(name, provider.Name);
        }
    }

    [Fact]
    public void ProviderFactoryReturnsNullForUnknown()
    {
        var factory = AiProviderFactory.Default;
        Assert.Null(factory.GetProvider("NonExistProvider"));
    }

    [Theory]
    [InlineData("OpenAI", "ChatCompletions")]
    [InlineData("DashScope", "ChatCompletions")]
    [InlineData("DeepSeek", "ChatCompletions")]
    [InlineData("Anthropic", "AnthropicMessages")]
    [InlineData("Gemini", "Gemini")]
    public void ProviderProtocolMatchesExpected(String providerName, String expectedProtocol)
    {
        var factory = AiProviderFactory.Default;
        var provider = factory.GetProvider(providerName);

        Assert.NotNull(provider);
        Assert.Equal(expectedProtocol, provider.ApiProtocol);
    }
    #endregion

    #region AiProviderOptions 测试
    [Fact]
    public void BuildOptionsUsesEndpointAndApiKey()
    {
        var options = new AiProviderOptions
        {
            Endpoint = "https://custom.api.com",
            ApiKey = "sk-test-key-123",
        };

        Assert.Equal("https://custom.api.com", options.Endpoint);
        Assert.Equal("sk-test-key-123", options.ApiKey);
    }

    [Fact]
    public void GetEndpointFallsBackToDefault()
    {
        var options = new AiProviderOptions();
        Assert.Equal("https://api.openai.com", options.GetEndpoint("https://api.openai.com"));
    }

    [Fact]
    public void GetEndpointPrefersCustom()
    {
        var options = new AiProviderOptions { Endpoint = "https://my-proxy.com" };
        Assert.Equal("https://my-proxy.com", options.GetEndpoint("https://api.openai.com"));
    }
    #endregion

    #region ChatCompletionRequest 构建测试
    [Fact]
    public void RequestCanSetModelAndMessages()
    {
        var request = new ChatCompletionRequest
        {
            Model = "gpt-4o",
            Messages =
            [
                new ChatMessage { Role = "system", Content = "You are helpful." },
                new ChatMessage { Role = "user", Content = "Hello" },
            ],
            Stream = true,
        };

        Assert.Equal("gpt-4o", request.Model);
        Assert.Equal(2, request.Messages.Count);
        Assert.True(request.Stream);
    }

    [Fact]
    public void RequestSupportsToolsDefinition()
    {
        var request = new ChatCompletionRequest
        {
            Model = "gpt-4o",
            Messages = [new ChatMessage { Role = "user", Content = "What's the weather?" }],
            Tools =
            [
                new ChatTool
                {
                    Type = "function",
                    Function = new FunctionDefinition
                    {
                        Name = "get_weather",
                        Description = "Get current weather",
                        Parameters = new Dictionary<String, Object>
                        {
                            ["type"] = "object",
                            ["properties"] = new Dictionary<String, Object>
                            {
                                ["city"] = new Dictionary<String, Object> { ["type"] = "string" }
                            }
                        }
                    }
                }
            ],
            ToolChoice = "auto",
        };

        Assert.NotNull(request.Tools);
        Assert.Single(request.Tools);
        Assert.Equal("get_weather", request.Tools[0].Function?.Name);
    }
    #endregion

    #region ChatCompletionResponse 解析测试
    [Fact]
    public void ResponseHasCorrectStructure()
    {
        var response = new ChatCompletionResponse
        {
            Id = "chatcmpl-test123",
            Object = "chat.completion",
            Created = 1700000000,
            Model = "gpt-4o",
            Choices =
            [
                new ChatChoice
                {
                    Index = 0,
                    Message = new ChatMessage { Role = "assistant", Content = "Hello!" },
                    FinishReason = "stop",
                }
            ],
            Usage = new ChatUsage
            {
                PromptTokens = 10,
                CompletionTokens = 5,
                TotalTokens = 15,
            }
        };

        Assert.Equal("chatcmpl-test123", response.Id);
        Assert.Equal("chat.completion", response.Object);
        Assert.Single(response.Choices);
        Assert.Equal("assistant", response.Choices[0].Message?.Role);
        Assert.Equal("Hello!", response.Choices[0].Message?.Content as String);
        Assert.Equal(15, response.Usage?.TotalTokens);
    }

    [Fact]
    public void StreamChunkHasDelta()
    {
        var chunk = new ChatCompletionResponse
        {
            Id = "chatcmpl-stream",
            Object = "chat.completion.chunk",
            Choices =
            [
                new ChatChoice
                {
                    Index = 0,
                    Delta = new ChatMessage { Role = "assistant", Content = "He" },
                }
            ],
        };

        Assert.NotNull(chunk.Choices);
        Assert.NotNull(chunk.Choices[0].Delta);
        Assert.Equal("He", chunk.Choices[0].Delta.Content as String);
        Assert.Null(chunk.Choices[0].Message);
    }
    #endregion

    #region 认证测试
    [Fact]
    public void ValidateAppKeyReturnsNullForEmptyHeader()
    {
        var service = new GatewayService(null, null);

        Assert.Null(service.ValidateAppKey(null));
        Assert.Null(service.ValidateAppKey(""));
        Assert.Null(service.ValidateAppKey("   "));
    }

    [Fact]
    public void ValidateAppKeyParsesBearer()
    {
        var service = new GatewayService(null, null);

        // 由于数据库中没有数据，FindBySecret 会返回 null
        var result = service.ValidateAppKey("Bearer sk-test-nonexistent");
        Assert.Null(result);
    }

    [Fact]
    public void ValidateAppKeyHandlesNoBearerPrefix()
    {
        var service = new GatewayService(null, null);

        // 直接传密钥（无 Bearer 前缀）也应尝试查找
        var result = service.ValidateAppKey("sk-direct-key");
        Assert.Null(result);
    }
    #endregion

    #region 模型解析测试
    [Fact]
    public void ResolveModelReturnsNullForEmpty()
    {
        var service = new GatewayService(null, null);

        Assert.Null(service.ResolveModel(null));
        Assert.Null(service.ResolveModel(""));
        Assert.Null(service.ResolveModel("   "));
    }

    [Fact]
    public void ResolveModelReturnsNullForNonExistent()
    {
        var service = new GatewayService(null, null);

        // 数据库无数据时返回 null
        Assert.Null(service.ResolveModel("nonexistent-model"));
    }
    #endregion

    #region SSE 事件模型测试
    [Fact]
    public void ChatStreamEventFactoryMethodsCreateCorrectTypes()
    {
        var start = ChatStreamEvent.MessageStart(1001, "gpt-4o", 0);
        Assert.Equal("message_start", start.Type);
        Assert.Equal(1001, start.MessageId);
        Assert.Equal("gpt-4o", start.Model);

        var thinkDelta = ChatStreamEvent.ThinkingDelta("分析中...");
        Assert.Equal("thinking_delta", thinkDelta.Type);
        Assert.Equal("分析中...", thinkDelta.Content);

        var thinkDone = ChatStreamEvent.ThinkingDone(3200);
        Assert.Equal("thinking_done", thinkDone.Type);
        Assert.Equal(3200, thinkDone.ThinkingTime);

        var contentDelta = ChatStreamEvent.ContentDelta("Hello");
        Assert.Equal("content_delta", contentDelta.Type);
        Assert.Equal("Hello", contentDelta.Content);

        var done = ChatStreamEvent.MessageDone(new ChatUsage { TotalTokens = 100 }, "测试标题");
        Assert.Equal("message_done", done.Type);
        Assert.Equal(100, done.Usage?.TotalTokens);
        Assert.Equal("测试标题", done.Title);

        var error = ChatStreamEvent.ErrorEvent("MODEL_UNAVAILABLE", "模型不可用");
        Assert.Equal("error", error.Type);
        Assert.Equal("MODEL_UNAVAILABLE", error.Code);
        Assert.Equal("模型不可用", error.Message);
    }

    [Fact]
    public void ChatStreamEventToolCallEventsHaveCorrectFields()
    {
        var toolStart = ChatStreamEvent.ToolCallStart("call_001", "get_weather", "{\"city\":\"北京\"}");
        Assert.Equal("tool_call_start", toolStart.Type);
        Assert.Equal("call_001", toolStart.ToolCallId);
        Assert.Equal("get_weather", toolStart.Name);

        var toolDone = ChatStreamEvent.ToolCallDone("call_001", "{\"temp\":25}", true);
        Assert.Equal("tool_call_done", toolDone.Type);
        Assert.True(toolDone.Success);

        var toolError = ChatStreamEvent.ToolCallError("call_001", "服务不可用");
        Assert.Equal("tool_call_error", toolError.Type);
        Assert.Equal("服务不可用", toolError.Error);
    }
    #endregion

    #region 请求参数完整性测试
    [Fact]
    public void RequestSupportsAllOptionalParameters()
    {
        var request = new ChatCompletionRequest
        {
            Model = "gpt-4o",
            Messages = [new ChatMessage { Role = "user", Content = "test" }],
            Temperature = 0.7,
            TopP = 0.9,
            MaxTokens = 4096,
            Stop = ["###"],
            PresencePenalty = 0.5,
            FrequencyPenalty = -0.5,
            User = "user-123",
        };

        Assert.Equal(0.7, request.Temperature);
        Assert.Equal(0.9, request.TopP);
        Assert.Equal(4096, request.MaxTokens);
        Assert.Single(request.Stop);
        Assert.Equal(0.5, request.PresencePenalty);
        Assert.Equal(-0.5, request.FrequencyPenalty);
        Assert.Equal("user-123", request.User);
    }

    [Fact]
    public void MessageSupportsReasoningContent()
    {
        var msg = new ChatMessage
        {
            Role = "assistant",
            Content = "最终答案",
            ReasoningContent = "先分析问题...",
        };

        Assert.Equal("assistant", msg.Role);
        Assert.Equal("最终答案", msg.Content as String);
        Assert.Equal("先分析问题...", msg.ReasoningContent);
    }

    [Fact]
    public void MessageSupportsToolCalls()
    {
        var msg = new ChatMessage
        {
            Role = "assistant",
            ToolCalls =
            [
                new ToolCall
                {
                    Id = "call_abc",
                    Type = "function",
                    Function = new FunctionCall
                    {
                        Name = "search",
                        Arguments = "{\"query\":\"test\"}",
                    }
                }
            ],
        };

        Assert.NotNull(msg.ToolCalls);
        Assert.Single(msg.ToolCalls);
        Assert.Equal("call_abc", msg.ToolCalls[0].Id);
        Assert.Equal("search", msg.ToolCalls[0].Function?.Name);
    }

    [Fact]
    public void ToolRoleMessageHasToolCallId()
    {
        var msg = new ChatMessage
        {
            Role = "tool",
            ToolCallId = "call_abc",
            Content = "{\"result\": \"found\"}",
        };

        Assert.Equal("tool", msg.Role);
        Assert.Equal("call_abc", msg.ToolCallId);
    }
    #endregion

    #region 多服务商路由一致性测试
    [Fact]
    public void AllChatCompletionsProvidersShareSameInterface()
    {
        var factory = AiProviderFactory.Default;
        var chatCompletionProviders = factory.GetProviderNames()
            .Select(n => factory.GetProvider(n))
            .Where(p => p?.ApiProtocol == "ChatCompletions")
            .ToList();

        // 至少28个 OpenAI 兼容 + 3个本地引擎 = 31
        Assert.True(chatCompletionProviders.Count >= 28, $"ChatCompletions 协议服务商应至少28个，实际 {chatCompletionProviders.Count}");

        foreach (var provider in chatCompletionProviders)
        {
            Assert.NotNull(provider);
            Assert.IsAssignableFrom<IAiProvider>(provider);
        }
    }

    [Fact]
    public void ProtocolGroupCountsMatchExpected()
    {
        var factory = AiProviderFactory.Default;
        var groups = factory.GetProviderNames()
            .Select(n => factory.GetProvider(n))
            .Where(p => p != null)
            .GroupBy(p => p!.ApiProtocol)
            .ToDictionary(g => g.Key, g => g.Count());

        // Anthropic 协议1个，Gemini 协议1个
        Assert.Equal(1, groups["AnthropicMessages"]);
        Assert.Equal(1, groups["Gemini"]);
        // ChatCompletions 至少28个
        Assert.True(groups["ChatCompletions"] >= 28);
    }
    #endregion
}
