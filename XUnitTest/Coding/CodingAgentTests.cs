using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NewLife.AI.Clients;
using NewLife.AI.Coding;
using NewLife.AI.Coding.Models;
using NewLife.AI.Models;
using Xunit;

namespace XUnitTest.Coding;

/// <summary>编程智能体（CodingAgent）单元测试。用假客户端驱动规划与管道流程，验证 JSON 解析与异常降级</summary>
[DisplayName("CodingAgent 单元测试")]
public class CodingAgentTests : IDisposable
{
    private readonly String _tempDir;

    public CodingAgentTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "coding_agent_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true); }
        catch { }
    }

    // 假客户端：按调用顺序出队预设响应，用尽后返回空文本
    private sealed class ScriptedChatClient : IChatClient
    {
        private readonly Queue<String> _responses;

        public ScriptedChatClient(params String[] responses) => _responses = new Queue<String>(responses);

        public Task<IChatResponse> GetResponseAsync(IChatRequest request, CancellationToken cancellationToken = default)
        {
            var text = _responses.Count > 0 ? _responses.Dequeue() : "";
            var resp = new ChatResponse();
            resp.Add(text);
            return Task.FromResult<IChatResponse>(resp);
        }

        public IAsyncEnumerable<IChatResponse> GetStreamingResponseAsync(IChatRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public void Dispose() { }
    }

    private CodingAgent CreateAgent(IChatClient client) => new(client, new CodingTools(_tempDir), _tempDir);

    [Fact]
    [DisplayName("PlanAsync—解析假客户端返回的 JSON 规划")]
    public async Task PlanAsync_ParsesPlan()
    {
        var json = """{"summary":"登录功能","tasks":[{"id":"F001","description":"实现登录接口","dependencies":[],"acceptanceCriteria":["可调用"],"estimatedComplexity":"Medium"}]}""";
        var agent = CreateAgent(new ScriptedChatClient(json));

        var plan = await agent.PlanAsync("实现登录");

        Assert.Single(plan.Tasks);
        Assert.Equal("F001", plan.Tasks[0].Id);
        Assert.Equal("实现登录接口", plan.Tasks[0].Description);
        Assert.Equal("Medium", plan.Tasks[0].EstimatedComplexity);
    }

    [Fact]
    [DisplayName("PlanAsync—响应含 ```json 代码块时提取并解析")]
    public async Task PlanAsync_JsonBlock_Parses()
    {
        var json = """
```json
{"tasks":[{"id":"T001","description":"分析需求","taskType":"Analysis"}]}
```
""";
        var agent = CreateAgent(new ScriptedChatClient(json));

        var plan = await agent.PlanAsync("分析需求");

        Assert.Single(plan.Tasks);
        Assert.Equal("T001", plan.Tasks[0].Id);
        Assert.Equal(CodingTaskType.Analysis, plan.Tasks[0].TaskType);
    }

    [Fact]
    [DisplayName("PlanAsync—响应非法文本时不抛异常返回空规划")]
    public async Task PlanAsync_InvalidResponse_NoThrow()
    {
        var agent = CreateAgent(new ScriptedChatClient("这不是 JSON"));

        var plan = await agent.PlanAsync("实现登录");

        Assert.NotNull(plan);
    }

    [Fact]
    [DisplayName("RunAsync—规划为空时管道提前终止无错误")]
    public async Task RunAsync_EmptyPlan_TerminatesEarly()
    {
        var agent = CreateAgent(new ScriptedChatClient(""));
        var report = await agent.RunAsync("实现登录");

        Assert.NotNull(report);
        Assert.Null(report.Error);
        Assert.Empty(report.TaskResults);
        Assert.False(report.AllPassed);
    }

    [Fact]
    [DisplayName("RunAsync—需求为空抛 ArgumentNullException")]
    public async Task RunAsync_EmptyRequirement_Throws()
    {
        var agent = CreateAgent(new ScriptedChatClient());
        await Assert.ThrowsAsync<ArgumentNullException>(() => agent.RunAsync("  "));
    }

    [Fact]
    [DisplayName("RunAsync—规划异常时优雅降级，报告无致命错误")]
    public async Task RunAsync_PlanThrows_GracefulDegradation()
    {
        // 抛出异常的假客户端：驱动 PlanAsync 异常→降级路径（返回空任务规划）
        var throwing = new ThrowingChatClient();
        var agent = CreateAgent(throwing);

        var report = await agent.RunAsync("实现登录");

        Assert.NotNull(report);
        Assert.Null(report.Error);
        Assert.NotNull(report.Plan);
        Assert.Contains("降级规划也失败", report.Plan!.Summary ?? "");
    }

    private sealed class ThrowingChatClient : IChatClient
    {
        public Task<IChatResponse> GetResponseAsync(IChatRequest request, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("模拟调用失败");

        public IAsyncEnumerable<IChatResponse> GetStreamingResponseAsync(IChatRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public void Dispose() { }
    }
}
