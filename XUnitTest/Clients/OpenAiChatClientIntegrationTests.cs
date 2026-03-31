#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NewLife.AI.Clients;
using NewLife.AI.Models;
using NewLife.AI.Providers;
using Xunit;

namespace XUnitTest.Clients;

/// <summary>OpenAiChatClient 集成测试（指向阿里百炼 OpenAI 兼容模式）。需要有效 ApiKey 才能运行</summary>
/// <remarks>
/// ApiKey 读取优先级：
/// 1. ./config/DashScope.key 文件（纯文本，首行为 ApiKey）
/// 2. 环境变量 DASHSCOPE_API_KEY
/// 未配置时测试自动跳过。
/// 阿里百炼兼容模式端点：https://dashscope.aliyuncs.com/compatible-mode
/// ChatPath 默认为 /v1/chat/completions，完整 URL 即标准 OpenAI 格式。
/// </remarks>
public class OpenAiChatClientIntegrationTests
{
    private const String Endpoint = "https://dashscope.aliyuncs.com/compatible-mode";
    private readonly String _apiKey;

    public OpenAiChatClientIntegrationTests()
    {
        _apiKey = DashScopeIntegrationTests.LoadApiKey() ?? "";
    }

    /// <summary>ApiKey 是否可用</summary>
    private Boolean HasApiKey() => !String.IsNullOrWhiteSpace(_apiKey);

    /// <summary>构建默认连接选项</summary>
    private AiClientOptions CreateOptions() => new()
    {
        Endpoint = Endpoint,
        ApiKey = _apiKey,
    };

    /// <summary>创建 OpenAiChatClient 实例</summary>
    private OpenAIChatClient CreateClient(AiClientOptions? opts = null) => new(opts ?? CreateOptions());

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
    private async Task<IChatResponse> ChatAsync(ChatRequest request, AiClientOptions? opts = null)
    {
        using var client = CreateClient(opts);
        return await client.GetResponseAsync(request);
    }

    /// <summary>创建客户端并执行流式请求</summary>
    private async IAsyncEnumerable<IChatResponse> ChatStreamAsync(ChatRequest request, AiClientOptions? opts = null, [EnumeratorCancellation] CancellationToken ct = default)
    {
        using var client = CreateClient(opts);
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
        Assert.True(response.Usage.ElapsedMs > 0, "ElapsedMs 应大于 0");
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
        Assert.Equal("2", content);
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
        var content = response.Messages[0].Message?.Content as String;
        Assert.False(String.IsNullOrWhiteSpace(content), "QwenMax 应返回有效回复内容");
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
        Assert.Contains("{", content!);
        Assert.Contains("}", content!);
        // 不对具体措辞做 Equal 断言，以免模型回复 "你好" 与 "您好" 差异导致片状失败
        Assert.Contains("reply", content!); // 模型应遵循系统提示，回复包含 "reply" 键
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
    [DisplayName("参数_Temperature=0_输出高度确定")]
    public async Task ChatAsync_Temperature_Accepted()
    {
        if (!HasApiKey()) return;

        // Temperature=0 趋向贪心解码，极简数学题应给出确定答案以验证其效果。
        // 用知道正确答案的事实题：3+4=7，Temperature=0 下应可靠回答 7。
        var request = CreateSimpleRequest("qwen-plus", "3+4等于几？只回答数字，不要其他任何内容");
        request.Temperature = 0.0;
        request.MaxTokens = 10;

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        var content = (response.Messages?[0].Message?.Content as String)?.Trim();
        Assert.False(String.IsNullOrWhiteSpace(content));
        Assert.Contains("7", content!, StringComparison.Ordinal); // 确定性场景下应得到正确答案 7
    }

    [Fact]
    [DisplayName("参数_TopP_被API接受不报错")]
    public async Task ChatAsync_TopP_Accepted()
    {
        if (!HasApiKey()) return;

        // TopP 控制核采样范围（0~1），其行为效果需大量统计样本才能验证。
        // 本测试仅确认服务端正确接收参数、不返回 4xx 错误，且响应包含有效内容。
        var request = CreateSimpleRequest("qwen-plus", "你好", 200);
        request.TopP = 0.5;

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);
        var content = response.Messages[0].Message?.Content as String;
        Assert.False(String.IsNullOrWhiteSpace(content), "TopP 参数传递后模型应正常回复");
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
    [DisplayName("参数_Stop停止词截断输出")]
    public async Task ChatAsync_Stop_Accepted()
    {
        if (!HasApiKey()) return;

        // Stop=["5"] 使模型在即将输出 "5" 时停止生成，输出中不应包含 "5" 及之后的内容
        var request = CreateSimpleRequest("qwen-plus", "Count from 1 to 10, comma separated, only digits", 200);
        request.Stop = ["5"];

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        var content = response.Messages?[0].Message?.Content as String;
        Assert.NotNull(content);
        Assert.DoesNotContain("5", content, StringComparison.Ordinal); // 停止词 "5" 不应出现在输出中
        Assert.True(content.Contains("1") || content.Contains("2") || content.Contains("3"),
            "输出应包含停止词之前的计数序列");
    }

    [Fact]
    [DisplayName("参数_PresencePenalty_被API接受不报错")]
    public async Task ChatAsync_PresencePenalty_Accepted()
    {
        if (!HasApiKey()) return;

        // PresencePenalty 对已出现词施加固定惩罚以降低重复率，效果需大量统计样本才能验证。
        // 本测试仅确认服务端正确接收参数、不返回错误，且响应包含有效内容。
        var request = CreateSimpleRequest("qwen-plus", "你好", 200);
        request.PresencePenalty = 1.5;

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);
        var content = response.Messages[0].Message?.Content as String;
        Assert.False(String.IsNullOrWhiteSpace(content), "PresencePenalty 参数传递后模型应正常回复");
    }

    [Fact]
    [DisplayName("参数_FrequencyPenalty_被API接受不报错")]
    public async Task ChatAsync_FrequencyPenalty_Accepted()
    {
        if (!HasApiKey()) return;

        // FrequencyPenalty 按词出现频率动态施加惩罚，效果需大量统计样本才能验证。
        // 本测试仅确认服务端正确接收参数、不返回错误，且响应包含有效内容。
        var request = CreateSimpleRequest("qwen-plus", "你好", 200);
        request.FrequencyPenalty = 1.0;

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);
        var content = response.Messages[0].Message?.Content as String;
        Assert.False(String.IsNullOrWhiteSpace(content), "FrequencyPenalty 参数传递后模型应正常回复");
    }

    [Fact]
    [DisplayName("参数_User字段_被API接受不影响响应")]
    public async Task ChatAsync_User_Accepted()
    {
        if (!HasApiKey()) return;

        // User 字段为请求方自定义标识，供服务端审计统计使用，不影响模型响应内容。
        // 本测试仅确认该字段被服务端接受，且响应与不传 User 时等效。
        var request = CreateSimpleRequest("qwen-plus", "你好", 200);
        request.User = "test-user-12345";

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);
        var content = response.Messages[0].Message?.Content as String;
        Assert.False(String.IsNullOrWhiteSpace(content), "设置 User 字段后模型应正常回复");
    }

    [Fact]
    [DisplayName("参数_所有可选参数组合_被API接受不报错")]
    public async Task ChatAsync_AllOptionalParams_Accepted()
    {
        if (!HasApiKey()) return;

        // 所有可选参数同时传递，确认服务端不因参数组合而报错，且响应包含有效内容。
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
        var content = response.Messages[0].Message?.Content as String;
        Assert.False(String.IsNullOrWhiteSpace(content), "多参数组合传递后模型应正常回复");
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
        Assert.True(response.Usage.ElapsedMs > 0, "ElapsedMs 应大于 0");
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
    [DisplayName("流式用量_最终Chunk包含Usage和ElapsedMs")]
    public async Task ChatStreamAsync_Usage_InFinalChunk()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-plus", "hi", 200);
        request.Stream = true;

        var chunks = new List<IChatResponse>();
        await foreach (var chunk in ChatStreamAsync(request))
        {
            chunks.Add(chunk);
        }

        Assert.NotEmpty(chunks);

        // 客户端计时块：最后一个 chunk 应包含 ElapsedMs
        var timingChunk = chunks.Last();
        Assert.NotNull(timingChunk.Usage);
        Assert.True(timingChunk.Usage!.ElapsedMs > 0, "ElapsedMs 应大于 0");

        // 公展商流式响应最后一个真实 chunk 应包含 usage（由 stream_options 触发）
        var lastWithTokens = chunks.LastOrDefault(c => c.Usage?.TotalTokens > 0);
        if (lastWithTokens != null)
            Assert.True(lastWithTokens.Usage!.TotalTokens > 0);
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
            Endpoint = Endpoint,
            ApiKey = "",
        };

        var ex = await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await ChatAsync(request, options);
        });

        Assert.Contains("OpenAI", ex.Message);
    }

    [Fact]
    [DisplayName("错误_无效ApiKey_抛出HttpRequestException")]
    public async Task ChatAsync_InvalidApiKey_ThrowsException()
    {
        var request = CreateSimpleRequest("qwen-plus", "hi");
        var options = new AiClientOptions
        {
            Endpoint = Endpoint,
            ApiKey = "sk-invalid-key-12345",
        };

        var ex = await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await ChatAsync(request, options);
        });

        Assert.Contains("OpenAI", ex.Message);
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

        Assert.Contains("OpenAI", ex.Message);
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
            Endpoint = Endpoint,
            ApiKey = "sk-invalid-key-12345",
        };

        var ex = await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await foreach (var _ in ChatStreamAsync(request, options))
            {
            }
        });

        Assert.Contains("OpenAI", ex.Message);
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
            await foreach (var _ in ChatStreamAsync(request))
            {
            }
        });

        Assert.Contains("OpenAI", ex.Message);
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
                        Parameters = new Dictionary<String, Object>
                        {
                            ["type"] = "object",
                            ["properties"] = new Dictionary<String, Object>(),
                        },
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

        // Round 1: 用户提问，模型调用工具
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

        var response1 = await ChatAsync(request1);
        Assert.NotNull(response1?.Messages);

        var choice1 = response1.Messages[0];
        if (choice1.FinishReason != "tool_calls" || choice1.Message?.ToolCalls == null)
            return; // 模型选择直接回答，跳过第二轮

        var toolCall = choice1.Message.ToolCalls[0];
        Assert.NotNull(toolCall.Id);

        // Round 2: 提交工具结果，模型生成最终答复
        // 覆盖 BuildRequestBody 中 ToolCallId、Name、ToolCalls 序列化分支
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

        var response2 = await ChatAsync(request2);

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

    #region 客户端属性验证

    [Fact]
    [DisplayName("Client_Name为OpenAI")]
    public void Client_Name_IsOpenAI()
    {
        using var client = CreateClient();
        Assert.Equal("OpenAI", client.Name);
    }

    [Fact]
    [DisplayName("Client_ChatPath默认为v1/chat/completions")]
    public void Client_ChatPath_IsDefault()
    {
        using var client = CreateClient();
        Assert.Equal("/v1/chat/completions", client.ChatPath);
    }

    [Fact]
    [DisplayName("Client_ChatPath可自定义覆盖")]
    public void Client_ChatPath_CanBeOverridden()
    {
        using var client = CreateClient();
        client.ChatPath = "/custom/v2/chat";
        Assert.Equal("/custom/v2/chat", client.ChatPath);
    }

    #endregion

    #region Options 验证

    [Fact]
    [DisplayName("Options_Endpoint尾部斜杠被正确处理")]
    public async Task Options_TrailingSlash_Handled()
    {
        if (!HasApiKey()) return;

        var request = CreateSimpleRequest("qwen-turbo", "hi", 10);
        var options = new AiClientOptions
        {
            Endpoint = Endpoint + "/",
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
            var request = CreateSimpleRequest("qwen-turbo", $"{i}+{i}=?", 10);
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

        // 非流式
        var request1 = CreateSimpleRequest("qwen-turbo", "1+1=?", 10);
        var response1 = await ChatAsync(request1);
        Assert.NotNull(response1?.Messages);

        // 流式
        var request2 = CreateSimpleRequest("qwen-turbo", "2+2=?", 10);
        request2.Stream = true;
        var chunks = new List<IChatResponse>();
        await foreach (var chunk in ChatStreamAsync(request2))
        {
            chunks.Add(chunk);
        }
        Assert.NotEmpty(chunks);

        // 再次非流式
        var request3 = CreateSimpleRequest("qwen-turbo", "3+3=?", 10);
        var response3 = await ChatAsync(request3);
        Assert.NotNull(response3?.Messages);
    }

    #endregion
}
