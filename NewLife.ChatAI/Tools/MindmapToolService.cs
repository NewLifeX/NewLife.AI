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
    [DisplayName("思维导图")]
    [Description("将知识层级/概念关系渲染为可折叠的交互式思维导图。输入 Markdown 大纲（# / ## / ###），前端自动转换为可视化树状图。适用场景：知识梳理、概念拆解、技术对比、方案评估、内容归纳。🎨 视觉风格：请结合对话主题主动选择 branchColors（3~8 个协调色）、maxDepth（控制复杂度）、collapsed（初始折叠以聚焦重点）。技术/数据主题用冷色系（蓝紫青）、商业/营销用暖色系（橙金红）、自然/健康用绿色系。节点密集时建议 maxDepth=2 保持可读性。📐 6 种布局：tree（默认缩进树，通用）/ radial（左右放射，知识梳理/概念图首选）/ lr（中心向右，流程图/决策树）/ rl（中心向左，反向追溯）/ tb（中心向下，组织架构）/ bt（中心向上，自底向上层级）。")]
    public ToolResult ShowMindmap(
        [Description("思维导图标题（≤ 30 字），如「人工智能技术体系」")] String title,
        [Description("Markdown 大纲，用 # / ## / ### 表示 1~3 级层级。示例：\n# 人工智能\n## 机器学习\n### 监督学习\n### 无监督学习\n## 深度学习\n### CNN\n### RNN")] String outline,
        [Description("布局模式：tree（默认缩进树，通用）/ radial（左右放射，知识梳理/概念图首选）/ lr（中心向右，流程图/决策树）/ rl（中心向左，反向追溯）/ tb（中心向下，组织架构）/ bt（中心向上，自底向上层级）")] String? layout = null,
        [Description("分支配色 JSON 数组，如 [\"#3b82f6\",\"#10b981\",\"#f59e0b\"]。3~8 个十六进制颜色，按顺序分配给各一级分支。不传则使用蓝紫/绿/黄/红/天蓝/深绿/橙/紫默认色板")] IList<String>? branchColors = null,
        [Description("初始折叠的节点 ID 列表 JSON 数组，如 [\"n2\",\"n5\"]。空则全部展开。用于聚焦重点分支")] IList<String>? collapsed = null,
        [Description("最大可见深度，1=仅一级分支，2=一二级，默认无限制。节点密集时建议设为 2 保持可读性")] Int32? maxDepth = null,
        ToolCallContext? context = null)
    {
        if (outline.IsNullOrEmpty())
        {
            // 记录关键上下文便于排查"参数已生成但解析后为空"类问题
            log.Warn("[Mindmap] outline 为空！Title={0}，ToolCallId={1}",
                title, context?.ToolCallId);
            throw new ToolException("参数错误：outline 不能为空", "请使用 Markdown 大纲格式（# / ## / ###）重新构建思维导图内容后重试，或直接回复用户说明无法生成思维导图。示例：# 中心主题\n## 一级分支\n### 子节点");
        }

        // 防御：限制 outline 最大长度（约 8KB），过大的大纲会导致 JSON 解析/API 传输问题
        const Int32 maxOutlineBytes = 8192;
        var outlineByteCount = Encoding.UTF8.GetByteCount(outline);
        if (outlineByteCount > maxOutlineBytes)
        {
            log.Warn("[Mindmap] outline 过大被截断，原始 {0} bytes → 截断为 {1} bytes。Title={2}",
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

        log.Info("[Mindmap] 渲染思维导图「{0}」，id={1}，layout={2}", title, mindmapId, layout ?? "tree");

        // 从 JsonSerializerOptions.Default 派生以携带 TypeInfoResolver，避免 ToJsonString 内部
        // 对 JsonValueCustomized 节点调用 MakeReadOnly() 时抛 "must specify a TypeInfoResolver"
        var writeOptions = new JsonSerializerOptions(JsonSerializerOptions.Default) { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        var resultJson = result.ToJsonString(writeOptions);
        return ToolResult.ForAudiences(resultJson, $"[已渲染思维导图到客户端：{title}]");
    }

    #endregion
}
