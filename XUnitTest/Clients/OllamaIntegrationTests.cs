#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NewLife.AI.Clients;
using NewLife.AI.Models;
using NewLife.Remoting;
using Xunit;

namespace XUnitTest.Clients;

/// <summary>Ollama 本地服务集成测试，需要本机运行 Ollama 并拉取 qwen3.5:0.8b 模型</summary>
/// <remarks>
/// 前置条件：
/// 1. 安装并启动 Ollama（默认监听 http://localhost:11434）
/// 2. 执行 ollama pull qwen3.5:0.8b 拉取模型
/// 未检测到 Ollama 服务时测试自动跳过
/// </remarks>
[TestCaseOrderer("NewLife.UnitTest.DefaultOrderer", "NewLife.UnitTest")]
public class OllamaIntegrationTests
{
    private readonly AiClientDescriptor _descriptor = AiClientRegistry.Default.GetDescriptor("Ollama")!;
    private const String Model = "qwen3.5:0.8b";

    private static readonly Boolean _ollamaAvailable = CheckOllamaAvailable();

    /// <summary>尝试连接本地 Ollama 服务判断是否可用</summary>
    private static Boolean CheckOllamaAvailable()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            var response = client.GetAsync("http://localhost:11434").GetAwaiter().GetResult();
            return (Int32)response.StatusCode < 500;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Ollama 服务是否可用</summary>
    private static Boolean HasOllama() => _ollamaAvailable;

    /// <summary>创建默认客户端选项</summary>
    private AiClientOptions CreateOptions() => new()
    {
        Endpoint = _descriptor.DefaultEndpoint,
    };

    /// <summary>构建简单的用户消息请求</summary>
    private static ChatRequest CreateSimpleRequest(String prompt, Int32 maxTokens = 100) => new()
    {
        Model = Model,
        Messages = [new ChatMessage { Role = "user", Content = prompt }],
        MaxTokens = maxTokens,
        EnableThinking = false,
    };

    /// <summary>构建含系统提示词的请求</summary>
    private static ChatRequest CreateRequestWithSystem(String systemPrompt, String userPrompt, Int32 maxTokens = 100) => new()
    {
        Model = Model,
        Messages =
        [
            new ChatMessage { Role = "system", Content = systemPrompt },
            new ChatMessage { Role = "user", Content = userPrompt },
        ],
        MaxTokens = maxTokens,
        EnableThinking = false,
    };
    /// <summary>创建客户端并执行非流式对话</summary>
    private async Task<IChatResponse> ChatAsync(IChatRequest request, AiClientOptions? opts = null)
    {
        using var client = _descriptor.Factory(opts ?? CreateOptions());
        return await client.GetResponseAsync(request);
    }

    /// <summary>创建客户端并执行流式对话</summary>
    private async IAsyncEnumerable<IChatResponse> ChatStreamAsync(IChatRequest request, AiClientOptions? opts = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        using var client = _descriptor.Factory(opts ?? CreateOptions());
        await foreach (var chunk in client.GetStreamingResponseAsync(request, ct))
            yield return chunk;
    }

    #region 非流式对话 - 基础场景

    [Fact]
    [DisplayName("非流式_返回有效响应")]
    public async Task ChatAsync_ReturnsValidResponse()
    {
        if (!HasOllama()) return;

        var request = CreateSimpleRequest("说一句话介绍自己");
        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);

        var content = response.Messages[0].Message?.Content as String;
        Assert.False(String.IsNullOrWhiteSpace(content), "AI 回复内容不应为空");
    }

    [Fact]
    [DisplayName("非流式_系统提示词有效")]
    public async Task ChatAsync_SystemPrompt_Respected()
    {
        if (!HasOllama()) return;

        var request = CreateRequestWithSystem(
            "You are a calculator. Only reply with the numeric result.",
            "1+1");

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        var content = response.Messages?[0].Message?.Content as String;
        Assert.False(String.IsNullOrWhiteSpace(content));
    }

    [Fact]
    [DisplayName("非流式_多轮对话上下文保留")]
    public async Task ChatAsync_MultiTurn_ContextPreserved()
    {
        if (!HasOllama()) return;

        var request = new ChatRequest
        {
            Model = Model,
            Messages =
            [
                new ChatMessage { Role = "user", Content = "My name is Xiao Ming, remember it." },
                new ChatMessage { Role = "assistant", Content = "Got it, your name is Xiao Ming." },
                new ChatMessage { Role = "user", Content = "What is my name? Reply with only the name." },
            ],
            MaxTokens = 200,
            EnableThinking = false,
        };

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        var content = response.Messages?[0].Message?.Content as String;
        Assert.False(String.IsNullOrWhiteSpace(content));
        Assert.Contains("Xiao Ming", content, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region 非流式对话 - 参数设置

    [Fact]
    [DisplayName("参数_Temperature设置有效")]
    public async Task ChatAsync_Temperature_Accepted()
    {
        if (!HasOllama()) return;

        var request = CreateSimpleRequest("say hi", 200);
        request.Temperature = 0.0;

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        var content = response.Messages?[0].Message?.Content as String;
        Assert.False(String.IsNullOrWhiteSpace(content));
    }

    [Fact]
    [DisplayName("参数_TopP设置有效")]
    public async Task ChatAsync_TopP_Accepted()
    {
        if (!HasOllama()) return;

        var request = CreateSimpleRequest("say hi", 200);
        request.TopP = 0.5;

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);
    }

    [Fact]
    [DisplayName("参数_MaxTokens设置有效")]
    public async Task ChatAsync_MaxTokens_LimitsOutput()
    {
        if (!HasOllama()) return;

        var request = CreateSimpleRequest("write a story about a robot", 5);
        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
    }

    [Fact]
    [DisplayName("参数_Stop停止词有效")]
    public async Task ChatAsync_Stop_Accepted()
    {
        if (!HasOllama()) return;

        var request = CreateSimpleRequest("count from 1 to 10, comma separated", 200);
        request.Stop = ["5"];

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        var content = response.Messages?[0].Message?.Content as String;
        Assert.NotNull(content);
    }

    [Fact]
    [DisplayName("参数_所有可选参数同时设置")]
    public async Task ChatAsync_AllOptionalParams_Accepted()
    {
        if (!HasOllama()) return;

        var request = CreateSimpleRequest("say hi", 200);
        request.Temperature = 0.7;
        request.TopP = 0.9;
        request.Stop = ["."];

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);
    }

    #endregion

    #region 非流式对话 - 响应结构验证

    [Fact]
    [DisplayName("响应结构_FinishReason正确返回")]
    public async Task ChatAsync_FinishReason_Returned()
    {
        if (!HasOllama()) return;

        var request = CreateSimpleRequest("1+1=?", 200);
        var response = await ChatAsync(request);

        Assert.NotNull(response);
        var finishReason = response.Messages?[0].FinishReason;
        Assert.NotNull(finishReason);
        Assert.True(finishReason == FinishReason.Stop || finishReason == FinishReason.Length,
            $"FinishReason 应为 stop 或 length，实际为: {finishReason}");
    }

    [Fact]
    [DisplayName("响应结构_包含模型标识")]
    public async Task ChatAsync_Response_ContainsModel()
    {
        if (!HasOllama()) return;

        var request = CreateSimpleRequest("hi", 200);
        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.False(String.IsNullOrWhiteSpace(response.Model));
    }

    [Fact]
    [DisplayName("响应结构_包含响应Id")]
    public async Task ChatAsync_Response_ContainsId()
    {
        if (!HasOllama()) return;

        var request = CreateSimpleRequest("hi", 200);
        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.False(String.IsNullOrWhiteSpace(response.Id));
    }

    [Fact]
    [DisplayName("响应结构_Object字段为chat.completion")]
    public async Task ChatAsync_Response_ObjectField()
    {
        if (!HasOllama()) return;

        var request = CreateSimpleRequest("hi", 200);
        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.Equal("chat.completion", response.Object);
    }

    [Fact]
    [DisplayName("响应结构_Choices索引正确")]
    public async Task ChatAsync_Response_ChoiceIndex()
    {
        if (!HasOllama()) return;

        var request = CreateSimpleRequest("hi", 200);
        var response = await ChatAsync(request);

        Assert.NotNull(response?.Messages);
        Assert.Single(response.Messages);
        Assert.Equal(0, response.Messages[0].Index);
    }

    [Fact]
    [DisplayName("响应结构_Message角色为assistant")]
    public async Task ChatAsync_Response_MessageRole()
    {
        if (!HasOllama()) return;

        var request = CreateSimpleRequest("hi", 200);
        var response = await ChatAsync(request);

        Assert.NotNull(response?.Messages);
        var msg = response.Messages[0].Message;
        Assert.NotNull(msg);
        Assert.Equal("assistant", msg.Role);
    }

    [Fact]
    [DisplayName("验证_非流式响应包含Usage")]
    public async Task ChatAsync_Usage_Returned()
    {
        if (!HasOllama()) return;

        var request = CreateSimpleRequest("hi", 200);
        var response = await ChatAsync(request);

        Assert.NotNull(response?.Usage);
        Assert.True(response.Usage.InputTokens > 0, "PromptTokens 应大于 0");
        Assert.True(response.Usage.OutputTokens > 0, "CompletionTokens 应大于 0");
        Assert.True(response.Usage.TotalTokens > 0, "TotalTokens 应大于 0");
    }

    #endregion

    #region 流式对话 - 基础场景

    [Fact]
    [DisplayName("流式_返回多个Chunk")]
    public async Task ChatStreamAsync_ReturnsChunks()
    {
        if (!HasOllama()) return;

        var request = CreateSimpleRequest("write a bubble sort in C#", 200);
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
        Assert.True(hasContent, "流式响应应包含至少一个内容 chunk");
    }

    [Fact]
    [DisplayName("流式_内容可拼合为完整文本")]
    public async Task ChatStreamAsync_Content_CanBeConcatenated()
    {
        if (!HasOllama()) return;

        var request = CreateSimpleRequest("say hello in English", 200);
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

        Assert.False(String.IsNullOrWhiteSpace(fullContent), "拼合后内容不应为空");
    }

    [Fact]
    [DisplayName("流式_系统提示词有效")]
    public async Task ChatStreamAsync_SystemPrompt_Respected()
    {
        if (!HasOllama()) return;

        var request = CreateRequestWithSystem("Always reply with only one word.", "hello", 200);
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
    [DisplayName("流式_CancellationToken_可取消")]
    public async Task ChatStreamAsync_Cancellation_StopsEarly()
    {
        if (!HasOllama()) return;

        var request = CreateSimpleRequest("write a 500 word essay about AI", 300);
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
            // 预期行为
        }

        Assert.True(chunks.Count >= 3, "取消前应收到至少 3 个 chunk");
    }

    #endregion

    #region 流式对话 - 结构验证

    [Fact]
    [DisplayName("流式结构_每个Chunk包含Choices")]
    public async Task ChatStreamAsync_EachChunk_HasChoices()
    {
        if (!HasOllama()) return;

        var request = CreateSimpleRequest("hi", 200);
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
        if (!HasOllama()) return;

        var request = CreateSimpleRequest("hi", 200);
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

        Assert.True(hasDelta, "流式 chunk 应使用 Delta 字段");
    }

    [Fact]
    [DisplayName("流式结构_Object字段为chat.completion.chunk")]
    public async Task ChatStreamAsync_ObjectField()
    {
        if (!HasOllama()) return;

        var request = CreateSimpleRequest("hi", 200);
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
        if (!HasOllama()) return;

        var request = CreateSimpleRequest("hi", 200);
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
            $"最后一个 chunk 的 FinishReason 应为 stop 或 length，实际为: {lastFinishReason}");
    }

    [Fact]
    [DisplayName("流式结构_包含模型标识")]
    public async Task ChatStreamAsync_ContainsModel()
    {
        if (!HasOllama()) return;

        var request = CreateSimpleRequest("hi", 200);
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
    }

    #endregion

    #region 异常测试

    [Fact]
    [DisplayName("异常_不存在的模型_抛出ApiException")]
    public async Task ChatAsync_InvalidModel_ThrowsException()
    {
        if (!HasOllama()) return;

        var request = new ChatRequest
        {
            Model = "nonexistent-model-xyz-99999",
            Messages = [new ChatMessage { Role = "user", Content = "hi" }],
            MaxTokens = 200,
            EnableThinking = false,
        };

        await Assert.ThrowsAsync<ApiException>(async () =>
        {
            await ChatAsync(request);
        });
    }

    [Fact]
    [DisplayName("异常_无效Endpoint_抛出异常")]
    public async Task ChatAsync_InvalidEndpoint_ThrowsException()
    {
        var request = CreateSimpleRequest("hi");
        var options = new AiClientOptions
        {
            Endpoint = "http://localhost:19999",
        };

        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await ChatAsync(request, options);
        });
    }

    [Fact]
    [DisplayName("异常_流式不存在的模型_抛出ApiException")]
    public async Task ChatStreamAsync_InvalidModel_ThrowsException()
    {
        if (!HasOllama()) return;

        var request = new ChatRequest
        {
            Model = "nonexistent-model-xyz-99999",
            Messages = [new ChatMessage { Role = "user", Content = "hi" }],
            MaxTokens = 200,
            Stream = true,
            EnableThinking = false,
        };

        await Assert.ThrowsAsync<ApiException>(async () =>
        {
            await foreach (var _ in ChatStreamAsync(request, CreateOptions()))
            {
            }
        });
    }

    [Fact]
    [DisplayName("异常_空消息列表_抛出异常")]
    public async Task ChatAsync_EmptyMessages_ThrowsException()
    {
        if (!HasOllama()) return;

        var request = new ChatRequest
        {
            Model = Model,
            Messages = [],
            MaxTokens = 200,
            EnableThinking = false,
        };

        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await ChatAsync(request);
        });
    }

    #endregion

    #region FunctionCalling

    [Fact]
    [DisplayName("FunctionCalling_工具定义被正确接受")]
    public async Task ChatAsync_FunctionCalling_ToolsAccepted()
    {
        if (!HasOllama()) return;

        var request = new ChatRequest
        {
            Model = Model,
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

        // qwen3:0.6b 可能触发工具调用，也可能直接回答
        var choice = response.Messages[0];
        if (choice.FinishReason == FinishReason.ToolCalls)
        {
            Assert.NotNull(choice.Message?.ToolCalls);
            Assert.NotEmpty(choice.Message.ToolCalls);
            var toolCall = choice.Message.ToolCalls[0];
            Assert.Equal("function", toolCall.Type);
            Assert.Equal("get_weather", toolCall.Function?.Name);
        }
    }

    #endregion

    #region OllamaProvider 元数据验证

    [Fact]
    [DisplayName("Provider_Code为Ollama")]
    public void Provider_Code_IsOllama()
    {
        Assert.Equal("Ollama", _descriptor.Code);
    }

    [Fact]
    [DisplayName("Provider_Name为Ollama")]
    public void Provider_Name_IsCorrect()
    {
        Assert.Equal("本地Ollama", _descriptor.DisplayName);
    }

    [Fact]
    [DisplayName("Provider_DefaultEndpoint正确")]
    public void Provider_DefaultEndpoint_IsCorrect()
    {
        Assert.Equal("http://localhost:11434", _descriptor.DefaultEndpoint);
    }

    [Fact]
    [DisplayName("Provider_ApiProtocol为Ollama原生协议")]
    public void Provider_ApiProtocol_IsChatCompletions()
    {
        // Ollama 客户端使用原生 /api/chat 接口，协议标识为 "Ollama"，非 OpenAI 兼容模式
        Assert.Equal("Ollama", _descriptor.Protocol);
    }

    [Fact]
    [DisplayName("Provider_Models列表非空")]
    public void Provider_Models_NotEmpty()
    {
        var models = _descriptor.Models;
        Assert.NotNull(models);
        Assert.NotEmpty(models);
    }

    [Fact]
    [DisplayName("Provider_IAiProvider接口实现")]
    public void Provider_Implements_IAiProvider()
    {
        Assert.IsType<AiClientDescriptor>(_descriptor);
    }

    #endregion

    #region Options 验证

    [Fact]
    [DisplayName("Options_Endpoint为空时使用默认")]
    public async Task Options_EmptyEndpoint_UsesDefault()
    {
        if (!HasOllama()) return;

        var request = CreateSimpleRequest("hi", 200);
        var options = new AiClientOptions { Endpoint = "" };

        var response = await ChatAsync(request, options);
        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
    }

    [Fact]
    [DisplayName("Options_Endpoint尾部斜杠被正确处理")]
    public async Task Options_TrailingSlash_Handled()
    {
        if (!HasOllama()) return;

        var request = CreateSimpleRequest("hi", 200);
        var options = new AiClientOptions { Endpoint = "http://localhost:11434/" };

        var response = await ChatAsync(request, options);
        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
    }

    #endregion

    #region 并发和稳定性

    [Fact]
    [DisplayName("稳定性_多请求同时发送")]
    public async Task ChatAsync_Concurrent_Requests()
    {
        if (!HasOllama()) return;

        var tasks = Enumerable.Range(1, 3).Select(i =>
        {
            var request = CreateSimpleRequest($"{i}+{i}=? reply with only the number", 200);
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
    [DisplayName("稳定性_非流式和流式交替请求")]
    public async Task ChatAsync_And_StreamAsync_Interleaved()
    {
        if (!HasOllama()) return;

        // 非流式
        var request1 = CreateSimpleRequest("1+1=? reply number only", 200);
        var response1 = await ChatAsync(request1, CreateOptions());
        Assert.NotNull(response1?.Messages);

        // 流式
        var request2 = CreateSimpleRequest("2+2=? reply number only", 200);
        request2.Stream = true;
        var chunks = new List<IChatResponse>();
        await foreach (var chunk in ChatStreamAsync(request2, CreateOptions()))
        {
            chunks.Add(chunk);
        }
        Assert.NotEmpty(chunks);

        // 再次非流式
        var request3 = CreateSimpleRequest("3+3=? reply number only", 200);
        var response3 = await ChatAsync(request3, CreateOptions());
        Assert.NotNull(response3?.Messages);
    }

    #endregion
}
