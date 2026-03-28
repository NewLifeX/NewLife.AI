using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NewLife.AI.Agents;
using NewLife.AI.Models;
using NewLife.AI.Providers;
using NewLife.AI.Tools;
using Xunit;

namespace XUnitTest.Agents;

/// <summary>Agent 系统（AgentMessage 子类型、ConversableAgent、GroupChat、AgentAsTool）单元测试</summary>
[DisplayName("Agent 系统单元测试")]
public class AgentSystemTests
{
    // ── 测试辅助：最简断言用 IAgent ──────────────────────────────────────────

    /// <summary>固定返回指定文本的假 Agent</summary>
    private sealed class FakeAgent : IAgent
    {
        public String Name { get; }
        public String Description { get; }
        private readonly String _reply;

        public FakeAgent(String name, String reply = "ok", String description = "fake")
        {
            Name = name;
            Description = description;
            _reply = reply;
        }

        public async IAsyncEnumerable<AgentMessage> HandleAsync(
            IList<AgentMessage> history,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            yield return new TextMessage { Source = Name, Content = _reply, Role = "assistant" };
        }
    }

    /// <summary>固定返回 StopMessage 的假 Agent</summary>
    private sealed class StoppingAgent : IAgent
    {
        public String Name => "stopper";
        public String Description => "always stops";

        public async IAsyncEnumerable<AgentMessage> HandleAsync(
            IList<AgentMessage> history,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            yield return new StopMessage { Source = Name, Reason = "done" };
        }
    }

    // ── AgentMessage 子类型 ──────────────────────────────────────────────────

    #region TextMessage

    [Fact]
    [DisplayName("TextMessage—Type 为 Text")]
    public void TextMessage_Type_IsText()
    {
        var msg = new TextMessage();
        Assert.Equal(AgentMessageType.Text, msg.Type);
    }

    [Fact]
    [DisplayName("TextMessage—ToChatMessage 返回对应 Role 和 Content")]
    public void TextMessage_ToChatMessage_ReturnsCorrect()
    {
        var msg = new TextMessage { Source = "user", Content = "Hello", Role = "user" };
        var cm = msg.ToChatMessage();
        Assert.NotNull(cm);
        Assert.Equal("Hello", cm!.Content?.ToString());
        Assert.Equal("user", cm.Role);
    }

    [Fact]
    [DisplayName("TextMessage—默认 Role 为 user")]
    public void TextMessage_DefaultRole_IsUser()
    {
        var msg = new TextMessage();
        Assert.Equal("user", msg.Role);
    }

    [Fact]
    [DisplayName("TextMessage—Source 和 Timestamp 默认值")]
    public void TextMessage_DefaultSourceAndTimestamp()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var msg = new TextMessage { Source = "agent1" };
        Assert.Equal("agent1", msg.Source);
        Assert.True(msg.Timestamp >= before);
    }

    #endregion

    #region SystemMessage

    [Fact]
    [DisplayName("SystemMessage—Type 为 System")]
    public void SystemMessage_Type_IsSystem()
    {
        var msg = new SystemMessage();
        Assert.Equal(AgentMessageType.System, msg.Type);
    }

    [Fact]
    [DisplayName("SystemMessage—ToChatMessage 返回 system 角色消息")]
    public void SystemMessage_ToChatMessage_HasSystemRole()
    {
        var msg = new SystemMessage { Source = "system", Content = "You are helpful." };
        var cm = msg.ToChatMessage();
        Assert.NotNull(cm);
        Assert.Equal("system", cm!.Role);
        Assert.Equal("You are helpful.", cm.Content?.ToString());
    }

    #endregion

    #region StopMessage

    [Fact]
    [DisplayName("StopMessage—Type 为 Stop")]
    public void StopMessage_Type_IsStop()
    {
        var msg = new StopMessage();
        Assert.Equal(AgentMessageType.Stop, msg.Type);
    }

    [Fact]
    [DisplayName("StopMessage—ToChatMessage 返回 null（终止信号不映射为 ChatMessage）")]
    public void StopMessage_ToChatMessage_ReturnsNull()
    {
        var msg = new StopMessage { Source = "stopper", Reason = "task complete" };
        Assert.Null(msg.ToChatMessage());
    }

    [Fact]
    [DisplayName("StopMessage—Reason 属性可设置")]
    public void StopMessage_Reason_ReadWrite()
    {
        var msg = new StopMessage { Reason = "done" };
        Assert.Equal("done", msg.Reason);
    }

    #endregion

    // ── RoundRobinSelector ──────────────────────────────────────────────────

    #region RoundRobinSelector

    [Fact]
    [DisplayName("RoundRobinSelector—依次轮询代理列表")]
    public async Task RoundRobinSelector_SelectsInSequence()
    {
        var selector = new RoundRobinSelector();
        var agents = new List<IAgent>
        {
            new FakeAgent("A"),
            new FakeAgent("B"),
            new FakeAgent("C"),
        };

        var first = await selector.SelectNextAsync(agents, []);
        var second = await selector.SelectNextAsync(agents, []);
        var third = await selector.SelectNextAsync(agents, []);
        var fourth = await selector.SelectNextAsync(agents, []); // 循环回到 A

        Assert.Equal("A", first.Name);
        Assert.Equal("B", second.Name);
        Assert.Equal("C", third.Name);
        Assert.Equal("A", fourth.Name);
    }

    [Fact]
    [DisplayName("RoundRobinSelector—Reset 后从第一个代理重新开始")]
    public async Task RoundRobinSelector_Reset_StartsOver()
    {
        var selector = new RoundRobinSelector();
        var agents = new List<IAgent> { new FakeAgent("X"), new FakeAgent("Y") };

        await selector.SelectNextAsync(agents, []); // X
        await selector.SelectNextAsync(agents, []); // Y
        selector.Reset();

        var afterReset = await selector.SelectNextAsync(agents, []);
        Assert.Equal("X", afterReset.Name);
    }

    [Fact]
    [DisplayName("RoundRobinSelector—空代理列表时抛 ArgumentException")]
    public async Task RoundRobinSelector_EmptyAgents_Throws()
    {
        var selector = new RoundRobinSelector();
        await Assert.ThrowsAsync<ArgumentException>(() => selector.SelectNextAsync([], []));
    }

    [Fact]
    [DisplayName("RoundRobinSelector—null 代理列表时抛 ArgumentException")]
    public async Task RoundRobinSelector_NullAgents_Throws()
    {
        var selector = new RoundRobinSelector();
        await Assert.ThrowsAsync<ArgumentException>(() => selector.SelectNextAsync(null!, []));
    }

    #endregion

    // ── GroupChat ────────────────────────────────────────────────────────────

    #region GroupChat

    [Fact]
    [DisplayName("GroupChat—空代理列表构造时抛 ArgumentException")]
    public void GroupChat_EmptyAgents_Throws()
    {
        Assert.Throws<ArgumentException>(() => new GroupChat([]));
    }

    [Fact]
    [DisplayName("GroupChat—null 代理列表构造时抛 ArgumentException")]
    public void GroupChat_NullAgents_Throws()
    {
        Assert.Throws<ArgumentException>(() => new GroupChat(null!));
    }

    [Fact]
    [DisplayName("GroupChat—未指定 Selector 时使用 RoundRobinSelector")]
    public void GroupChat_DefaultSelector_IsRoundRobin()
    {
        var gc = new GroupChat([new FakeAgent("A")]);
        Assert.IsType<RoundRobinSelector>(gc.Selector);
    }

    [Fact]
    [DisplayName("GroupChat—MaxRounds 默认值为 20")]
    public void GroupChat_MaxRounds_Default20()
    {
        var gc = new GroupChat([new FakeAgent("A")]);
        Assert.Equal(20, gc.MaxRounds);
    }

    [Fact]
    [DisplayName("GroupChat—MaxRounds 可设置")]
    public void GroupChat_MaxRounds_CanSet()
    {
        var gc = new GroupChat([new FakeAgent("A")]) { MaxRounds = 5 };
        Assert.Equal(5, gc.MaxRounds);
    }

    [Fact]
    [DisplayName("GroupChat—RunAsync 收到 StopMessage 时停止")]
    public async Task GroupChat_StopsOnStopMessage()
    {
        var gc = new GroupChat([new StoppingAgent()]);
        var messages = new List<AgentMessage>();
        var initial = new TextMessage { Source = "user", Content = "Start" };

        await foreach (var msg in gc.RunAsync(initial))
            messages.Add(msg);

        Assert.Contains(messages, m => m is StopMessage);
    }

    [Fact]
    [DisplayName("GroupChat—RunAsync 达到 MaxRounds 时停止")]
    public async Task GroupChat_StopsAtMaxRounds()
    {
        var gc = new GroupChat([new FakeAgent("A", "reply")]) { MaxRounds = 3 };
        var initial = new TextMessage { Source = "user", Content = "Go" };
        var count = 0;

        await foreach (var msg in gc.RunAsync(initial))
            count++;

        // 不应超过 MaxRounds 次代理响应
        Assert.True(count <= 3 + 1); // +1 因为初始消息也会被 yield
    }

    [Fact]
    [DisplayName("GroupChat—Agents 属性返回注册的代理列表")]
    public void GroupChat_Agents_ReturnsRegistered()
    {
        var agentA = new FakeAgent("A");
        var agentB = new FakeAgent("B");
        var gc = new GroupChat([agentA, agentB]);
        Assert.Contains(gc.Agents, a => a.Name == "A");
        Assert.Contains(gc.Agents, a => a.Name == "B");
    }

    #endregion

    // ── ConversableAgent ─────────────────────────────────────────────────────

    #region ConversableAgent

    /// <summary>固定返回指定文本的假 IChatClient</summary>
    private sealed class FakeChatClient : IChatClient
    {
        private readonly String _reply;
        public FakeChatClient(String reply = "answer") => _reply = reply;

        public Task<ChatResponse> GetResponseAsync(ChatRequest request, CancellationToken cancellationToken = default)
        {
            var resp = new ChatResponse();
            resp.Add(_reply);
            return Task.FromResult(resp);
        }

        public async IAsyncEnumerable<ChatResponse> GetStreamingResponseAsync(
            ChatRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return await GetResponseAsync(request, cancellationToken).ConfigureAwait(false);
        }

        public void Dispose() { }
    }

    [Fact]
    [DisplayName("ConversableAgent—空名称构造时抛 ArgumentNullException")]
    public void ConversableAgent_EmptyName_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new ConversableAgent("", new FakeChatClient()));
    }

    [Fact]
    [DisplayName("ConversableAgent—null chatClient 构造时抛 ArgumentNullException")]
    public void ConversableAgent_NullClient_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new ConversableAgent("agent", null!));
    }

    [Fact]
    [DisplayName("ConversableAgent—Name、SystemPrompt 属性正确")]
    public void ConversableAgent_Properties()
    {
        var a = new ConversableAgent("planner", new FakeChatClient(), "You are helpful") { MaxAutoReply = 3 };
        Assert.Equal("planner", a.Name);
        Assert.Equal("You are helpful", a.SystemPrompt);
        Assert.Equal(3, a.MaxAutoReply);
    }

    [Fact]
    [DisplayName("ConversableAgent—带描述的构造正确")]
    public void ConversableAgent_WithDescription()
    {
        var a = new ConversableAgent("writer", "写作助手", new FakeChatClient());
        Assert.Equal("writer", a.Name);
        Assert.Equal("写作助手", a.Description);
    }

    [Fact]
    [DisplayName("ConversableAgent—HandleAsync 返回助手文本消息")]
    public async Task ConversableAgent_HandleAsync_ReturnsAssistantText()
    {
        var client = new FakeChatClient("the answer");
        var agent = new ConversableAgent("qa", client);
        var history = new List<AgentMessage>
        {
            new TextMessage { Source = "user", Content = "what?", Role = "user" }
        };

        var results = new List<AgentMessage>();
        await foreach (var msg in agent.HandleAsync(history))
            results.Add(msg);

        Assert.True(results.Count > 0);
        var textMsg = results.Find(m => m is TextMessage t && !String.IsNullOrEmpty(t.Content));
        Assert.NotNull(textMsg);
        Assert.Equal("the answer", ((TextMessage)textMsg!).Content);
    }

    #endregion

    // ── AgentAsTool ──────────────────────────────────────────────────────────

    #region AgentAsTool

    [Fact]
    [DisplayName("AgentAsTool.Register—null registry 时抛 ArgumentNullException")]
    public void AgentAsTool_Register_NullRegistry_Throws()
    {
        var agent = new FakeAgent("A");
        Assert.Throws<ArgumentNullException>(() => AgentAsTool.Register(null!, agent));
    }

    [Fact]
    [DisplayName("AgentAsTool.Register—null agent 时抛 ArgumentNullException")]
    public void AgentAsTool_Register_NullAgent_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => AgentAsTool.Register(new ToolRegistry(), null!));
    }

    [Fact]
    [DisplayName("AgentAsTool.Register—注册后 ToolRegistry 包含对应工具")]
    public void AgentAsTool_Register_AddsToolToRegistry()
    {
        var registry = new ToolRegistry();
        var agent = new FakeAgent("TranslateAgent", description: "翻译代理");
        AgentAsTool.Register(registry, agent);

        var tools = registry.Tools;
        Assert.Single(tools);
        Assert.Equal("translate_agent", tools[0].Function!.Name);
    }

    [Fact]
    [DisplayName("AgentAsTool.Register—自定义工具名覆盖自动生成名")]
    public void AgentAsTool_Register_CustomToolName()
    {
        var registry = new ToolRegistry();
        var agent = new FakeAgent("MyAgent");
        AgentAsTool.Register(registry, agent, toolName: "custom_name");

        var tools = registry.Tools;
        Assert.Equal("custom_name", tools[0].Function!.Name);
    }

    [Fact]
    [DisplayName("AgentAsTool.CreateToolDefinition—null agent 时抛 ArgumentNullException")]
    public void AgentAsTool_CreateToolDefinition_NullAgent_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => AgentAsTool.CreateToolDefinition(null!));
    }

    [Fact]
    [DisplayName("AgentAsTool.CreateToolDefinition—返回包含 message 参数的 ChatTool")]
    public void AgentAsTool_CreateToolDefinition_HasMessageParam()
    {
        var agent = new FakeAgent("SummaryAgent", description: "总结代理");
        var tool = AgentAsTool.CreateToolDefinition(agent);

        Assert.NotNull(tool.Function);
        Assert.Equal("summary_agent", tool.Function!.Name);
        Assert.Equal("总结代理", tool.Function.Description);
        Assert.NotNull(tool.Function.Parameters);
    }

    [Fact]
    [DisplayName("AgentAsTool—Agent 名称 snake_case 转换正确")]
    public void AgentAsTool_SnakeCaseConversion()
    {
        // "WeatherAgent" -> "weather_agent"
        var agent = new FakeAgent("WeatherAgent");
        var tool = AgentAsTool.CreateToolDefinition(agent);
        Assert.Equal("weather_agent", tool.Function!.Name);
    }

    #endregion
}
