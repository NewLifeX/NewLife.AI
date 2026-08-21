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
    [Description("将时间序列数据渲染为美观的时间轴卡片。只需输出 JSON（约 100 tokens），前端即时渲染。适用场景：项目里程碑、历史事件、版本迭代计划、发展历程。每个事件可含日期、标题、描述、颜色和类别。🎨 视觉风格：请通过 palette 参数设置全局配色方案（如蓝紫渐变/暖棕古铜/品牌色系），让时间线有统一的情绪基调。各 item.color 可覆盖单独条目。推荐 density=relaxed 用于≤5 条精选事件，density=compact 用于 10+ 条密集时间线。📐 8 种布局：vertical（纵向，5-20 条通用）/ alternating-top（交替上，左右交替、时间倒序）/ alternating-bottom（交替下，左右交替、时间顺序）/ horizontal-left（横向左，时间倒序、箭头左）/ horizontal-right（横向右，时间顺序、箭头右）/ s-curve（S 形蜿蜒路径，≤8 条）/ fishbone-left（鱼骨左，时间倒序、箭头左）/ fishbone-right（鱼骨右，时间顺序、箭头右）。")]
    public ToolResult ShowTimeline(
        [Description("时间轴标题（≤ 30 字），如「产品发展里程碑」")] String title,
        [Description(@"时间轴事件 JSON 数组。示例：[{""date"":""2024-01"",""title"":""v1.0发布"",""description"":""基础功能上线"",""color"":""#5470c6"",""category"":""里程碑""}]。description/color/category 可省略")] IList<TimelineItem> items,
        [Description("布局模式：vertical（纵向）/ alternating-top（交替上，左右交替倒序）/ alternating-bottom（交替下，左右交替顺序）/ horizontal-left（横向左，倒序）/ horizontal-right（横向右，顺序）/ s-curve（S形）/ fishbone-left（鱼骨左，倒序）/ fishbone-right（鱼骨右，顺序）")] String? layout = null,
        [Description("全局配色方案 JSON 数组，如 [\"#3b82f6\",\"#10b981\",\"#f59e0b\"]。覆盖默认 8 色轮转。各 item.color 优先级更高")] IList<String>? palette = null,
        [Description("条目间距：compact（紧凑 12px）/ normal（默认 20px）/ relaxed（宽松 32px）。≤5 条精选事件推荐 relaxed，10+ 条推荐 compact")] String? density = null,
        ToolCallContext? context = null)
    {
        if (items == null || items.Count == 0)
            throw new ToolException("参数错误：items 不能为空", "请重新构建时间轴事件数组后重试，或直接回复用户说明无法生成时间轴。示例：[{\"date\":\"2024-01\",\"title\":\"v1.0发布\"}]");

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

        log.Info("[Timeline] 渲染时间轴「{0}」，id={1}，layout={2}", title, timelineId, layout ?? "vertical");

        // 从 JsonSerializerOptions.Default 派生以携带 TypeInfoResolver：
        // 若用裸 new JsonSerializerOptions，当节点树含 JsonValueCustomized 时 ToJsonString 内部
        // 会调用 options.MakeReadOnly() 并抛 "must specify a TypeInfoResolver" 异常（生产事故）
        var writeOptions = new JsonSerializerOptions(JsonSerializerOptions.Default) { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        var resultJson = result.ToJsonString(writeOptions);
        return ToolResult.ForAudiences(resultJson, $"[已渲染时间轴到客户端：{title}]");
    }

    #endregion
}
