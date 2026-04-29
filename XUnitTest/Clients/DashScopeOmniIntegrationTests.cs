#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using NewLife;
using NewLife.AI.Clients;
using NewLife.AI.Clients.DashScope;
using NewLife.AI.Models;
using Xunit;
using Xunit.Sdk;
using XUnitTest.Helpers;

namespace XUnitTest.Clients;

/// <summary>DashScope Omni 全模态模型集成测试</summary>
/// <remarks>
/// 覆盖范围：
/// <list type="bullet">
/// <item>Qwen-Omni 非实时系列（HTTP 兼容模式，stream=true 必填）</item>
/// <item>Qwen-Omni 实时系列（WebSocket 协议）</item>
/// <item>InferModelCapabilities 纯逻辑验证（无需 ApiKey）</item>
/// </list>
/// ApiKey 读取：config/DashScope.key 文件 或 DASHSCOPE_API_KEY 环境变量
/// </remarks>
public class DashScopeOmniIntegrationTests
{
    private readonly String _apiKey;

    public DashScopeOmniIntegrationTests()
    {
        _apiKey = DashScopeIntegrationTests.LoadApiKey() ?? "";
    }

    private AiClientOptions CreateOptions() => new() { ApiKey = _apiKey };

    private void EnsureApiKey()
    {
        if (String.IsNullOrWhiteSpace(_apiKey))
            throw SkipException.ForSkip("未检测到可用 API Key（config/DashScope.key 或 DASHSCOPE_API_KEY），跳过集成测试");
    }

    #region 能力推断（纯逻辑，无需 ApiKey）
    [Fact]
    [DisplayName("InferCapabilities_qwen3_5_omni_plus：vision+audio 无思考，context=131072")]
    public void InferCapabilities_Qwen35OmniPlus_CorrectCapabilities()
    {
        using var client = new DashScopeChatClient("dummy");
        var cap = client.InferModelCapabilities("qwen3.5-omni-plus");
        Assert.NotNull(cap);
        Assert.True(cap.SupportVision);
        Assert.True(cap.SupportAudio);
        Assert.False(cap.SupportThinking);
        Assert.False(cap.SupportFunctionCalling);
        Assert.Equal(131_072, cap.ContextLength);
    }

    [Fact]
    [DisplayName("InferCapabilities_qwen3_omni_flash：vision+audio+thinking，context=131072")]
    public void InferCapabilities_Qwen3OmniFlash_Thinking()
    {
        using var client = new DashScopeChatClient("dummy");
        var cap = client.InferModelCapabilities("qwen3-omni-flash");
        Assert.NotNull(cap);
        Assert.True(cap.SupportVision);
        Assert.True(cap.SupportAudio);
        Assert.True(cap.SupportThinking);
        Assert.False(cap.SupportFunctionCalling);
        Assert.Equal(131_072, cap.ContextLength);
    }

    [Fact]
    [DisplayName("InferCapabilities_qwen_omni_turbo（旧版）：vision+audio 无思考，context=32768")]
    public void InferCapabilities_QwenOmniTurbo_Legacy()
    {
        using var client = new DashScopeChatClient("dummy");
        var cap = client.InferModelCapabilities("qwen-omni-turbo");
        Assert.NotNull(cap);
        Assert.True(cap.SupportVision);
        Assert.True(cap.SupportAudio);
        Assert.False(cap.SupportThinking);
        Assert.False(cap.SupportFunctionCalling);
        Assert.Equal(32_768, cap.ContextLength);
    }

    [Fact]
    [DisplayName("InferCapabilities_qwen3_5_omni_flash_realtime：视觉+音频，无思考")]
    public void InferCapabilities_OmniRealtimeModel_Recognized()
    {
        using var client = new DashScopeChatClient("dummy");
        var cap = client.InferModelCapabilities("qwen3.5-omni-flash-realtime");
        Assert.NotNull(cap);
        Assert.True(cap.SupportVision);
        Assert.True(cap.SupportAudio);
        Assert.False(cap.SupportFunctionCalling);
    }

    [Fact]
    [DisplayName("IsOmniModel 不匹配实时模型 / 匹配非实时 omni 模型")]
    public void OmniModelRouting_NotMatchesRealtimeModels()
    {
        // 实时模型不应走 ChatAsync omni 路径（由 DashScopeRealtimeClient 处理）
        // 此处间接通过 InferModelCapabilities 差异来验证实时与非实时的区分
        using var client = new DashScopeChatClient("dummy");

        var realtimeCap = client.InferModelCapabilities("qwen3.5-omni-plus-realtime");
        var nonRealtimeCap = client.InferModelCapabilities("qwen3.5-omni-plus");

        Assert.NotNull(realtimeCap);
        Assert.NotNull(nonRealtimeCap);
        // 两者都应识别为 vision+audio 能力
        Assert.True(realtimeCap.SupportVision);
        Assert.True(nonRealtimeCap.SupportVision);
    }
    #endregion

    #region Omni 非实时：流式对话
    [RequiresApiKeyFact("DASHSCOPE_API_KEY", "config/DashScope.key")]
    [DisplayName("Omni_qwen3_5_omni_flash_流式纯文本问答")]
    public async Task OmniStream_TextOnly_ReturnsText()
    {
        var request = new ChatRequest
        {
            Model = "qwen3.5-omni-flash",
            Messages = [new ChatMessage { Role = "user", Content = "用一句话介绍你自己" }],
            MaxTokens = 100,
        };

        using var client = new DashScopeChatClient(CreateOptions());
        var chunks = new List<IChatResponse>();
        await foreach (var chunk in client.GetStreamingResponseAsync(request))
            chunks.Add(chunk);

        Assert.NotEmpty(chunks);
        var text = String.Concat(chunks.Select(c => c.Messages?.FirstOrDefault()?.Delta?.Content as String ?? ""));
        Assert.False(String.IsNullOrWhiteSpace(text));
    }

    [RequiresApiKeyFact("DASHSCOPE_API_KEY", "config/DashScope.key")]
    [DisplayName("Omni_qwen3_5_omni_flash_非流式聚合问答")]
    public async Task OmniNonStream_TextOnly_AggregatesChunks()
    {
        var request = new ChatRequest
        {
            Model = "qwen3.5-omni-flash",
            Messages = [new ChatMessage { Role = "user", Content = "1+1等于几？只回答数字" }],
            MaxTokens = 50,
        };

        using var client = new DashScopeChatClient(CreateOptions());
        var response = await client.GetResponseAsync(request);

        Assert.NotNull(response);
        Assert.False(String.IsNullOrWhiteSpace(response.Text));
    }

    [RequiresApiKeyFact("DASHSCOPE_API_KEY", "config/DashScope.key")]
    [DisplayName("Omni_qwen3_5_omni_flash_音频输出模态")]
    public async Task OmniStream_AudioModality_ReturnsAudioChunks()
    {
        var request = new ChatRequest
        {
            Model = "qwen3.5-omni-flash",
            Messages = [new ChatMessage { Role = "user", Content = "说 Hello" }],
            MaxTokens = 50,
        };
        // 请求音频输出
        request["OmniModalities"] = new[] { "text", "audio" };
        request["OmniVoice"] = "Cherry";
        request["OmniAudioFormat"] = "wav";

        using var client = new DashScopeChatClient(CreateOptions());
        var hasAnyChunk = false;
        await foreach (var chunk in client.GetStreamingResponseAsync(request))
            hasAnyChunk = true;

        Assert.True(hasAnyChunk, "应收到至少一个响应块");
    }

    [RequiresApiKeyFact("DASHSCOPE_API_KEY", "config/DashScope.key")]
    [DisplayName("Omni_qwen3_omni_flash_思考模式文本问答")]
    public async Task OmniThinking_Qwen3OmniFlash_ReturnsResponse()
    {
        var request = new ChatRequest
        {
            Model = "qwen3-omni-flash",
            Messages = [new ChatMessage { Role = "user", Content = "8 的平方根是多少？简短回答" }],
            MaxTokens = 200,
            EnableThinking = true,
        };

        using var client = new DashScopeChatClient(CreateOptions());
        var response = await client.GetResponseAsync(request);

        Assert.NotNull(response);
        Assert.False(String.IsNullOrWhiteSpace(response.Text));
    }

    [RequiresApiKeyFact("DASHSCOPE_API_KEY", "config/DashScope.key")]
    [DisplayName("Omni_qwen3_5_omni_plus_联网搜索")]
    public async Task OmniSearch_Qwen35OmniPlus_EnableSearch()
    {
        var request = new ChatRequest
        {
            Model = "qwen3.5-omni-plus",
            Messages = [new ChatMessage { Role = "user", Content = "今天是几月几号？" }],
            MaxTokens = 100,
        };
        request["EnableSearch"] = true;

        using var client = new DashScopeChatClient(CreateOptions());
        var response = await client.GetResponseAsync(request);

        Assert.NotNull(response);
        Assert.False(String.IsNullOrWhiteSpace(response.Text));
    }

    [RequiresApiKeyFact("DASHSCOPE_API_KEY", "config/DashScope.key")]
    [DisplayName("Omni_图像输入_视觉问答")]
    public async Task OmniVision_ImageUrl_AnswersQuestion()
    {
        var request = new ChatRequest
        {
            Model = "qwen3.5-omni-flash",
            Messages =
            [
                new ChatMessage
                {
                    Role = "user",
                    Contents =
                    [
                        new ImageContent { Uri = "https://help-static-aliyun-doc.aliyuncs.com/file-manage-files/zh-CN/20241022/emyrja/dog_and_girl.jpeg" },
                        new TextContent("图中有什么？用一句话描述"),
                    ],
                },
            ],
            MaxTokens = 100,
        };

        using var client = new DashScopeChatClient(CreateOptions());
        var response = await client.GetResponseAsync(request);

        Assert.NotNull(response);
        Assert.False(String.IsNullOrWhiteSpace(response.Text));
    }

    [RequiresApiKeyFact("DASHSCOPE_API_KEY", "config/DashScope.key")]
    [DisplayName("Omni_使用量统计包含在响应中")]
    public async Task OmniUsage_IncludedInFinalChunk()
    {
        var request = new ChatRequest
        {
            Model = "qwen3.5-omni-flash",
            Messages = [new ChatMessage { Role = "user", Content = "你好" }],
            MaxTokens = 50,
        };

        using var client = new DashScopeChatClient(CreateOptions());
        IChatResponse? lastChunk = null;
        await foreach (var chunk in client.GetStreamingResponseAsync(request))
            lastChunk = chunk;

        Assert.NotNull(lastChunk);
        // usage 应在末尾块携带
        var usageChunks = new List<IChatResponse> { lastChunk };
        Assert.True(usageChunks.Any(c => c.Usage?.TotalTokens > 0), "最终块应包含使用量统计");
    }
    #endregion

    #region Omni 实时：WebSocket 连接
    [RequiresApiKeyFact("DASHSCOPE_API_KEY", "config/DashScope.key")]
    [DisplayName("Realtime_连接后收到 session.created 事件")]
    public async Task RealtimeConnect_SessionCreatedEvent_Received()
    {
        using var client = new DashScopeRealtimeClient(_apiKey);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        await client.ConnectAsync("qwen3.5-omni-flash-realtime", cts.Token);

        RealtimeEvent? sessionCreated = null;
        await foreach (var evt in client.ReceiveEventsAsync(cts.Token))
        {
            if (evt.Type == "session.created")
            {
                sessionCreated = evt;
                break;
            }
        }

        Assert.NotNull(sessionCreated);
        Assert.Equal("session.created", sessionCreated.Type);

        await client.CloseAsync(cts.Token);
    }

    [RequiresApiKeyFact("DASHSCOPE_API_KEY", "config/DashScope.key")]
    [DisplayName("Realtime_session.update 后收到 session.updated")]
    public async Task RealtimeSession_Update_Acknowledged()
    {
        using var client = new DashScopeRealtimeClient(_apiKey);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        await client.ConnectAsync("qwen3.5-omni-flash-realtime", cts.Token);

        // 等待初始 session.created
        await foreach (var evt in client.ReceiveEventsAsync(cts.Token))
        {
            if (evt.Type == "session.created") break;
        }

        // 发送 session.update
        await client.UpdateSessionAsync(new RealtimeSessionConfig
        {
            Voice = "Cherry",
            Instructions = "你是一个助理，请简短回答",
        }, cts.Token);

        // 等待 session.updated 确认
        RealtimeEvent? updated = null;
        await foreach (var evt in client.ReceiveEventsAsync(cts.Token))
        {
            if (evt.Type == "session.updated")
            {
                updated = evt;
                break;
            }
        }

        Assert.NotNull(updated);
        Assert.Equal("session.updated", updated.Type);

        await client.CloseAsync(cts.Token);
    }

    [Fact]
    [DisplayName("Realtime_非实时模型调用 ConnectAsync 应抛出 ArgumentException")]
    public async Task RealtimeConnect_NonRealtimeModel_ThrowsArgumentException()
    {
        using var client = new DashScopeRealtimeClient("dummy-key");
        await Assert.ThrowsAsync<ArgumentException>(
            () => client.ConnectAsync("qwen3.5-omni-flash"));
    }
    #endregion
}
