using System.ComponentModel;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using NewLife.AI.Tools;
using NewLife.Log;

namespace NewLife.ChatAI.Tools;

/// <summary>看板工具服务。AI 输出列/卡片 JSON，前端渲染多列任务看板，支持拖拽、筛选、折叠等交互</summary>
/// <param name="log">日志</param>
public class KanbanToolService(ILog log)
{
    /// <summary>类型化参数序列化选项：camelCase 字段名 + 忽略 null（与前端字段对齐，保持输出结构稳定）</summary>
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
    #region 工具方法

    /// <summary>将任务清单渲染为多列看板（如：待办 → 进行中 → 已完成）</summary>
    /// <param name="title">看板标题（≤ 30 字），如「Sprint 1 任务看板」</param>
    /// <param name="columns">
    /// 列定义 JSON 数组。列字段：
    /// - id（必填）：唯一标识，如 "todo"、"doing"、"done"
    /// - title（必填）：列标题（≤ 20 字）
    /// - color（可选）：列标题颜色，十六进制，如 "#3b82f6"
    /// - wipLimit（可选）：WIP 上限，超限时列标题变红
    /// - cards（必填）：卡片数组，每张卡片：
    ///   - id（必填）：唯一标识
    ///   - title（必填）：卡片标题（≤ 40 字）
    ///   - description（可选）：补充说明（≤ 100 字）
    ///   - priority（可选）：优先级，high | medium | low
    ///   - tags（可选）：标签字符串数组，如 ["设计","前端"]
    ///   - dueDate（可选）：截止日期，ISO 8601 格式如 "2026-07-15"
    ///   - assignee（可选）：负责人姓名
    ///   - checklist（可选）：子任务清单 JSON 数组 [{title, done}]
    ///   - progress（可选）：完成进度 0-100
    ///   - link（可选）：关联链接 URL
    /// </param>
    /// <param name="layout">布局模式：board（默认多列看板）/ swimlane（泳道视图，需同时提供 swimlanes 参数）</param>
    /// <param name="swimlanes">
    /// 泳道定义 JSON 数组（layout=swimlane 时使用）。
    /// 每项：id（唯一标识）、title（泳道标题）、columnIds（该泳道包含的列 ID 数组）。
    /// 示例：[{"id":"sl1","title":"前端团队","columnIds":["todo","doing"]}]
    /// </param>
    /// <param name="context">工具调用上下文（框架自动注入）</param>
    /// <returns>ToolResult（ForUser=完整Kanban JSON + ForLlm=简短确认）</returns>
    [ToolDescription("show_kanban", IsSystem = false,
        Triggers = "看板,任务板,任务分解,待办,Sprint,工作流,流程状态,任务状态,泳道,项目计划,任务清单",
        AssistantTriggers = "看板,任务分解,待办清单,工作流,Sprint,任务状态,泳道,任务规划",
        ReadOnly = true)]
    [DisplayName("任务看板")]
    [Description("将任务清单渲染为多列看板（如：待办 → 进行中 → 已完成）。适用场景：项目拆解、Sprint 规划、工作流展示、任务状态分类。每列可有颜色标记和 WIP 上限，每张卡片可含标题、描述、优先级、标签、截止日期、负责人、子任务清单、进度和链接。📐 2 种布局：board（默认多列看板）/ swimlane（泳道视图，按团队/模块分组列）。")]
    public ToolResult ShowKanban(
        [Description("看板标题（≤ 30 字），如「Sprint 1 任务看板」")] String title,
        [Description(@"列定义 JSON 数组。示例：[{""id"":""todo"",""title"":""待办"",""color"":""#94a3b8"",""wipLimit"":5,""cards"":[{""id"":""1"",""title"":""需求分析"",""description"":""整理用户需求"",""priority"":""high"",""tags"":[""设计""],""dueDate"":""2026-07-15"",""assignee"":""张三"",""checklist"":[{""title"":""访谈用户"",""done"":true}],""progress"":60,""link"":""https://example.com""}]}]。color/wipLimit/description/priority/tags/dueDate/assignee/checklist/progress/link 均可省略")] IList<KanbanColumn>? columns,
        [Description("布局模式：board（默认多列看板）/ swimlane（泳道视图，按团队/模块分组列，需同时提供 swimlanes 参数）")] String? layout = null,
        [Description(@"泳道定义 JSON 数组（layout=swimlane 时使用）。示例：[{""id"":""sl1"",""title"":""前端团队"",""columnIds"":[""todo"",""doing""]},{""id"":""sl2"",""title"":""后端团队"",""columnIds"":[""todo"",""doing""]}]")] IList<KanbanSwimlane>? swimlanes = null,
        ToolCallContext? context = null)
    {
        if (columns == null || columns.Count == 0)
            throw new ToolException("参数错误：columns 不能为空", "请提供列定义数组后重试，或直接回复用户说明无法生成看板。示例：[{\"id\":\"todo\",\"title\":\"待办\",\"cards\":[...]}]");

        var kanbanId = context?.ToolCallId;
        if (kanbanId.IsNullOrEmpty()) kanbanId = $"kb_{Guid.NewGuid():N}";

        var result = new JsonObject
        {
            ["kanbanId"] = kanbanId,
            ["title"] = title,
            ["columns"] = JsonSerializer.SerializeToNode(columns, _jsonOptions),
        };

        // 将 layout 写入返回 JSON
        if (!layout.IsNullOrEmpty() && layout is "board" or "swimlane")
            result["layout"] = layout;

        // 将 swimlanes 写入返回 JSON
        if (swimlanes is { Count: > 0 })
            result["swimlanes"] = JsonSerializer.SerializeToNode(swimlanes, _jsonOptions);

        log.Info("[Kanban] 渲染看板「{0}」，id={1}，layout={2}", title, kanbanId, layout ?? "board");

        var writeOptions = new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        var resultJson = result.ToJsonString(writeOptions);
        return ToolResult.ForAudiences(resultJson, $"[已渲染看板到客户端：{title}]");
    }

    #endregion
}
