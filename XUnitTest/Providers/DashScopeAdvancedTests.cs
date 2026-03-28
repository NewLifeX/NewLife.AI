using System;
using System.ComponentModel;
using NewLife.AI.Clients;
using NewLife.AI.Models;
using NewLife.AI.Providers;
using Xunit;

namespace XUnitTest.Providers;

/// <summary>DashScopeChatOptions、DashScopeUsage、DashScopeChoice 及服务商注册测试</summary>
[DisplayName("DashScope 高级选项测试")]
public class DashScopeAdvancedTests
{
    // ── DashScopeChatOptions 继承关系 ─────────────────────────────────────

    [Fact]
    [DisplayName("DashScopeChatOptions—继承自 ChatOptions")]
    public void DashScopeChatOptions_InheritsChatOptions()
    {
        var opts = new DashScopeChatOptions();
        Assert.IsAssignableFrom<ChatOptions>(opts);
    }

    [Fact]
    [DisplayName("DashScopeChatOptions—所有属性默认为 null")]
    public void DashScopeChatOptions_DefaultProperties_Null()
    {
        var opts = new DashScopeChatOptions();

        Assert.Null(opts.Seed);
        Assert.Null(opts.RepetitionPenalty);
        Assert.Null(opts.N);
        Assert.Null(opts.ThinkingBudget);
        Assert.Null(opts.EnableCodeInterpreter);
        Assert.Null(opts.Logprobs);
        Assert.Null(opts.TopLogprobs);
        Assert.Null(opts.EnableSearch);
        Assert.Null(opts.SearchStrategy);
        Assert.Null(opts.EnableSource);
        Assert.Null(opts.ForcedSearch);
    }

    [Fact]
    [DisplayName("DashScopeChatOptions—Seed 可读写")]
    public void DashScopeChatOptions_Seed_ReadWrite()
    {
        var opts = new DashScopeChatOptions { Seed = 42 };
        Assert.Equal(42, opts.Seed);
    }

    [Fact]
    [DisplayName("DashScopeChatOptions—EnableSearch 可读写")]
    public void DashScopeChatOptions_EnableSearch_ReadWrite()
    {
        var opts = new DashScopeChatOptions { EnableSearch = true };
        Assert.True(opts.EnableSearch);
    }

    [Fact]
    [DisplayName("DashScopeChatOptions—N 可读写")]
    public void DashScopeChatOptions_N_ReadWrite()
    {
        var opts = new DashScopeChatOptions { N = 3 };
        Assert.Equal(3, opts.N);
    }

    [Fact]
    [DisplayName("DashScopeChatOptions—ThinkingBudget -1 表示不限制")]
    public void DashScopeChatOptions_ThinkingBudget_Unlimited()
    {
        var opts = new DashScopeChatOptions { ThinkingBudget = -1 };
        Assert.Equal(-1, opts.ThinkingBudget);
    }

    [Fact]
    [DisplayName("DashScopeChatOptions—SearchStrategy 三种合法值")]
    public void DashScopeChatOptions_SearchStrategy_ThreeLegalValues()
    {
        foreach (var strategy in new[] { "intelligent", "force", "prohibited" })
        {
            var opts = new DashScopeChatOptions { SearchStrategy = strategy };
            Assert.Equal(strategy, opts.SearchStrategy);
        }
    }

    [Fact]
    [DisplayName("DashScopeChatOptions—RepetitionPenalty 可读写")]
    public void DashScopeChatOptions_RepetitionPenalty_ReadWrite()
    {
        var opts = new DashScopeChatOptions { RepetitionPenalty = 1.2 };
        Assert.Equal(1.2, opts.RepetitionPenalty);
    }

    [Fact]
    [DisplayName("DashScopeChatOptions—VlHighResolutionImages 可读写")]
    public void DashScopeChatOptions_VlHighResolutionImages_ReadWrite()
    {
        var opts = new DashScopeChatOptions { VlHighResolutionImages = true };
        Assert.True(opts.VlHighResolutionImages);
    }

    [Fact]
    [DisplayName("DashScopeChatOptions—MaxPixels 可读写")]
    public void DashScopeChatOptions_MaxPixels_ReadWrite()
    {
        var opts = new DashScopeChatOptions { MaxPixels = 1920 * 1080 };
        Assert.Equal(1920 * 1080, opts.MaxPixels);
    }

    // ── DashScopeChatOptions 继承自 ChatOptions 的属性 ────────────────────

    [Fact]
    [DisplayName("DashScopeChatOptions—TopK 继承自 ChatOptions")]
    public void DashScopeChatOptions_TopK_InheritedFromChatOptions()
    {
        var opts = new DashScopeChatOptions { TopK = 50 };
        // TopK 是 ChatOptions 基类属性
        ChatOptions baseOpts = opts;
        Assert.Equal(50, baseOpts.TopK);
    }

    // ── DashScopeUsage ────────────────────────────────────────────────────

    [Fact]
    [DisplayName("DashScopeUsage—继承自 UsageDetails")]
    public void DashScopeUsage_InheritsUsageDetails()
    {
        var usage = new DashScopeUsage();
        Assert.IsAssignableFrom<UsageDetails>(usage);
    }

    [Fact]
    [DisplayName("DashScopeUsage—多模态 Token 字段默认值为 0")]
    public void DashScopeUsage_DefaultMultimodalTokens_Zero()
    {
        var usage = new DashScopeUsage();
        Assert.Equal(0, usage.ImageTokens);
        Assert.Equal(0, usage.VideoTokens);
        Assert.Equal(0, usage.AudioTokens);
    }

    [Fact]
    [DisplayName("DashScopeUsage—多模态 Token 字段可读写")]
    public void DashScopeUsage_MultimodalTokens_ReadWrite()
    {
        var usage = new DashScopeUsage { ImageTokens = 100, VideoTokens = 200, AudioTokens = 50 };
        Assert.Equal(100, usage.ImageTokens);
        Assert.Equal(200, usage.VideoTokens);
        Assert.Equal(50, usage.AudioTokens);
    }

    // ── DashScopeChoice ────────────────────────────────────────────────────

    [Fact]
    [DisplayName("DashScopeChoice—继承自 ChatChoice")]
    public void DashScopeChoice_InheritsChatChoice()
    {
        var choice = new DashScopeChoice();
        Assert.IsAssignableFrom<ChatChoice>(choice);
    }

    [Fact]
    [DisplayName("DashScopeChoice—Logprobs 默认为 null")]
    public void DashScopeChoice_Logprobs_DefaultNull()
    {
        var choice = new DashScopeChoice();
        Assert.Null(choice.Logprobs);
    }

    [Fact]
    [DisplayName("DashScopeChoice—Logprobs 可读写")]
    public void DashScopeChoice_Logprobs_ReadWrite()
    {
        var choice = new DashScopeChoice { Logprobs = new { token = "hello" } };
        Assert.NotNull(choice.Logprobs);
    }

    // ── DashScopeAdvancedProvider via Registry ────────────────────────────

    [Fact]
    [DisplayName("DashScope 服务商描述符—Code 为 DashScope")]
    public void DashScope_Descriptor_Code_IsDashScope()
    {
        var descriptor = AiClientRegistry.Default.GetDescriptor("DashScope");
        Assert.NotNull(descriptor);
        Assert.Equal("DashScope", descriptor.Code);
    }

    [Fact]
    [DisplayName("DashScope 服务商描述符—Factory 不为 null")]
    public void DashScope_Descriptor_Factory_NotNull()
    {
        var descriptor = AiClientRegistry.Default.GetDescriptor("DashScope");
        Assert.NotNull(descriptor);
        Assert.NotNull(descriptor.Factory);
    }
}
