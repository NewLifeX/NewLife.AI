using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NewLife.AI.Models;
using NewLife.AI.Planner;
using NewLife.AI.Providers;
using Xunit;

namespace XUnitTest.Planner;

[DisplayName("规划器测试")]
public class PlannerTests
{
    // ── 假客户端：返回固定 tool_calls 响应 ──────────────────────────────────────

    private sealed class ToolCallingFakeClient : IChatClient
    {
        private readonly IList<ToolCall> _toolCalls;

        public ToolCallingFakeClient(IList<ToolCall> toolCalls) => _toolCalls = toolCalls;

        public Task<ChatResponse> GetResponseAsync(ChatRequest request, CancellationToken cancellationToken = default)
        {
            var resp = new ChatResponse
            {
                Messages =
                [
                    new ChatChoice
                    {
                        Message = new ChatMessage
                        {
                            Role = "assistant",
                            Content = null,
                            ToolCalls = _toolCalls,
                        }
                    }
                ]
            };
            return Task.FromResult(resp);
        }

        public IAsyncEnumerable<ChatResponse> GetStreamingResponseAsync(ChatRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public void Dispose() { }
    }

    // ── 测试 ──────────────────────────────────────────────────────────────────

    [Fact]
    [DisplayName("工具为空时返回空步骤计划")]
    public async Task Planner_EmptyTools_ReturnsEmptyPlan()
    {
        var planner = new FunctionCallingPlanner();
        var fakeClient = new ToolCallingFakeClient([]);
        var plan = await planner.CreatePlanAsync("do something", [], fakeClient);

        Assert.NotNull(plan);
        Assert.Empty(plan.Steps);
        Assert.Equal("do something", plan.Goal);
    }

    [Fact]
    [DisplayName("LLM 返回 2 个 tool_call 时计划含 2 个步骤")]
    public async Task Planner_TwoToolCalls_CreatesTwoSteps()
    {
        var toolCalls = new List<ToolCall>
        {
            new() { Id = "tc1", Function = new FunctionCall { Name = "search", Arguments = "{\"q\":\"news\"}" } },
            new() { Id = "tc2", Function = new FunctionCall { Name = "summarize", Arguments = "{}" } },
        };

        var planner = new FunctionCallingPlanner();
        var fakeClient = new ToolCallingFakeClient(toolCalls);
        var tools = new List<ChatTool>
        {
            new() { Function = new FunctionDefinition { Name = "search", Description = "搜索" } },
            new() { Function = new FunctionDefinition { Name = "summarize", Description = "汇总" } },
        };

        var plan = await planner.CreatePlanAsync("搜索最新新闻并汇总", tools, fakeClient);

        Assert.Equal(2, plan.Steps.Count);
        Assert.Equal("search", plan.Steps[0].ToolName);
        Assert.Equal("{\"q\":\"news\"}", plan.Steps[0].Arguments);
        Assert.Equal("summarize", plan.Steps[1].ToolName);
        Assert.Equal(PlanStepStatus.Pending, plan.Steps[0].Status);
    }

    [Fact]
    [DisplayName("ExecuteAsync—所有步骤成功后状态为 Completed")]
    public async Task Plan_ExecuteAsync_AllStepsComplete()
    {
        var toolCalls = new List<ToolCall>
        {
            new() { Id = "t1", Function = new FunctionCall { Name = "greet", Arguments = "{}" } },
        };
        var planner = new FunctionCallingPlanner();
        var fakeClient = new ToolCallingFakeClient(toolCalls);
        var plan = await planner.CreatePlanAsync("say hello", [new() { Function = new FunctionDefinition { Name = "greet" } }], fakeClient);

        await plan.ExecuteAsync((toolName, args, ct) => Task.FromResult($"result of {toolName}"));

        Assert.Equal(PlanStatus.Completed, plan.Status);
        Assert.Equal("result of greet", plan.Steps[0].Result);
        Assert.Equal(PlanStepStatus.Completed, plan.Steps[0].Status);
    }

    [Fact]
    [DisplayName("ExecuteAsync—工具抛出异常时计划状态变为 Failed")]
    public async Task Plan_ExecuteAsync_ToolThrows_StatusFailed()
    {
        var toolCalls = new List<ToolCall>
        {
            new() { Id = "t1", Function = new FunctionCall { Name = "fail_tool" } },
        };
        var planner = new FunctionCallingPlanner();
        var fakeClient = new ToolCallingFakeClient(toolCalls);
        var plan = await planner.CreatePlanAsync("trigger failure", [new() { Function = new FunctionDefinition { Name = "fail_tool" } }], fakeClient);

        await plan.ExecuteAsync((_, _, _) => throw new InvalidOperationException("tool error"));

        Assert.Equal(PlanStatus.Failed, plan.Status);
        Assert.Equal(PlanStepStatus.Failed, plan.Steps[0].Status);
        Assert.Contains("tool error", plan.Steps[0].ErrorMessage);
    }

    [Fact]
    [DisplayName("步骤序号—从 0 开始顺序递增")]
    public async Task Planner_StepIndexes_AreSequential()
    {
        var toolCalls = new List<ToolCall>
        {
            new() { Id = "a", Function = new FunctionCall { Name = "step1" } },
            new() { Id = "b", Function = new FunctionCall { Name = "step2" } },
            new() { Id = "c", Function = new FunctionCall { Name = "step3" } },
        };

        var planner = new FunctionCallingPlanner();
        var fakeClient = new ToolCallingFakeClient(toolCalls);
        var plan = await planner.CreatePlanAsync("multi-step", [], fakeClient);

        // 空工具列表时返回空计划，使用真实 tool_calls 解析
        // 为了测试解析逻辑，直接调用内部创建
        // 此处通过非空工具列表触发解析路径
        var tools = new List<ChatTool>
        {
            new() { Function = new FunctionDefinition { Name = "step1" } },
        };
        var plan2 = await planner.CreatePlanAsync("multi-step", tools, fakeClient);

        Assert.Equal(0, plan2.Steps[0].Index);
        Assert.Equal(1, plan2.Steps[1].Index);
        Assert.Equal(2, plan2.Steps[2].Index);
    }
}
