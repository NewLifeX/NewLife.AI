using NewLife.AI.Models;
using NewLife.AI.Providers;

namespace NewLife.AI.Planner;

/// <summary>基于函数调用的执行计划实现</summary>
internal sealed class FunctionCallingPlan : IPlan
{
    private readonly Object _lock = new();

    /// <summary>规划目标</summary>
    public String Goal { get; }

    /// <summary>步骤列表</summary>
    public IList<PlanStep> Steps { get; }

    /// <summary>计划状态</summary>
    public PlanStatus Status { get; private set; } = PlanStatus.Pending;

    /// <summary>最终答案</summary>
    public String? FinalAnswer { get; private set; }

    internal FunctionCallingPlan(String goal, IList<PlanStep> steps)
    {
        Goal = goal;
        Steps = steps;
    }

    /// <summary>顺序执行所有步骤</summary>
    /// <param name="toolInvoker">工具调用委托</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task ExecuteAsync(Func<String, String?, CancellationToken, Task<String>> toolInvoker, CancellationToken cancellationToken = default)
    {
        if (toolInvoker == null) throw new ArgumentNullException(nameof(toolInvoker));

        Status = PlanStatus.Running;
        try
        {
            foreach (var step in Steps)
            {
                cancellationToken.ThrowIfCancellationRequested();
                step.Status = PlanStepStatus.Running;
                try
                {
                    step.Result = await toolInvoker(step.ToolName, step.Arguments, cancellationToken).ConfigureAwait(false);
                    step.Status = PlanStepStatus.Completed;
                }
                catch (Exception ex)
                {
                    step.Status = PlanStepStatus.Failed;
                    step.ErrorMessage = ex.Message;
                    Status = PlanStatus.Failed;
                    return;
                }
            }
            Status = PlanStatus.Completed;
        }
        catch (OperationCanceledException)
        {
            Status = PlanStatus.Failed;
            throw;
        }
    }

    internal void SetFinalAnswer(String answer) => FinalAnswer = answer;
}

/// <summary>基于函数调用的规划器。向 LLM 提交目标与工具描述，解析 tool_calls 作为计划步骤</summary>
/// <remarks>
/// 工作流：
/// <list type="number">
/// <item>将 goal 封装为 user 消息，将 tools 追加到请求</item>
/// <item>调用 chatClient.CompleteAsync 获取 LLM 响应</item>
/// <item>解析 tool_calls 数组转换为 PlanStep 列表</item>
/// <item>返回 FunctionCallingPlan，调用方可自行执行</item>
/// </list>
/// </remarks>
public class FunctionCallingPlanner : IPlanner
{
    /// <summary>系统提示词模板。可通过构造函数注入自定义提示</summary>
    private readonly String _systemPrompt;

    /// <summary>初始化规划器，使用默认系统提示</summary>
    public FunctionCallingPlanner() : this(DefaultSystemPrompt) { }

    /// <summary>初始化规划器</summary>
    /// <param name="systemPrompt">系统提示词，指导 LLM 如何选择并排列工具调用</param>
    public FunctionCallingPlanner(String systemPrompt)
    {
        if (String.IsNullOrWhiteSpace(systemPrompt)) throw new ArgumentNullException(nameof(systemPrompt));
        _systemPrompt = systemPrompt;
    }

    /// <summary>根据目标和工具创建执行计划</summary>
    /// <param name="goal">用户目标文本</param>
    /// <param name="tools">可用工具列表</param>
    /// <param name="chatClient">用于规划的客户端</param>
    /// <param name="options">可选请求参数（Temperature 等）</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<IPlan> CreatePlanAsync(
        String goal,
        IList<ChatTool> tools,
        IChatClient chatClient,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (String.IsNullOrEmpty(goal)) throw new ArgumentNullException(nameof(goal));
        if (chatClient == null) throw new ArgumentNullException(nameof(chatClient));
        if (tools == null || tools.Count == 0) return new FunctionCallingPlan(goal, []);

        var messages = new List<ChatMessage>
        {
            new() { Role = "system", Content = _systemPrompt },
            new() { Role = "user", Content = goal },
        };
        var chatOptions = new ChatOptions
        {
            Model = options?.Model,
            Temperature = options?.Temperature ?? 0,  // 规划阶段使用低温度以确保确定性
            Tools = tools,
            ToolChoice = "required",  // 强制使用工具，确保返回步骤
        };

        var response = await chatClient.GetResponseAsync(messages, chatOptions, cancellationToken).ConfigureAwait(false);
        var steps = ParseSteps(response);
        return new FunctionCallingPlan(goal, steps);
    }

    #region 辅助

    private static IList<PlanStep> ParseSteps(ChatResponse response)
    {
        var steps = new List<PlanStep>();
        if (response?.Messages == null) return steps;

        var idx = 0;
        foreach (var choice in response.Messages)
        {
            var toolCalls = choice?.Message?.ToolCalls;
            if (toolCalls == null) continue;
            foreach (var tc in toolCalls)
            {
                if (tc?.Function == null) continue;
                steps.Add(new PlanStep
                {
                    Index = idx++,
                    ToolName = tc.Function.Name ?? String.Empty,
                    Arguments = tc.Function.Arguments,
                });
            }
        }
        return steps;
    }

    private const String DefaultSystemPrompt =
        "You are a task planner. Given the user's goal and available tools, " +
        "respond ONLY with one or more tool_calls that collectively achieve the goal. " +
        "Do not include any explanatory text.";

    #endregion
}
