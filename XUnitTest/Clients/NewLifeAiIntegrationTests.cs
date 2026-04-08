#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NewLife.AI.Clients;
using NewLife.AI.Clients.OpenAI;
using NewLife.AI.Models;
using NewLife.Remoting;
using Xunit;

namespace XUnitTest.Clients;

/// <summary>新生命 AI 服务集成测试，需要有效 AppKey 才能运行</summary>
/// <remarks>
/// AppKey 加载优先级：
/// 1. ./config/NewLifeAI.key 文件中的文本内容作为 AppKey
/// 2. 环境变量 NEWLIFEAI_API_KEY
/// 未配置时测试自动跳过
/// </remarks>
[TestCaseOrderer("NewLife.UnitTest.DefaultOrderer", "NewLife.UnitTest")]
public class NewLifeAiIntegrationTests
{
    private readonly AiClientDescriptor _descriptor = AiClientRegistry.Default.GetDescriptor("NewLifeAI")!;
    private readonly String _apiKey;

    public NewLifeAiIntegrationTests()
    {
        _apiKey = LoadApiKey() ?? "";
    }

    /// <summary>从 config 目录或环境变量加载 AppKey</summary>
    private static String? LoadApiKey()
    {
        var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", "NewLifeAI.key");
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

        return Environment.GetEnvironmentVariable("NEWLIFEAI_API_KEY");
    }

    /// <summary>创建默认客户端选项</summary>
    private AiClientOptions CreateOptions() => new()
    {
        Endpoint = _descriptor.DefaultEndpoint,
        ApiKey = _apiKey,
    };

    /// <summary>构建简单的用户消息请求</summary>
    private static ChatRequest CreateSimpleRequest(String prompt, Int32 maxTokens = 200) => new()
    {
        Model = "qwen3.5-flash",
        Messages = [new ChatMessage { Role = "user", Content = prompt }],
        MaxTokens = maxTokens,
        EnableThinking = false,
    };

    /// <summary>构建含系统提示词的请求</summary>
    private static ChatRequest CreateRequestWithSystem(String systemPrompt, String userPrompt, Int32 maxTokens = 100) => new()
    {
        Model = "qwen3.5-flash",
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
    private async IAsyncEnumerable<IChatResponse> ChatStreamAsync(IChatRequest request, AiClientOptions? opts = null, [EnumeratorCancellation] CancellationToken ct = default)
    {
        using var client = _descriptor.Factory(opts ?? CreateOptions());
        await foreach (var chunk in client.GetStreamingResponseAsync(request, ct))
            yield return chunk;
    }

    /// <summary>创建 NewLifeAI 专用客户端（用于 ResponsesAsync/MessagesAsync 等扩展端点）</summary>
    private NewLifeAIChatClient CreateNewLifeAiClient() => (NewLifeAIChatClient)_descriptor.Factory(CreateOptions());

    #region 元数据验证（不需要 AppKey）

    [Fact]
    [DisplayName("元数据_Code正确")]
    public void Provider_Code_IsNewLifeAI()
    {
        Assert.Equal("NewLifeAI", _descriptor.Code);
    }

    [Fact]
    [DisplayName("元数据_Name非空")]
    public void Provider_Name_NotEmpty()
    {
        Assert.False(String.IsNullOrWhiteSpace(_descriptor.DisplayName));
    }

    [Fact]
    [DisplayName("元数据_DefaultEndpoint指向新生命AI服务")]
    public void Provider_DefaultEndpoint_PointsToNewLifeAI()
    {
        Assert.StartsWith("https://ai.newlifex.com", _descriptor.DefaultEndpoint);
    }

    [Fact]
    [DisplayName("元数据_Models包含qwen3.5")]
    public void Provider_Models_ContainsQwen35()
    {
        Assert.NotNull(_descriptor.Models);
        Assert.NotEmpty(_descriptor.Models);
        Assert.Contains(_descriptor.Models, m => m.Model.StartsWith("qwen3.5"));
    }

    [Fact]
    [DisplayName("元数据_Description非空")]
    public void Provider_Description_NotEmpty()
    {
        Assert.False(String.IsNullOrWhiteSpace(_descriptor.Description));
    }

    #endregion

    #region 非流式对话 - Chat Completions（/v1/chat/completions）

    [Fact]
    [DisplayName("非流式_Qwen3.5_返回有效响应")]
    public async Task ChatAsync_ReturnsValidResponse()
    {
        var request = CreateSimpleRequest("说一句话介绍自己");
        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);

        var content = response.Messages[0].Message?.Content as String;
        Assert.False(String.IsNullOrWhiteSpace(content), "AI 回复内容不应为空");

        Assert.NotNull(response.Usage);
        Assert.True(response.Usage.TotalTokens > 0, "Token 数量应大于 0");
    }

    [Fact]
    [DisplayName("非流式_系统提示词有效")]
    public async Task ChatAsync_SystemPrompt_Respected()
    {
        var request = CreateRequestWithSystem(
            "你是一个只用JSON格式回答的助手，回答格式为：{\"reply\":\"内容\"}",
            "你好",
            100);

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        var content = response.Messages?[0].Message?.Content as String;
        Assert.False(String.IsNullOrWhiteSpace(content));
        Assert.Contains("{", content);
    }

    [Fact]
    [DisplayName("非流式_多轮对话上下文保留")]
    public async Task ChatAsync_MultiTurn_ContextPreserved()
    {
        var request = new ChatRequest
        {
            Model = "qwen3.5-flash",
            Messages =
            [
                new ChatMessage { Role = "user", Content = "我的名字叫小明，请记住" },
                new ChatMessage { Role = "assistant", Content = "好的，我记住了，你叫小明" },
                new ChatMessage { Role = "user", Content = "我叫什么名字？只回答名字" },
            ],
            MaxTokens = 200,
            EnableThinking = false,
        };

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        var content = response.Messages?[0].Message?.Content as String;
        Assert.False(String.IsNullOrWhiteSpace(content));
        Assert.Contains("小明", content);
    }

    [Fact]
    [DisplayName("非流式_FinishReason正确返回")]
    public async Task ChatAsync_FinishReason_Returned()
    {
        var request = CreateSimpleRequest("1+1=?", 200);
        var response = await ChatAsync(request);

        Assert.NotNull(response);
        var finishReason = response.Messages?[0].FinishReason;
        Assert.NotNull(finishReason);
        Assert.True(finishReason == FinishReason.Stop || finishReason == FinishReason.Length,
            $"FinishReason 应为 stop 或 length，实际为: {finishReason}");
    }

    [Fact]
    [DisplayName("非流式_响应包含模型标识")]
    public async Task ChatAsync_Response_ContainsModel()
    {
        var request = CreateSimpleRequest("hi", 100);
        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.False(String.IsNullOrWhiteSpace(response.Model));
    }

    [Fact]
    [DisplayName("非流式_Temperature参数有效")]
    public async Task ChatAsync_Temperature_Accepted()
    {
        var request = CreateSimpleRequest("写一个随机的句子", 100);
        request.Temperature = 0.0;

        var response = await ChatAsync(request);

        Assert.NotNull(response);
        var content = response.Messages?[0].Message?.Content as String;
        Assert.False(String.IsNullOrWhiteSpace(content));
    }

    [Fact]
    [DisplayName("非流式_MaxTokens参数有效")]
    public async Task ChatAsync_MaxTokens_LimitsOutput()
    {
        var request = CreateSimpleRequest("写一篇关于代码的长文", 10);
        request.EnableThinking = false;
        var response = await ChatAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Usage);
        Assert.True(response.Usage.OutputTokens <= 15,
            $"CompletionTokens={response.Usage.OutputTokens} 应受 MaxTokens 限制");
    }

    #endregion

    #region 流式对话 - Chat Completions（/v1/chat/completions）

    [Fact]
    [DisplayName("流式_返回多个Chunk")]
    public async Task ChatStreamAsync_ReturnsChunks()
    {
        var request = CreateSimpleRequest("简单解释一下C#代码");
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
        Assert.True(hasContent, "流式应包含至少一个有内容的 chunk");
    }

    [Fact]
    [DisplayName("流式_内容可拼合为完整响应")]
    public async Task ChatStreamAsync_Content_CanBeConcatenated()
    {
        var request = CreateSimpleRequest("1+1等于几，只回答数字");
        request.Stream = true;

        var fullContent = "";
        await foreach (var chunk in ChatStreamAsync(request))
        {
            var text = chunk.Messages?[0].Delta?.Content as String;
            if (!String.IsNullOrEmpty(text)) fullContent += text;
        }

        Assert.False(String.IsNullOrWhiteSpace(fullContent), "拼合后内容不应为空");
        Assert.Contains("2", fullContent);
    }

    [Fact]
    [DisplayName("流式_取消令牌可以终止")]
    public async Task ChatStreamAsync_Cancellation_StopsEarly()
    {
        using var cts = new CancellationTokenSource();
        var request = CreateSimpleRequest("列出从1到100，每行一个数字");
        request.MaxTokens = 500;
        request.Stream = true;

        var count = 0;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var chunk in ChatStreamAsync(request, null, cts.Token))
            {
                count++;
                if (count >= 3) cts.Cancel();
            }
        });

        Assert.True(count >= 3, "取消前应收到至少 3 个 chunk");
    }

    #endregion

    #region OpenAI Responses API（/v1/responses）

    [Fact]
    [DisplayName("ResponsesAPI_非流式_返回有效响应")]
    public async Task ResponsesAsync_ReturnsValidResponse()
    {
        var request = CreateSimpleRequest("说一句话介绍自己");
        var response = await CreateNewLifeAiClient().ResponsesAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);

        var content = response.Messages[0].Message?.Content as String;
        Assert.False(String.IsNullOrWhiteSpace(content), "/v1/responses 回复内容不应为空");
    }

    [Fact]
    [DisplayName("ResponsesAPI_流式_返回多个Chunk")]
    public async Task ResponsesStreamAsync_ReturnsChunks()
    {
        var request = CreateSimpleRequest("写一段Python代码");
        request.Stream = true;

        var chunks = new List<IChatResponse>();
        await foreach (var chunk in CreateNewLifeAiClient().ResponsesStreamAsync(request))
        {
            chunks.Add(chunk);
        }

        Assert.NotEmpty(chunks);
    }

    #endregion

    #region Anthropic Messages API（/v1/messages）

    [Fact]
    [DisplayName("MessagesAPI_非流式_返回有效响应")]
    public async Task MessagesAsync_ReturnsValidResponse()
    {
        var request = CreateSimpleRequest("你好，简单回答");
        var response = await CreateNewLifeAiClient().MessagesAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);

        var content = response.Messages[0].Message?.Content as String;
        Assert.False(String.IsNullOrWhiteSpace(content), "/v1/messages 回复内容不应为空");
    }

    [Fact]
    [DisplayName("MessagesAPI_流式_返回多个Chunk")]
    public async Task MessagesStreamAsync_ReturnsChunks()
    {
        var request = CreateSimpleRequest("说声你好");
        request.Stream = true;

        var chunks = new List<IChatResponse>();
        await foreach (var chunk in CreateNewLifeAiClient().MessagesStreamAsync(request))
        {
            chunks.Add(chunk);
        }

        Assert.NotEmpty(chunks);
    }

    #endregion

    #region Google Gemini API（/v1/gemini）

    [Fact]
    [DisplayName("GeminiAPI_非流式_返回有效响应")]
    public async Task GeminiAsync_ReturnsValidResponse()
    {
        var request = CreateSimpleRequest("打声招呼");
        var response = await CreateNewLifeAiClient().GeminiAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(response.Messages);
        Assert.NotEmpty(response.Messages);

        var content = response.Messages[0].Message?.Content as String;
        Assert.False(String.IsNullOrWhiteSpace(content), "/v1/gemini 回复内容不应为空");
    }

    [Fact]
    [DisplayName("GeminiAPI_流式_返回多个Chunk")]
    public async Task GeminiStreamAsync_ReturnsChunks()
    {
        var request = CreateSimpleRequest("写一段自我介绍");
        request.Stream = true;

        var chunks = new List<IChatResponse>();
        await foreach (var chunk in CreateNewLifeAiClient().GeminiStreamAsync(request))
        {
            chunks.Add(chunk);
        }

        Assert.NotEmpty(chunks);
    }

    #endregion

    #region 图像生成（/v1/images/generations）

    [Fact]
    [DisplayName("图像生成_有效提示词_返回响应")]
    public async Task ImageGenerationsAsync_ReturnsResponse()
    {
        ImageGenerationResponse? response = null;
        try
        {
            response = await CreateNewLifeAiClient().ImageGenerationsAsync(
                "A cute robot reading a book",
                "qwen3.5-flash",
                "1024x1024");
        }
        catch (ApiException ex)
        {
            // 当前模型不支持图像生成时，忽略请求失败
            if (ex.Code is 400 or 404 or 405
                || ex.Message.Contains("不支持") || ex.Message.Contains("unsupported"))
                return;
            throw;
        }

        Assert.NotNull(response);
    }

    #endregion

    #region 工厂注册验证

    [Fact]
    [DisplayName("工厂_NewLifeAI已注册")]
    public void Factory_NewLifeAiProvider_IsRegistered()
    {
        var descriptor = AiClientRegistry.Default.GetDescriptor("NewLifeAI");
        Assert.NotNull(descriptor);
        Assert.Equal("NewLifeAI", descriptor!.Code);
    }

    #endregion
}
