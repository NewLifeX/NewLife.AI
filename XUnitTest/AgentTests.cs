using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NewLife.AI.Agents;
using NewLife.AI.Models;
using NewLife.AI.Providers;
using Xunit;

namespace XUnitTest;

[DisplayName("AgentChat 协议与 GroupChat 测试")]
public class AgentTests
{
    // ── 假 IChatClient ────────────────────────────────────────────────────────

    /// <summary>返回固定文本回应的假客户端</summary>
    private sealed class StaticReplyClient : IChatClient
    {
        private readonly String _reply;
        private readonly IList<ToolCall>? _toolCalls;

        public StaticReplyClient(String reply, IList<ToolCall>? toolCalls = null)
        {
            _reply = reply;
            _toolCalls = toolCalls;
        }

        public ChatClientMetadata Metadata { get; } = default!;

        public Task<ChatCompletionResponse> CompleteAsync(ChatCompletionRequest request, CancellationToken ct)
        {
            var resp = new ChatCompletionResponse
            {
                Choices =
                [
                    new ChatChoice
                    {
                        Message = new ChatMessage
                        {
                            Role = "assistant",
                            Content = _toolCalls != null ? null : _reply,
                            ToolCalls = _toolCalls,
                        }
                    }
                ]
            };
            return Task.FromResult(resp);
        }

        public IAsyncEnumerable<ChatCompletionResponse> CompleteStreamingAsync(ChatCompletionRequest r, CancellationToken ct)
            => throw new System.NotImplementedException();

        public void Dispose() { }
    }

    /// <summary>收到任意消息即返回 StopMessage 的 Agent</summary>
    private sealed class StopAgent : IAgent
    {
        public String Name => "stopper";
        public String? Description => null;

        public async IAsyncEnumerable<AgentMessage> HandleAsync(
            IList<AgentMessage> history,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            yield return new StopMessage { Source = Name, Reason = "done" };
        }
    }

    // ── AgentMessage 测试 ───────────────────────────────────────────────────

    [Fact]
    [DisplayName("TextMessage.ToChatMessage 返回 user/assistant ChatMessage")]
    public void TextMessage_ToChatMessage_CorrectRole()
    {
        var msg = new TextMessage { Source = "user", Role = "user", Content = "hello" };
        var cm = msg.ToChatMessage();
        Assert.NotNull(cm);
        Assert.Equal("user", cm.Role);
        Assert.Equal("hello", cm.Content?.ToString());
    }

    [Fact]
    [DisplayName("StopMessage.ToChatMessage 返回 null")]
    public void StopMessage_ToChatMessage_ReturnsNull()
    {
        var msg = new StopMessage { Source = "agent", Reason = "done" };
        Assert.Null(msg.ToChatMessage());
    }

    [Fact]
    [DisplayName("AgentMessageHelper.ToChatMessages 过滤掉 StopMessage")]
    public void AgentMessageHelper_FiltersStopMessages()
    {
        var messages = new List<AgentMessage>
        {
            new TextMessage { Source = "u", Content = "hi", Role = "user" },
            new StopMessage { Source = "a", Reason = "bye" },
            new SystemMessage { Source = "sys", Content = "system prompt" },
        };

        var chatMsgs = AgentMessageHelper.ToChatMessages(messages);
        // StopMessage 被过滤，剩 2 条
        Assert.Equal(2, chatMsgs.Count);
    }

    [Fact]
    [DisplayName("ToolCallMessage.ToChatMessage 包含 ToolCalls")]
    public void ToolCallMessage_ToChatMessage_HasToolCalls()
    {
        var msg = new ToolCallMessage { Source = "agent", ToolName = "search", Arguments = "{}", CallId = "id1" };
        var cm = msg.ToChatMessage();
        Assert.NotNull(cm);
        Assert.Equal("assistant", cm.Role);
        Assert.NotNull(cm.ToolCalls);
        Assert.Equal("search", cm.ToolCalls![0].Function!.Name);
    }

    // ── ConversableAgent 测试 ────────────────────────────────────────────────

    [Fact]
    [DisplayName("ConversableAgent—普通文本响应产出 TextMessage")]
    public async Task ConversableAgent_TextReply_ProducesTextMessage()
    {
        var agent = new ConversableAgent("bot", new StaticReplyClient("hi there"), "系统提示");
        var history = new List<AgentMessage>
        {
            new TextMessage { Source = "user", Content = "hello", Role = "user" }
        };

        var messages = new List<AgentMessage>();
        await foreach (var m in agent.HandleAsync(history))
            messages.Add(m);

        Assert.Single(messages);
        var textMsg = Assert.IsType<TextMessage>(messages[0]);
        Assert.Equal("hi there", textMsg.Content);
        Assert.Equal("bot", textMsg.Source);
    }

    [Fact]
    [DisplayName("ConversableAgent—工具调用响应产出 ToolCallMessage")]
    public async Task ConversableAgent_ToolCallReply_ProducesToolCallMessage()
    {
        var toolCalls = new List<ToolCall>
        {
            new() { Id = "tc1", Function = new FunctionCall { Name = "get_weather", Arguments = "{\"city\":\"Beijing\"}" } }
        };
        var agent = new ConversableAgent("bot", new StaticReplyClient("", toolCalls));

        var messages = new List<AgentMessage>();
        await foreach (var m in agent.HandleAsync([]))
            messages.Add(m);

        Assert.Single(messages);
        var tc = Assert.IsType<ToolCallMessage>(messages[0]);
        Assert.Equal("get_weather", tc.ToolName);
        Assert.Equal("{\"city\":\"Beijing\"}", tc.Arguments);
    }

    // ── GroupChat 测试 ───────────────────────────────────────────────────────

    [Fact]
    [DisplayName("GroupChat RoundRobin—StopMessage 终止循环")]
    public async Task GroupChat_StopMessage_Terminates()
    {
        var agent1 = new ConversableAgent("a1", new StaticReplyClient("reply from a1"));
        var stopAgent = new StopAgent();

        var chat = new GroupChat([agent1, stopAgent], new RoundRobinSelector(), maxRounds: 10);

        var all = new List<AgentMessage>();
        await foreach (var m in chat.RunAsync(new TextMessage { Source = "user", Content = "start" }))
            all.Add(m);

        // 应含：initial(user) + a1(TextMessage) + stopAgent(StopMessage)
        Assert.Contains(all, m => m.Type == AgentMessageType.Stop);
        Assert.Contains(all, m => m.Type == AgentMessageType.Text && m.Source == "a1");
    }

    [Fact]
    [DisplayName("GroupChat—MaxRounds 到达时自动停止")]
    public async Task GroupChat_MaxRounds_Stops()
    {
        var agent = new ConversableAgent("a", new StaticReplyClient("pong"));
        var chat = new GroupChat([agent], new RoundRobinSelector(), maxRounds: 3);

        var all = new List<AgentMessage>();
        await foreach (var m in chat.RunAsync(new TextMessage { Source = "user", Content = "ping" }))
            all.Add(m);

        // initial(1) + 3 rounds * 1 TextMessage = 4 messages total
        Assert.Equal(4, all.Count);
    }

    [Fact]
    [DisplayName("RoundRobinSelector—按顺序循环选择代理")]
    public async Task RoundRobinSelector_CyclesAgents()
    {
        var selector = new RoundRobinSelector();
        var agents = new List<IAgent>
        {
            new ConversableAgent("a", new StaticReplyClient("a")),
            new ConversableAgent("b", new StaticReplyClient("b")),
        };
        var history = new List<AgentMessage>();

        var first = await selector.SelectNextAsync(agents, history);
        var second = await selector.SelectNextAsync(agents, history);
        var third = await selector.SelectNextAsync(agents, history);

        Assert.Equal("a", first.Name);
        Assert.Equal("b", second.Name);
        Assert.Equal("a", third.Name);  // 循环回第一个
    }
}
