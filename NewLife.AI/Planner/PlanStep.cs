namespace NewLife.AI.Planner;

/// <summary>计划步骤状态</summary>
public enum PlanStepStatus
{
    /// <summary>等待执行</summary>
    Pending,

    /// <summary>执行中</summary>
    Running,

    /// <summary>已完成</summary>
    Completed,

    /// <summary>执行失败</summary>
    Failed,

    /// <summary>已跳过</summary>
    Skipped,
}

/// <summary>计划步骤。代表规划器生成的一个原子执行动作（工具调用）</summary>
public class PlanStep
{
    /// <summary>步骤序号（从 0 开始）</summary>
    public Int32 Index { get; set; }

    /// <summary>工具/函数名称</summary>
    public String ToolName { get; set; } = String.Empty;

    /// <summary>调用参数（JSON 字符串）</summary>
    public String? Arguments { get; set; }

    /// <summary>执行结果（JSON 字符串或纯文本）</summary>
    public String? Result { get; set; }

    /// <summary>步骤状态</summary>
    public PlanStepStatus Status { get; set; } = PlanStepStatus.Pending;

    /// <summary>错误信息（Status=Failed 时填充）</summary>
    public String? ErrorMessage { get; set; }
}
