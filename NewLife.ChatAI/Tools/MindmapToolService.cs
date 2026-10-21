using System.ComponentModel;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using NewLife.AI.Tools;
using NewLife.Log;

namespace NewLife.ChatAI.Tools;

/// <summary>思维导图工具服务。AI 输出 Markdown 大纲，前端渲染可折叠思维导图树</summary>
/// <param name="log">日志</param>
public class MindmapToolService(ILog log)
{
    #region 工具方法

    /// <summary>将知识层级/概念关系渲染为可折叠的交互式思维导图</summary>
    /// <param name="title">思维导图标题（≤ 30 字），如「人工智能技术体系」</param>
    /// <param name="outline">
    /// Markdown 大纲格式，用 # / ## / ### 表示层级（最多三级）：
    /// # 中心主题
    /// ## 一级分支A
    /// ### 子节点1
    /// ### 子节点2
    /// ## 一级分支B
    /// ### 子节点3
    /// 每行格式：{# 前缀空格} {标题文字}，只需标题不需其他 Markdown 语法
    /// </param>
    /// <param name="branchColors">分支配色 JSON 数组，按顺序分配给各一级分支</param>
    /// <param name="collapsed">初始折叠的节点 ID 列表 JSON 数组</param>
    /// <param name="layout">布局模式：tree（默认缩进树）/ radial（左右放射）/ lr（中心向右）/ rl（中心向左）/ tb（中心向下）/ bt（中心向上）</param>
    /// <param name="maxDepth">最大可见深度</param>
    /// <param name="context">工具调用上下文（框架自动注入）</param>
    /// <returns>ToolResult（ForUser=完整Mindmap JSON + ForLlm=简短确认）</returns>
    [ToolDescription("show_mindmap", IsSystem = false,
        Triggers = "思维导图,脑图,知识树,知识图谱,结构图,大纲,层级结构,知识整理,归纳总结,概念图",
        AssistantTriggers = "思维导图,知识树,体系梳理,层级结构,概念图,归纳,脑图",
        ReadOnly = true)]
    [DisplayName("Mind Map")]
    [Description("Render knowledge hierarchies or concept relationships as a collapsible interactive mind map. The input is a Markdown outline (# / ## / ###) that the front end converts into a visual tree. Use it for knowledge organization, concept breakdown, technology comparison, option evaluation, and content summarization. Visual style: choose branchColors (3-8 coordinated colors), maxDepth (complexity), and collapsed (initially collapsed to focus) based on the topic. Use cool colors (blue/purple/cyan) for technology and data, warm colors (orange/gold/red) for business and marketing, and greens for nature and health. For dense nodes, set maxDepth=2 for readability. 6 layouts: tree (default indented tree, general purpose) / radial (left-right radial, best for knowledge maps) / lr (center to right, flowcharts/decision trees) / rl (center to left, backward tracing) / tb (center downward, org charts) / bt (center upward, bottom-up hierarchy).")]
    public ToolResult ShowMindmap(
        [Description("Mind map title (up to 30 characters), e.g. AI Technology Stack")] String title,
        [Description("Markdown outline using # / ## / ### for levels 1-3. Example:\n# Artificial Intelligence\n## Machine Learning\n### Supervised Learning\n### Unsupervised Learning\n## Deep Learning\n### CNN\n### RNN")] String outline,
        [Description("Layout mode: tree (default indented tree, general purpose) / radial (left-right radial, best for knowledge maps) / lr (center to right, flowcharts/decision trees) / rl (center to left, backward tracing) / tb (center downward, org charts) / bt (center upward, bottom-up hierarchy)")] String? layout = null,
        [Description("Branch color JSON array, e.g. [\"#3b82f6\",\"#10b981\",\"#f59e0b\"]. 3-8 hex colors assigned in order to the top-level branches. When omitted, the default blue/purple, green, yellow, red, sky blue, dark green, orange, and purple palette is used")] IList<String>? branchColors = null,
        [Description("JSON array of initially collapsed node IDs, e.g. [\"n2\",\"n5\"]. Empty expands everything. Use it to focus on key branches")] IList<String>? collapsed = null,
        [Description("Maximum visible depth: 1 = top-level branches only, 2 = two levels, unlimited by default. For dense nodes, set it to 2 for readability")] Int32? maxDepth = null,
        ToolCallContext? context = null)
    {
        if (outline.IsNullOrEmpty())
        {
            // 记录关键上下文便于排查"参数已生成但解析后为空"类问题
            log.Warn("[Mindmap] outline is empty! Title={0}, ToolCallId={1}",
                title, context?.ToolCallId);
            throw new ToolException("Invalid parameter: outline cannot be empty", "Rebuild the mindmap content in Markdown outline format (# / ## / ###) and try again, or reply to the user that the mindmap cannot be generated. Example: # Central topic\n## Branch\n### Child node");
        }

        // 防御：限制 outline 最大长度（约 8KB），过大的大纲会导致 JSON 解析/API 传输问题
        const Int32 maxOutlineBytes = 8192;
        var outlineByteCount = Encoding.UTF8.GetByteCount(outline);
        if (outlineByteCount > maxOutlineBytes)
        {
            log.Warn("[Mindmap] outline is too large and was truncated, original {0} bytes -> truncated to {1} bytes. Title={2}",
                outlineByteCount, maxOutlineBytes, title);
            // 按 UTF-8 字节边界安全截断，避免切割多字节字符
            var chars = outline.ToCharArray();
            var byteCount = 0;
            var cutIndex = 0;
            for (var i = 0; i < chars.Length; i++)
            {
                var charBytes = Encoding.UTF8.GetByteCount(chars, i, 1);
                if (byteCount + charBytes > maxOutlineBytes) break;
                byteCount += charBytes;
                cutIndex = i + 1;
            }
            outline = outline[..cutIndex];
        }

        var mindmapId = context?.ToolCallId;
        if (mindmapId.IsNullOrEmpty()) mindmapId = $"mm_{Guid.NewGuid():N}";

        var result = new JsonObject
        {
            ["mindmapId"] = mindmapId,
            ["title"] = title,
            ["content"] = outline,
        };

        // 将 branchColors 写入返回 JSON（前端 MindmapBlock 已解析该字段）
        if (branchColors is { Count: > 0 })
        {
            var node = new JsonArray();
            foreach (var c in branchColors)
            {
                if (!c.IsNullOrEmpty()) node.Add(c);
            }
            if (node.Count > 0) result["branchColors"] = node;
        }

        // 将 collapsed 写入返回 JSON
        if (collapsed is { Count: > 0 })
        {
            var node = new JsonArray();
            foreach (var c in collapsed)
            {
                if (!c.IsNullOrEmpty()) node.Add(c);
            }
            if (node.Count > 0) result["collapsed"] = node;
        }

        // 将 layout 写入返回 JSON
        if (!layout.IsNullOrEmpty() && layout is "tree" or "radial" or "lr" or "rl" or "tb" or "bt")
            result["layout"] = layout;

        // 将 maxDepth 写入返回 JSON
        if (maxDepth != null && maxDepth >= 1)
            result["maxDepth"] = maxDepth.Value;

        log.Info("[Mindmap] Rendering mindmap '{0}', id={1}, layout={2}", title, mindmapId, layout ?? "tree");

        // 从 JsonSerializerOptions.Default 派生以携带 TypeInfoResolver，避免 ToJsonString 内部
        // 对 JsonValueCustomized 节点调用 MakeReadOnly() 时抛 "must specify a TypeInfoResolver"
        var writeOptions = new JsonSerializerOptions(JsonSerializerOptions.Default) { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        var resultJson = result.ToJsonString(writeOptions);
        return ToolResult.ForAudiences(resultJson, $"[已渲染思维导图到客户端：{title}]");
    }

    #endregion
}
