using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NewLife.AI.Agents;
using NewLife.AI.Clients;
using NewLife.AI.Models;
using NewLife.AI.Tools;
using Xunit;

namespace XUnitTest.Agents;

[DisplayName("多Agent增强：DelegatingAgent / ParallelGroupChat / AgentAsTool")]
public class ParallelAgentTests
{
    // ── 辅助：固定回复的假 Agent ─────────────────────────────────────────────

    private sealed class EchoAgent : IAgent
    {
        private readonly String _reply;

        public EchoAgent(String name, String reply)
        {
            Name = name;
            _reply = reply;
        }

        public String Name { get; }
        public String? Description => $"回复固定文本：{_reply}";

        public async IAsyncEnumerable<AgentMessage> HandleAsync(
            IList<AgentMessage> history,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            yield return new TextMessage { Source = Name, Role = "assistant", Content = _reply };
        }
    }

    /// <summary>停止时返回 StopMessage 的 Agent</summary>
    private sealed class StopAgent : IAgent
    {
        public String Name => "stopper";
        public String? Description => null;

        public async IAsyncEnumerable<AgentMessage> HandleAsync(
            IList<AgentMessage> history,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            yield return new StopMessage { Source = Name, Reason = "done" };
        }
    }

    // ── DelegatingAgent 测试 ─────────────────────────────────────────────────

    [Fact]
    [DisplayName("DelegatingAgent 正确转发给 InnerAgent 并返回相同结果")]
    public async Task DelegatingAgent_ForwardsToInnerAgent()
    {
        var inner = new EchoAgent("inner", "hello from inner");
        var delegating = new DelegatingAgent(inner);

        var history = new List<AgentMessage>
        {
            new TextMessage { Source = "user", Role = "user", Content = "hi" }
        };

        var results = new List<AgentMessage>();
        await foreach (var msg in delegating.HandleAsync(history))
            results.Add(msg);

        Assert.Single(results);
        var text = Assert.IsType<TextMessage>(results[0]);
        Assert.Equal("hello from inner", text.Content);
    }

    [Fact]
    [DisplayName("DelegatingAgent Name/Description 委托给 InnerAgent")]
    public void DelegatingAgent_DelegatesToInnerAgentMetadata()
    {
        var inner = new EchoAgent("myAgent", "reply");
        var delegating = new DelegatingAgent(inner);

        Assert.Equal("myAgent", delegating.Name);
        Assert.Equal(inner.Description, delegating.Description);
    }

    [Fact]
    [DisplayName("DelegatingAgent 子类可通过 OnBeforeAsync 注入消息到历史")]
    public async Task DelegatingAgent_OnBefore_CanModifyHistory()
    {
        var inner = new EchoAgent("inner", "ok");

        // 子类：OnBefore 追加一条系统消息
        var delegating = new InjectionDelegatingAgent(inner, "injected system prompt");

        var history = new List<AgentMessage>
        {
            new TextMessage { Source = "user", Role = "user", Content = "hello" }
        };

        await foreach (var _ in delegating.HandleAsync(history)) { }

        // 原始历史被修改（注入了系统消息）
        Assert.Equal(2, history.Count);
        var injected = Assert.IsType<SystemMessage>(history[0]);
        Assert.Equal("injected system prompt", injected.Content);
    }

    [Fact]
    [DisplayName("DelegatingAgent 子类可通过 OnAfterAsync 过滤结果")]
    public async Task DelegatingAgent_OnAfter_CanFilterResults()
    {
        var inner = new EchoAgent("inner", "secret");
        var delegating = new FilteringDelegatingAgent(inner);

        var history = new List<AgentMessage>
        {
            new TextMessage { Source = "user", Role = "user", Content = "hi" }
        };

        var results = new List<AgentMessage>();
        await foreach (var msg in delegating.HandleAsync(history))
            results.Add(msg);

        // FilteringDelegatingAgent 过滤掉了所有结果
        Assert.Empty(results);
    }

    [Fact]
    [DisplayName("DelegatingAgent 构造时 innerAgent 为 null 抛出 ArgumentNullException")]
    public void DelegatingAgent_NullInner_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new DelegatingAgent(null!));
    }

    // ── ParallelGroupChat 测试 ───────────────────────────────────────────────

    [Fact]
    [DisplayName("ParallelGroupChat 并行执行所有工作代理并产出初始消息")]
    public async Task ParallelGroupChat_YieldsInitialMessage()
    {
        var workers = new List<IAgent>
        {
            new EchoAgent("w1", "result from w1"),
            new EchoAgent("w2", "result from w2"),
        };
        var aggregator = new EchoAgent("agg", "aggregated result");
        var chat = new ParallelGroupChat(workers, aggregator);

        var initial = new TextMessage { Source = "user", Role = "user", Content = "test" };
        var results = new List<AgentMessage>();
        await foreach (var msg in chat.RunAsync(initial))
            results.Add(msg);

        // 第一条是初始消息
        var first = Assert.IsType<TextMessage>(results[0]);
        Assert.Equal("test", first.Content);
    }

    [Fact]
    [DisplayName("ParallelGroupChat 包含所有工作代理的回复")]
    public async Task ParallelGroupChat_ContainsWorkerReplies()
    {
        var workers = new List<IAgent>
        {
            new EchoAgent("w1", "reply_w1"),
            new EchoAgent("w2", "reply_w2"),
        };
        var aggregator = new EchoAgent("agg", "final");
        var chat = new ParallelGroupChat(workers, aggregator);

        var initial = new TextMessage { Source = "user", Role = "user", Content = "go" };
        var results = new List<AgentMessage>();
        await foreach (var msg in chat.RunAsync(initial))
            results.Add(msg);

        var texts = results.OfType<TextMessage>().Select(m => m.Content).ToList();
        Assert.Contains("reply_w1", texts);
        Assert.Contains("reply_w2", texts);
    }

    [Fact]
    [DisplayName("ParallelGroupChat 最后一条消息是聚合代理的回复")]
    public async Task ParallelGroupChat_LastMessageIsAggregatorReply()
    {
        var workers = new List<IAgent>
        {
            new EchoAgent("w1", "w1"),
        };
        var aggregator = new EchoAgent("agg", "aggregated");
        var chat = new ParallelGroupChat(workers, aggregator);

        var results = new List<AgentMessage>();
        await foreach (var msg in chat.RunAsync(new TextMessage { Source = "user", Content = "q" }))
            results.Add(msg);

        var last = Assert.IsType<TextMessage>(results[^1]);
        Assert.Equal("aggregated", last.Content);
    }

    [Fact]
    [DisplayName("ParallelGroupChat 工作代理列表为空抛出 ArgumentException")]
    public void ParallelGroupChat_EmptyWorkers_Throws()
    {
        Assert.Throws<ArgumentException>(() => new ParallelGroupChat([], new EchoAgent("agg", "agg")));
    }

    [Fact]
    [DisplayName("ParallelGroupChat 聚合代理为 null 抛出 ArgumentNullException")]
    public void ParallelGroupChat_NullAggregator_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new ParallelGroupChat([new EchoAgent("w", "w")], null!));
    }

    // ── AgentAsTool 测试 ─────────────────────────────────────────────────────

    [Fact]
    [DisplayName("AgentAsTool.Register 将 Agent 注册到 ToolRegistry")]
    public async Task AgentAsTool_Register_ToolInvokable()
    {
        var agent = new EchoAgent("translator", "翻译结果");
        var registry = new ToolRegistry();

        AgentAsTool.Register(registry, agent);

        // 工具名称应为 snake_case
        var result = await registry.InvokeAsync("translator", "{\"message\":\"hello\"}", default);
        Assert.Equal("翻译结果", result);
    }

    [Fact]
    [DisplayName("AgentAsTool.Register 使用自定义工具名称")]
    public async Task AgentAsTool_Register_WithCustomName()
    {
        var agent = new EchoAgent("myAgent", "custom reply");
        var registry = new ToolRegistry();

        AgentAsTool.Register(registry, agent, "my_custom_tool");

        var result = await registry.InvokeAsync("my_custom_tool", "{\"message\":\"test\"}", default);
        Assert.Equal("custom reply", result);
    }

    [Fact]
    [DisplayName("AgentAsTool.CreateToolDefinition 返回包含正确名称的 ChatTool")]
    public void AgentAsTool_CreateToolDefinition_HasCorrectName()
    {
        var agent = new EchoAgent("weatherAgent", "sunny");

        var toolDef = AgentAsTool.CreateToolDefinition(agent);

        Assert.NotNull(toolDef.Function);
        Assert.Equal("weather_agent", toolDef.Function!.Name);
        Assert.NotNull(toolDef.Function.Description);
    }

    [Fact]
    [DisplayName("AgentAsTool.Register agent 为 null 抛出 ArgumentNullException")]
    public void AgentAsTool_NullAgent_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => AgentAsTool.Register(new ToolRegistry(), null!));
    }

    [Fact]
    [DisplayName("AgentAsTool.Register registry 为 null 抛出 ArgumentNullException")]
    public void AgentAsTool_NullRegistry_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => AgentAsTool.Register(null!, new EchoAgent("a", "b")));
    }

    // ── AgentConclusion 结构化结论测试 ────────────────────────────────────────

    [Fact]
    [DisplayName("AgentConclusion.ToSummaryText 格式化含结论文本")]
    public void AgentConclusion_ToSummaryText_ContainsConclusion()
    {
        var conclusion = new AgentConclusion { Conclusion = "模块 A 评估通过" };
        var text = conclusion.ToSummaryText();
        Assert.Contains("模块 A 评估通过", text);
    }

    [Fact]
    [DisplayName("AgentConclusion.ToSummaryText 置信度小于 1 时输出百分比")]
    public void AgentConclusion_ToSummaryText_ShowsConfidence()
    {
        var conclusion = new AgentConclusion { Conclusion = "结论", Confidence = 0.9f };
        var text = conclusion.ToSummaryText();
        Assert.Contains("90%", text);
    }

    [Fact]
    [DisplayName("AgentConclusion.ToSummaryText 置信度为 1 时不输出百分比")]
    public void AgentConclusion_ToSummaryText_NoConfidenceWhenFull()
    {
        var conclusion = new AgentConclusion { Conclusion = "结论", Confidence = 1f };
        var text = conclusion.ToSummaryText();
        Assert.DoesNotContain("100%", text);
    }

    [Fact]
    [DisplayName("AgentConclusion.ToSummaryText 含 Impact 时输出影响范围")]
    public void AgentConclusion_ToSummaryText_ShowsImpact()
    {
        var conclusion = new AgentConclusion { Conclusion = "评估通过", Impact = "核心业务模块" };
        var text = conclusion.ToSummaryText();
        Assert.Contains("核心业务模块", text);
    }

    [Fact]
    [DisplayName("AgentConclusion.ExtractFrom 超长文本时截断并追加省略号")]
    public void AgentConclusion_ExtractFrom_TruncatesLongText()
    {
        var longText = new String('C', 500);
        var conclusion = AgentConclusion.ExtractFrom(longText, maxLength: 100);

        Assert.True(conclusion.Conclusion.Length <= 104);
        Assert.EndsWith("...", conclusion.Conclusion);
    }

    [Fact]
    [DisplayName("AgentConclusion.ExtractFrom 短文本不截断")]
    public void AgentConclusion_ExtractFrom_ShortTextUnchanged()
    {
        var conclusion = AgentConclusion.ExtractFrom("简短结论", maxLength: 100);

        Assert.Equal("简短结论", conclusion.Conclusion);
        Assert.DoesNotContain("...", conclusion.Conclusion);
    }

    [Fact]
    [DisplayName("AgentConclusion.ExtractFrom 空文本返回空结论")]
    public void AgentConclusion_ExtractFrom_EmptyTextReturnsEmpty()
    {
        var conclusion = AgentConclusion.ExtractFrom(String.Empty);
        Assert.Equal(String.Empty, conclusion.Conclusion);
    }

    // ── ParallelGroupChat MaxWorkerConclusionLength 测试 ──────────────────────

    [Fact]
    [DisplayName("MaxWorkerConclusionLength 为 0 时不截断工作代理输出")]
    public async Task ParallelGroupChat_ConclusionLength0_NeverTruncates()
    {
        var longReply = new String('A', 500);
        var workers = new List<IAgent> { new EchoAgent("worker1", longReply) };
        var aggregator = new CapturingAggregatorAgent();
        var chat = new ParallelGroupChat(workers, aggregator); // MaxWorkerConclusionLength 默认 0

        await foreach (var _ in chat.RunAsync(new TextMessage { Source = "user", Role = "user", Content = "test" })) { }

        Assert.Contains(longReply, aggregator.ReceivedContent);
    }

    [Fact]
    [DisplayName("MaxWorkerConclusionLength 设置后截断工作代理长输出")]
    public async Task ParallelGroupChat_WithConclusionLength_TruncatesWorkerOutput()
    {
        var longReply = new String('B', 500);
        var workers = new List<IAgent> { new EchoAgent("worker1", longReply) };
        var aggregator = new CapturingAggregatorAgent();
        var chat = new ParallelGroupChat(workers, aggregator) { MaxWorkerConclusionLength = 100 };

        await foreach (var _ in chat.RunAsync(new TextMessage { Source = "user", Role = "user", Content = "test" })) { }

        Assert.True(aggregator.ReceivedContent.Length < 500, "截断后聚合输入应大幅缩短");
        Assert.Contains("...", aggregator.ReceivedContent);
    }

    [Fact]
    [DisplayName("MaxWorkerConclusionLength 短输出不截断")]
    public async Task ParallelGroupChat_ShortOutput_NotTruncated()
    {
        var shortReply = "短回复";
        var workers = new List<IAgent> { new EchoAgent("worker1", shortReply) };
        var aggregator = new CapturingAggregatorAgent();
        var chat = new ParallelGroupChat(workers, aggregator) { MaxWorkerConclusionLength = 100 };

        await foreach (var _ in chat.RunAsync(new TextMessage { Source = "user", Role = "user", Content = "test" })) { }

        Assert.Contains(shortReply, aggregator.ReceivedContent);
        Assert.DoesNotContain("...", aggregator.ReceivedContent);
    }

    // 捕获聚合输入内容的假聚合代理
    private sealed class CapturingAggregatorAgent : IAgent
    {
        public String Name => "aggregator";
        public String? Description => null;
        public String ReceivedContent { get; private set; } = String.Empty;

        public async IAsyncEnumerable<AgentMessage> HandleAsync(
            IList<AgentMessage> history, [EnumeratorCancellation] CancellationToken ct = default)
        {
            // 取最后一条 user 角色消息（即 BuildAggregatorHistory 生成的汇总输入）
            var userMsg = history.OfType<TextMessage>().LastOrDefault(m => m.Role == "user");
            if (userMsg != null) ReceivedContent = userMsg.Content ?? String.Empty;
            yield return new TextMessage { Source = Name, Role = "assistant", Content = "ok" };
        }
    }

    // ── 辅助子类 ─────────────────────────────────────────────────────────────

    /// <summary>OnBefore 注入系统消息到历史头部</summary>
    private sealed class InjectionDelegatingAgent : DelegatingAgent
    {
        private readonly String _systemPrompt;

        public InjectionDelegatingAgent(IAgent inner, String systemPrompt) : base(inner)
        {
            _systemPrompt = systemPrompt;
        }

        protected override Task OnBeforeAsync(AgentContext context)
        {
            context.History.Insert(0, new SystemMessage { Source = "system", Content = _systemPrompt });
            return Task.CompletedTask;
        }
    }

    /// <summary>OnAfter 清空所有结果</summary>
    private sealed class FilteringDelegatingAgent : DelegatingAgent
    {
        public FilteringDelegatingAgent(IAgent inner) : base(inner) { }

        protected override Task<IList<AgentMessage>> OnAfterAsync(AgentContext context, IList<AgentMessage> results)
            => Task.FromResult<IList<AgentMessage>>(new List<AgentMessage>());
    }
}
