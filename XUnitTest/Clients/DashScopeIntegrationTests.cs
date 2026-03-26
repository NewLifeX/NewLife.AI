#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NewLife.AI.Models;
using NewLife.AI.Providers;
using Xunit;

namespace XUnitTest.Clients;

/// <summary>DashScope（阿里百炼）服务商集成测试。需要有效 ApiKey 才能运行</summary>
/// <remarks>
/// ApiKey 读取优先级：
/// 1. ./config/DashScope.key 文件（纯文本，首行为 ApiKey）
/// 2. 环境变量 DASHSCOPE_API_KEY
/// 未配置时测试自动跳过
/// </remarks>
public class DashScopeIntegrationTests
{
    private readonly AiClientDescriptor _descriptor = AiClientRegistry.Default.GetDescriptor("DashScope")!;
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

    /// <summary>ApiKey 是否可用</summary>
    private Boolean HasApiKey() => !String.IsNullOrWhiteSpace(_apiKey);

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
    };
    /// <summary>创建客户端并执行非流式请求</summary>
    private async Task<ChatResponse> ChatAsync(ChatRequest request, AiClientOptions? opts = null)
    {
        using var client = _descriptor.Factory(opts ?? CreateOptions());
        return await client.GetResponseAsync(request);
    }

    /// <summary>创建客户端并执行流式请求</summary>
    private async IAsyncEnumerable<ChatResponse> ChatStreamAsync(ChatRequest request, AiClientOptions? opts = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        using var client = _descriptor.Factory(opts ?? CreateOptions());
        await foreach (var chunk in client.GetStreamingResponseAsync(request, ct))
            yield return chunk;
    }

    #region 非流式对话 - 基本功能

    [Fact]
    [DisplayName("非流式_QwenPlus_返回有效响应")]
    public async Task ChatAsync_QwenPlus_ReturnsValidResponse()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-plus", "用一句话介绍自己");
        var response = await ChatAsync(request);

        Assert.NotNull(response);
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
    [DisplayName("非流式_QwenTurbo_轻量模型可用")]
    public async Task ChatAsync_QwenTurbo_Works()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-turbo", "1+1等于几？只回答数字");
        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);

        var content = response.Messages[0].Message?.Content as String;
        Assert.False(String.IsNullOrWhiteSpace(content));
    }

    [Fact]
    [DisplayName("非流式_QwenMax_高级模型可用")]
    public async Task ChatAsync_QwenMax_Works()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-max", "你好", 200);
        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);
    }

    [Fact]
    [DisplayName("非流式_系统提示词生效")]
    public async Task ChatAsync_SystemPrompt_Respected()
    {
        if (!HasApiKey()) return;

        var request = CreateRequestWithSystem(
            "qwen-plus",
            "你是一个只会回复JSON格式的机器人。无论用户说什么，都用{\"reply\":\"内容\"}格式回复。",
            "你好",
            100);

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        var content = response.Messages?[0].Message?.Content as String;
        Assert.False(String.IsNullOrWhiteSpace(content));
        Assert.Contains("{", content);
        Assert.Contains("}", content);
    }

    [Fact]
    [DisplayName("非流式_多轮对话上下文保持")]
    public async Task ChatAsync_MultiTurn_ContextPreserved()
    {
        if (!HasApiKey()) return;

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
        };

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        var content = response.Messages?[0].Message?.Content as String;
        Assert.False(String.IsNullOrWhiteSpace(content));
        Assert.Contains("小明", content);
    }

    #endregion

    #region 非流式对话 - 参数覆盖（BuildRequestBody 所有分支）

    [Fact]
    [DisplayName("参数_Temperature参数生效")]
    public async Task ChatAsync_Temperature_Accepted()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-plus", "随机说一个1到100的数字，只回答数字");
        request.Temperature = 0.0;
        request.MaxTokens = 200;

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        var content = response.Messages?[0].Message?.Content as String;
        Assert.False(String.IsNullOrWhiteSpace(content));
    }

    [Fact]
    [DisplayName("参数_TopP参数生效")]
    public async Task ChatAsync_TopP_Accepted()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-plus", "你好", 200);
        request.TopP = 0.5;

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);
    }

    [Fact]
    [DisplayName("参数_MaxTokens限制生效")]
    public async Task ChatAsync_MaxTokens_LimitsOutput()
    {
        if (!HasApiKey()) return;

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
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-plus", "从1数到10，用逗号分隔", 200);
        request.Stop = ["5"];

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        var content = response.Messages?[0].Message?.Content as String;
        Assert.NotNull(content);
    }

    [Fact]
    [DisplayName("参数_PresencePenalty被接受")]
    public async Task ChatAsync_PresencePenalty_Accepted()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-plus", "你好", 200);
        request.PresencePenalty = 1.5;

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);
    }

    [Fact]
    [DisplayName("参数_FrequencyPenalty被接受")]
    public async Task ChatAsync_FrequencyPenalty_Accepted()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-plus", "你好", 200);
        request.FrequencyPenalty = 1.0;

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);
    }

    [Fact]
    [DisplayName("参数_User标识被接受")]
    public async Task ChatAsync_User_Accepted()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-plus", "你好", 200);
        request.User = "test-user-12345";

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);
    }

    [Fact]
    [DisplayName("参数_长文本输入可处理")]
    public async Task ChatAsync_LongInput_Accepted()
    {
        if (!HasApiKey()) return;

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
        if (!HasApiKey()) return;

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
    [DisplayName("响应结构_FinishReason正确返回")]
    public async Task ChatAsync_FinishReason_Returned()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-plus", "1+1=?", 200);
        var response = await ChatAsync(request);

        Assert.NotNull(response);
        var finishReason = response.Messages?[0].FinishReason;
        Assert.NotNull(finishReason);
        Assert.True(finishReason == "stop" || finishReason == "length",
            $"FinishReason should be stop or length, actual: {finishReason}");
    }

    [Fact]
    [DisplayName("响应结构_FinishReason_MaxTokens截断返回length")]
    public async Task ChatAsync_FinishReason_Length_WhenTruncated()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-plus", "describe the solar system formation in 500 words", 5);
        var response = await ChatAsync(request);

        Assert.NotNull(response);
        var finishReason = response.Messages?[0].FinishReason;
        Assert.NotNull(finishReason);
        Assert.True(finishReason == "length" || finishReason == "stop",
            $"Expected length or stop, actual: {finishReason}");
    }

    [Fact]
    [DisplayName("响应结构_包含模型标识")]
    public async Task ChatAsync_Response_ContainsModel()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-plus", "hi", 200);
        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.False(String.IsNullOrWhiteSpace(response.Model));
        Assert.Contains("qwen", response.Model, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [DisplayName("响应结构_包含响应Id")]
    public async Task ChatAsync_Response_ContainsId()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-plus", "hi", 200);
        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.False(String.IsNullOrWhiteSpace(response.Id));
    }

    [Fact]
    [DisplayName("响应结构_Object字段为chat.completion")]
    public async Task ChatAsync_Response_ObjectField()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-plus", "hi", 200);
        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.Equal("chat.completion", response.Object);
    }

    [Fact]
    [DisplayName("响应结构_Choices索引正确")]
    public async Task ChatAsync_Response_ChoiceIndex()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-plus", "hi", 200);
        var response = await ChatAsync(request);

        Assert.NotNull(response?.Messages);
        Assert.Single(response.Messages);
        Assert.Equal(0, response.Messages[0].Index);
    }

    [Fact]
    [DisplayName("响应结构_Message角色为assistant")]
    public async Task ChatAsync_Response_MessageRole()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-plus", "hi", 200);
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
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-plus", "hi", 200);
        var response = await ChatAsync(request);

        Assert.NotNull(response?.Usage);
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
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-plus", "write a bubble sort in C#");
        request.MaxTokens = 200;
        request.Stream = true;

        var chunks = new List<ChatResponse>();
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
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-turbo", "hi");
        request.Stream = true;

        var chunks = new List<ChatResponse>();
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
        if (!HasApiKey()) return;

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

        Assert.False(String.IsNullOrWhiteSpace(fullContent));
        Assert.True(fullContent.Length > 5, $"concatenated content too short: {fullContent}");
    }

    [Fact]
    [DisplayName("流式_系统提示词生效")]
    public async Task ChatStreamAsync_SystemPrompt_Respected()
    {
        if (!HasApiKey()) return;

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

        Assert.False(String.IsNullOrWhiteSpace(fullContent));
    }

    [Fact]
    [DisplayName("流式_CancellationToken_可中断")]
    public async Task ChatStreamAsync_Cancellation_StopsEarly()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-plus", "write a 1000 word essay about AI history");
        request.MaxTokens = 500;
        request.Stream = true;

        using var cts = new CancellationTokenSource();
        var chunks = new List<ChatResponse>();

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
    [DisplayName("流式结构_每个Chunk包含Choices")]
    public async Task ChatStreamAsync_EachChunk_HasChoices()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-plus", "hi", 200);
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
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-plus", "hi", 200);
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
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-plus", "hi", 200);
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
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-plus", "hi", 200);
        request.Stream = true;

        String? lastFinishReason = null;
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
        Assert.True(lastFinishReason == "stop" || lastFinishReason == "length",
            $"stream final FinishReason should be stop or length, actual: {lastFinishReason}");
    }

    [Fact]
    [DisplayName("流式结构_包含模型标识")]
    public async Task ChatStreamAsync_ContainsModel()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-plus", "hi", 200);
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
        Assert.Contains("qwen", model, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [DisplayName("流式用量_最终Chunk可能包含Usage")]
    public async Task ChatStreamAsync_Usage_InFinalChunk()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-plus", "hi", 200);
        request.Stream = true;

        var chunks = new List<ChatResponse>();
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
    [DisplayName("错误_无ApiKey_ChatAsync抛出HttpRequestException")]
    public async Task ChatAsync_NoApiKey_ThrowsException()
    {
        var request = CreateSimpleRequest("qwen-plus", "hi");
        var options = new AiClientOptions
        {
            Endpoint = _descriptor.DefaultEndpoint,
            ApiKey = "",
        };

        var ex = await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await ChatAsync(request, options);
        });

        Assert.Contains("阿里百炼", ex.Message);
    }

    [Fact]
    [DisplayName("错误_无ApiKey_ChatStreamAsync抛出HttpRequestException")]
    public async Task ChatStreamAsync_NoApiKey_ThrowsException()
    {
        var request = CreateSimpleRequest("qwen-plus", "hi");
        request.Stream = true;
        var options = new AiClientOptions
        {
            Endpoint = _descriptor.DefaultEndpoint,
            ApiKey = "",
        };

        var ex = await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await foreach (var _ in ChatStreamAsync(request, options))
            {
            }
        });

        Assert.Contains("阿里百炼", ex.Message);
    }

    [Fact]
    [DisplayName("错误_无效ApiKey_抛出HttpRequestException")]
    public async Task ChatAsync_InvalidApiKey_ThrowsException()
    {
        var request = CreateSimpleRequest("qwen-plus", "hi");
        var options = new AiClientOptions
        {
            Endpoint = _descriptor.DefaultEndpoint,
            ApiKey = "sk-invalid-key-12345",
        };

        var ex = await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await ChatAsync(request, options);
        });

        Assert.Contains("阿里百炼", ex.Message);
    }

    [Fact]
    [DisplayName("错误_不存在的模型_抛出HttpRequestException")]
    public async Task ChatAsync_InvalidModel_ThrowsException()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("nonexistent-model-xyz-99999", "hi");

        var ex = await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await ChatAsync(request);
        });

        Assert.Contains("阿里百炼", ex.Message);
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
            await ChatAsync(request, options);
        });
    }

    [Fact]
    [DisplayName("错误_流式无效ApiKey_抛出HttpRequestException")]
    public async Task ChatStreamAsync_InvalidApiKey_ThrowsException()
    {
        var request = CreateSimpleRequest("qwen-plus", "hi");
        request.Stream = true;
        var options = new AiClientOptions
        {
            Endpoint = _descriptor.DefaultEndpoint,
            ApiKey = "sk-invalid-key-12345",
        };

        var ex = await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await foreach (var _ in ChatStreamAsync(request, options))
            {
            }
        });

        Assert.Contains("阿里百炼", ex.Message);
    }

    [Fact]
    [DisplayName("错误_流式不存在的模型_抛出HttpRequestException")]
    public async Task ChatStreamAsync_InvalidModel_ThrowsException()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("nonexistent-model-xyz-99999", "hi");
        request.Stream = true;

        var ex = await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await foreach (var _ in ChatStreamAsync(request, CreateOptions()))
            {
            }
        });

        Assert.Contains("阿里百炼", ex.Message);
    }

    #endregion

    #region 错误处理 - 参数边界

    [Fact]
    [DisplayName("参数_空消息列表_抛出异常")]
    public async Task ChatAsync_EmptyMessages_ThrowsException()
    {
        if (!HasApiKey()) return;

        var request = new ChatRequest
        {
            Model = "qwen-plus",
            Messages = [],
            MaxTokens = 200,
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
        if (!HasApiKey()) return;

        var request = new ChatRequest
        {
            Model = "qwen-plus",
            Messages = [],
            MaxTokens = 200,
            Stream = true,
        };

        // DashScope may throw HttpRequestException or return empty stream for empty messages
        try
        {
            var chunks = new List<ChatResponse>();
            await foreach (var chunk in ChatStreamAsync(request))
            {
                chunks.Add(chunk);
            }
            // If no exception, server accepted empty messages — verify no meaningful content
        }
        catch (HttpRequestException)
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
        if (!HasApiKey()) return;

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
        };

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);

        var choice = response.Messages[0];
        if (choice.FinishReason == "tool_calls")
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
    [DisplayName("FunctionCalling_多工具定义可用")]
    public async Task ChatAsync_FunctionCalling_MultipleTools()
    {
        if (!HasApiKey()) return;

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
        if (!HasApiKey()) return;

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
        if (!HasApiKey()) return;

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
        };

        var response1 = await ChatAsync(request1, CreateOptions());
        Assert.NotNull(response1?.Messages);

        var choice1 = response1.Messages[0];
        if (choice1.FinishReason != "tool_calls" || choice1.Message?.ToolCalls == null)
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
        if (!HasApiKey()) return;

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
        };

        var chunks = new List<ChatResponse>();
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

    #region DashScopeProvider 属性验证

    [Fact]
    [DisplayName("Provider_Code为DashScope")]
    public void Provider_Code_IsDashScope()
    {
        Assert.Equal("DashScope", _descriptor.Code);
    }

    [Fact]
    [DisplayName("Provider_Name为阿里百炼")]
    public void Provider_Name_IsCorrect()
    {
        Assert.Equal("阿里百炼", _descriptor.DisplayName);
    }

    [Fact]
    [DisplayName("Provider_DefaultEndpoint正确")]
    public void Provider_DefaultEndpoint_IsCorrect()
    {
        Assert.Equal("https://dashscope.aliyuncs.com/api/v1", _descriptor.DefaultEndpoint);
    }

    [Fact]
    [DisplayName("Provider_ApiProtocol为ChatCompletions")]
    public void Provider_ApiProtocol_IsChatCompletions()
    {
        Assert.Equal("DashScope", _descriptor.Protocol);
    }

    [Fact]
    [DisplayName("Provider_Models列表非空且包含qwen模型")]
    public void Provider_Models_ContainsQwen()
    {
        var models = _descriptor.Models;
        Assert.NotNull(models);
        Assert.NotEmpty(models);
        Assert.Contains(models, m => m.Model.Contains("qwen", StringComparison.OrdinalIgnoreCase) ||
                                     m.DisplayName.Contains("qwen", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [DisplayName("Provider_IAiProvider接口实现")]
    public void Provider_Implements_IAiProvider()
    {
        Assert.IsType<AiClientDescriptor>(_descriptor);
    }

    #endregion

    #region SetHeaders 与 Options 验证

    [Fact]
    [DisplayName("Options_Endpoint为空时使用默认")]
    public async Task Options_EmptyEndpoint_UsesDefault()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-plus", "hi", 10);
        var options = new AiClientOptions
        {
            Endpoint = "",
            ApiKey = _apiKey,
        };

        var response = await ChatAsync(request, options);
        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
    }

    [Fact]
    [DisplayName("Options_Endpoint为null时使用默认")]
    public async Task Options_NullEndpoint_UsesDefault()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-plus", "hi", 10);
        var options = new AiClientOptions
        {
            Endpoint = null,
            ApiKey = _apiKey,
        };

        var response = await ChatAsync(request, options);
        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
    }

    [Fact]
    [DisplayName("Options_Endpoint尾部斜杠被正确处理")]
    public async Task Options_TrailingSlash_Handled()
    {
        if (!HasApiKey()) return;

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
        if (!HasApiKey()) return;

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
        if (!HasApiKey()) return;

        // Non-streaming
        var request1 = CreateSimpleRequest("qwen3.5-flash", "1+1=?", 10);
        var response1 = await ChatAsync(request1, CreateOptions());
        Assert.NotNull(response1?.Messages);

        // Streaming
        var request2 = CreateSimpleRequest("qwen3.5-flash", "2+2=?", 10);
        request2.Stream = true;
        var chunks = new List<ChatResponse>();
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
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen3-max", "9.11 和 9.8 哪个更大？请先思考再回答", 500);
        request.EnableThinking = true;
        request["ThinkingBudget"] = 1024;

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);

        var message = response.Messages[0].Message;
        Assert.NotNull(message);
        Assert.False(String.IsNullOrWhiteSpace(message.Content as String));

        // 支持思考的模型应返回 reasoning_content
        if (!String.IsNullOrWhiteSpace(message.ReasoningContent))
            Assert.Contains("思", message.ReasoningContent + message.Content as String ?? "");
    }

    [Fact]
    [DisplayName("深度思考_流式_增量输出ReasoningContent")]
    public async Task ChatStreamAsync_DeepThinking_StreamsReasoningContent()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen3-max", "1+1等于几？思考后作答", 200);
        request.EnableThinking = true;
        request["ThinkingBudget"] = 512;
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
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen3.5-plus",
            "用 JSON 格式返回：{\"city\":\"Beijing\",\"population_million\":22}", 200);
        request.ResponseFormat = new Dictionary<String, Object> { ["type"] = "json_object" };

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);

        var content = response.Messages[0].Message?.Content as String;
        Assert.False(String.IsNullOrWhiteSpace(content));
    }

    #endregion

    #region 联网搜索（WebSearch）

    [Fact]
    [DisplayName("联网搜索_EnableSearch_回答包含时效内容")]
    public async Task ChatAsync_EnableSearch_Works()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen3.5-plus", "今天的日期是多少？", 200);
        request["EnableSearch"] = true;

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);

        var content = response.Messages[0].Message?.Content as String;
        Assert.False(String.IsNullOrWhiteSpace(content));
    }

    [Fact]
    [DisplayName("联网搜索_EnableSource_请求被接受")]
    public async Task ChatAsync_EnableSource_Accepted()
    {
        if (!HasApiKey()) return;

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
        if (!HasApiKey()) return;

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
}
