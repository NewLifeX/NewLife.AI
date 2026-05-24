namespace NewLife.AI.Models;

/// <summary>决策检查点选项。用于 checkpoint_request 事件中的单条候选路径</summary>
public class CheckpointOption
{
    #region 属性
    /// <summary>选项唯一标识，如 "1"、"2"、"a"、"b"</summary>
    public String Id { get; set; } = null!;

    /// <summary>选项标签（简洁标题，≤30 字）</summary>
    public String Label { get; set; } = null!;

    /// <summary>选项描述（可选，补充说明该路径的重点或预期耗时）</summary>
    public String? Description { get; set; }
    #endregion
}
