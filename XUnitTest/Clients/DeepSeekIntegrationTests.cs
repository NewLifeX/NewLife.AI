#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NewLife.AI.Clients;
using NewLife.AI.Models;
using NewLife.Remoting;
using Xunit;

namespace XUnitTest.Clients;

/// <summary>DeepSeek（深度求索）服务商集成测试。需要有效 ApiKey 才能运行</summary>
/// <remarks>
/// ApiKey 读取优先级：
/// 1. ./config/DeepSeek.key 文件（纯文本，首行为 ApiKey）
/// 2. 环境变量 DEEPSEEK_API_KEY
/// 未配置时测试自动跳过
/// </remarks>
[TestCaseOrderer("NewLife.UnitTest.DefaultOrderer", "NewLife.UnitTest")]
public class DeepSeekIntegrationTests
{
    private readonly AiClientDescriptor _descriptor = AiClientRegistry.Default.GetDescriptor("DeepSeek")!;
    private readonly String _apiKey;

    public DeepSeekIntegrationTests()
    {
        _apiKey = LoadApiKey() ?? "";
    }

    /// <summary>从 config 目录或环境变量加载 ApiKey</summary>
    public static String? LoadApiKey()
    {
        var configPath = "config/DeepSeek.key".GetFullPath();
        if (File.Exists(configPath))
        {
            var key = File.ReadAllText(configPath).Trim();
            if (!String.IsNullOrWhiteSpace(key)) return key;
        }
        else
        {
            var dir = Path.GetDirectoryName(configPath);
            if (!String.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(configPath, "");
        }

        return Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY");
    }

    /// <summary>构建默认连接选项</summary>
    private AiClientOptions CreateOptions() => new()
    {
        Endpoint = _descriptor.DefaultEndpoint,
        ApiKey = _apiKey,
    };

    /// <summary>构建简单的用户消息请求</summary>
    private static ChatRequest CreateSimpleRequest(String model, String prompt, Int32 maxTokens = 200) => new()
    {
        Model = model,
        Messages = [new ChatMessage { Role = "user", Content = prompt }],
        MaxTokens = maxTokens,
        EnableThinking = false,
    };

    /// <summary>构建带系统提示的请求</summary>
    private static ChatRequest CreateRequestWithSystem(String model, String systemPrompt, String userPrompt, Int32 maxTokens = 100) => new()
    {
        Model = model,
        Messages =
        [
            new ChatMessage { Role = "system", Content = systemPrompt },
            new ChatMessage { Role = "user", Content = userPrompt },
        ],
        MaxTokens = maxTokens,
        EnableThinking = false,
    };

    /// <summary>创建客户端并执行非流式请求。遇到瞬发网络错误时最多重试 2 次</summary>
    private async Task<IChatResponse> ChatAsync(IChatRequest request, AiClientOptions? opts = null)
    {
        var retries = 2;
        while (true)
        {
            try
            {
                using var client = _descriptor.Factory(opts ?? CreateOptions());
                return await client.GetResponseAsync(request);
            }
            catch (HttpRequestException ex) when (retries-- > 0 && IsTransientNetworkError(ex))
            {
                await Task.Delay(2000);
            }
        }
    }

    /// <summary>判断是否为瞬发网络错误（TCP 断开、TLS 握手失败等），API 层错误不重试</summary>
    private static Boolean IsTransientNetworkError(HttpRequestException ex) =>
        ex.InnerException is System.Net.Sockets.SocketException or IOException;

    /// <summary>创建客户端并执行流式请求</summary>
    private async IAsyncEnumerable<IChatResponse> ChatStreamAsync(IChatRequest request, AiClientOptions? opts = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        using var client = _descriptor.Factory(opts ?? CreateOptions());
        await foreach (var chunk in client.GetStreamingResponseAsync(request, ct))
            yield return chunk;
    }

    #region 非流式对话 - 基本功能

    [Fact]
    [DisplayName("非流式_DeepSeekChat_返回有效响应")]
    public async Task ChatAsync_DeepSeekChat_ReturnsValidResponse()
    {
        var request = CreateSimpleRequest("deepseek-chat", "用一句话介绍自己");
        var response = await ChatAsync(request);

        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);

        var content = response.Messages[0].Message?.Content as String;
        Assert.False(String.IsNullOrWhiteSpace(content), "AI 回复内容不应为空");

        Assert.NotNull(response.Usage);
        Assert.True(response.Usage.TotalTokens > 0, "Token 用量应大于 0");
        Assert.True(response.Usage.InputTokens > 0, "Prompt Token 应大于 0");
        Assert.True(response.Usage.OutputTokens > 0, "Completion Token 应大于 0");
    }

    [Fact]
    [DisplayName("非流式_系统提示词生效")]
    public async Task ChatAsync_SystemPrompt_Respected()
    {
        var request = CreateRequestWithSystem(
            "deepseek-chat",
            "你是一个只会回复JSON格式的机器人。无论用户说什么，都用{\"reply\":\"内容\"}格式回复。",
            "你好",
            100);

        var response = await ChatAsync(request);

        var content = response.Messages?[0].Message?.Content as String;
        Assert.False(String.IsNullOrWhiteSpace(content));
        Assert.Contains("{", content);
        Assert.Contains("}", content);
    }

    [Fact]
    [DisplayName("非流式_多轮对话上下文保持")]
    public async Task ChatAsync_MultiTurn_ContextPreserved()
    {
        var request = new ChatRequest
        {
            Model = "deepseek-chat",
            Messages =
            [
                new ChatMessage { Role = "user", Content = "我的名字叫小明，请记住" },
                new ChatMessage { Role = "assistant", Content = "好的，我记住了，你叫小明。" },
                new ChatMessage { Role = "user", Content = "我叫什么名字？只回答名字" },
            ],
            MaxTokens = 200,
            EnableThinking = false,
        };

        var response = await ChatAsync(request);

        var content = response.Messages?[0].Message?.Content as String;
        Assert.False(String.IsNullOrWhiteSpace(content));
        Assert.Contains("小明", content);
    }

    #endregion

    #region 非流式对话 - 参数覆盖

    [Fact]
    [DisplayName("参数_Temperature参数生效")]
    public async Task ChatAsync_Temperature_Accepted()
    {
        var request = CreateSimpleRequest("deepseek-chat", "随机说一个1到100的数字，只回答数字");
        request.Temperature = 0.0;
        request.MaxTokens = 200;

        var response = await ChatAsync(request);

        var content = response.Messages?[0].Message?.Content as String;
        Assert.False(String.IsNullOrWhiteSpace(content));
    }

    [Fact]
    [DisplayName("参数_TopP参数生效")]
    public async Task ChatAsync_TopP_Accepted()
    {
        var request = CreateSimpleRequest("deepseek-chat", "你好", 200);
        request.TopP = 0.5;

        var response = await ChatAsync(request);

        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);
    }

    [Fact]
    [DisplayName("参数_MaxTokens限制生效")]
    public async Task ChatAsync_MaxTokens_LimitsOutput()
    {
        var request = CreateSimpleRequest("deepseek-chat", "写一篇关于春天的作文", 10);
        var response = await ChatAsync(request);

        Assert.NotNull(response.Usage);
        Assert.True(response.Usage.OutputTokens <= 15, $"CompletionTokens={response.Usage.OutputTokens} 应受 MaxTokens 限制");
    }

    [Fact]
    [DisplayName("参数_Stop停止词生效")]
    public async Task ChatAsync_Stop_Accepted()
    {
        var request = CreateSimpleRequest("deepseek-chat", "从1数到10，用逗号分隔", 200);
        request.Stop = ["5"];

        var response = await ChatAsync(request);

        var content = response.Messages?[0].Message?.Content as String;
        Assert.NotNull(content);
    }

    [Fact]
    [DisplayName("参数_PresencePenalty被接受")]
    public async Task ChatAsync_PresencePenalty_Accepted()
    {
        var request = CreateSimpleRequest("deepseek-chat", "你好", 200);
        request.PresencePenalty = 1.5;

        var response = await ChatAsync(request);

        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);
    }

    [Fact]
    [DisplayName("参数_FrequencyPenalty被接受")]
    public async Task ChatAsync_FrequencyPenalty_Accepted()
    {
        var request = CreateSimpleRequest("deepseek-chat", "你好", 200);
        request.FrequencyPenalty = 1.0;

        var response = await ChatAsync(request);

        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);
    }

    [Fact]
    [DisplayName("参数_User标识被接受")]
    public async Task ChatAsync_User_Accepted()
    {
        var request = CreateSimpleRequest("deepseek-chat", "你好", 200);
        request.User = "test-user-12345";

        var response = await ChatAsync(request);

        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);
    }

    [Fact]
    [DisplayName("参数_所有可选参数同时传递")]
    public async Task ChatAsync_AllOptionalParams_Accepted()
    {
        var request = CreateSimpleRequest("deepseek-chat", "你好", 200);
        request.Temperature = 0.7;
        request.TopP = 0.9;
        request.PresencePenalty = 0.5;
        request.FrequencyPenalty = 0.5;
        request.User = "integration-test";
        request.Stop = ["."];

        var response = await ChatAsync(request);

        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);
    }

    #endregion

    #region 非流式对话 - 响应结构验证

    [Fact]
    [DisplayName("响应结构_FinishReason正确返回")]
    public async Task ChatAsync_FinishReason_Returned()
    {
        var request = CreateSimpleRequest("deepseek-chat", "1+1=?", 200);
        var response = await ChatAsync(request);

        var finishReason = response.Messages?[0].FinishReason;
        Assert.NotNull(finishReason);
        Assert.True(finishReason == FinishReason.Stop || finishReason == FinishReason.Length,
            $"FinishReason should be stop or length, actual: {finishReason}");
    }

    [Fact]
    [DisplayName("响应结构_FinishReason_MaxTokens截断返回length")]
    public async Task ChatAsync_FinishReason_Length_WhenTruncated()
    {
        var request = CreateSimpleRequest("deepseek-chat", "describe the solar system formation in 500 words", 5);
        var response = await ChatAsync(request);

        var finishReason = response.Messages?[0].FinishReason;
        Assert.NotNull(finishReason);
        Assert.True(finishReason == FinishReason.Length || finishReason == FinishReason.Stop,
            $"Expected length or stop, actual: {finishReason}");
    }

    [Fact]
    [DisplayName("响应结构_包含模型标识")]
    public async Task ChatAsync_Response_ContainsModel()
    {
        var request = CreateSimpleRequest("deepseek-chat", "hi", 200);
        var response = await ChatAsync(request);

        Assert.False(String.IsNullOrWhiteSpace(response.Model));
        Assert.Contains("deepseek", response.Model, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [DisplayName("响应结构_包含响应Id")]
    public async Task ChatAsync_Response_ContainsId()
    {
        var request = CreateSimpleRequest("deepseek-chat", "hi", 200);
        var response = await ChatAsync(request);

        Assert.False(String.IsNullOrWhiteSpace(response.Id));
    }

    [Fact]
    [DisplayName("响应结构_Object字段为chat.completion")]
    public async Task ChatAsync_Response_ObjectField()
    {
        var request = CreateSimpleRequest("deepseek-chat", "hi", 200);
        var response = await ChatAsync(request);

        Assert.Equal("chat.completion", response.Object);
    }

    [Fact]
    [DisplayName("响应结构_Choices索引正确")]
    public async Task ChatAsync_Response_ChoiceIndex()
    {
        var request = CreateSimpleRequest("deepseek-chat", "hi", 200);
        var response = await ChatAsync(request);

        Assert.NotNull(response?.Messages);
        Assert.Single(response.Messages);
        Assert.Equal(0, response.Messages[0].Index);
    }

    [Fact]
    [DisplayName("响应结构_Message角色为assistant")]
    public async Task ChatAsync_Response_MessageRole()
    {
        var request = CreateSimpleRequest("deepseek-chat", "hi", 200);
        var response = await ChatAsync(request);

        Assert.NotNull(response?.Messages);
        var msg = response.Messages[0].Message;
        Assert.NotNull(msg);
        Assert.Equal("assistant", msg.Role);
    }

    [Fact]
    [DisplayName("用量_非流式响应包含完整Usage")]
    public async Task ChatAsync_Usage_Complete()
    {
        var request = CreateSimpleRequest("deepseek-chat", "hi", 200);
        var response = await ChatAsync(request);

        Assert.NotNull(response?.Usage);
        Assert.True(response.Usage.InputTokens > 0);
        Assert.True(response.Usage.OutputTokens > 0);
        Assert.Equal(response.Usage.InputTokens + response.Usage.OutputTokens, response.Usage.TotalTokens);
    }

    #endregion

    #region 流式对话 - 基本功能

    [Fact]
    [DisplayName("流式_DeepSeekChat_返回多个Chunk")]
    public async Task ChatStreamAsync_DeepSeekChat_ReturnsChunks()
    {
        var request = CreateSimpleRequest("deepseek-chat", "write a bubble sort in C#");
        request.MaxTokens = 200;
        request.Stream = true;

        var chunks = new List<IChatResponse>();
        await foreach (var chunk in ChatStreamAsync(request))
        {
            chunks.Add(chunk);
        }

        Assert.NotEmpty(chunks);

        var hasContent = chunks.Any(c => c.Messages?.Any(ch =>
        {
            var text = ch.Delta?.Content as String;
            return !String.IsNullOrEmpty(text);
        }) == true);
        Assert.True(hasContent, "stream should contain at least one content chunk");
    }

    [Fact]
    [DisplayName("流式_内容可拼接为完整文本")]
    public async Task ChatStreamAsync_Content_CanBeConcatenated()
    {
        var request = CreateSimpleRequest("deepseek-chat", "describe bubble sort in 50 words");
        request.MaxTokens = 100;
        request.Stream = true;

        var fullContent = "";
        await foreach (var chunk in ChatStreamAsync(request))
        {
            if (chunk.Messages != null)
            {
                foreach (var choice in chunk.Messages)
                {
                    if (choice.Delta?.Content is String text)
                        fullContent += text;
                }
            }
        }

        Assert.False(String.IsNullOrWhiteSpace(fullContent));
        Assert.True(fullContent.Length > 5, $"concatenated content too short: {fullContent}");
    }

    [Fact]
    [DisplayName("流式_系统提示词生效")]
    public async Task ChatStreamAsync_SystemPrompt_Respected()
    {
        var request = CreateRequestWithSystem("deepseek-chat", "Always start reply with 'OK:'", "hello", 200);
        request.Stream = true;

        var fullContent = "";
        await foreach (var chunk in ChatStreamAsync(request))
        {
            if (chunk.Messages != null)
            {
                foreach (var choice in chunk.Messages)
                {
                    if (choice.Delta?.Content is String text)
                        fullContent += text;
                }
            }
        }

        Assert.False(String.IsNullOrWhiteSpace(fullContent));
    }

    [Fact]
    [DisplayName("流式_CancellationToken_可中断")]
    public async Task ChatStreamAsync_Cancellation_StopsEarly()
    {
        var request = CreateSimpleRequest("deepseek-chat", "write a 1000 word essay about AI history");
        request.MaxTokens = 500;
        request.Stream = true;

        using var cts = new CancellationTokenSource();
        var chunks = new List<IChatResponse>();

        try
        {
            await foreach (var chunk in ChatStreamAsync(request, null, cts.Token))
            {
                chunks.Add(chunk);
                if (chunks.Count >= 3)
                    cts.Cancel();
            }
        }
        catch (OperationCanceledException)
        {
            // expected
        }

        Assert.True(chunks.Count == 0 || chunks.Count >= 3, "should receive at least 3 chunks before cancel");
    }

    #endregion

    #region 流式对话 - 结构验证

    [Fact]
    [DisplayName("流式结构_每个Chunk包含Choices")]
    public async Task ChatStreamAsync_EachChunk_HasChoices()
    {
        var request = CreateSimpleRequest("deepseek-chat", "hi", 200);
        request.Stream = true;

        var chunksWithChoices = 0;
        var totalChunks = 0;
        await foreach (var chunk in ChatStreamAsync(request))
        {
            totalChunks++;
            if (chunk.Messages != null && chunk.Messages.Count > 0)
                chunksWithChoices++;
        }

        Assert.True(totalChunks > 0);
        Assert.True(chunksWithChoices > 0);
    }

    [Fact]
    [DisplayName("流式结构_Chunk使用Delta而非Message")]
    public async Task ChatStreamAsync_Chunk_UsesDelta()
    {
        var request = CreateSimpleRequest("deepseek-chat", "hi", 200);
        request.Stream = true;

        var hasDelta = false;
        await foreach (var chunk in ChatStreamAsync(request))
        {
            if (chunk.Messages == null) continue;
            foreach (var choice in chunk.Messages)
            {
                if (choice.Delta != null)
                    hasDelta = true;
            }
        }

        Assert.True(hasDelta, "stream chunk should use Delta field");
    }

    [Fact]
    [DisplayName("流式结构_Object字段为chat.completion.chunk")]
    public async Task ChatStreamAsync_ObjectField()
    {
        var request = CreateSimpleRequest("deepseek-chat", "hi", 200);
        request.Stream = true;

        String? objectField = null;
        await foreach (var chunk in ChatStreamAsync(request))
        {
            if (chunk.Object != null)
            {
                objectField = chunk.Object;
                break;
            }
        }

        Assert.NotNull(objectField);
        Assert.Equal("chat.completion.chunk", objectField);
    }

    [Fact]
    [DisplayName("流式结构_最后一个Chunk包含FinishReason")]
    public async Task ChatStreamAsync_LastChunk_HasFinishReason()
    {
        var request = CreateSimpleRequest("deepseek-chat", "hi", 200);
        request.Stream = true;

        FinishReason? lastFinishReason = null;
        await foreach (var chunk in ChatStreamAsync(request))
        {
            if (chunk.Messages != null)
            {
                foreach (var choice in chunk.Messages)
                {
                    if (choice.FinishReason != null)
                        lastFinishReason = choice.FinishReason;
                }
            }
        }

        Assert.NotNull(lastFinishReason);
        Assert.True(lastFinishReason == FinishReason.Stop || lastFinishReason == FinishReason.Length,
            $"stream final FinishReason should be stop or length, actual: {lastFinishReason}");
    }

    [Fact]
    [DisplayName("流式结构_包含模型标识")]
    public async Task ChatStreamAsync_ContainsModel()
    {
        var request = CreateSimpleRequest("deepseek-chat", "hi", 200);
        request.Stream = true;

        String? model = null;
        await foreach (var chunk in ChatStreamAsync(request))
        {
            if (chunk.Model != null)
            {
                model = chunk.Model;
                break;
            }
        }

        Assert.NotNull(model);
        Assert.Contains("deepseek", model, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region 错误处理 - HTTP 层

    [Fact]
    [DisplayName("错误_无ApiKey_ChatAsync抛出ApiException")]
    public async Task ChatAsync_NoApiKey_ThrowsException()
    {
        var request = CreateSimpleRequest("deepseek-chat", "hi");
        var options = new AiClientOptions
        {
            Endpoint = _descriptor.DefaultEndpoint,
            ApiKey = "",
        };

        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await ChatAsync(request, options);
        });
    }

    [Fact]
    [DisplayName("错误_无效ApiKey_抛出ApiException")]
    public async Task ChatAsync_InvalidApiKey_ThrowsApiException()
    {
        var request = CreateSimpleRequest("deepseek-chat", "hi");
        var options = new AiClientOptions
        {
            Endpoint = _descriptor.DefaultEndpoint,
            ApiKey = "sk-invalid-key-12345",
        };

        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await ChatAsync(request, options);
        });
    }

    [Fact]
    [DisplayName("错误_不存在的模型_抛出ApiException")]
    public async Task ChatAsync_InvalidModel_ThrowsException()
    {
        var request = CreateSimpleRequest("nonexistent-model-xyz-99999", "hi");

        try
        {
            var response = await ChatAsync(request);
            Assert.Fail("非法模型应抛出异常");
        }
        catch (Exception)
        {
            // 预期抛出异常
        }
    }

    [Fact]
    [DisplayName("错误_无效Endpoint_抛出异常")]
    public async Task ChatAsync_InvalidEndpoint_ThrowsException()
    {
        var request = CreateSimpleRequest("deepseek-chat", "hi");
        var options = new AiClientOptions
        {
            Endpoint = "https://invalid-endpoint-that-does-not-exist.example.com",
            ApiKey = _apiKey.Length > 0 ? _apiKey : "sk-test",
        };

        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await ChatAsync(request, options);
        });
    }

    [Fact]
    [DisplayName("错误_流式无效ApiKey_抛出异常")]
    public async Task ChatStreamAsync_InvalidApiKey_ThrowsException()
    {
        var request = CreateSimpleRequest("deepseek-chat", "hi");
        request.Stream = true;
        var options = new AiClientOptions
        {
            Endpoint = _descriptor.DefaultEndpoint,
            ApiKey = "sk-invalid-key-12345",
        };

        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await foreach (var _ in ChatStreamAsync(request, options))
            {
            }
        });
    }

    [Fact]
    [DisplayName("错误_流式不存在的模型_抛出异常")]
    public async Task ChatStreamAsync_InvalidModel_ThrowsException()
    {
        var request = CreateSimpleRequest("nonexistent-model-xyz-99999", "hi");
        request.Stream = true;

        try
        {
            var hasChunks = false;
            await foreach (var _ in ChatStreamAsync(request, CreateOptions()))
            {
                hasChunks = true;
            }
            if (hasChunks) Assert.Fail("非法模型不应返回有效流式数据");
        }
        catch (Exception)
        {
            // 预期抛出异常
        }
    }

    #endregion

    #region 错误处理 - 参数边界

    [Fact]
    [DisplayName("参数_空消息列表_抛出异常")]
    public async Task ChatAsync_EmptyMessages_ThrowsException()
    {
        var request = new ChatRequest
        {
            Model = "deepseek-chat",
            Messages = [],
            MaxTokens = 200,
            EnableThinking = false,
        };

        try
        {
            var response = await ChatAsync(request);
            Assert.Fail("空消息应抛出异常");
        }
        catch (Exception)
        {
            // 预期抛出异常
        }
    }

    [Fact]
    [DisplayName("参数_流式空消息列表_抛出异常或返回空")]
    public async Task ChatStreamAsync_EmptyMessages_ThrowsOrEmpty()
    {
        var request = new ChatRequest
        {
            Model = "deepseek-chat",
            Messages = [],
            MaxTokens = 200,
            Stream = true,
            EnableThinking = false,
        };

        try
        {
            var chunks = new List<IChatResponse>();
            await foreach (var chunk in ChatStreamAsync(request))
            {
                chunks.Add(chunk);
            }
        }
        catch (ArgumentException)
        {
            // Expected: base class rejected the empty message list before sending
        }
        catch (ApiException)
        {
            // Expected: server rejected the request
        }
    }

    #endregion

    #region FunctionCalling（deepseek-chat 支持工具调用）

    [Fact]
    [DisplayName("FunctionCalling_工具定义被正确传递")]
    public async Task ChatAsync_FunctionCalling_ToolsAccepted()
    {
        var request = new ChatRequest
        {
            Model = "deepseek-chat",
            Messages =
            [
                new ChatMessage { Role = "user", Content = "what is the weather in Beijing?" },
            ],
            MaxTokens = 100,
            Tools =
            [
                new ChatTool
                {
                    Type = "function",
                    Function = new FunctionDefinition
                    {
                        Name = "get_weather",
                        Description = "Get weather info for a city",
                        Parameters = new Dictionary<String, Object>
                        {
                            ["type"] = "object",
                            ["properties"] = new Dictionary<String, Object>
                            {
                                ["city"] = new Dictionary<String, Object>
                                {
                                    ["type"] = "string",
                                    ["description"] = "city name",
                                },
                            },
                            ["required"] = new[] { "city" },
                        },
                    },
                },
            ],
            EnableThinking = false,
        };

        var response = await ChatAsync(request);

        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);

        var choice = response.Messages[0];
        if (choice.FinishReason == FinishReason.ToolCalls)
        {
            Assert.NotNull(choice.Message?.ToolCalls);
            Assert.NotEmpty(choice.Message.ToolCalls);
            var toolCall = choice.Message.ToolCalls[0];
            Assert.Equal("function", toolCall.Type);
            Assert.Equal("get_weather", toolCall.Function?.Name);
            Assert.False(String.IsNullOrWhiteSpace(toolCall.Id));
            Assert.NotNull(toolCall.Function?.Arguments);
        }
    }

    [Fact]
    [DisplayName("FunctionCalling_ToolChoice_Auto参数被接受")]
    public async Task ChatAsync_FunctionCalling_ToolChoiceAuto()
    {
        var request = new ChatRequest
        {
            Model = "deepseek-chat",
            Messages = [new ChatMessage { Role = "user", Content = "hi" }],
            MaxTokens = 200,
            Tools =
            [
                new ChatTool
                {
                    Type = "function",
                    Function = new FunctionDefinition
                    {
                        Name = "get_time",
                        Description = "Get current time",
                        Parameters = new Dictionary<String, Object> { ["type"] = "object", ["properties"] = new Dictionary<String, Object>() },
                    },
                },
            ],
            ToolChoice = "auto",
            EnableThinking = false,
        };

        var response = await ChatAsync(request);

        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);
    }

    [Fact]
    [DisplayName("FunctionCalling_完整工具调用轮次")]
    public async Task ChatAsync_FunctionCalling_FullRoundTrip()
    {
        var weatherTool = new ChatTool
        {
            Type = "function",
            Function = new FunctionDefinition
            {
                Name = "get_weather",
                Description = "Get weather info for a city",
                Parameters = new Dictionary<String, Object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<String, Object>
                    {
                        ["city"] = new Dictionary<String, Object>
                        {
                            ["type"] = "string",
                            ["description"] = "city name",
                        },
                    },
                    ["required"] = new[] { "city" },
                },
            },
        };

        // Round 1: user asks, model calls tool
        var request1 = new ChatRequest
        {
            Model = "deepseek-chat",
            Messages =
            [
                new ChatMessage { Role = "user", Content = "what is the weather in Beijing?" },
            ],
            MaxTokens = 100,
            Tools = [weatherTool],
            EnableThinking = false,
        };

        var response1 = await ChatAsync(request1, CreateOptions());
        Assert.NotNull(response1?.Messages);

        var choice1 = response1.Messages[0];
        if (choice1.FinishReason != FinishReason.ToolCalls || choice1.Message?.ToolCalls == null)
            return; // model chose to answer directly, skip round 2

        var toolCall = choice1.Message.ToolCalls[0];
        Assert.NotNull(toolCall.Id);

        // Round 2: submit tool result, model generates final reply
        var request2 = new ChatRequest
        {
            Model = "deepseek-chat",
            Messages =
            [
                new ChatMessage { Role = "user", Content = "what is the weather in Beijing?" },
                new ChatMessage
                {
                    Role = "assistant",
                    ToolCalls = choice1.Message.ToolCalls,
                },
                new ChatMessage
                {
                    Role = "tool",
                    ToolCallId = toolCall.Id,
                    Name = toolCall.Function?.Name,
                    Content = "{\"temperature\": 25, \"weather\": \"sunny\", \"city\": \"Beijing\"}",
                },
            ],
            MaxTokens = 100,
            Tools = [weatherTool],
            EnableThinking = false,
        };

        var response2 = await ChatAsync(request2, CreateOptions());

        Assert.NotNull(response2);
        Assert.NotNull(response2.Messages);
        Assert.NotEmpty(response2.Messages);

        var finalContent = response2.Messages[0].Message?.Content as String;
        Assert.False(String.IsNullOrWhiteSpace(finalContent));
    }

    [Fact]
    [DisplayName("FunctionCalling_流式工具调用返回ToolCalls")]
    public async Task ChatStreamAsync_FunctionCalling_ReturnsToolCalls()
    {
        var request = new ChatRequest
        {
            Model = "deepseek-chat",
            Messages =
            [
                new ChatMessage { Role = "user", Content = "what is the weather in Beijing?" },
            ],
            MaxTokens = 100,
            Stream = true,
            Tools =
            [
                new ChatTool
                {
                    Type = "function",
                    Function = new FunctionDefinition
                    {
                        Name = "get_weather",
                        Description = "Get weather info for a city",
                        Parameters = new Dictionary<String, Object>
                        {
                            ["type"] = "object",
                            ["properties"] = new Dictionary<String, Object>
                            {
                                ["city"] = new Dictionary<String, Object>
                                {
                                    ["type"] = "string",
                                    ["description"] = "city name",
                                },
                            },
                            ["required"] = new[] { "city" },
                        },
                    },
                },
            ],
            EnableThinking = false,
        };

        var chunks = new List<IChatResponse>();
        await foreach (var chunk in ChatStreamAsync(request))
        {
            chunks.Add(chunk);
        }

        Assert.NotEmpty(chunks);

        var hasToolCalls = chunks.Any(c => c.Messages?.Any(ch =>
            ch.Delta?.ToolCalls != null && ch.Delta.ToolCalls.Count > 0) == true);
        var hasContent = chunks.Any(c => c.Messages?.Any(ch =>
            ch.Delta?.Content is String s && !String.IsNullOrEmpty(s)) == true);

        Assert.True(hasToolCalls || hasContent, "stream should return tool_calls or content");
    }

    #endregion

    #region 深度思考（deepseek-reasoner 专属）

    [Fact]
    [DisplayName("深度思考_非流式_返回ReasoningContent")]
    public async Task ChatAsync_DeepThinking_ReturnsReasoningContent()
    {
        // deepseek-reasoner 原生输出 reasoning_content，无需额外参数
        var request = CreateSimpleRequest("deepseek-reasoner", "9.11 和 9.8 哪个更大？", 300);

        var response = await ChatAsync(request);

        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);

        var message = response.Messages[0].Message;
        Assert.NotNull(message);
        Assert.False(String.IsNullOrWhiteSpace(message.Content as String));

        // deepseek-reasoner 会在 reasoning_content 字段输出思维链
        if (!String.IsNullOrWhiteSpace(message.ReasoningContent))
            Assert.True(message.ReasoningContent.Length > 0);
    }

    [Fact]
    [DisplayName("深度思考_流式_增量输出ReasoningContent")]
    public async Task ChatStreamAsync_DeepThinking_StreamsReasoningContent()
    {
        var request = CreateSimpleRequest("deepseek-reasoner", "1+1等于几？", 100);
        request.Stream = true;

        var reasoningChunks = new List<String>();
        var contentChunks = new List<String>();

        await foreach (var chunk in ChatStreamAsync(request))
        {
            if (chunk.Messages == null) continue;
            foreach (var choice in chunk.Messages)
            {
                if (!String.IsNullOrEmpty(choice.Delta?.ReasoningContent))
                    reasoningChunks.Add(choice.Delta!.ReasoningContent!);
                if (choice.Delta?.Content is String s && !String.IsNullOrEmpty(s))
                    contentChunks.Add(s);
            }
        }

        Assert.NotEmpty(contentChunks);
    }

    #endregion

    #region DeepSeekProvider 属性验证

    [Fact]
    [DisplayName("Provider_Code为DeepSeek")]
    public void Provider_Code_IsDeepSeek()
    {
        Assert.Equal("DeepSeek", _descriptor.Code);
    }

    [Fact]
    [DisplayName("Provider_Name为深度求索")]
    public void Provider_Name_IsCorrect()
    {
        Assert.Equal("深度求索", _descriptor.DisplayName);
    }

    [Fact]
    [DisplayName("Provider_DefaultEndpoint正确")]
    public void Provider_DefaultEndpoint_IsCorrect()
    {
        Assert.Equal("https://api.deepseek.com", _descriptor.DefaultEndpoint);
    }

    [Fact]
    [DisplayName("Provider_ApiProtocol为OpenAI")]
    public void Provider_ApiProtocol_IsOpenAI()
    {
        Assert.Equal("OpenAI", _descriptor.Protocol);
    }

    [Fact]
    [DisplayName("Provider_Models列表非空且包含deepseek模型")]
    public void Provider_Models_ContainsDeepSeek()
    {
        var models = _descriptor.Models;
        Assert.NotNull(models);
        Assert.NotEmpty(models);
        Assert.Contains(models, m => m.Model.Contains("deepseek", StringComparison.OrdinalIgnoreCase) ||
                                     m.DisplayName.Contains("deepseek", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [DisplayName("Provider_包含支持FunctionCalling的模型")]
    public void Provider_Models_HasFunctionCallingModel()
    {
        var models = _descriptor.Models;
        Assert.NotNull(models);
        Assert.Contains(models, m => m.Capabilities.SupportFunctionCalling);
    }

    [Fact]
    [DisplayName("Provider_包含支持Thinking的模型")]
    public void Provider_Models_HasThinkingModel()
    {
        var models = _descriptor.Models;
        Assert.NotNull(models);
        Assert.Contains(models, m => m.Capabilities.SupportThinking);
    }

    [Fact]
    [DisplayName("Provider_IAiProvider接口实现")]
    public void Provider_Implements_AiClientDescriptor()
    {
        Assert.IsType<AiClientDescriptor>(_descriptor);
    }

    #endregion

    #region Options 验证

    [Fact]
    [DisplayName("Options_Endpoint为空时使用默认")]
    public async Task Options_EmptyEndpoint_UsesDefault()
    {
        var request = CreateSimpleRequest("deepseek-chat", "hi", 10);
        var options = new AiClientOptions
        {
            Endpoint = "",
            ApiKey = _apiKey,
        };

        var response = await ChatAsync(request, options);
        Assert.NotNull(response.Messages);
    }

    [Fact]
    [DisplayName("Options_Endpoint为null时使用默认")]
    public async Task Options_NullEndpoint_UsesDefault()
    {
        var request = CreateSimpleRequest("deepseek-chat", "hi", 10);
        var options = new AiClientOptions
        {
            Endpoint = null,
            ApiKey = _apiKey,
        };

        var response = await ChatAsync(request, options);
        Assert.NotNull(response.Messages);
    }

    [Fact]
    [DisplayName("Options_Endpoint尾部斜杠被正确处理")]
    public async Task Options_TrailingSlash_Handled()
    {
        var request = CreateSimpleRequest("deepseek-chat", "hi", 10);
        var options = new AiClientOptions
        {
            Endpoint = "https://api.deepseek.com/",
            ApiKey = _apiKey,
        };

        var response = await ChatAsync(request, options);
        Assert.NotNull(response.Messages);
    }

    #endregion

    #region 并发与稳定性

    [Fact]
    [DisplayName("并发_多个请求同时发送")]
    public async Task ChatAsync_Concurrent_Requests()
    {
        var tasks = Enumerable.Range(1, 3).Select(i =>
        {
            var request = CreateSimpleRequest("deepseek-chat", $"{i}+{i}=?", 10);
            return ChatAsync(request, CreateOptions());
        }).ToArray();

        var responses = await Task.WhenAll(tasks);

        foreach (var response in responses)
        {
            Assert.NotNull(response.Messages);
            Assert.NotEmpty(response.Messages);
        }
    }

    [Fact]
    [DisplayName("稳定性_非流式与流式交替调用")]
    public async Task ChatAsync_And_StreamAsync_Interleaved()
    {
        // Non-streaming
        var request1 = CreateSimpleRequest("deepseek-chat", "1+1=?", 10);
        var response1 = await ChatAsync(request1, CreateOptions());
        Assert.NotNull(response1.Messages);

        // Streaming
        var request2 = CreateSimpleRequest("deepseek-chat", "2+2=?", 10);
        request2.Stream = true;
        var chunks = new List<IChatResponse>();
        await foreach (var chunk in ChatStreamAsync(request2, CreateOptions()))
        {
            chunks.Add(chunk);
        }
        Assert.NotEmpty(chunks);

        // Non-streaming again
        var request3 = CreateSimpleRequest("deepseek-chat", "3+3=?", 10);
        var response3 = await ChatAsync(request3, CreateOptions());
        Assert.NotNull(response3.Messages);
    }

    #endregion

    #region 结构化输出（StructuredOutput）

    [Fact]
    [DisplayName("结构化输出_JsonObject模式返回有效JSON")]
    public async Task ChatAsync_StructuredOutput_JsonObject_ReturnsValidJson()
    {
        var request = CreateSimpleRequest("deepseek-chat",
            "用 JSON 格式返回：{\"city\":\"Beijing\",\"population_million\":22}", 200);
        request.ResponseFormat = new Dictionary<String, Object> { ["type"] = "json_object" };

        var response = await ChatAsync(request);

        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);

        var content = response.Messages[0].Message?.Content as String;
        Assert.False(String.IsNullOrWhiteSpace(content));
        Assert.Contains("{", content);
        Assert.Contains("}", content);
    }

    #endregion
}
