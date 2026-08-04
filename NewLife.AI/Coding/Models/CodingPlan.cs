using NewLife.Serialization;

namespace NewLife.AI.Coding.Models;

/// <summary>编码规划结果。由 Plan 阶段产出，包含任务列表和规划摘要</summary>
public class CodingPlan
{
    /// <summary>用户原始需求</summary>
    public String Requirement { get; set; } = null!;

    /// <summary>拆解后的子任务列表</summary>
    public IList<CodingTask> Tasks { get; set; } = [];

    /// <summary>规划摘要，简述整体方案</summary>
    public String? Summary { get; set; }

    /// <summary>预估影响的文件列表</summary>
    public IList<String>? AffectedFiles { get; set; }

    /// <summary>规划时间戳</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>从 JSON 反序列化</summary>
    /// <param name="json">JSON 字符串，为 null 或空时返回空规划</param>
    /// <returns>反序列化的规划实例</returns>
    public static CodingPlan FromJson(String? json)
    {
        if (json.IsNullOrWhiteSpace()) return new CodingPlan();
        return json.ToJsonEntity<CodingPlan>() ?? new CodingPlan();
    }

    /// <summary>序列化为 JSON。显式调用静态扩展方法，避免与实例方法同名导致的无限递归</summary>
    /// <returns>JSON 字符串</returns>
    public String ToJson() => NewLife.Serialization.JsonHelper.ToJson(this);

    /// <summary>字符串表示，显示任务数量和摘要</summary>
    public override String ToString() => $"[{Tasks?.Count}] {Summary}";
}
