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
    public static CodingPlan FromJson(String json) => json.ToJsonEntity<CodingPlan>() ?? new CodingPlan();

    /// <summary>序列化为 JSON</summary>
    public String ToJson() => this.ToJson();
}
