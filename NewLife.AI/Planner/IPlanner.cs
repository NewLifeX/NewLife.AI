using NewLife.AI.Clients;
using NewLife.AI.Models;

namespace NewLife.AI.Planner;

/// <summary>规划器接口。根据目标与可用工具列表生成执行计划</summary>
/// <remarks>
/// 规划流程：
/// <list type="number">
/// <item>将目标与工具描述发送给 LLM</item>
/// <item>LLM 返回工具调用序列作为计划步骤</item>
/// <item>调用方使用 IPlan.ExecuteAsync 逐步执行</item>
/// </list>
/// </remarks>
public interface IPlanner
{
    /// <summary>根据目标和可用工具生成执行计划</summary>
    /// <param name="goal">用户意图/任务目标文本</param>
    /// <param name="tools">可用工具定义列表</param>
    /// <param name="chatClient">用于规划的 IChatClient 实例</param>
    /// <param name="options">请求参数（可覆盖 Temperature 等）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>包含步骤列表的执行计划</returns>
    Task<IPlan> CreatePlanAsync(
        String goal,
        IList<ChatTool> tools,
        IChatClient chatClient,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default);
}
