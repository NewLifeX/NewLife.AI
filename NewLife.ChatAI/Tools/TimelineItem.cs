using System.ComponentModel;

namespace NewLife.ChatAI.Tools;

/// <summary>时间轴事件数据描述。供 <see cref="TimelineToolService"/> 的类型化参数使用，避免 LLM 手工转义 JSON 字符串</summary>
public class TimelineItem
{
    /// <summary>事件日期（必填），任意格式，如 "2024-01"、"2024年Q3"、"第一阶段"</summary>
    [Description("事件日期（必填），任意格式，如 \"2024-01\"、\"2024年Q3\"、\"第一阶段\"")]
    public String Date { get; set; } = String.Empty;

    /// <summary>事件标题（必填，≤ 30 字）</summary>
    [Description("事件标题（必填，≤ 30 字）")]
    public String Title { get; set; } = String.Empty;

    /// <summary>补充说明（可选，≤ 80 字）</summary>
    [Description("补充说明（可选，≤ 80 字）")]
    public String? Description { get; set; }

    /// <summary>十六进制颜色（可选），如 "#5470c6"；不填则自动轮转配色</summary>
    [Description("十六进制颜色（可选），如 \"#5470c6\"；不填则自动轮转配色")]
    public String? Color { get; set; }

    /// <summary>事件类别标签（可选），如 "里程碑"、"发布"、"问题"</summary>
    [Description("事件类别标签（可选），如 \"里程碑\"、\"发布\"、\"问题\"")]
    public String? Category { get; set; }
}
