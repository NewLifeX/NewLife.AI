using System.ComponentModel;

namespace NewLife.ChatAI.Tools;

/// <summary>看板列定义。供 <see cref="KanbanToolService"/> 的类型化参数使用，避免 LLM 手工转义 JSON 字符串</summary>
public class KanbanColumn
{
    /// <summary>列唯一标识（必填），如 "todo"、"doing"、"done"</summary>
    [Description("列唯一标识（必填），如 \"todo\"、\"doing\"、\"done\"")]
    public String Id { get; set; } = String.Empty;

    /// <summary>列标题（必填，≤ 20 字）</summary>
    [Description("列标题（必填，≤ 20 字）")]
    public String Title { get; set; } = String.Empty;

    /// <summary>列标题颜色（可选），十六进制，如 "#3b82f6"</summary>
    [Description("列标题颜色（可选），十六进制，如 \"#3b82f6\"")]
    public String? Color { get; set; }

    /// <summary>WIP 上限（可选），超限时列标题变红</summary>
    [Description("WIP 上限（可选），超限时列标题变红")]
    public Int32? WipLimit { get; set; }

    /// <summary>卡片数组（必填）</summary>
    [Description("卡片数组（必填）")]
    public List<KanbanCard>? Cards { get; set; }
}

/// <summary>看板卡片定义</summary>
public class KanbanCard
{
    /// <summary>卡片唯一标识（必填）</summary>
    [Description("卡片唯一标识（必填）")]
    public String Id { get; set; } = String.Empty;

    /// <summary>卡片标题（必填，≤ 40 字）</summary>
    [Description("卡片标题（必填，≤ 40 字）")]
    public String Title { get; set; } = String.Empty;

    /// <summary>补充说明（可选，≤ 100 字）</summary>
    [Description("补充说明（可选，≤ 100 字）")]
    public String? Description { get; set; }

    /// <summary>优先级（可选）：high | medium | low</summary>
    [Description("优先级（可选）：high | medium | low")]
    public String? Priority { get; set; }

    /// <summary>标签字符串数组（可选），如 ["设计","前端"]</summary>
    [Description("标签字符串数组（可选），如 [\"设计\",\"前端\"]")]
    public List<String>? Tags { get; set; }

    /// <summary>截止日期（可选），ISO 8601 格式如 "2026-07-15"</summary>
    [Description("截止日期（可选），ISO 8601 格式如 \"2026-07-15\"")]
    public String? DueDate { get; set; }

    /// <summary>负责人姓名（可选）</summary>
    [Description("负责人姓名（可选）")]
    public String? Assignee { get; set; }

    /// <summary>子任务清单（可选），每项含 title（标题）和 done（是否完成）</summary>
    [Description("子任务清单（可选），每项含 title（标题）和 done（是否完成）")]
    public List<KanbanCheckItem>? Checklist { get; set; }

    /// <summary>完成进度（可选）0-100</summary>
    [Description("完成进度（可选）0-100")]
    public Int32? Progress { get; set; }

    /// <summary>关联链接 URL（可选）</summary>
    [Description("关联链接 URL（可选）")]
    public String? Link { get; set; }
}

/// <summary>看板子任务清单项</summary>
public class KanbanCheckItem
{
    /// <summary>子任务标题</summary>
    [Description("子任务标题")]
    public String? Title { get; set; }

    /// <summary>是否已完成</summary>
    [Description("是否已完成")]
    public Boolean? Done { get; set; }
}

/// <summary>看板泳道定义（layout=swimlane 时使用）</summary>
public class KanbanSwimlane
{
    /// <summary>泳道唯一标识（必填）</summary>
    [Description("泳道唯一标识（必填）")]
    public String Id { get; set; } = String.Empty;

    /// <summary>泳道标题（必填）</summary>
    [Description("泳道标题（必填）")]
    public String Title { get; set; } = String.Empty;

    /// <summary>该泳道包含的列 ID 数组（必填）</summary>
    [Description("该泳道包含的列 ID 数组（必填）")]
    public List<String>? ColumnIds { get; set; }
}
