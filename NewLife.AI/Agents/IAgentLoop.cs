namespace NewLife.AI.Agents;

/// <summary>Agent 执行循环抽象。借鉴 GenericAgent ~100 行循环，提供工作便签注入和完成钩子机制</summary>
/// <remarks>
/// 核心设计：极简正交——只做"调用 LLM → 分发工具 → 注入便签 → 检查完成"四件事。
/// 工作便签（working_checkpoint）每轮自动注入 user message 顶部，防止长任务上下文遗忘。
/// 完成钩子（DoneHooks）在 AI 声称完成时出队一个验证提示，强制进一步检查。
/// 失败升级由 <see cref="IAgentHook"/> 实现决定，连续失败时注入 ask_user 提示。
/// </remarks>
public interface IAgentLoop
{
    /// <summary>运行单次任务，流式返回中间步骤</summary>
    /// <param name="systemPrompt">系统提示词</param>
    /// <param name="userInput">用户输入</param>
    /// <param name="hook">回合钩子（工作便签注入、完成验证、失败升级）</param>
    /// <param name="maxTurns">最大回合数。默认 40</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>步骤异步序列，每步含本轮文本与工具调用情况</returns>
    IAsyncEnumerable<AgentStep> RunAsync(String systemPrompt, String userInput, IAgentHook hook, Int32 maxTurns = 40, CancellationToken cancellationToken = default);
}

/// <summary>Agent 回合钩子。控制每轮的副作用（便签注入、完成验证、失败升级）</summary>
/// <remarks>
/// 实现方可在 <see cref="BeforeToolAsync"/> / <see cref="AfterToolAsync"/> 中记录工具执行情况，
/// 在 <see cref="TurnEndAsync"/> 中将工作便签、失败警告拼入下一轮 user prompt。
/// </remarks>
public interface IAgentHook
{
    /// <summary>工具调用前回调</summary>
    /// <param name="toolName">工具名</param>
    /// <param name="argumentsJson">调用参数 JSON</param>
    ValueTask BeforeToolAsync(String toolName, String argumentsJson);

    /// <summary>工具调用后回调</summary>
    /// <param name="toolName">工具名</param>
    /// <param name="argumentsJson">调用参数 JSON</param>
    /// <param name="result">工具返回结果（JSON 字符串）</param>
    ValueTask AfterToolAsync(String toolName, String argumentsJson, Object? result);

    /// <summary>回合结束回调。可将工作便签、失败警告等前置到下一轮 user prompt</summary>
    /// <param name="turn">当前回合数（从 1 开始）</param>
    /// <param name="nextPrompt">基础下一轮提示</param>
    /// <param name="exitReason">退出原因（null 表示继续）</param>
    /// <returns>最终下一轮 user prompt（可能被改写）</returns>
    ValueTask<String> TurnEndAsync(Int32 turn, String nextPrompt, ExitReason? exitReason);

    /// <summary>任务完成钩子队列。AI 未调用工具时出队一条验证提示强制继续检查，队空则正式退出</summary>
    Queue<String> DoneHooks { get; }
}

/// <summary>单步执行快照</summary>
public class AgentStep
{
    /// <summary>回合序号（从 1 开始）</summary>
    public Int32 Turn { get; set; }

    /// <summary>本轮 LLM 响应文本</summary>
    public String? Text { get; set; }

    /// <summary>本轮调用的工具名列表（已去重）</summary>
    public IList<String> ToolNames { get; set; } = [];

    /// <summary>退出原因。null 表示继续，非 null 表示本步是最后一步</summary>
    public ExitReason? ExitReason { get; set; }
}

/// <summary>Agent 循环退出原因</summary>
public enum ExitReason
{
    /// <summary>任务自然完成（AI 未调用工具且完成钩子队列为空）</summary>
    CurrentTaskDone,

    /// <summary>工具或 Hook 主动请求退出（如 ask_user 超时）</summary>
    Exited,

    /// <summary>达到最大回合数限制</summary>
    MaxTurnsExceeded,
}
