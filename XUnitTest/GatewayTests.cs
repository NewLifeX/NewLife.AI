using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using NewLife;
using NewLife.AI.Models;
using NewLife.AI.Providers;
using NewLife.ChatAI.Entity;
using NewLife.ChatAI.Services;
using Xunit;
using ChatMessage = NewLife.AI.Models.ChatMessage;

namespace XUnitTest;

/// <summary>API ���ص�Ԫ����</summary>
public class GatewayTests
{
    #region �����ӳٲ���
    [Fact]
    public void GetRetryDelayReturnsIncreasingValues()
    {
        var d0 = GatewayService.GetRetryDelay(0);
        var d1 = GatewayService.GetRetryDelay(1);
        var d2 = GatewayService.GetRetryDelay(2);
        var d3 = GatewayService.GetRetryDelay(3);

        // �����ӳ٣�1s, 2s, 4s, 8s + 0~250ms ����
        Assert.InRange(d0, 1000, 1250);
        Assert.InRange(d1, 2000, 2250);
        Assert.InRange(d2, 4000, 4250);
        Assert.InRange(d3, 8000, 8250);
    }

    [Fact]
    public void GetRetryDelayCapsAtMaximum()
    {
        // ��10�����ԣ�2^10 = 1024s >> 30s��Ӧ�������� 30s
        var delay = GatewayService.GetRetryDelay(10);

        Assert.InRange(delay, 30000, 30250);
    }

    [Fact]
    public void GetRetryDelayIncludesJitter()
    {
        // ��ε���ͬһ retryIndex���������������Ӧ������ֵͬ
        var delays = Enumerable.Range(0, 100).Select(_ => GatewayService.GetRetryDelay(0)).ToList();
        var distinct = delays.Distinct().Count();

        // ����ʳ��ֶ��ֲ�ֵͬ��������С����ȫ��ͬ��
        Assert.True(distinct > 1, "�����ӳ�Ӧ�����������");
    }
    #endregion

    #region 429 ������
    [Fact]
    public void Is429DetectsStatusCodeProperty()
    {
        var ex = new HttpRequestException("Too Many Requests", null, HttpStatusCode.TooManyRequests);

        Assert.True(GatewayService.Is429(ex));
    }

    [Fact]
    public void Is429DetectsMessageFallback()
    {
        var ex = new HttpRequestException("AI ������ OpenAI ���ش��� 429: rate limit exceeded");

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

    #region ģ��·�ɲ���
    [Fact]
    public void ProviderFactoryResolvesAllBuiltInProviders()
    {
        var registry = AiClientRegistry.Default;
        var byCode = registry.Descriptors.Values
            .ToDictionary(p => p.Code, StringComparer.OrdinalIgnoreCase);

        // ��֤���ķ����̶��ܱ���ȷ����
        var expectedCodes = new[] { "OpenAI", "DashScope", "DeepSeek", "Anthropic", "Gemini" };
        foreach (var code in expectedCodes)
        {
            Assert.True(byCode.ContainsKey(code), $"������ {code} δע��");
        }
    }

    [Fact]
    public void ProviderFactoryReturnsNullForUnknown()
    {
        var registry = AiClientRegistry.Default;
        Assert.Null(registry.GetDescriptor("NonExistProvider"));
    }

    [Theory]
    [InlineData("OpenAI", "OpenAI")]
    [InlineData("DashScope", "DashScope")]
    [InlineData("DeepSeek", "OpenAI")]
    [InlineData("Anthropic", "AnthropicMessages")]
    [InlineData("Gemini", "Gemini")]
    public void ProviderProtocolMatchesExpected(String providerCode, String expectedProtocol)
    {
        var registry = AiClientRegistry.Default;
        var provider = registry.Descriptors.Values
            .FirstOrDefault(p => p.Code.Equals(providerCode, StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(provider);
        Assert.Equal(expectedProtocol, provider.Protocol);
    }
    #endregion

    #region AiClientOptions ����
    [Fact]
    public void BuildOptionsUsesEndpointAndApiKey()
    {
        var options = new AiClientOptions
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
        var options = new AiClientOptions();
        Assert.Equal("https://api.openai.com", options.GetEndpoint("https://api.openai.com"));
    }

    [Fact]
    public void GetEndpointPrefersCustom()
    {
        var options = new AiClientOptions { Endpoint = "https://my-proxy.com" };
        Assert.Equal("https://my-proxy.com", options.GetEndpoint("https://api.openai.com"));
    }
    #endregion

    #region ChatCompletionRequest ��������
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

    #region ChatCompletionResponse ��������
    [Fact]
    public void ResponseHasCorrectStructure()
    {
        var response = new ChatResponse
        {
            Id = "chatcmpl-test123",
            Object = "chat.completion",
            Created = 1700000000.ToDateTimeOffset(),
            Model = "gpt-4o",
            Messages =
            [
                new ChatChoice
                {
                    Index = 0,
                    Message = new ChatMessage { Role = "assistant", Content = "Hello!" },
                    FinishReason = "stop",
                }
            ],
            Usage = new UsageDetails
            {
                InputTokens = 10,
                OutputTokens = 5,
                TotalTokens = 15,
            }
        };

        Assert.Equal("chatcmpl-test123", response.Id);
        Assert.Equal("chat.completion", response.Object);
        Assert.Single(response.Messages);
        Assert.Equal("assistant", response.Messages[0].Message?.Role);
        Assert.Equal("Hello!", response.Messages[0].Message?.Content as String);
        Assert.Equal(15, response.Usage?.TotalTokens);
    }

    [Fact]
    public void StreamChunkHasDelta()
    {
        var chunk = new ChatResponse
        {
            Id = "chatcmpl-stream",
            Object = "chat.completion.chunk",
            Messages =
            [
                new ChatChoice
                {
                    Index = 0,
                    Delta = new ChatMessage { Role = "assistant", Content = "He" },
                }
            ],
        };

        Assert.NotNull(chunk.Messages);
        Assert.NotNull(chunk.Messages[0].Delta);
        Assert.Equal("He", chunk.Messages[0].Delta.Content as String);
        Assert.Null(chunk.Messages[0].Message);
    }
    #endregion

    #region ��֤����
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

        // �������ݿ���û�����ݣ�FindBySecret �᷵�� null
        var result = service.ValidateAppKey("Bearer sk-test-nonexistent");
        Assert.Null(result);
    }

    [Fact]
    public void ValidateAppKeyHandlesNoBearerPrefix()
    {
        var service = new GatewayService(null, null);

        // ֱ�Ӵ���Կ���� Bearer ǰ׺��ҲӦ���Բ���
        var result = service.ValidateAppKey("sk-direct-key");
        Assert.Null(result);
    }
    #endregion

    #region ģ�ͽ�������
    [Fact]
    public void ResolveModelReturnsNullForEmpty()
    {
        var service = new GatewayService(null, null);

        Assert.Null(service.ResolveModel(0));
        Assert.Null(service.ResolveModel(-1));
    }

    [Fact]
    public void ResolveModelReturnsNullForNonExistent()
    {
        var service = new GatewayService(null, null);

        // ���ݿ�������ʱ���� null
        Assert.Null(service.ResolveModel(99999));
    }

    [Fact]
    public void IsModelAllowedReturnsTrueWhenNoFilter()
    {
        var service = new GatewayService(null, null);
        var appKey = new AppKey { Models = null };
        var model = new ModelConfig { Code = "gpt-4o", Name = "GPT-4o" };

        Assert.True(service.IsModelAllowed(appKey, model));
    }

    [Fact]
    public void IsModelAllowedMatchesByCodeOrName()
    {
        var service = new GatewayService(null, null);
        var appKey = new AppKey { Models = "qwen-max, GPT-4o" };

        Assert.True(service.IsModelAllowed(appKey, new ModelConfig { Code = "qwen-max", Name = "Qwen Max" }));
        Assert.True(service.IsModelAllowed(appKey, new ModelConfig { Code = "o4-mini", Name = "GPT-4o" }));
        Assert.False(service.IsModelAllowed(appKey, new ModelConfig { Code = "deepseek-r1", Name = "DeepSeek-R1" }));
    }

    [Fact]
    public void NormalizeModelsBuildsCommaSeparatedUniqueValues()
    {
        var text = NewLife.ChatAI.Entity.AppKey.NormalizeModels(" gpt-4o，qwen-max\nGPT-4o ; deepseek-r1 ");

        Assert.Equal("gpt-4o,qwen-max,deepseek-r1", text);
    }
    #endregion

    #region SSE �¼�ģ�Ͳ���
    [Fact]
    public void ChatStreamEventFactoryMethodsCreateCorrectTypes()
    {
        var start = ChatStreamEvent.MessageStart(1001, "gpt-4o", 0);
        Assert.Equal("message_start", start.Type);
        Assert.Equal(1001, start.MessageId);
        Assert.Equal("gpt-4o", start.Model);

        var thinkDelta = ChatStreamEvent.ThinkingDelta("������...");
        Assert.Equal("thinking_delta", thinkDelta.Type);
        Assert.Equal("������...", thinkDelta.Content);

        var thinkDone = ChatStreamEvent.ThinkingDone(3200);
        Assert.Equal("thinking_done", thinkDone.Type);
        Assert.Equal(3200, thinkDone.ThinkingTime);

        var contentDelta = ChatStreamEvent.ContentDelta("Hello");
        Assert.Equal("content_delta", contentDelta.Type);
        Assert.Equal("Hello", contentDelta.Content);

        var done = ChatStreamEvent.MessageDone(new UsageDetails { TotalTokens = 100 }, "���Ա���");
        Assert.Equal("message_done", done.Type);
        Assert.Equal(100, done.Usage?.TotalTokens);
        Assert.Equal("���Ա���", done.Title);

        var error = ChatStreamEvent.ErrorEvent("MODEL_UNAVAILABLE", "ģ�Ͳ�����");
        Assert.Equal("error", error.Type);
        Assert.Equal("MODEL_UNAVAILABLE", error.Code);
        Assert.Equal("ģ�Ͳ�����", error.Message);
    }

    [Fact]
    public void ChatStreamEventToolCallEventsHaveCorrectFields()
    {
        var toolStart = ChatStreamEvent.ToolCallStart("call_001", "get_weather", "{\"city\":\"����\"}");
        Assert.Equal("tool_call_start", toolStart.Type);
        Assert.Equal("call_001", toolStart.ToolCallId);
        Assert.Equal("get_weather", toolStart.Name);

        var toolDone = ChatStreamEvent.ToolCallDone("call_001", "{\"temp\":25}", true);
        Assert.Equal("tool_call_done", toolDone.Type);
        Assert.True(toolDone.Success);

        var toolError = ChatStreamEvent.ToolCallError("call_001", "���񲻿���");
        Assert.Equal("tool_call_error", toolError.Type);
        Assert.Equal("���񲻿���", toolError.Error);
    }
    #endregion

    #region ������������Բ���
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
            Content = "���մ�",
            ReasoningContent = "�ȷ�������...",
        };

        Assert.Equal("assistant", msg.Role);
        Assert.Equal("���մ�", msg.Content as String);
        Assert.Equal("�ȷ�������...", msg.ReasoningContent);
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

    #region �������·��һ���Բ���
    [Fact]
    public void AllChatCompletionsProvidersShareSameInterface()
    {
        var registry = AiClientRegistry.Default;
        var chatCompletionProviders = registry.Descriptors.Values
            .Where(p => p.Protocol == "OpenAI")
            .ToList();

        // ����28�� OpenAI ���� + 3���������� = 31
        Assert.True(chatCompletionProviders.Count >= 28, $"ChatCompletions Э�������Ӧ����28����ʵ�� {chatCompletionProviders.Count}");

        foreach (var descriptor in chatCompletionProviders)
        {
            Assert.NotNull(descriptor);
        }
    }

    [Fact]
    public void ProtocolGroupCountsMatchExpected()
    {
        var registry = AiClientRegistry.Default;
        var groups = registry.Descriptors.Values
            .GroupBy(p => p.Protocol)
            .ToDictionary(g => g.Key, g => g.Count());

        // Anthropic Э��1����Gemini Э��1��
        Assert.Equal(1, groups["AnthropicMessages"]);
        Assert.Equal(1, groups["Gemini"]);
        // ChatCompletions ����28��
        Assert.True(groups["OpenAI"] >= 28);
    }
    #endregion
}
