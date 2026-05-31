using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NewLife.AI.Agents;
using Xunit;

namespace XUnitTest.Agents;

/// <summary>ReflectionAgent 与 CriticAgent 单元测试</summary>
[DisplayName("ReflectionAgent 单元测试")]
public class ReflectionAgentTests
{
    // ── 测试辅助 ──────────────────────────────────────────────────────────────

    /// <summary>固定返回指定文本的假 Agent（不依赖真实 IChatClient）</summary>
    private sealed class FakeAgent : IAgent
    {
        public String Name { get; }
        public String? Description { get; }
        private readonly IReadOnlyList<String> _replies;
        private Int32 _callCount;

        public FakeAgent(String name, params String[] replies)
        {
            Name = name;
            _replies = replies.Length > 0 ? replies : ["ok"];
        }

        public async IAsyncEnumerable<AgentMessage> HandleAsync(
            IList<AgentMessage> history,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            var idx = Math.Min(_callCount, _replies.Count - 1);
            _callCount++;
            yield return new TextMessage { Source = Name, Role = "assistant", Content = _replies[idx] };
        }
    }

    private static IList<AgentMessage> MakeHistory(String content = "请解释量子纠缠")
        => [new TextMessage { Source = "User", Role = "user", Content = content }];

    // ── CriticAgent.IsApproved ────────────────────────────────────────────────

    [Fact]
    [DisplayName("IsApproved—独立行 APPROVED 返回 true")]
    public void IsApproved_StandaloneLine_ReturnsTrue()
    {
        Assert.True(CriticAgent.IsApproved("回复不错。\nAPPROVED"));
    }

    [Fact]
    [DisplayName("IsApproved—末尾带标点 APPROVED 返回 true")]
    public void IsApproved_WithTrailingPunctuation_ReturnsTrue()
    {
        Assert.True(CriticAgent.IsApproved("APPROVED."));
        Assert.True(CriticAgent.IsApproved("APPROVED!"));
    }

    [Fact]
    [DisplayName("IsApproved—NOT APPROVED 不匹配")]
    public void IsApproved_NotApproved_ReturnsFalse()
    {
        Assert.False(CriticAgent.IsApproved("NOT APPROVED"));
        Assert.False(CriticAgent.IsApproved("not approved"));
    }

    [Fact]
    [DisplayName("IsApproved—空字符串返回 false")]
    public void IsApproved_Null_ReturnsFalse()
    {
        Assert.False(CriticAgent.IsApproved(null));
        Assert.False(CriticAgent.IsApproved(""));
    }

    // ── ReflectionAgent 基本行为 ───────────────────────────────────────────────

    [Fact]
    [DisplayName("HandleAsync—评审立即批准时产出草稿（1 次起草）")]
    public async Task HandleAsync_ImmediateApproval_ReturnsDraft()
    {
        var primary = new FakeAgent("Drafter", "初始草稿");
        var critic = new FakeAgent("Critic", CriticAgent.ApprovalSignal);
        var agent = new ReflectionAgent(primary, critic) { MaxIterations = 3 };

        var messages = new List<AgentMessage>();
        await foreach (var m in agent.HandleAsync(MakeHistory()))
            messages.Add(m);

        Assert.Single(messages);
        Assert.IsType<TextMessage>(messages[0]);
        Assert.Equal("初始草稿", ((TextMessage)messages[0]).Content);
    }

    [Fact]
    [DisplayName("HandleAsync—评审拒绝后再迭代，产出修订草稿")]
    public async Task HandleAsync_OneRejectionThenApproval_ReturnsFinalDraft()
    {
        var primary = new FakeAgent("Drafter", "草稿一", "修订草稿");
        var critic = new FakeAgent("Critic", "逻辑不清晰，请改进", CriticAgent.ApprovalSignal);
        var agent = new ReflectionAgent(primary, critic) { MaxIterations = 3 };

        var messages = new List<AgentMessage>();
        await foreach (var m in agent.HandleAsync(MakeHistory()))
            messages.Add(m);

        Assert.Single(messages);
        Assert.Equal("修订草稿", ((TextMessage)messages[0]).Content);
    }

    [Fact]
    [DisplayName("HandleAsync—达到 MaxIterations 上限时产出最后一份草稿")]
    public async Task HandleAsync_ReachMaxIterations_ReturnsLastDraft()
    {
        var primary = new FakeAgent("Drafter", "草稿一", "草稿二", "草稿三");
        // 评审永远不批准
        var critic = new FakeAgent("Critic", "请继续改进");
        var agent = new ReflectionAgent(primary, critic) { MaxIterations = 3 };

        var messages = new List<AgentMessage>();
        await foreach (var m in agent.HandleAsync(MakeHistory()))
            messages.Add(m);

        Assert.Single(messages);
        Assert.Equal("草稿三", ((TextMessage)messages[0]).Content);
    }

    [Fact]
    [DisplayName("HandleAsync—MaxIterations=1 退化为无评审单次调用")]
    public async Task HandleAsync_MaxIterationsOne_NoCriticInvoked()
    {
        var primary = new FakeAgent("Drafter", "唯一草稿");
        var critic = new FakeAgent("Critic", "不应被调用");
        var agent = new ReflectionAgent(primary, critic) { MaxIterations = 1 };

        var messages = new List<AgentMessage>();
        await foreach (var m in agent.HandleAsync(MakeHistory()))
            messages.Add(m);

        Assert.Single(messages);
        Assert.Equal("唯一草稿", ((TextMessage)messages[0]).Content);
    }

    [Fact]
    [DisplayName("HandleAsync—EmitIterationMessages=true 时产出中间消息")]
    public async Task HandleAsync_EmitIterationMessages_EmitsAllMessages()
    {
        var primary = new FakeAgent("Drafter", "草稿", "修订");
        var critic = new FakeAgent("Critic", "请改进", CriticAgent.ApprovalSignal);
        var agent = new ReflectionAgent(primary, critic)
        {
            MaxIterations = 3,
            EmitIterationMessages = true,
        };

        var messages = new List<AgentMessage>();
        await foreach (var m in agent.HandleAsync(MakeHistory()))
            messages.Add(m);

        // 应包含：草稿1 + 评审1(拒绝) + 草稿2 + 评审2(批准) = 4 条
        Assert.Equal(4, messages.Count);
    }

    [Fact]
    [DisplayName("HandleAsync—history 为 null 时抛出 ArgumentNullException")]
    public async Task HandleAsync_NullHistory_Throws()
    {
        var agent = new ReflectionAgent(
            new FakeAgent("p", "d"),
            new FakeAgent("c", CriticAgent.ApprovalSignal));

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await foreach (var _ in agent.HandleAsync(null!)) { }
        });
    }

    [Fact]
    [DisplayName("构造函数—primary 为 null 时抛出 ArgumentNullException")]
    public void Constructor_NullPrimary_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ReflectionAgent(null!, new FakeAgent("c", "ok")));
    }

    [Fact]
    [DisplayName("构造函数—critic 为 null 时抛出 ArgumentNullException")]
    public void Constructor_NullCritic_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ReflectionAgent(new FakeAgent("p", "d"), null!));
    }

    [Fact]
    [DisplayName("Name 属性—未指定 name 时默认为 Reflection:{primary.Name}")]
    public void Name_DefaultFormat()
    {
        var primary = new FakeAgent("Writer", "text");
        var agent = new ReflectionAgent(primary, new FakeAgent("c", "ok"));
        Assert.Equal("Reflection:Writer", agent.Name);
    }

    [Fact]
    [DisplayName("Name 属性—指定自定义名称时使用指定值")]
    public void Name_CustomName()
    {
        var agent = new ReflectionAgent(new FakeAgent("p", "d"), new FakeAgent("c", "ok"), "MyAgent");
        Assert.Equal("MyAgent", agent.Name);
    }
}
