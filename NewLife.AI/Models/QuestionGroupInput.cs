namespace NewLife.AI.Models;

/// <summary>ask_user 工具的问题组输入模型。AI 调用工具时 questions 参数 JSON 数组中每个元素的结构</summary>
public class QuestionGroupInput
{
    #region 属性
    /// <summary>问题组唯一标识，如 "q1"、"q2"</summary>
    public String Id { get; set; } = null!;

    /// <summary>问题描述（≤40 字，说明为什么需要用户在此组做出选择）</summary>
    public String Question { get; set; } = null!;

    /// <summary>候选选项列表</summary>
    public CheckpointOption[] Options { get; set; } = [];

    /// <summary>是否允许多选。true=用户可选多项后统一提交；false（默认）=单选</summary>
    public Boolean AllowMultiple { get; set; }
    #endregion
}
