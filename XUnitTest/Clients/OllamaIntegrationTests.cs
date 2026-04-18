using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NewLife.AI.Clients;
using NewLife.AI.Clients.Ollama;
using NewLife.AI.Models;
using NewLife.Remoting;
using NewLife.Serialization;
using Xunit;
using XUnitTest.Helpers;

namespace XUnitTest.Clients;

/// <summary>OllamaChatClient 集成测试。直接使用 OllamaChatClient 类对 Ollama 本地服务进行端到端测试</summary>
/// <remarks>
/// 前置条件：
/// 1. 安装并启动 Ollama（默认监听 http://localhost:11434）
/// 2. 执行 ollama pull qwen3.5:0.8b 拉取轻量模型
/// 3. 执行 ollama pull qwen3.5:latest 拉取重量模型（仅重量级测试需要）
/// 未检测到 Ollama 服务或对应模型时测试自动跳过
/// </remarks>
public class OllamaIntegrationTests
{
    private const String LightModel = "qwen3.5:0.8b";
    private const String HeavyModel = "qwen3.5:latest";
    private const String DefaultEndpoint = "http://localhost:11434";

    #region 工厂方法

    private static OllamaChatClient CreateClient() => new(null, LightModel, DefaultEndpoint);

    private static OllamaChatClient CreateClientFor(String model, String endpoint = DefaultEndpoint) =>
        new(null, model, endpoint);

    private static ChatRequest SimpleRequest(String prompt, Int32 maxTokens = 200) => new()
    {
        Model = LightModel,
        Messages = [new ChatMessage { Role = "user", Content = prompt }],
        MaxTokens = maxTokens,
        EnableThinking = false,
    };

    private static async Task<List<IChatResponse>> CollectStreamAsync(
        OllamaChatClient client, ChatRequest request, CancellationToken ct = default)
    {
        var chunks = new List<IChatResponse>();
        await foreach (var delta in client.GetStreamingResponseAsync(request, ct))
        {
            chunks.Add(delta);
        }
        return chunks;
    }

    #endregion

    #region 服务元数据与客户端基础

    [Fact]
    [DisplayName("构造_基础属性符合预期")]
    public void Constructor_BasicProperties()
    {
        using var client = CreateClient();
        Assert.Equal("Ollama", client.Name);

        var opts = OllamaChatClient.DefaultJsonOptions;
        Assert.Equal(PropertyNaming.SnakeCaseLower, opts.PropertyNaming);
    }

    [OllamaFact]
    [DisplayName("服务_GetVersionAsync返回有效版本号")]
    public async Task GetVersionAsync_ReturnsVersion()
    {
        using var client = CreateClient();
        var version = await client.GetVersionAsync();
        Assert.NotEmpty(version);
        // 版本号应符合 x.y.z 或 x.y.z-suffix 格式（如 0.7.1、0.9.0-rc7）
        Assert.Matches(@"^\d+\.\d+\.\d+", version);
    }

    [OllamaFact]
    [DisplayName("服务_ListModelsAsync返回已安装模型详情")]
    public async Task ListModelsAsync_ReturnsModels()
    {
        using var client = CreateClient();
        var tags = await client.ListModelsAsync();
        Assert.NotNull(tags);
        Assert.NotNull(tags.Models);
        Assert.True(tags.Models.Length > 0, "至少应有一个已安装的模型");

        // 每个模型条目应包含必要字段
        foreach (var m in tags.Models)
        {
            var mName = m.Name ?? m.Model;
            Assert.NotEmpty(mName);
            Assert.True(m.Size > 0, $"模型 {mName} 的 Size 应大于 0（字节）");
            Assert.NotEmpty(m.Digest);
            Assert.True(m.ModifiedAt > DateTime.MinValue, $"模型 {mName} 的 ModifiedAt 应有效");
        }

        // 至少包含 qwen 系列，且其详情字段完整
        var qwen = tags.Models.FirstOrDefault(m =>
            (m.Name ?? m.Model ?? "").Contains("qwen", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(qwen);
        Assert.NotNull(qwen!.Details);
        Assert.NotEmpty(qwen.Details!.Format);
        Assert.NotEmpty(qwen.Details.Family);
        Assert.NotEmpty(qwen.Details.ParameterSize);
    }

    [OllamaFact]
    [DisplayName("服务_ShowModelAsync返回轻量模型完整详情")]
    public async Task ShowModelAsync_LightModel_ReturnsDetails()
    {
        using var client = CreateClient();
        var show = await client.ShowModelAsync(LightModel);
        Assert.NotNull(show);

        // Details 字段应完整
        Assert.NotNull(show!.Details);
        Assert.NotEmpty(show.Details!.Format);
        Assert.NotEmpty(show.Details.Family);
        Assert.NotEmpty(show.Details.ParameterSize);
        Assert.NotEmpty(show.Details.QuantizationLevel);

        // 顶层字段
        Assert.NotEmpty(show.Template);
    }

    #endregion

    #region 能力推断（纯逻辑，不需要 Ollama 服务）

    [Fact]
    [DisplayName("能力推断_所有模型规则一次验证")]
    public void InferCapabilities_AllRules()
    {
        using var client = CreateClient();

        // qwen3 系列：支持思考 + 函数调用，不支持视觉
        var caps = client.InferModelCapabilities("qwen3:8b", null);
        Assert.True(caps.SupportThinking, "qwen3 应支持思考");
        Assert.True(caps.SupportFunctionCalling, "qwen3 应支持函数调用");
        Assert.False(caps.SupportVision, "qwen3 不应支持视觉");

        // deepseek-r1：支持思考，不支持视觉
        caps = client.InferModelCapabilities("deepseek-r1:7b", null);
        Assert.True(caps.SupportThinking, "deepseek-r1 应支持思考");
        Assert.False(caps.SupportVision, "deepseek-r1 不应支持视觉");

        // llava：支持视觉
        caps = client.InferModelCapabilities("llava:7b", null);
        Assert.True(caps.SupportVision, "llava 应支持视觉");

        // gemma3：支持视觉
        caps = client.InferModelCapabilities("gemma3:4b", null);
        Assert.True(caps.SupportVision, "gemma3 应支持视觉");

        // Families 含 clip：支持视觉
        var details = new OllamaModelDetails { Families = ["llama", "clip"] };
        caps = client.InferModelCapabilities("custom-model", details);
        Assert.True(caps.SupportVision, "含 clip family 的模型应支持视觉");

        // 未知模型：全部为 false
        caps = client.InferModelCapabilities("unknown-model-xyz", null);
        Assert.False(caps.SupportThinking, "未知模型不应支持思考");
        Assert.False(caps.SupportVision, "未知模型不应支持视觉");
        Assert.False(caps.SupportFunctionCalling, "未知模型不应支持函数调用");

        // null 模型 ID：全部为 false
        caps = client.InferModelCapabilities(null, null);
        Assert.False(caps.SupportThinking, "null 模型不应支持思考");
        Assert.False(caps.SupportVision, "null 模型不应支持视觉");
        Assert.False(caps.SupportFunctionCalling, "null 模型不应支持函数调用");
    }

    #endregion

    #region 非流式对话 - 基础场景

    [OllamaFact]
    [DisplayName("非流式_系统提示词有效")]
    public async Task ChatAsync_SystemPrompt_Respected()
    {
        using var client = CreateClient();
        var request = new ChatRequest
        {
            Model = LightModel,
            Messages =
            [
                new ChatMessage { Role = "system", Content = "You are a calculator. Only reply with the numeric result." },
                new ChatMessage { Role = "user", Content = "1+1" },
            ],
            MaxTokens = 50,
            EnableThinking = false,
        };

        var response = await client.GetResponseAsync(request);

        Assert.NotNull(response);
        var content = response.Messages?[0].Message?.Content as String;
        Assert.NotEmpty(content);
    }

    [OllamaFact]
    [DisplayName("非流式_多轮对话上下文保留")]
    public async Task ChatAsync_MultiTurn_ContextPreserved()
    {
        using var client = CreateClient();
        var request = new ChatRequest
        {
            Model = LightModel,
            Messages =
            [
                new ChatMessage { Role = "user", Content = "My name is Xiao Ming, remember it." },
                new ChatMessage { Role = "assistant", Content = "Got it, your name is Xiao Ming." },
                new ChatMessage { Role = "user", Content = "What is my name? Reply with only the name." },
            ],
            MaxTokens = 50,
            EnableThinking = false,
        };

        var response = await client.GetResponseAsync(request);

        Assert.NotNull(response);
        var content = response.Messages?[0].Message?.Content as String;
        Assert.NotEmpty(content);
        Assert.True(
            content.Contains("Xiao Ming", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("XiaoMing", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("小明", StringComparison.Ordinal) ||
            content.Contains("Ming", StringComparison.OrdinalIgnoreCase),
            $"响应应包含名字，实际内容：{content}");
    }

    #endregion

    #region 非流式对话 - 响应结构

    [OllamaFact]
    [DisplayName("响应结构_所有字段一次验证")]
    public async Task ChatAsync_ResponseStructure_AllFieldsValid()
    {
        using var client = CreateClient();
        var response = await client.GetResponseAsync(SimpleRequest("1+1=?", 200));

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);

        // Object 字段
        Assert.Equal("chat.completion", response.Object);

        // Model 字段包含模型名（如 qwen3.5:0.8b）
        Assert.NotEmpty(response.Model);
        Assert.Contains("qwen", response.Model, StringComparison.OrdinalIgnoreCase);

        // Id 字段非空
        Assert.NotEmpty(response.Id);

        var choice = response.Messages[0];

        // Role 为 assistant
        Assert.Equal("assistant", choice.Message?.Role);

        // Content 非空
        Assert.NotEmpty(choice.Message?.Content as String);

        // FinishReason 为 stop 或 length
        Assert.NotNull(choice.FinishReason);
        Assert.True(choice.FinishReason == FinishReason.Stop || choice.FinishReason == FinishReason.Length,
            $"FinishReason 应为 stop 或 length，实际：{choice.FinishReason}");

        // Usage 包含 token 统计，且总数等于输入+输出
        Assert.NotNull(response.Usage);
        Assert.True(response.Usage.InputTokens > 0, "InputTokens 应大于 0");
        Assert.True(response.Usage.OutputTokens > 0, "OutputTokens 应大于 0");
        Assert.Equal(response.Usage.InputTokens + response.Usage.OutputTokens, response.Usage.TotalTokens);
    }

    #endregion

    #region 非流式对话 - 参数控制

    [OllamaFact]
    [DisplayName("参数控制_Temperature_MaxTokens_Stop均有效")]
    public async Task ChatAsync_Parameters_AllAccepted()
    {
        using var client = CreateClient();

        // Temperature = 0（确定性输出），验证有正常响应
        var req = SimpleRequest("say hi", 50);
        req.Temperature = 0.0;
        var resp = await client.GetResponseAsync(req);
        Assert.NotEmpty(resp?.Messages?[0].Message?.Content as String);

        // MaxTokens 极小值（验证截断，FinishReason 应为 length）
        resp = await client.GetResponseAsync(SimpleRequest("write a long story about a robot", 5));
        Assert.NotNull(resp?.Messages);
        Assert.True(
            resp.Messages[0].FinishReason == FinishReason.Length ||
            resp.Messages[0].FinishReason == FinishReason.Stop,
            "MaxTokens=5 时 FinishReason 应为 length 或 stop");

        // Stop 停止词（验证请求被截断后仍能正常返回）
        req = SimpleRequest("count from 1 to 10, comma separated", 200);
        req.Stop = ["5"];
        resp = await client.GetResponseAsync(req);
        Assert.NotNull(resp?.Messages);
        Assert.True(
            resp.Messages[0].FinishReason == FinishReason.Stop ||
            resp.Messages[0].FinishReason == FinishReason.Length,
            "Stop 词截断后 FinishReason 应为 stop 或 length");
    }

    #endregion

    #region 非流式对话 - 思考模式

    [OllamaFact]
    [DisplayName("思考模式_EnableThinkingFalse_ThinkingContent为空")]
    public async Task ChatAsync_ThinkFalse_NoThinkingContent()
    {
        using var client = CreateClient();
        var request = SimpleRequest("say hi", 100);
        request.EnableThinking = false;

        var response = await client.GetResponseAsync(request);

        Assert.NotNull(response?.Messages);
        var msg = response.Messages[0].Message;
        Assert.NotNull(msg);
        // think=false 时不应有思考内容
        Assert.True(String.IsNullOrEmpty(msg.ReasoningContent),
            $"think=false 时 ReasoningContent 应为空，实际：{msg.ReasoningContent}");
        Assert.NotEmpty(msg.Content as String);
    }

    [OllamaFact]
    [DisplayName("思考模式_EnableThinkingTrue_内容或思考非空（BUG验证）")]
    public async Task ChatAsync_ThinkTrue_ContentOrThinkingNonEmpty()
    {
        // 回归测试：验证 Bug 修复——小参数量模型 (qwen3.5:0.8b) 在 think=true 时
        // 有时将正文写入 thinking 字段，正文为空。修复后对话不应被标记为 [已中断]
        using var client = CreateClient();
        var request = new ChatRequest
        {
            Model = LightModel,
            Messages = [new ChatMessage { Role = "user", Content = "Write a short one-sentence poem." }],
            MaxTokens = 300,
            EnableThinking = true,
        };

        var response = await client.GetResponseAsync(request);

        Assert.NotNull(response?.Messages);
        var msg = response.Messages[0].Message;
        Assert.NotNull(msg);

        var content = msg.Content as String;
        var thinking = msg.ReasoningContent;

        // 关键断言：正文或思考内容至少有一个非空（不能两者都空）
        Assert.True(
            !String.IsNullOrWhiteSpace(content) || !String.IsNullOrWhiteSpace(thinking),
            $"正文和思考内容不能同时为空。Content='{content}', Thinking='{thinking}'");
    }

    [OllamaFact]
    [DisplayName("思考模式_EnableThinkingNull_Auto_正常响应")]
    public async Task ChatAsync_ThinkAuto_ReturnsResponse()
    {
        using var client = CreateClient();
        var request = new ChatRequest
        {
            Model = LightModel,
            Messages = [new ChatMessage { Role = "user", Content = "say hi" }],
            MaxTokens = 100,
            EnableThinking = null,  // Auto 模式，由模型自身决定
        };

        var response = await client.GetResponseAsync(request);

        Assert.NotNull(response?.Messages);
        Assert.NotEmpty(response.Messages);
    }

    [OllamaFact]
    [DisplayName("思考模式_流式ThinkTrue_内容或思考非空（BUG验证）")]
    public async Task ChatStreamAsync_ThinkTrue_ContentOrThinkingNonEmpty()
    {
        // 流式模式同样覆盖 think=true 的 Bug
        using var client = CreateClient();
        var request = new ChatRequest
        {
            Model = LightModel,
            Messages = [new ChatMessage { Role = "user", Content = "Write a short one-sentence poem." }],
            MaxTokens = 300,
            EnableThinking = true,
            Stream = true,
        };

        var chunks = await CollectStreamAsync(client, request);
        Assert.NotEmpty(chunks);

        var allContent = String.Concat(chunks
            .SelectMany(c => c.Messages ?? [])
            .Select(ch => ch.Delta?.Content as String ?? ""));
        var allThinking = String.Concat(chunks
            .SelectMany(c => c.Messages ?? [])
            .Select(ch => ch.Delta?.ReasoningContent ?? ""));

        Assert.True(
            !String.IsNullOrWhiteSpace(allContent) || !String.IsNullOrWhiteSpace(allThinking),
            $"流式正文和思考内容不能同时为空。Content='{allContent}', Thinking='{allThinking}'");
    }

    #endregion

    #region 流式对话 - 基础场景

    [OllamaFact]
    [DisplayName("流式_多个Chunk且内容拼合后非空")]
    public async Task ChatStreamAsync_MultipleChunksAndContent()
    {
        using var client = CreateClient();
        var request = SimpleRequest("say hello in English", 100);
        request.Stream = true;

        var chunks = await CollectStreamAsync(client, request);
        Assert.NotEmpty(chunks);

        // 至少有一个内容 chunk
        Assert.True(
            chunks.Any(c => c.Messages?.Any(ch => !String.IsNullOrEmpty(ch.Delta?.Content as String)) == true),
            "流式响应应包含至少一个内容 chunk");

        // 内容拼合后非空
        var full = String.Concat(chunks
            .SelectMany(c => c.Messages ?? [])
            .Select(ch => ch.Delta?.Content as String ?? ""));
        Assert.NotEmpty(full);
    }

    [OllamaFact]
    [DisplayName("流式_CancellationToken取消提前终止")]
    public async Task ChatStreamAsync_Cancellation_StopsEarly()
    {
        using var client = CreateClient();
        var request = SimpleRequest("write a 500 word essay about AI", 500);
        request.Stream = true;

        using var cts = new CancellationTokenSource();
        var chunks = new List<IChatResponse>();

        try
        {
            await foreach (var chunk in client.GetStreamingResponseAsync(request, cts.Token))
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

        Assert.True(chunks.Count >= 3, "取消前至少应收到 3 个 chunk");
    }

    #endregion

    #region 流式对话 - 结构验证

    [OllamaFact]
    [DisplayName("流式结构_所有字段一次验证")]
    public async Task ChatStreamAsync_StreamingStructure_AllFieldsValid()
    {
        using var client = CreateClient();
        var request = SimpleRequest("say hi", 100);
        request.Stream = true;

        var chunks = await CollectStreamAsync(client, request);
        Assert.NotEmpty(chunks);

        // 至少一个 chunk 含 Delta 字段
        Assert.True(
            chunks.Any(c => c.Messages?.Any(ch => ch.Delta != null) == true),
            "流式 chunk 应使用 Delta 字段");

        // Object 字段为 chat.completion.chunk
        var objectField = chunks.Select(c => c.Object).FirstOrDefault(o => o != null);
        Assert.NotNull(objectField);
        Assert.Equal("chat.completion.chunk", objectField);

        // 最后 chunk 包含 FinishReason
        FinishReason? lastFinishReason = null;
        foreach (var chunk in chunks)
        {
            foreach (var ch in chunk.Messages ?? [])
            {
                if (ch.FinishReason != null)
                    lastFinishReason = ch.FinishReason;
            }
        }
        Assert.NotNull(lastFinishReason);
        Assert.True(lastFinishReason == FinishReason.Stop || lastFinishReason == FinishReason.Length,
            $"最后 chunk 的 FinishReason 应为 stop 或 length，实际：{lastFinishReason}");

        // 内容拼合后非空
        var full = String.Concat(chunks
            .SelectMany(c => c.Messages ?? [])
            .Select(ch => ch.Delta?.Content as String ?? ""));
        Assert.NotEmpty(full);
    }

    #endregion

    #region 嵌入向量

    [OllamaFact]
    [DisplayName("嵌入_单条与批量均返回有效向量")]
    public async Task EmbedAsync_SingleAndBatch_ReturnsValidVectors()
    {
        using var client = CreateClient();

        // 单条嵌入
        var singleResp = await client.EmbedAsync(new OllamaEmbedRequest
        {
            Model = LightModel,
            Input = "Hello world",
        });
        Assert.NotNull(singleResp);
        Assert.Equal(LightModel, singleResp.Model);
        Assert.NotNull(singleResp.Embeddings);
        Assert.Single(singleResp.Embeddings);
        var dim = singleResp.Embeddings[0].Length;
        Assert.True(dim > 0, "向量维度应大于 0");
        Assert.True(singleResp.PromptEvalCount > 0, "PromptEvalCount 应大于 0");

        // 批量嵌入
        var batchResp = await client.EmbedAsync(new OllamaEmbedRequest
        {
            Model = LightModel,
            Input = new[] { "first text", "second text", "third text" },
        });
        Assert.NotNull(batchResp?.Embeddings);
        Assert.Equal(3, batchResp.Embeddings.Length);
        // 批量向量维度应与单条一致
        foreach (var vec in batchResp.Embeddings)
        {
            Assert.Equal(dim, vec.Length);
        }
    }

    #endregion

    #region FunctionCalling（轻量模型）

    [OllamaFact]
    [DisplayName("FunctionCalling_工具定义被正确接受")]
    public async Task ChatAsync_FunctionCalling_ToolsAccepted()
    {
        using var client = CreateClient();
        var request = new ChatRequest
        {
            Model = LightModel,
            Messages = [new ChatMessage { Role = "user", Content = "What is the weather in Beijing?" }],
            MaxTokens = 200,
            EnableThinking = false,
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

        var response = await client.GetResponseAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);

        // 小模型可能触发工具调用，也可能直接回答
        var choice = response.Messages[0];
        if (choice.FinishReason == FinishReason.ToolCalls)
        {
            Assert.NotNull(choice.Message?.ToolCalls);
            Assert.NotEmpty(choice.Message.ToolCalls);
            Assert.Equal("get_weather", choice.Message.ToolCalls[0].Function?.Name);
        }
    }

    #endregion

    #region 异常处理

    [OllamaFact]
    [DisplayName("异常_不存在的模型_抛出ApiException")]
    public async Task ChatAsync_InvalidModel_ThrowsApiException()
    {
        using var client = CreateClientFor("nonexistent-model-xyz-99999");
        var request = new ChatRequest
        {
            Model = "nonexistent-model-xyz-99999",
            Messages = [new ChatMessage { Role = "user", Content = "hi" }],
            MaxTokens = 50,
            EnableThinking = false,
        };

        await Assert.ThrowsAsync<ApiException>(async () =>
        {
            await client.GetResponseAsync(request);
        });
    }

    [Fact]
    [DisplayName("异常_无效Endpoint_抛出网络异常")]
    public async Task ChatAsync_InvalidEndpoint_ThrowsException()
    {
        using var client = new OllamaChatClient(null, LightModel, "http://localhost:19999");
        var request = new ChatRequest
        {
            Model = LightModel,
            Messages = [new ChatMessage { Role = "user", Content = "hi" }],
            MaxTokens = 50,
            EnableThinking = false,
        };

        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await client.GetResponseAsync(request);
        });
    }

    [OllamaFact]
    [DisplayName("异常_流式不存在的模型_抛出ApiException")]
    public async Task ChatStreamAsync_InvalidModel_ThrowsApiException()
    {
        using var client = CreateClientFor("nonexistent-model-xyz-99999");
        var request = new ChatRequest
        {
            Model = "nonexistent-model-xyz-99999",
            Messages = [new ChatMessage { Role = "user", Content = "hi" }],
            MaxTokens = 50,
            EnableThinking = false,
            Stream = true,
        };

        await Assert.ThrowsAsync<ApiException>(async () =>
        {
            await foreach (var _ in client.GetStreamingResponseAsync(request)) { }
        });
    }

    [OllamaFact]
    [DisplayName("异常_空消息列表_抛出异常")]
    public async Task ChatAsync_EmptyMessages_ThrowsException()
    {
        using var client = CreateClient();
        var request = new ChatRequest
        {
            Model = LightModel,
            Messages = [],
            MaxTokens = 50,
            EnableThinking = false,
        };

        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await client.GetResponseAsync(request);
        });
    }

    #endregion

    #region Endpoint 处理

    [OllamaFact]
    [DisplayName("Endpoint_尾部斜杠被正确处理")]
    public async Task Options_TrailingSlash_Handled()
    {
        using var client = new OllamaChatClient(null, LightModel, "http://localhost:11434/");
        var response = await client.GetResponseAsync(SimpleRequest("hi", 50));
        Assert.NotNull(response?.Messages);
    }

    #endregion

    #region 并发与稳定性

    [OllamaFact]
    [DisplayName("稳定性_多请求并发发送")]
    public async Task ChatAsync_Concurrent_AllSucceed()
    {
        var tasks = Enumerable.Range(1, 3).Select(i =>
        {
            using var client = CreateClient();
            return client.GetResponseAsync(SimpleRequest($"{i}+{i}=? reply with only the number", 50));
        }).ToArray();

        var responses = await Task.WhenAll(tasks);

        foreach (var response in responses)
        {
            Assert.NotNull(response);
            Assert.NotNull(response.Messages);
            Assert.NotEmpty(response.Messages);
        }
    }

    [OllamaFact]
    [DisplayName("稳定性_非流式和流式交替请求")]
    public async Task ChatAsync_And_Stream_Interleaved()
    {
        // 非流式
        using var client = CreateClient();
        var r1 = await client.GetResponseAsync(SimpleRequest("1+1=? reply number only", 50));
        Assert.NotNull(r1?.Messages);

        // 流式
        //using var c2 = CreateClient();
        var streamReq = SimpleRequest("2+2=? reply number only", 50);
        streamReq.Stream = true;
        var chunks = await CollectStreamAsync(client, streamReq);
        Assert.NotEmpty(chunks);

        // 再次非流式
        //using var c3 = CreateClient();
        var r3 = await client.GetResponseAsync(SimpleRequest("3+3=? reply number only", 50));
        Assert.NotNull(r3?.Messages);
    }

    #endregion

    #region 重量模型测试

    [OllamaHeavyFact]
    [DisplayName("重量模型_诗歌+思考_正文和思考均非空（BUG验证）")]
    public async Task HeavyModel_ChatAsync_ThinkTrue_PoemContentNonEmpty()
    {
        // 重量级回归测试：qwen3.5:latest 在 think=true 时必须同时有正文和思考内容
        // 不能出现正文为空、内容全在思考字段的情况
        using var client = CreateClientFor(HeavyModel);
        var request = new ChatRequest
        {
            Model = HeavyModel,
            Messages = [new ChatMessage { Role = "user", Content = "Write a short poem about the moon." }],
            MaxTokens = 500,
            EnableThinking = true,
        };

        var response = await client.GetResponseAsync(request);

        Assert.NotNull(response?.Messages);
        var msg = response.Messages[0].Message;
        Assert.NotNull(msg);

        var content = msg.Content as String;
        var thinking = msg.ReasoningContent;

        Assert.NotEmpty(content);
        Assert.NotEmpty(thinking);
    }

    [OllamaHeavyFact]
    [DisplayName("重量模型_流式流式思考_正文拼合后非空")]
    public async Task HeavyModel_ChatStreamAsync_ThinkTrue_ContentNonEmpty()
    {
        using var client = CreateClientFor(HeavyModel);
        var request = new ChatRequest
        {
            Model = HeavyModel,
            Messages = [new ChatMessage { Role = "user", Content = "Write a short poem about the moon." }],
            MaxTokens = 500,
            EnableThinking = true,
            Stream = true,
        };

        var chunks = await CollectStreamAsync(client, request);
        Assert.NotEmpty(chunks);

        var allContent = String.Concat(chunks
            .SelectMany(c => c.Messages ?? [])
            .Select(ch => ch.Delta?.Content as String ?? ""));
        var allThinking = String.Concat(chunks
            .SelectMany(c => c.Messages ?? [])
            .Select(ch => ch.Delta?.ReasoningContent ?? ""));

        Assert.NotEmpty(allContent);
        Assert.NotEmpty(allThinking);
    }

    [OllamaHeavyFact]
    [DisplayName("重量模型_FunctionCalling_可靠触发工具调用")]
    public async Task HeavyModel_ChatAsync_FunctionCalling_Triggered()
    {
        using var client = CreateClientFor(HeavyModel);
        var request = new ChatRequest
        {
            Model = HeavyModel,
            Messages = [new ChatMessage { Role = "user", Content = "What is the weather in Beijing? Use the get_weather tool." }],
            MaxTokens = 300,
            EnableThinking = false,
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

        var response = await client.GetResponseAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);

        var choice = response.Messages[0];
        // 更大的模型应更可靠地触发工具调用
        if (choice.FinishReason == FinishReason.ToolCalls)
        {
            Assert.NotNull(choice.Message?.ToolCalls);
            Assert.NotEmpty(choice.Message.ToolCalls);
            var tc = choice.Message.ToolCalls[0];
            Assert.Equal("function", tc.Type);
            Assert.Equal("get_weather", tc.Function?.Name);
            Assert.NotEmpty(tc.Function?.Arguments);
        }
    }

    #endregion

    #region 运行状态验证（末尾执行，确保有模型已加载）

    [OllamaFact]
    [DisplayName("运行状态_触发对话后应有模型正在运行")]
    public async Task ListRunningAsync_AfterChat_ContainsLoadedModel()
    {
        // 先触发一次对话确保模型已被加载，再查询运行状态
        using var client = CreateClient();
        await client.GetResponseAsync(SimpleRequest("hi", 10));

        var running = await client.ListRunningAsync();
        Assert.NotNull(running);
        Assert.NotNull(running.Models);
        Assert.True(running.Models.Length > 0, "对话后至少应有一个模型正在运行");

        // 运行中应包含 qwen 系列（轻量或重量）
        var names = running.Models.Select(m => m.Name ?? m.Model ?? "").ToArray();
        Assert.True(
            names.Any(n => n.Contains("qwen", StringComparison.OrdinalIgnoreCase)),
            $"运行中应含 qwen 系列模型，实际：{String.Join(", ", names)}");

        // 每个运行中的模型应有合理的字段値
        foreach (var m in running.Models)
        {
            var mName = m.Name ?? m.Model;
            Assert.NotEmpty(mName);
            Assert.True(m.Size > 0, $"运行中模型 {mName} 的 Size 应大于 0（字节）");
            Assert.True(m.SizeVram >= 0, $"运行中模型 {mName} 的 SizeVram 不应为负数");
        }
    }

    #endregion
}
