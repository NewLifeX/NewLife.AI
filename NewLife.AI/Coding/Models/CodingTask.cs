namespace NewLife.AI.Coding.Models;

/// <summary>单个编码任务。由规划阶段产出，包含验收条件和依赖关系</summary>
public class CodingTask
{
    /// <summary>任务唯一编号，如 F001</summary>
    public String Id { get; set; } = null!;

    /// <summary>任务描述</summary>
    public String Description { get; set; } = null!;

    /// <summary>前置依赖任务编号列表</summary>
    public IList<String> Dependencies { get; set; } = [];

    /// <summary>验收条件列表</summary>
    public IList<String> AcceptanceCriteria { get; set; } = [];

    /// <summary>预估需要修改的文件路径</summary>
    public IList<String>? FilesToModify { get; set; }

    /// <summary>预估复杂度：Low / Medium / High</summary>
    public String? EstimatedComplexity { get; set; }

    /// <summary>任务状态</summary>
    public CodingTaskStatus Status { get; set; } = CodingTaskStatus.Pending;
}

/// <summary>编码任务状态</summary>
public enum CodingTaskStatus
{
    /// <summary>待执行</summary>
    Pending = 0,

    /// <summary>执行中</summary>
    InProgress = 1,

    /// <summary>已完成</summary>
    Completed = 2,

    /// <summary>已跳过（依赖未满足）</summary>
    Skipped = 3,

    /// <summary>失败</summary>
    Failed = 4,
}
