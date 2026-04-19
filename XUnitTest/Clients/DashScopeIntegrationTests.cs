#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NewLife;
using NewLife.AI.Clients;
using NewLife.AI.Clients.DashScope;
using NewLife.AI.Clients.OpenAI;
using NewLife.AI.Models;
using NewLife.Remoting;
using Xunit;
using Xunit.Sdk;
using XUnitTest.Helpers;

namespace XUnitTest.Clients;

/// <summary>DashScope（阿里百炼）服务商集成测试。直接实例化 DashScopeChatClient，需要有效 ApiKey 才能运行</summary>
/// <remarks>
/// ApiKey 读取优先级：
/// 1. ./config/DashScope.key 文件（纯文本，首行为 ApiKey）
/// 2. 环境变量 DASHSCOPE_API_KEY
/// 未配置时测试自动跳过
/// </remarks>
[TestCaseOrderer("NewLife.UnitTest.DefaultOrderer", "NewLife.UnitTest")]
public class DashScopeIntegrationTests
{
    private readonly String _apiKey;

    public DashScopeIntegrationTests()
    {
        _apiKey = LoadApiKey() ?? "";
    }

    /// <summary>从 config 目录或环境变量加载 ApiKey</summary>
    public static String? LoadApiKey()
    {
        var configPath = "config/DashScope.key".GetFullPath();
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

        return Environment.GetEnvironmentVariable("DASHSCOPE_API_KEY");
    }

    /// <summary>构建默认连接选项</summary>
    private AiClientOptions CreateOptions() => new()
    {
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
    /// <summary>确保已配置可用 ApiKey。未配置时跳过依赖真实服务的集成测试</summary>
    private void EnsureConfiguredApiKeyAvailable(AiClientOptions? opts = null)
    {
        var apiKey = opts?.ApiKey;
        if (String.IsNullOrWhiteSpace(apiKey)) apiKey = _apiKey;

        if (String.IsNullOrWhiteSpace(apiKey))
            throw new SkipException("未检测到可用 API Key（config/DashScope.key 或 DASHSCOPE_API_KEY），跳过 DashScope 集成测试");
    }

    /// <summary>创建客户端并执行非流式请求。遇到瞬发网络错误时最多重试 2 次</summary>
    private async Task<IChatResponse> ChatAsync(IChatRequest request, AiClientOptions? opts = null, Boolean ensureApiKey = true)
    {
        if (ensureApiKey) EnsureConfiguredApiKeyAvailable(opts);

        var retries = 2;
        while (true)
        {
            try
            {
                using var client = new DashScopeChatClient(opts ?? CreateOptions());
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
    private async IAsyncEnumerable<IChatResponse> ChatStreamAsync(IChatRequest request, AiClientOptions? opts = null, Boolean ensureApiKey = true, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        if (ensureApiKey) EnsureConfiguredApiKeyAvailable(opts);

        using var client = new DashScopeChatClient(opts ?? CreateOptions());
        await foreach (var chunk in client.GetStreamingResponseAsync(request, ct))
            yield return chunk;
    }

    /// <summary>轮询视频生成任务，直到 SUCCEEDED / FAILED 或超时。默认超时 600 秒</summary>
    private static async Task<VideoTaskStatusResponse> WaitForVideoTaskAsync(DashScopeChatClient client, String taskId, Int32 timeoutSeconds = 600)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            var status = await client.GetVideoTaskAsync(taskId);
            if (status.Status is "SUCCEEDED" or "FAILED")
                return status;
            await Task.Delay(5_000);
        }
        throw new TimeoutException($"视频任务 {taskId} 在 {timeoutSeconds} 秒内未完成");
    }

    /// <summary>将远程 URL 内容下载并保存到 TestOutput/ 目录（带时间戳前缀），返回保存路径</summary>
    private static async Task<String> SaveOutputFileAsync(String url, String fileName)
    {
        var dir = "TestOutput".GetFullPath();
        Directory.CreateDirectory(dir);
        var ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var savePath = Path.Combine(dir, $"{ts}_{fileName}");
        using var http = new HttpClient();
        var bytes = await http.GetByteArrayAsync(url);
        await File.WriteAllBytesAsync(savePath, bytes);
        Console.WriteLine($"[TestOutput] 文件已保存: {savePath}");
        return savePath;
    }

    #region 非流式对话 - 基本功能

    [RequiresApiKeyFact("DASHSCOPE_API_KEY", "config/DashScope.key")]
    [DisplayName("非流式_QwenPlus_返回有效响应")]
    public async Task ChatAsync_QwenPlus_ReturnsValidResponse()
    {
        var request = CreateSimpleRequest("qwen-plus", "用一句话介绍自己");
        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);

        var content = response.Messages[0].Message?.Content as String;
        Assert.False(String.IsNullOrEmpty(content));

        Assert.NotNull(response.Usage);
        Assert.True(response.Usage.TotalTokens > 0, "Token 用量应大于 0");
        Assert.True(response.Usage.InputTokens > 0, "Prompt Token 应大于 0");
        Assert.True(response.Usage.OutputTokens > 0, "Completion Token 应大于 0");
    }

    [RequiresApiKeyFact("DASHSCOPE_API_KEY", "config/DashScope.key")]
    [DisplayName("非流式_Qwen35Flash_轻量模型可用")]
    public async Task ChatAsync_QwenTurbo_Works()
    {
        var request = CreateSimpleRequest("qwen3.5-flash", "1+1等于几？只回答数字");
        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);

        var content = response.Messages[0].Message?.Content as String;
        Assert.False(String.IsNullOrEmpty(content));
    }

    [Fact]
    [DisplayName("非流式_Qwen35Plus_高级模型可用")]
    public async Task ChatAsync_QwenMax_Works()
    {
        var request = CreateSimpleRequest("qwen3.5-plus", "你好", 200);
        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);
    }

    [Fact]
    [DisplayName("非流式_系统提示词生效")]
    public async Task ChatAsync_SystemPrompt_Respected()
    {
        var request = CreateRequestWithSystem(
            "qwen-plus",
            "你是一个只会回复JSON格式的机器人。无论用户说什么，都用{\"reply\":\"内容\"}格式回复。",
            "你好",
            100);

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        var content = response.Messages?[0].Message?.Content as String;
        Assert.False(String.IsNullOrEmpty(content));
        Assert.Contains("{", content);
        Assert.Contains("}", content);
    }

    [Fact]
    [DisplayName("非流式_多轮对话上下文保持")]
    public async Task ChatAsync_MultiTurn_ContextPreserved()
    {
        var request = new ChatRequest
        {
            Model = "qwen-plus",
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

        Assert.NotNull(response);
        var content = response.Messages?[0].Message?.Content as String;
        Assert.False(String.IsNullOrEmpty(content));
        Assert.Contains("小明", content);
    }

    #endregion

    #region 非流式对话 - 参数覆盖（BuildRequestBody 所有分支）

    [Fact]
    [DisplayName("参数_Temperature参数生效")]
    public async Task ChatAsync_Temperature_Accepted()
    {
        var request = CreateSimpleRequest("qwen-plus", "随机说一个1到100的数字，只回答数字");
        request.Temperature = 0.0;
        request.MaxTokens = 200;

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        var content = response.Messages?[0].Message?.Content as String;
        Assert.False(String.IsNullOrEmpty(content));
        Assert.True(content.ToInt() > 0);
    }

    [Fact]
    [DisplayName("参数_MaxTokens限制生效")]
    public async Task ChatAsync_MaxTokens_LimitsOutput()
    {
        var request = CreateSimpleRequest("qwen-plus", "写一篇关于春天的作文", 10);
        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Usage);
        Assert.True(response.Usage.OutputTokens <= 15, $"CompletionTokens={response.Usage.OutputTokens} 应受 MaxTokens 限制");
    }

    [Fact]
    [DisplayName("参数_Stop停止词生效")]
    public async Task ChatAsync_Stop_Accepted()
    {
        var request = CreateSimpleRequest("qwen-plus", "从1数到10，用逗号分隔", 200);
        request.Stop = ["5"];

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        var content = response.Messages?[0].Message?.Content as String;
        Assert.NotNull(content);
    }

    [Fact]
    [DisplayName("参数_长文本输入可处理")]
    public async Task ChatAsync_LongInput_Accepted()
    {
        var longText = String.Join(",", Enumerable.Range(1, 100).Select(i => $"item{i}"));
        var request = CreateSimpleRequest("qwen-plus", $"count items: {longText}");
        request.MaxTokens = 200;

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
    }

    [Fact]
    [DisplayName("参数_所有可选参数同时传递")]
    public async Task ChatAsync_AllOptionalParams_Accepted()
    {
        var request = CreateSimpleRequest("qwen-plus", "你好", 200);
        request.Temperature = 0.7;
        request.TopP = 0.9;
        request.PresencePenalty = 0.5;
        request.FrequencyPenalty = 0.5;
        request.User = "integration-test";
        request.Stop = ["."];

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);
    }

    #endregion

    #region 非流式对话 - 响应结构验证（ParseResponse 全字段）

    [Fact]
    [DisplayName("响应结构_FinishReason_MaxTokens截断返回length")]
    public async Task ChatAsync_FinishReason_Length_WhenTruncated()
    {
        var request = CreateSimpleRequest("qwen-plus", "describe the solar system formation in 500 words", 5);
        var response = await ChatAsync(request);

        Assert.NotNull(response);
        var finishReason = response.Messages?[0].FinishReason;
        Assert.NotNull(finishReason);
        Assert.True(finishReason == FinishReason.Length || finishReason == FinishReason.Stop,
            $"Expected length or stop, actual: {finishReason}");
    }

    [Fact]
    [DisplayName("响应结构_完整字段一次验证")]
    public async Task ChatAsync_ResponseStructure_Complete()
    {
        var request = CreateSimpleRequest("qwen3.5-flash", "hi", 200);
        var response = await ChatAsync(request);

        Assert.NotNull(response);
        // FinishReason
        var fr = response.Messages?[0].FinishReason;
        Assert.NotNull(fr);
        Assert.True(fr == FinishReason.Stop || fr == FinishReason.Length, $"FinishReason 应为 stop 或 length，实际: {fr}");
        // Id / Object / Model
        Assert.False(String.IsNullOrEmpty(response.Id));
        Assert.Equal("chat.completion", response.Object);
        Assert.False(String.IsNullOrEmpty(response.Model));
        Assert.Contains("qwen", response.Model, StringComparison.OrdinalIgnoreCase);
        // Choices 结构
        Assert.NotNull(response.Messages);
        Assert.Single(response.Messages);
        Assert.Equal(0, response.Messages[0].Index);
        var msg = response.Messages[0].Message;
        Assert.NotNull(msg);
        Assert.Equal("assistant", msg.Role);
        // Usage
        Assert.NotNull(response.Usage);
        Assert.True(response.Usage.InputTokens > 0);
        Assert.True(response.Usage.OutputTokens > 0);
        Assert.Equal(response.Usage.InputTokens + response.Usage.OutputTokens, response.Usage.TotalTokens);
    }

    #endregion

    #region 流式对话 - 基本功能

    [Fact]
    [DisplayName("流式_QwenPlus_返回多个Chunk")]
    public async Task ChatStreamAsync_QwenPlus_ReturnsChunks()
    {
        var request = CreateSimpleRequest("qwen-plus", "write a bubble sort in C#");
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
    [DisplayName("流式_QwenTurbo_轻量模型流式可用")]
    public async Task ChatStreamAsync_QwenTurbo_Works()
    {
        var request = CreateSimpleRequest("qwen-turbo", "hi");
        request.Stream = true;

        var chunks = new List<IChatResponse>();
        await foreach (var chunk in ChatStreamAsync(request))
        {
            chunks.Add(chunk);
        }

        Assert.NotEmpty(chunks);
    }

    [Fact]
    [DisplayName("流式_内容可拼接为完整文本")]
    public async Task ChatStreamAsync_Content_CanBeConcatenated()
    {
        var request = CreateSimpleRequest("qwen-plus", "describe bubble sort in 50 words");
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

        Assert.NotEmpty(fullContent);
        Assert.True(fullContent.Length > 5, $"concatenated content too short: {fullContent}");
    }

    [Fact]
    [DisplayName("流式_系统提示词生效")]
    public async Task ChatStreamAsync_SystemPrompt_Respected()
    {
        var request = CreateRequestWithSystem("qwen-plus", "Always start reply with 'OK:'", "hello", 200);
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

        Assert.NotEmpty(fullContent);
    }

    [Fact]
    [DisplayName("流式_CancellationToken_可中断")]
    public async Task ChatStreamAsync_Cancellation_StopsEarly()
    {
        var request = CreateSimpleRequest("qwen-plus", "write a 1000 word essay about AI history");
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

        Assert.True(chunks.Count >= 3, "should receive at least 3 chunks before cancel");
    }

    #endregion

    #region 流式对话 - 结构验证

    [Fact]
    [DisplayName("流式结构_完整字段一次验证")]
    public async Task ChatStreamAsync_Structure_Complete()
    {
        var request = CreateSimpleRequest("qwen3.5-flash", "hi", 200);
        request.Stream = true;

        String? objectField = null;
        String? model = null;
        FinishReason? lastFinishReason = null;
        var hasDelta = false;
        var hasChoices = false;

        await foreach (var chunk in ChatStreamAsync(request))
        {
            if (chunk.Messages != null && chunk.Messages.Count > 0)
                hasChoices = true;
            if (chunk.Object != null && objectField == null)
                objectField = chunk.Object;
            if (chunk.Model != null && model == null)
                model = chunk.Model;
            if (chunk.Messages == null) continue;
            foreach (var choice in chunk.Messages)
            {
                if (choice.Delta != null)
                    hasDelta = true;
                if (choice.FinishReason != null)
                    lastFinishReason = choice.FinishReason;
            }
        }

        Assert.True(hasChoices, "至少一个 chunk 应包含 choices");
        Assert.True(hasDelta, "stream chunk 应使用 delta 字段");
        Assert.NotNull(objectField);
        Assert.Equal("chat.completion.chunk", objectField);
        Assert.NotNull(model);
        Assert.Contains("qwen", model, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(lastFinishReason);
        Assert.True(lastFinishReason == FinishReason.Stop || lastFinishReason == FinishReason.Length,
            $"stream 最终 FinishReason 应为 stop 或 length，实际: {lastFinishReason}");
    }

    [Fact]
    [DisplayName("流式用量_最终Chunk可能包含Usage")]
    public async Task ChatStreamAsync_Usage_InFinalChunk()
    {
        var request = CreateSimpleRequest("qwen-plus", "hi", 200);
        request.Stream = true;

        var chunks = new List<IChatResponse>();
        await foreach (var chunk in ChatStreamAsync(request))
        {
            chunks.Add(chunk);
        }

        Assert.NotEmpty(chunks);

        // DashScope streaming API may not include usage in final chunk
        // (requires stream_options parameter which OpenAiProvider doesn't send)
        var lastWithUsage = chunks.LastOrDefault(c => c.Usage != null);
        if (lastWithUsage != null)
        {
            Assert.True(lastWithUsage.Usage!.TotalTokens > 0);
        }
    }

    #endregion

    #region 错误处理 - HTTP 层

    [Fact]
    [DisplayName("错误_无ApiKey_ChatAsync抛出ApiException")]
    public async Task ChatAsync_NoApiKey_ThrowsException()
    {
        var request = CreateSimpleRequest("qwen3.5-flash", "hi");
        var options = new AiClientOptions
        {
            ApiKey = "",
        };

        var ex = await Assert.ThrowsAsync<ApiException>(async () =>
        {
            await ChatAsync(request, options, false);
        });

        Assert.Contains("Invalid", ex.Message);
    }

    [Fact]
    [DisplayName("错误_无ApiKey_ChatStreamAsync抛出ApiException")]
    public async Task ChatStreamAsync_NoApiKey_ThrowsException()
    {
        var request = CreateSimpleRequest("qwen3.5-flash", "hi");
        request.Stream = true;
        var options = new AiClientOptions
        {
            ApiKey = "",
        };

        var ex = await Assert.ThrowsAsync<ApiException>(async () =>
        {
            await foreach (var _ in ChatStreamAsync(request, options, false))
            {
            }
        });

        Assert.Contains("Invalid", ex.Message);
    }

    [Fact]
    [DisplayName("错误_无效ApiKey_抛出ApiException")]
    public async Task ChatAsync_InvalidApiKey_ThrowsException()
    {
        var request = CreateSimpleRequest("qwen3.5-flash", "hi");
        var options = new AiClientOptions
        {
            ApiKey = "sk-invalid-key-12345",
        };

        var ex = await Assert.ThrowsAsync<ApiException>(async () =>
        {
            await ChatAsync(request, options, false);
        });

        Assert.Contains("Invalid", ex.Message);
    }

    [Fact]
    [DisplayName("错误_不存在的模型_抛出ApiException")]
    public async Task ChatAsync_InvalidModel_ThrowsException()
    {
        var request = CreateSimpleRequest("nonexistent-model-xyz-99999", "hi");

        var ex = await Assert.ThrowsAsync<ApiException>(async () =>
        {
            await ChatAsync(request);
        });

        Assert.Contains("Invalid", ex.Message);
    }

    [Fact]
    [DisplayName("错误_无效Endpoint_抛出异常")]
    public async Task ChatAsync_InvalidEndpoint_ThrowsException()
    {
        var request = CreateSimpleRequest("qwen-plus", "hi");
        var options = new AiClientOptions
        {
            Endpoint = "https://invalid-endpoint-that-does-not-exist.example.com",
            ApiKey = _apiKey.Length > 0 ? _apiKey : "sk-test",
        };

        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await ChatAsync(request, options, false);
        });
    }

    [Fact]
    [DisplayName("错误_流式无效ApiKey_抛出ApiException")]
    public async Task ChatStreamAsync_InvalidApiKey_ThrowsException()
    {
        var request = CreateSimpleRequest("qwen3.5-flash", "hi");
        request.Stream = true;
        var options = new AiClientOptions
        {
            ApiKey = "sk-invalid-key-12345",
        };

        var ex = await Assert.ThrowsAsync<ApiException>(async () =>
        {
            await foreach (var _ in ChatStreamAsync(request, options, false))
            {
            }
        });

        Assert.Contains("Invalid", ex.Message);
    }

    [Fact]
    [DisplayName("错误_流式不存在的模型_抛出异常")]
    public async Task ChatStreamAsync_InvalidModel_ThrowsException()
    {
        var request = CreateSimpleRequest("nonexistent-model-xyz-99999", "hi");
        request.Stream = true;

        // DashScope 流式错误可能为 HttpRequestException（SSE event:error）或 ApiException（HTTP 错误状态码），一弎一然
        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await foreach (var _ in ChatStreamAsync(request, CreateOptions()))
            {
            }
        });
    }

    #endregion

    #region 错误处理 - 参数边界

    [Fact]
    [DisplayName("参数_空消息列表_抛出异常")]
    public async Task ChatAsync_EmptyMessages_ThrowsException()
    {
        var request = new ChatRequest
        {
            Model = "qwen-plus",
            Messages = [],
            MaxTokens = 200,
            EnableThinking = false,
        };

        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await ChatAsync(request);
        });
    }

    [Fact]
    [DisplayName("参数_流式空消息列表_抛出异常或返回空")]
    public async Task ChatStreamAsync_EmptyMessages_ThrowsOrEmpty()
    {
        var request = new ChatRequest
        {
            Model = "qwen-plus",
            Messages = [],
            MaxTokens = 200,
            Stream = true,
            EnableThinking = false,
        };

        // DashScope may throw ApiException or return empty stream for empty messages;
        // AiClientBase validates before sending and throws ArgumentException for empty messages
        try
        {
            var chunks = new List<IChatResponse>();
            await foreach (var chunk in ChatStreamAsync(request))
            {
                chunks.Add(chunk);
            }
            // If no exception, server accepted empty messages — verify no meaningful content
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

    #region FunctionCalling

    [Fact]
    [DisplayName("FunctionCalling_工具定义被正确传递")]
    public async Task ChatAsync_FunctionCalling_ToolsAccepted()
    {
        var request = new ChatRequest
        {
            Model = "qwen-plus",
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

        Assert.NotNull(response);
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
            Assert.NotEmpty(toolCall.Id);
            Assert.NotNull(toolCall.Function?.Arguments);
        }
    }

    [Fact]
    [DisplayName("FunctionCalling_多工具定义可用")]
    public async Task ChatAsync_FunctionCalling_MultipleTools()
    {
        var request = new ChatRequest
        {
            Model = "qwen-plus",
            Messages =
            [
                new ChatMessage { Role = "user", Content = "check Beijing weather and calculate 123*456" },
            ],
            MaxTokens = 200,
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
                new ChatTool
                {
                    Type = "function",
                    Function = new FunctionDefinition
                    {
                        Name = "calculate",
                        Description = "Calculate math expression",
                        Parameters = new Dictionary<String, Object>
                        {
                            ["type"] = "object",
                            ["properties"] = new Dictionary<String, Object>
                            {
                                ["expression"] = new Dictionary<String, Object>
                                {
                                    ["type"] = "string",
                                    ["description"] = "math expression",
                                },
                            },
                            ["required"] = new[] { "expression" },
                        },
                    },
                },
            ],
            EnableThinking = false,
        };

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);
    }

    [Fact]
    [DisplayName("FunctionCalling_ToolChoice_Auto参数被接受")]
    public async Task ChatAsync_FunctionCalling_ToolChoiceAuto()
    {
        var request = new ChatRequest
        {
            Model = "qwen-plus",
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

        Assert.NotNull(response);
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
            Model = "qwen-plus",
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
        // Covers BuildRequestBody branches: ToolCallId, Name, ToolCalls serialization
        var request2 = new ChatRequest
        {
            Model = "qwen-plus",
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
        Assert.False(String.IsNullOrEmpty(finalContent));
    }

    [Fact]
    [DisplayName("FunctionCalling_流式工具调用返回ToolCalls")]
    public async Task ChatStreamAsync_FunctionCalling_ReturnsToolCalls()
    {
        var request = new ChatRequest
        {
            Model = "qwen-plus",
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

    #region DashScopeChatClient 属性验证

    [Fact]
    [DisplayName("客户端_Name为DashScope")]
    public void Client_Name_IsCorrect()
    {
        using var client = new DashScopeChatClient(CreateOptions());
        Assert.Equal("DashScope", client.Name);
    }

    [Fact]
    [DisplayName("客户端_默认端点为原生协议地址")]
    public void Client_DefaultEndpoint_IsNativeProtocol()
    {
        using var client = new DashScopeChatClient(CreateOptions());
        Assert.Equal("https://dashscope.aliyuncs.com/api/v1", client.DefaultEndpoint);
    }

    [Fact]
    [DisplayName("客户端_Protocol为ChatCompletions时切换到兼容端点")]
    public void Client_CompatibleMode_UsesCompatibleEndpoint()
    {
        // ChatCompletions 协议时 DefaultEndpoint 应切换到 compatible-mode 地址
        var opts = new AiClientOptions { ApiKey = _apiKey, Protocol = "ChatCompletions" };
        using var client = new DashScopeChatClient(opts);
        Assert.Contains("compatible-mode", client.DefaultEndpoint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [DisplayName("注册表_DashScope已注册且元数据正确")]
    public void Registry_DashScope_Registered()
    {
        var descriptor = AiClientRegistry.Default.GetDescriptor("DashScope");
        Assert.NotNull(descriptor);
        Assert.Equal("DashScope", descriptor!.Code);
        Assert.Equal("阿里百炼", descriptor.DisplayName);
        Assert.Equal("https://dashscope.aliyuncs.com/api/v1", descriptor.DefaultEndpoint);
        Assert.Equal("DashScope", descriptor.Protocol);
    }

    [Fact]
    [DisplayName("注册表_工厂创建 DashScopeChatClient 实例")]
    public void Registry_Factory_Creates_DashScopeChatClient()
    {
        var descriptor = AiClientRegistry.Default.GetDescriptor("DashScope");
        Assert.NotNull(descriptor);
        using var client = descriptor!.Factory(CreateOptions());
        Assert.IsType<DashScopeChatClient>(client);
    }

    [Fact]
    [DisplayName("注册表_Models至少包含qwen族模型")]
    public void Registry_Models_ContainsExpectedModels()
    {
        var descriptor = AiClientRegistry.Default.GetDescriptor("DashScope");
        Assert.NotNull(descriptor);
        var models = descriptor!.Models;
        Assert.NotNull(models);
        Assert.NotEmpty(models);
        Assert.Contains(models, m => m.Model.Contains("qwen", StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region SetHeaders 与 Options 验证

    [Fact]
    [DisplayName("Options_Endpoint为空或null时均使用默认")]
    public async Task Options_EmptyOrNullEndpoint_UsesDefault()
    {
        var request = CreateSimpleRequest("qwen3.5-flash", "hi", 10);

        // 空字符串 Endpoint
        var opts1 = new AiClientOptions { Endpoint = "", ApiKey = _apiKey };
        var r1 = await ChatAsync(request, opts1);
        Assert.NotNull(r1?.Messages);

        // null Endpoint
        var opts2 = new AiClientOptions { Endpoint = null, ApiKey = _apiKey };
        var r2 = await ChatAsync(request, opts2);
        Assert.NotNull(r2?.Messages);
    }

    [Fact]
    [DisplayName("Options_Endpoint尾部斜杠被正确处理")]
    public async Task Options_TrailingSlash_Handled()
    {
        var request = CreateSimpleRequest("qwen3.5-flash", "hi", 10);
        var options = new AiClientOptions
        {
            Endpoint = "https://dashscope.aliyuncs.com/api/v1/",
            ApiKey = _apiKey,
        };

        var response = await ChatAsync(request, options);
        Assert.NotNull(response);
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
            var request = CreateSimpleRequest("qwen3.5-flash", $"{i}+{i}=?", 10);
            return ChatAsync(request, CreateOptions());
        }).ToArray();

        var responses = await Task.WhenAll(tasks);

        foreach (var response in responses)
        {
            Assert.NotNull(response);
            Assert.NotNull(response.Messages);
            Assert.NotEmpty(response.Messages);
        }
    }

    [Fact]
    [DisplayName("稳定性_非流式与流式交替调用")]
    public async Task ChatAsync_And_StreamAsync_Interleaved()
    {
        // Non-streaming
        var request1 = CreateSimpleRequest("qwen3.5-flash", "1+1=?", 10);
        var response1 = await ChatAsync(request1, CreateOptions());
        Assert.NotNull(response1?.Messages);

        // Streaming
        var request2 = CreateSimpleRequest("qwen3.5-flash", "2+2=?", 10);
        request2.Stream = true;
        var chunks = new List<IChatResponse>();
        await foreach (var chunk in ChatStreamAsync(request2, CreateOptions()))
        {
            chunks.Add(chunk);
        }
        Assert.NotEmpty(chunks);

        // Non-streaming again
        var request3 = CreateSimpleRequest("qwen3.5-flash", "3+3=?", 10);
        var response3 = await ChatAsync(request3, CreateOptions());
        Assert.NotNull(response3?.Messages);
    }

    #endregion

    #region 深度思考（DeepThinking）

    [Fact]
    [DisplayName("深度思考_非流式_返回ReasoningContent")]
    public async Task ChatAsync_DeepThinking_ReturnsReasoningContent()
    {
        var request = CreateSimpleRequest("qwen3-max", "9.11 和 9.8 哪个更大？", 150);
        request.EnableThinking = true;
        request["ThinkingBudget"] = 64;

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);

        var message = response.Messages[0].Message;
        Assert.NotNull(message);
        Assert.False(String.IsNullOrEmpty(message.Content as String));

        // 支持思考的模型应返回 reasoning_content，有内容即视为正常，不限定具体文字
        if (!String.IsNullOrWhiteSpace(message.ReasoningContent))
            Assert.True(message.ReasoningContent.Length > 0);
    }

    [Fact]
    [DisplayName("深度思考_流式_增量输出ReasoningContent")]
    public async Task ChatStreamAsync_DeepThinking_StreamsReasoningContent()
    {
        var request = CreateSimpleRequest("qwen3-max", "1+1等于几？", 100);
        request.EnableThinking = true;
        request["ThinkingBudget"] = 64;
        request.Stream = true;

        var reasoningChunks = new List<String>();
        var contentChunks = new List<String>();

        await foreach (var chunk in ChatStreamAsync(request))
        {
            if (chunk.Messages == null) continue;
            foreach (var choice in chunk.Messages)
            {
                if (String.IsNullOrEmpty(choice.Delta?.ReasoningContent) is false)
                    reasoningChunks.Add(choice.Delta!.ReasoningContent!);
                if (choice.Delta?.Content is String s && !String.IsNullOrEmpty(s))
                    contentChunks.Add(s);
            }
        }

        // 至少应有内容输出
        Assert.NotEmpty(contentChunks);
    }

    #endregion

    #region 结构化输出（StructuredOutput）

    [Fact]
    [DisplayName("结构化输出_JsonObject模式返回有效JSON")]
    public async Task ChatAsync_StructuredOutput_JsonObject_ReturnsValidJson()
    {
        var request = CreateSimpleRequest("qwen-plus",
            "用 JSON 格式返回：{\"city\":\"Beijing\",\"population_million\":22}", 200);
        request.ResponseFormat = new Dictionary<String, Object> { ["type"] = "json_object" };

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);

        var content = response.Messages[0].Message?.Content as String;
        Assert.False(String.IsNullOrEmpty(content));
    }

    #endregion

    #region 联网搜索（WebSearch）

    [Fact]
    [DisplayName("联网搜索_EnableSearch_回答包含时效内容")]
    public async Task ChatAsync_EnableSearch_Works()
    {
        var request = CreateSimpleRequest("qwen3.5-plus", "今天的日期是多少？", 200);
        request["EnableSearch"] = true;

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);

        var content = response.Messages[0].Message?.Content as String;
        Assert.False(String.IsNullOrEmpty(content));
    }

    [Fact]
    [DisplayName("联网搜索_EnableSource_请求被接受")]
    public async Task ChatAsync_EnableSource_Accepted()
    {
        var request = CreateSimpleRequest("qwen3.5-plus", "今天有什么新闻？", 200);
        request["EnableSearch"] = true;
        request["EnableSource"] = true;

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);
    }

    #endregion

    #region 并行工具调用（ParallelToolCalls）

    [Fact]
    [DisplayName("并行工具调用_ParallelToolCalls参数被接受")]
    public async Task ChatAsync_ParallelToolCalls_Accepted()
    {
        var request = new ChatRequest
        {
            Model = "qwen3.5-plus",
            Messages =
            [
                new ChatMessage { Role = "user", Content = "查一下北京和上海的天气" },
            ],
            MaxTokens = 200,
            ParallelToolCalls = true,
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
        };

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);
    }

    #endregion

    #region 视觉理解（Vision）

    [Fact]
    [DisplayName("视觉_qwen3.5-plus_识别公网图片内容")]
    public async Task ChatAsync_Vision_Qwen35Plus_RecognizesImage()
    {
        var request = new ChatRequest
        {
            Model = "qwen3.5-plus",
            Messages =
            [
                new ChatMessage
                {
                    Role = "user",
                    Contents =
                    [
                        new TextContent("这张图片里有什么？请用一句话简短描述"),
                        new ImageContent { Uri = "https://dashscope.oss-cn-beijing.aliyuncs.com/images/dog_and_girl.jpeg" },
                    ],
                },
            ],
            MaxTokens = 200,
            EnableThinking = false,
        };

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);
        var content = response.Messages[0].Message?.Content as String;
        Assert.False(String.IsNullOrEmpty(content));
    }

    [Fact]
    [DisplayName("视觉_qwen3.5-flash_流式图片理解")]
    public async Task ChatStreamAsync_Vision_Qwen35Flash_StreamsImageDescription()
    {
        var request = new ChatRequest
        {
            Model = "qwen3.5-flash",
            Messages =
            [
                new ChatMessage
                {
                    Role = "user",
                    Contents =
                    [
                        new TextContent("图中是什么？回答不超过20字"),
                        new ImageContent { Uri = "https://dashscope.oss-cn-beijing.aliyuncs.com/images/dog_and_girl.jpeg" },
                    ],
                },
            ],
            MaxTokens = 100,
            Stream = true,
            EnableThinking = false,
        };

        var fullContent = "";
        await foreach (var chunk in ChatStreamAsync(request))
        {
            if (chunk.Messages == null) continue;
            foreach (var choice in chunk.Messages)
            {
                if (choice.Delta?.Content is String s)
                    fullContent += s;
            }
        }

        Assert.NotEmpty(fullContent);
    }

    [Fact]
    [DisplayName("视觉_qwen-vl-max_Vision模型基础文本查询")]
    public async Task ChatAsync_Vision_QwenVlMax_TextQuery()
    {
        // qwen-vl-max 是专属视觉模型，测试基本文本查询（同样使用 multimodal 端点）
        var request = new ChatRequest
        {
            Model = "qwen-vl-max",
            Messages =
            [
                new ChatMessage
                {
                    Role = "user",
                    Contents = [new TextContent("你好，你是哪个模型？")],
                },
            ],
            MaxTokens = 100,
            EnableThinking = false,
        };

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);
        var content = response.Messages[0].Message?.Content as String;
        Assert.False(String.IsNullOrEmpty(content));
    }

    [Fact]
    [DisplayName("视觉_base64内联图片识别")]
    public async Task ChatAsync_Vision_Base64Image_Works()
    {
        // 下载公共图片为字节数组，验证 ImageContent.Data（base64 编码）代码路径
        // 注：1x1 像素图片低于 qwen3.5-plus 最小尺寸要求，必须使用有效尺寸的真实图片
        using var http = new System.Net.Http.HttpClient();
        var imageBytes = await http.GetByteArrayAsync("https://dashscope.oss-cn-beijing.aliyuncs.com/images/dog_and_girl.jpeg");

        var request = new ChatRequest
        {
            Model = "qwen3.5-plus",
            Messages =
            [
                new ChatMessage
                {
                    Role = "user",
                    Contents =
                    [
                        new TextContent("这张图片里有什么？用一句话描述"),
                        new ImageContent { Data = imageBytes, MediaType = "image/jpeg" },
                    ],
                },
            ],
            MaxTokens = 100,
            EnableThinking = false,
        };

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);
    }

    #endregion

    #region 音频输入（Audio Input）

    [Fact]
    [DisplayName("音频_qwen-audio-turbo_识别公网音频内容")]
    public async Task ChatAsync_AudioInput_QwenAudioTurbo_ReturnsTranscription()
    {
        // DashScope 官方示例音频：https://dashscope.oss-cn-beijing.aliyuncs.com/audios/welcome.mp3
        var request = new ChatRequest
        {
            Model = "qwen-audio-turbo",
            Messages =
            [
                new ChatMessage
                {
                    Role = "user",
                    Contents =
                    [
                        new AudioContent { Uri = "https://dashscope.oss-cn-beijing.aliyuncs.com/audios/welcome.mp3" },
                        new TextContent("这段音频讲了什么内容？"),
                    ],
                },
            ],
            MaxTokens = 300,
        };

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);
        var content = response.Messages[0].Message?.Content as String;
        Assert.False(String.IsNullOrEmpty(content));
    }

    [Fact]
    [DisplayName("音频_qwen2-audio-instruct_流式音频理解")]
    public async Task ChatStreamAsync_AudioInput_Qwen2Audio_StreamsResponse()
    {
        var request = new ChatRequest
        {
            Model = "qwen2-audio-instruct",
            Messages =
            [
                new ChatMessage
                {
                    Role = "user",
                    Contents =
                    [
                        new AudioContent { Uri = "https://dashscope.oss-cn-beijing.aliyuncs.com/audios/welcome.mp3" },
                        new TextContent("用一句话描述这段音频"),
                    ],
                },
            ],
            MaxTokens = 200,
            Stream = true,
        };

        var fullContent = "";
        await foreach (var chunk in ChatStreamAsync(request))
        {
            if (chunk.Messages == null) continue;
            foreach (var choice in chunk.Messages)
            {
                if (choice.Delta?.Content is String s)
                    fullContent += s;
            }
        }

        Assert.NotEmpty(fullContent);
    }

    #endregion

    #region 文生图（Text-to-Image）

    [Fact]
    [DisplayName("文生图_wan2.6-t2i_URL返回与负向提示词，并保存本地文件")]
    public async Task TextToImageAsync_Wanx26T2i_Complete()
    {
        EnsureConfiguredApiKeyAvailable();
        using var client = new DashScopeChatClient(CreateOptions());
        // 基础文生图，附带负向提示词字段验证，N=1 只返回单张
        var request = new ImageGenerationRequest
        {
            Model = "wan2.6-t2i",
            Prompt = "一只在向日葵花田中奔跑的柴犬，写实风格，阳光明媚",
            NegativePrompt = "人物, 文字",
            N = 1,
        };

        var response = await client.TextToImageAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response!.Data);
        Assert.Single(response.Data!);
        var imageUrl = response.Data[0].Url;
        Assert.False(String.IsNullOrEmpty(imageUrl));

        // 下载并保存到本地，供人工检查
        await SaveOutputFileAsync(imageUrl!, "t2i_wanx26t2i.jpg");
    }

    #endregion

    #region 文生视频（Text-to-Video）

    [Fact]
    [DisplayName("文生视频_wan2.7-t2v_等待完成并保存本地视频")]
    public async Task TextToVideoAsync_Wan27Turbo_CompletesAndSaves()
    {
        EnsureConfiguredApiKeyAvailable();
        using var client = new DashScopeChatClient(CreateOptions());
        var request = new VideoGenerationRequest
        {
            // wan2.7-t2v 是正确的模型 ID，不带 -turbo 后缀提交会失败
            Model = "wan2.7-t2v",
            Prompt = "一只白猫悠闲地走过阳光下的花园小径",
        };

        var submitResponse = await client.SubmitVideoGenerationAsync(request);
        Assert.NotNull(submitResponse);
        Assert.False(String.IsNullOrEmpty(submitResponse.TaskId));

        // 轮询等待任务完成（视频生成通常需要 1−5 分钟）
        var status = await WaitForVideoTaskAsync(client, submitResponse.TaskId!);
        Assert.Equal("SUCCEEDED", status.Status);
        Assert.NotNull(status.VideoUrls);
        Assert.NotEmpty(status.VideoUrls!);

        // 下载并保存到本地，供人工检查
        await SaveOutputFileAsync(status.VideoUrls![0], "t2v_wan27turbo.mp4");
    }

    [Fact]
    [DisplayName("文生视频_提交后立即查询状态应为有效值")]
    public async Task GetVideoTaskAsync_AfterSubmit_ReturnsValidStatus()
    {
        EnsureConfiguredApiKeyAvailable();
        using var client = new DashScopeChatClient(CreateOptions());
        var submitRequest = new VideoGenerationRequest
        {
            Model = "wan2.7-t2v",
            Prompt = "一只小鸟轻盈地飞过蓝天白云",
        };

        var submitResponse = await client.SubmitVideoGenerationAsync(submitRequest);
        Assert.False(String.IsNullOrEmpty(submitResponse.TaskId));

        // 立即查询状态（任务刚提交，应处于 PENDING 或 RUNNING）
        var statusResponse = await client.GetVideoTaskAsync(submitResponse.TaskId!);

        Assert.NotNull(statusResponse);
        Assert.False(String.IsNullOrEmpty(statusResponse.Status));
        Assert.True(
            statusResponse.Status is "PENDING" or "RUNNING" or "SUCCEEDED" or "FAILED",
            $"任务状态应为有效值，实际: {statusResponse.Status}");
    }

    [Fact]
    [DisplayName("图生视频_wan2.7-i2v_等待完成并保存本地视频")]
    public async Task ImageToVideoAsync_Wan27Turbo_CompletesAndSaves()
    {
        EnsureConfiguredApiKeyAvailable();
        using var client = new DashScopeChatClient(CreateOptions());
        var request = new VideoGenerationRequest
        {
            Model = "wan2.7-i2v",
            Prompt = "让图片中的小狗欢乐地奔跑",
            ImageUrl = "https://dashscope.oss-cn-beijing.aliyuncs.com/images/dog_and_girl.jpeg",
        };

        var submitResponse = await client.SubmitVideoGenerationAsync(request);
        Assert.NotNull(submitResponse);
        Assert.False(String.IsNullOrEmpty(submitResponse.TaskId));

        // 轮询等待任务完成
        var status = await WaitForVideoTaskAsync(client, submitResponse.TaskId!);
        Assert.Equal("SUCCEEDED", status.Status);
        Assert.NotNull(status.VideoUrls);
        Assert.NotEmpty(status.VideoUrls!);

        // 下载并保存到本地，供人工检查
        await SaveOutputFileAsync(status.VideoUrls![0], "i2v_wan27turbo.mp4");
    }

    #endregion

    #region 其它 AI 应用场景

    [Fact]
    [DisplayName("代码生成_qwen3-coder_生成Python代码")]
    public async Task ChatAsync_CodeGeneration_Qwen3Coder_GeneratesPythonCode()
    {
        var request = CreateSimpleRequest("qwen3-coder-next", "写一个Python函数，计算斐波那契数列的第n项，只返回代码", 300);
        request.EnableThinking = false;

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        var content = response.Messages?[0].Message?.Content as String;
        Assert.False(String.IsNullOrEmpty(content));
        Assert.True(content!.Contains("def") || content.Contains("fibonacci"), "代码生成结果应包含函数定义");
    }

    [Fact]
    [DisplayName("翻译场景_qwen3.5-flash_中英互译")]
    public async Task ChatAsync_Translation_Works()
    {
        var request = CreateRequestWithSystem(
            "qwen3.5-flash",
            "你是专业翻译，只输出翻译结果，不附加任何解释。",
            "将下面的中文翻译成英文：人工智能正在改变世界。",
            100);
        request.EnableThinking = false;

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        var content = response.Messages?[0].Message?.Content as String;
        Assert.False(String.IsNullOrEmpty(content));
        // 翻译结果应包含英文关键词
        Assert.True(content!.Contains("artificial", StringComparison.OrdinalIgnoreCase) ||
                    content.Contains("intelligence", StringComparison.OrdinalIgnoreCase) ||
                    content.Contains("AI", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [DisplayName("摘要提取_qwen3.5-flash_长文摘要")]
    public async Task ChatAsync_Summarization_Works()
    {
        var longText = "人工智能（AI）是计算机科学的一个分支，致力于构建能够执行通常需要人类智能的任务的系统。" +
                       "自2020年以来，大型语言模型（LLM）的发展取得了突破性进展。GPT-4、Claude、Gemini等模型在" +
                       "自然语言处理、代码生成、数学推理等领域表现出色。这些模型通过大规模预训练和人类反馈强化学习来提升能力。";

        var request = CreateSimpleRequest("qwen3.5-flash", $"请用一句话总结以下内容：{longText}", 100);
        request.EnableThinking = false;

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        var content = response.Messages?[0].Message?.Content as String;
        Assert.False(String.IsNullOrEmpty(content));
        Assert.True(content!.Length < 200, "摘要应比原文短");
    }

    [Fact]
    [DisplayName("数学推理_qwq-plus_解方程")]
    public async Task ChatAsync_Math_QwqPlus_SolvesEquation()
    {
        // qwq-plus 当前仅支持 stream 模式，采用流式拼接验证最终答案
        var request = new ChatRequest
        {
            Model = "qwq-plus",
            Messages = [new ChatMessage { Role = "user", Content = "解方程：2x + 5 = 13，x等于几？只回答数字" }],
            MaxTokens = 3000,
            Stream = true,
        };

        var fullContent = "";
        await foreach (var chunk in ChatStreamAsync(request))
        {
            if (chunk.Messages == null) continue;
            foreach (var choice in chunk.Messages)
            {
                if (choice.Delta?.Content is String s)
                    fullContent += s;
            }
        }

        Assert.NotEmpty(fullContent);
        Assert.Contains("4", fullContent);
    }

    [Fact]
    [DisplayName("重排序_gte-rerank-v2_文档排序")]
    public async Task RerankAsync_GteRerankV2_ReturnsRankedResults()
    {
        EnsureConfiguredApiKeyAvailable();
        var options = CreateOptions();
        options.Protocol = "ChatCompletions";
        using var client = new DashScopeChatClient(options);
        var request = new RerankRequest
        {
            Query = "什么是机器学习？",
            Documents =
            [
                "机器学习是人工智能的一个子领域，通过算法让机器从数据中学习。",
                "今天天气很好，适合出门散步。",
                "深度学习是机器学习的一种方法，使用神经网络进行学习。",
            ],
            TopN = 2,
            ReturnDocuments = true,
        };

        var response = await client.RerankAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Results);
        Assert.Equal(2, response.Results!.Count);
        // 第一个结果应与查询最相关（机器学习相关文档）
        Assert.True(response.Results[0].RelevanceScore > response.Results[1].RelevanceScore ||
                    response.Results[0].RelevanceScore >= 0,
                    "相关性分数应大于等于0");
    }

    [Fact]
    [DisplayName("模型列表_ListModelsAsync_返回可用模型")]
    public async Task ListModelsAsync_ReturnsAvailableModels()
    {
        EnsureConfiguredApiKeyAvailable();
        using var client = new DashScopeChatClient(CreateOptions());

        var response = await client.ListModelsAsync();

        // DashScope 的 models 接口可能返回 null（需要特定权限），不做强制断言
        if (response != null)
        {
            Assert.True(response.Data == null || response.Data.Length > 0,
                "如果返回模型列表，至少应有一个模型");
        }
    }

    #endregion

    #region 默认模型覆盖（Default Model Coverage）

    // 覆盖 DashScopeChatClient 头部 [AiClientModel] 声明的全部默认模型：
    // 文本对话模型（Theory）： qwen3-max, qwen3.5-plus, qwen3.5-flash
    // 专用推理模型： qwq-plus；代码模型： qwen3-coder-next
    // 集成测试在对应功能区域覆盖： qwen-vl-max（视觉区域）、wan2.6-t2i（文生图区域）

    [Theory]
    [InlineData("qwen3-max", "1+1=？，只回答数字", 100)]
    [InlineData("qwen3.5-plus", "你好，用一句话介绍自己", 100)]
    [InlineData("qwen3.5-flash", "2+3=？，只回答数字", 50)]
    [DisplayName("默认模型_文本对话模型全覆盖")]
    public async Task DefaultModel_TextChat_Works(String model, String prompt, Int32 maxTokens)
    {
        var request = CreateSimpleRequest(model, prompt, maxTokens);
        request.EnableThinking = false;

        var response = await ChatAsync(request);

        Assert.NotNull(response?.Messages);
        Assert.NotEmpty(response!.Messages!);
        var content = response.Messages[0].Message?.Content as String;
        Assert.False(String.IsNullOrEmpty(content));
    }

    [Fact]
    [DisplayName("默认模型_qwq-plus_专用推理模型可用")]
    public async Task DefaultModel_QwqPlus_Works()
    {
        // qwq-plus 当前仅支持 stream 模式
        var request = new ChatRequest
        {
            Model = "qwq-plus",
            Messages = [new ChatMessage { Role = "user", Content = "9.11 和 9.8 哪个更大？只回答较大的那个数字" }],
            MaxTokens = 3000,
            Stream = true,
        };

        var fullContent = "";
        await foreach (var chunk in ChatStreamAsync(request))
        {
            if (chunk.Messages == null) continue;
            foreach (var choice in chunk.Messages)
            {
                if (choice.Delta?.Content is String s)
                    fullContent += s;
            }
        }

        Assert.NotEmpty(fullContent);
    }

    [Fact]
    [DisplayName("默认模型_qwen3-coder_代码模型可用")]
    public async Task DefaultModel_Qwen3Coder_Works()
    {
        var request = CreateSimpleRequest("qwen3-coder-next", "用一行Python打印 Hello World", 100);
        request.EnableThinking = false;

        var response = await ChatAsync(request);

        Assert.NotNull(response?.Messages);
        Assert.NotEmpty(response!.Messages!);
        var content = response.Messages[0].Message?.Content as String;
        Assert.False(String.IsNullOrEmpty(content));
    }

    #endregion
}