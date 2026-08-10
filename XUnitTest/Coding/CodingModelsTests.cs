using System;
using System.ComponentModel;
using NewLife.AI.Coding.Models;
using NewLife.Serialization;
using Xunit;

namespace XUnitTest.Coding;

/// <summary>编码模型（CodingPlan/CodingTask/ReviewResult）序列化测试。重点防 A-12 ToJson 无限递归复发</summary>
[DisplayName("编码模型序列化测试")]
public class CodingModelsTests
{
    [Fact]
    [DisplayName("CodingPlan—ToJson/FromJson 往返保留字段")]
    public void CodingPlan_RoundTrip()
    {
        var plan = new CodingPlan
        {
            Requirement = "实现用户登录",
            Summary = "拆分为 2 个任务",
            Tasks =
            [
                new CodingTask { Id = "F001", Description = "实现登录接口", AcceptanceCriteria = ["可调用"], EstimatedComplexity = "Medium" },
                new CodingTask { Id = "F002", Description = "实现登录页面", Dependencies = ["F001"], AcceptanceCriteria = ["可填写"] },
            ],
            AffectedFiles = ["AuthController.cs", "Login.tsx"],
        };

        var json = plan.ToJson();
        Assert.False(String.IsNullOrWhiteSpace(json));

        var restored = CodingPlan.FromJson(json);

        Assert.Equal("实现用户登录", restored.Requirement);
        Assert.Equal(2, restored.Tasks.Count);
        Assert.Equal("F002", restored.Tasks[1].Id);
        Assert.Equal("F001", restored.Tasks[1].Dependencies[0]);
        Assert.Equal("可填写", restored.Tasks[1].AcceptanceCriteria[0]);
        Assert.Equal(2, restored.AffectedFiles!.Count);
        Assert.Equal("Medium", restored.Tasks[0].EstimatedComplexity);
    }

    [Fact]
    [DisplayName("CodingPlan—FromJson null/空/空白返回空规划不抛异常")]
    public void CodingPlan_FromJsonEmpty_ReturnsEmpty()
    {
        Assert.Empty(CodingPlan.FromJson(null).Tasks);
        Assert.Empty(CodingPlan.FromJson("").Tasks);
        Assert.Empty(CodingPlan.FromJson("  ").Tasks);
    }

    [Fact]
    [DisplayName("CodingPlan—FromJson 非法 JSON 不抛异常")]
    public void CodingPlan_FromJsonInvalid_NoThrow()
    {
        var plan = CodingPlan.FromJson("{ not valid json ");
        Assert.NotNull(plan);
    }

    [Fact]
    [DisplayName("CodingPlan—任务类型与状态枚举往返保留")]
    public void CodingPlan_TaskTypeAndStatusRoundTrip()
    {
        var plan = new CodingPlan
        {
            Requirement = "分析现状",
            Tasks =
            [
                new CodingTask { Id = "A001", Description = "分析", TaskType = CodingTaskType.Analysis, Status = CodingTaskStatus.Completed },
            ],
        };

        var restored = CodingPlan.FromJson(plan.ToJson());

        Assert.Equal(CodingTaskType.Analysis, restored.Tasks[0].TaskType);
        Assert.Equal(CodingTaskStatus.Completed, restored.Tasks[0].Status);
    }

    [Fact]
    [DisplayName("ReviewResult—ToJson/FromJson 往返保留字段")]
    public void ReviewResult_RoundTrip()
    {
        var result = new ReviewResult
        {
            Passed = true,
            Summary = "通过审查",
            Issues =
            [
                new ReviewIssue { Severity = "warning", File = "a.cs", Line = "10", Description = "命名不规范", Suggestion = "改为 PascalCase" },
            ],
        };

        var restored = result.ToJson().ToJsonEntity<ReviewResult>();

        Assert.NotNull(restored);
        Assert.True(restored!.Passed);
        Assert.Single(restored.Issues);
        Assert.Equal("warning", restored.Issues[0].Severity);
        Assert.Equal("a.cs", restored.Issues[0].File);
        Assert.Equal("改为 PascalCase", restored.Issues[0].Suggestion);
    }

    [Fact]
    [DisplayName("CodingPlan—ToString 显示任务数量与摘要")]
    public void CodingPlan_ToString()
    {
        var plan = new CodingPlan { Summary = "方案", Tasks = [new CodingTask { Id = "F001", Description = "任务" }] };
        Assert.Contains("1", plan.ToString());
        Assert.Contains("方案", plan.ToString());
    }
}
