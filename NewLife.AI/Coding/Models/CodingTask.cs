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

    /// <summary>任务类型：Modification（修改代码）或 Analysis（分析/报告）</summary>
    public CodingTaskType TaskType { get; set; } = CodingTaskType.Modification;

    /// <summary>任务状态</summary>
    public CodingTaskStatus Status { get; set; } = CodingTaskStatus.Pending;

    /// <summary>任务执行结果或备注信息</summary>
    public override String ToString() => $"[{Id}]{Description}";
}

/// <summary>编码任务类型。规划阶段根据需求性质分类，后续管道据此选择策略</summary>
public enum CodingTaskType
{
    /// <summary>修改代码：需要写文件、编译验证、代码审查</summary>
    Modification = 0,

    /// <summary>分析报告：只读文件、输出分析结果，不写文件、不编译</summary>
    Analysis = 1,
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
