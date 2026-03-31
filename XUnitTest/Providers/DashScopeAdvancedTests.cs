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
