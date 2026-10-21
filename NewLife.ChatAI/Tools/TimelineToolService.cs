using System.ComponentModel;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using NewLife.AI.Tools;
using NewLife.Log;

namespace NewLife.ChatAI.Tools;

/// <summary>时间轴工具服务。AI 输出紧凑 JSON（~100 tokens），前端渲染多种布局时间轴卡片</summary>
/// <param name="log">日志</param>
public class TimelineToolService(ILog log)
{
    #region 工具方法

    /// <summary>将时间序列数据渲染为时间轴。支持 5 种布局，适用于里程碑、历史事件、版本迭代、发展历程</summary>
    /// <param name="title">时间轴标题（≤ 30 字），如「产品发展里程碑」</param>
    /// <param name="items">时间轴事件数组。每项：date（必填，任意格式）、title（必填）、description/color/category（可选）</param>
    /// <param name="layout">布局模式：vertical（纵向）/ alternating-top（交替上）/ alternating-bottom（交替下）/ horizontal-left（横向左）/ horizontal-right（横向右）/ s-curve（S形）/ fishbone-left（鱼骨左）/ fishbone-right（鱼骨右）</param>
    /// <param name="palette">全局配色方案数组，如 ["#3b82f6","#10b981"]</param>
    /// <param name="density">条目间距：compact/normal/relaxed</param>
    /// <param name="context">工具调用上下文（框架自动注入）</param>
    /// <returns>ToolResult（ForUser=完整Timeline JSON + ForLlm=简短确认）</returns>
    [ToolDescription("show_timeline", IsSystem = false,
        Triggers = "时间轴,里程碑,时间线,进度,历史,发展历程,路线图,计划,迭代,版本历史,甘特",
        AssistantTriggers = "时间轴,里程碑,历史沿革,发展进程,版本迭代,路线图,进度规划",
        ReadOnly = true)]
    [DisplayName("时间轴")]
    [Description("Render time-series data as attractive timeline cards. Only JSON is needed (about 100 tokens) and the front end renders it instantly. Use it for project milestones, historical events, release plans, and development history. Each event can have a date, title, description, color, and category. Visual style: set a global palette through the palette parameter (e.g. blue-purple gradient, warm bronze, brand colors) so the timeline has a consistent mood; item.color overrides an individual entry. Use density=relaxed for 5 or fewer curated events and density=compact for 10+ dense timelines. 8 layouts: vertical (5-20 items) / alternating-top (alternating above, reverse chronological) / alternating-bottom (alternating below, chronological) / horizontal-left (reverse chronological, arrows left) / horizontal-right (chronological, arrows right) / s-curve (S-shaped path, 8 items or fewer) / fishbone-left (reverse chronological, arrows left) / fishbone-right (chronological, arrows right).")]
    public ToolResult ShowTimeline(
        [Description("Timeline title (up to 30 characters), e.g. Product Milestones")] String title,
        [Description(@"Timeline event JSON array. Example: [{""date"":""2024-01"",""title"":""v1.0 release"",""description"":""Core features launched"",""color"":""#5470c6"",""category"":""Milestone""}]. description/color/category are optional")] IList<TimelineItem> items,
        [Description("Layout mode: vertical / alternating-top (alternating above, reverse order) / alternating-bottom (alternating below, chronological) / horizontal-left (reverse chronological) / horizontal-right (chronological) / s-curve / fishbone-left (reverse chronological) / fishbone-right (chronological)")] String? layout = null,
        [Description("Global color scheme JSON array, e.g. [\"#3b82f6\",\"#10b981\",\"#f59e0b\"]. Overrides the default 8-color rotation. item.color takes precedence")] IList<String>? palette = null,
        [Description("Item spacing: compact (12px) / normal (20px, default) / relaxed (32px). relaxed suits 5 or fewer curated events; compact suits 10+")] String? density = null,
        ToolCallContext? context = null)
    {
        if (items == null || items.Count == 0)
            throw new ToolException("Invalid parameter: items cannot be empty", "Rebuild the timeline event array and try again, or reply to the user that the timeline cannot be generated. Example: [{\"date\":\"2024-01\",\"title\":\"v1.0 release\"}]");

        var timelineId = context?.ToolCallId;
        if (timelineId.IsNullOrEmpty()) timelineId = $"tl_{Guid.NewGuid():N}";

        // 组装 items JSON 数组（仅写入非空字段，保持与旧 String 参数版本一致的输出结构，前端 parseTimelineData 依赖）
        var itemsNode = new JsonArray();
        foreach (var item in items)
        {
            var jo = new JsonObject
            {
                ["date"] = item.Date,
                ["title"] = item.Title,
            };
            if (!item.Description.IsNullOrEmpty()) jo["description"] = item.Description;
            if (!item.Color.IsNullOrEmpty()) jo["color"] = item.Color;
            if (!item.Category.IsNullOrEmpty()) jo["category"] = item.Category;
            itemsNode.Add(jo);
        }

        var result = new JsonObject
        {
            ["timelineId"] = timelineId,
            ["title"] = title,
            ["items"] = itemsNode,
        };

        // 将 palette 写入返回 JSON（前端 parseTimelineData 已解析该字段）
        if (palette is { Count: > 0 })
        {
            var paletteNode = new JsonArray();
            foreach (var p in palette)
            {
                if (!p.IsNullOrEmpty()) paletteNode.Add(p);
            }
            if (paletteNode.Count > 0) result["palette"] = paletteNode;
        }

        // 将 layout 写入返回 JSON
        if (!layout.IsNullOrEmpty() && layout is "vertical" or "alternating-top" or "alternating-bottom" or "horizontal-left" or "horizontal-right" or "s-curve" or "fishbone-left" or "fishbone-right")
            result["layout"] = layout;

        // 将 density 写入返回 JSON
        if (!density.IsNullOrEmpty() && density is "compact" or "normal" or "relaxed")
            result["density"] = density;

        log.Info("[Timeline] Rendering timeline '{0}', id={1}, layout={2}", title, timelineId, layout ?? "vertical");

        // 从 JsonSerializerOptions.Default 派生以携带 TypeInfoResolver：
        // 若用裸 new JsonSerializerOptions，当节点树含 JsonValueCustomized 时 ToJsonString 内部
        // 会调用 options.MakeReadOnly() 并抛 "must specify a TypeInfoResolver" 异常（生产事故）
        var writeOptions = new JsonSerializerOptions(JsonSerializerOptions.Default) { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        var resultJson = result.ToJsonString(writeOptions);
        return ToolResult.ForAudiences(resultJson, $"[已渲染时间轴到客户端：{title}]");
    }

    #endregion
}
