namespace NewLife.AI.Coding.Models;

/// <summary>代码审查结果。由 Review 阶段产出</summary>
public class ReviewResult
{
    /// <summary>是否通过审查</summary>
    public Boolean Passed { get; set; }

    /// <summary>问题列表</summary>
    public IList<ReviewIssue> Issues { get; set; } = [];

    /// <summary>审查摘要</summary>
    public String? Summary { get; set; }

    /// <summary>审查时间戳</summary>
    public DateTime ReviewedAt { get; set; } = DateTime.Now;
}

/// <summary>审查发现的问题</summary>
public class ReviewIssue
{
    /// <summary>严重程度：error / warning / suggestion</summary>
    public String Severity { get; set; } = "warning";

    /// <summary>问题所在文件路径</summary>
    public String? File { get; set; }

    /// <summary>问题所在行号</summary>
    public String? Line { get; set; }

    /// <summary>问题描述</summary>
    public String Description { get; set; } = null!;

    /// <summary>修复建议</summary>
    public String? Suggestion { get; set; }
}
