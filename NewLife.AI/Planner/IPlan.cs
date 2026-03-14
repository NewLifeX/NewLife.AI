namespace NewLife.AI.Planner;

/// <summary>计划执行状态</summary>
public enum PlanStatus
{
    /// <summary>等待执行</summary>
    Pending,

    /// <summary>执行中</summary>
    Running,

    /// <summary>所有步骤成功完成</summary>
    Completed,

    /// <summary>某步骤失败导致计划终止</summary>
    Failed,
}

/// <summary>执行计划接口。包含目标和步骤列表，可驱动工具调用执行</summary>
public interface IPlan
{
    /// <summary>规划目标（原始用户意图文本）</summary>
    String Goal { get; }

    /// <summary>步骤列表。按执行顺序排列</summary>
    IList<PlanStep> Steps { get; }

    /// <summary>计划整体状态</summary>
    PlanStatus Status { get; }

    /// <summary>最终答案/汇总文本。所有步骤执行完成后由规划器填充</summary>
    String? FinalAnswer { get; }

    /// <summary>执行计划。逐步调用 toolInvoker 执行每个步骤</summary>
    /// <param name="toolInvoker">工具调用函数：(toolName, arguments) → result</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task ExecuteAsync(Func<String, String?, CancellationToken, Task<String>> toolInvoker, CancellationToken cancellationToken = default);
}
