using NewLife.AI.Clients;
using NewLife.AI.Models;
using NewLife.Serialization;

namespace NewLife.AI.Agents;

/// <summary>单个执行步骤</summary>
public class PlanStep
{
    /// <summary>步骤序号（从 1 开始）</summary>
    public Int32 Index { get; set; }

    /// <summary>步骤描述</summary>
    public String Description { get; set; } = String.Empty;

    /// <summary>执行函数名（对应工具名称）</summary>
    public String? FunctionName { get; set; }

    /// <summary>函数参数（JSON 字符串）</summary>
    public String? Arguments { get; set; }

    /// <summary>执行状态</summary>
    public PlanStepStatus Status { get; set; } = PlanStepStatus.Pending;

    /// <summary>执行结果（成功时写入）</summary>
    public String? Result { get; set; }

    /// <summary>失败原因（失败时写入）</summary>
    public String? FailureReason { get; set; }
}

/// <summary>步骤执行状态</summary>
public enum PlanStepStatus
{
    /// <summary>待执行</summary>
    Pending,

    /// <summary>执行中</summary>
    Running,

    /// <summary>已完成</summary>
    Completed,

    /// <summary>已失败</summary>
    Failed,

    /// <summary>已跳过（重规划后被替代）</summary>
    Skipped,
}

/// <summary>计划执行结果</summary>
public class PlanResult
{
    /// <summary>是否完整成功</summary>
    public Boolean IsSuccess { get; set; }

    /// <summary>所有步骤列表（含重规划新增步骤）</summary>
    public List<PlanStep> Steps { get; set; } = [];

    /// <summary>最终汇总结果</summary>
    public String? Summary { get; set; }

    /// <summary>重规划次数</summary>
    public Int32 ReplanCount { get; set; }
}

/// <summary>函数调用规划器。使用 LLM 规划任务步骤，并在步骤失败时自动重规划剩余步骤</summary>
/// <remarks>
/// 执行流程：
/// <list type="number">
/// <item>调用规划 LLM，输出有序步骤列表（JSON）</item>
/// <item>逐步执行工具调用</item>
/// <item>步骤失败时，若剩余重规划次数 &gt; 0，将失败上下文重新发给 LLM 获取新的后续步骤</item>
/// <item>重规划超出次数后，终止并返回部分完成结果</item>
/// </list>
/// </remarks>
public class FunctionCallingPlanner
{
    #region 属性

    /// <summary>规划用 IChatClient。用于生成和重规划步骤</summary>
    public IChatClient PlannerClient { get; }

    /// <summary>可用工具列表（步骤执行时使用）</summary>
    public IList<ChatTool> Tools { get; set; } = [];

    /// <summary>最大重规划次数。0 表示不重规划</summary>
    public Int32 MaxReplanning { get; set; } = 2;

    /// <summary>规划器使用的模型参数（可覆盖 client 默认值）</summary>
    public ChatOptions? Options { get; set; }

    #endregion

    #region 提示词

    private static readonly String PlanSystemPrompt = """
        你是一个任务规划专家。将用户的任务分解为有序的、可通过函数调用执行的步骤列表。
        
        返回 JSON 数组，每个元素格式：
        {"index":1,"description":"步骤描述","functionName":"工具名称","arguments":"{\"参数\":\"值\"}"}
        
        规则：
        - 每个步骤必须对应一个可用工具
        - arguments 是 JSON 字符串（工具参数）
        - index 从 1 开始连续编号
        - 只返回 JSON 数组，不加任何说明
        """;

    private static readonly String ReplanSystemPrompt = """
        你是一个任务重规划专家。某个步骤执行失败了，请根据已完成的步骤结果和失败信息，重新规划剩余的步骤。
        
        返回 JSON 数组，格式同初始规划（每个步骤含 index/description/functionName/arguments）。
        index 从失败步骤的下一个编号开始。只返回 JSON 数组，不加任何说明。
        """;

    #endregion

    #region 构造

    /// <summary>初始化函数调用规划器</summary>
    /// <param name="plannerClient">规划用 LLM 客户端</param>
    public FunctionCallingPlanner(IChatClient plannerClient)
    {
        PlannerClient = plannerClient ?? throw new ArgumentNullException(nameof(plannerClient));
    }

    #endregion

    #region 规划与执行

    /// <summary>规划并执行任务</summary>
    /// <param name="goal">任务目标描述</param>
    /// <param name="toolExecutor">工具调用执行器。接收 (functionName, arguments) 返回工具结果字符串</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>执行结果</returns>
    public async Task<PlanResult> RunAsync(
        String goal,
        Func<String, String, Task<String>> toolExecutor,
        CancellationToken cancellationToken = default)
    {
        if (goal == null) throw new ArgumentNullException(nameof(goal));
        if (toolExecutor == null) throw new ArgumentNullException(nameof(toolExecutor));

        var result = new PlanResult();

        // 第一次规划
        var steps = await PlanAsync(goal, null, null, cancellationToken).ConfigureAwait(false);
        if (steps.Count == 0)
        {
            result.Summary = "规划结果为空，无法执行";
            return result;
        }

        result.Steps.AddRange(steps);

        var completedSummary = new System.Text.StringBuilder();

        foreach (var step in result.Steps)
        {
            if (cancellationToken.IsCancellationRequested) break;
            if (step.Status == PlanStepStatus.Skipped) continue;

            step.Status = PlanStepStatus.Running;
            try
            {
                var toolResult = await toolExecutor(step.FunctionName ?? String.Empty, step.Arguments ?? "{}").ConfigureAwait(false);
                step.Status = PlanStepStatus.Completed;
                step.Result = toolResult;
                completedSummary.AppendLine($"步骤{step.Index}: {step.Description} → {toolResult}");
            }
            catch (Exception ex)
            {
                step.Status = PlanStepStatus.Failed;
                step.FailureReason = ex.Message;

                if (result.ReplanCount >= MaxReplanning)
                {
                    // 超出重规划次数，终止
                    result.Summary = $"步骤{step.Index}失败且已达最大重规划次数({MaxReplanning})，中止执行。失败原因：{ex.Message}";
                    return result;
                }

                // 触发重规划
                result.ReplanCount++;
                var replanContext = BuildReplanContext(completedSummary.ToString(), step);
                var remainingSteps = await PlanAsync(goal, replanContext, step.Index, cancellationToken).ConfigureAwait(false);

                if (remainingSteps.Count == 0) break;

                // 标记后续原步骤为 Skipped，追加新步骤
                var currentIndex = result.Steps.IndexOf(step);
                for (var i = currentIndex + 1; i < result.Steps.Count; i++)
                    result.Steps[i].Status = PlanStepStatus.Skipped;

                foreach (var newStep in remainingSteps)
                    result.Steps.Add(newStep);

                break; // 退出当前循环，foreach 会自动遍历新追加的步骤
            }
        }

        // 第二次遍历新追加的步骤（重规划后追加的步骤在原 foreach 中已退出）
        var needSecondPass = result.Steps.Any(s => s.Status == PlanStepStatus.Pending);
        if (needSecondPass)
        {
            foreach (var step in result.Steps.Where(s => s.Status == PlanStepStatus.Pending))
            {
                if (cancellationToken.IsCancellationRequested) break;
                step.Status = PlanStepStatus.Running;
                try
                {
                    var toolResult = await toolExecutor(step.FunctionName ?? String.Empty, step.Arguments ?? "{}").ConfigureAwait(false);
                    step.Status = PlanStepStatus.Completed;
                    step.Result = toolResult;
                    completedSummary.AppendLine($"步骤{step.Index}(重规划): {step.Description} → {toolResult}");
                }
                catch (Exception ex)
                {
                    step.Status = PlanStepStatus.Failed;
                    step.FailureReason = ex.Message;
                    break;
                }
            }
        }

        result.IsSuccess = result.Steps.All(s => s.Status is PlanStepStatus.Completed or PlanStepStatus.Skipped);
        result.Summary = completedSummary.ToString().Trim();
        return result;
    }

    #endregion

    #region 内部辅助

    private async Task<List<PlanStep>> PlanAsync(
        String goal,
        String? replanContext,
        Int32? startIndex,
        CancellationToken cancellationToken)
    {
        var userContent = replanContext == null
            ? $"任务目标：{goal}"
            : $"任务目标：{goal}\n\n{replanContext}";

        var messages = new List<ChatMessage>
        {
            new() { Role = "system", Content = replanContext == null ? PlanSystemPrompt : ReplanSystemPrompt },
            new() { Role = "user", Content = userContent },
        };

        var request = new ChatRequest
        {
            Messages = messages,
            Model = Options?.Model,
            Temperature = Options?.Temperature,
        };
        if (Tools.Count > 0) request.Tools = [.. Tools];

        var response = await PlannerClient.GetResponseAsync(request, cancellationToken).ConfigureAwait(false);
        var json = response?.Text?.Trim();
        if (String.IsNullOrEmpty(json)) return [];

        return ParseSteps(json, startIndex ?? 0);
    }

    private static List<PlanStep> ParseSteps(String json, Int32 baseIndex)
    {
        try
        {
            var raw = json.TrimStart('[').TrimEnd(']');
            // 使用 NewLife.Serialization 的 JSON 解析
            var list = json.ToJsonEntity<List<PlanStepRaw>>();
            if (list == null) return [];

            var result = new List<PlanStep>(list.Count);
            foreach (var item in list)
            {
                result.Add(new PlanStep
                {
                    Index = baseIndex > 0 ? baseIndex + item.Index : item.Index,
                    Description = item.Description ?? String.Empty,
                    FunctionName = item.FunctionName,
                    Arguments = item.Arguments,
                    Status = PlanStepStatus.Pending,
                });
            }
            return result;
        }
        catch
        {
            return [];
        }
    }

    private static String BuildReplanContext(String completedSummary, PlanStep failedStep) =>
        $"""
        已完成步骤：
        {completedSummary}

        失败步骤：步骤{failedStep.Index} [{failedStep.Description}]
        失败原因：{failedStep.FailureReason}

        请重新规划从步骤{failedStep.Index + 1}开始的后续步骤，使任务仍能完成。
        """;

    private class PlanStepRaw
    {
        public Int32 Index { get; set; }
        public String? Description { get; set; }
        public String? FunctionName { get; set; }
        public String? Arguments { get; set; }
    }

    #endregion
}
