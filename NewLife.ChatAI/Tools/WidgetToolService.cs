using System.ComponentModel;
using System.Text.RegularExpressions;
using NewLife.AI.Tools;
using NewLife.Log;
using NewLife.Serialization;

namespace NewLife.ChatAI.Tools;

/// <summary>可视化 Widget 工具服务。在对话气泡中渲染 SVG 或 HTML 片段，用于流程图、时间线、表单预览、数据图表等可视化场景</summary>
/// <remarks>
/// <para>设计理念：</para>
/// <list type="bullet">
/// <item>仅做"展示"，工具调用立即返回 JSON，前端解析后渲染 iframe，不阻塞 AI 推理</item>
/// <item>后端做尺寸校验（≤ 200KB）和远程脚本白名单校验，仅允许受信任 CDN 域名的 <c>&lt;script src=...&gt;</c></item>
/// <item>前端使用 sandbox=&quot;allow-scripts&quot; iframe 隔离运行环境，AI 脚本无法访问父窗口 DOM</item>
/// </list>
/// </remarks>
/// <param name="log">日志</param>
public class WidgetToolService(ILog log)
{
    #region 常量

    /// <summary>Widget 代码最大字节数（按 UTF-8 计算），约 200KB</summary>
    public const Int32 MaxWidgetBytes = 200 * 1024;

    /// <summary>受信任的 CDN 主机白名单。仅允许来自这些域名的远程 <c>&lt;script src=...&gt;</c>，其余域名一律拒绝</summary>
    private static readonly HashSet<String> _trustedCdnHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "cdn.jsdelivr.net",
        "cdnjs.cloudflare.com",
        "unpkg.com",
        "cdn.bootcdn.net",
        "cdn.staticfile.org",
        "cdn.staticfile.net",
    };

    /// <summary>从 <c>&lt;script ... src="..."&gt;</c> 中提取 src 属性值的正则。支持有引号与无引号写法</summary>
    private static readonly Regex _scriptSrcRegex = new(
        @"<script\b[^>]*\bsrc\s*=\s*[""']?([^""'\s>]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    #endregion

    #region 工具方法

    /// <summary>渲染可视化 Widget 到对话气泡。AI 在需要绘制流程图、时间线、关系图、表单预览或数据图表时调用</summary>
    /// <param name="title">Widget 简短标题（≤ 30 字），用于卡片头部展示</param>
    /// <param name="content">SVG 字符串（以 <c>&lt;svg</c> 开头）或 HTML 片段。大小 ≤ 200KB，不得包含远程 <c>&lt;script src=...&gt;</c></param>
    /// <param name="loadingMessage">加载占位文本（可选），渲染完成前展示</param>
    /// <param name="theme">主题提示：light（浅色）/ dark（深色）/ auto（跟随系统）</param>
    /// <param name="initialHeight">Widget 初始高度（px，80~1200），默认 480</param>
    /// <param name="context">工具调用上下文（框架自动注入）</param>
    /// <returns>ToolResult（ForUser=完整Widget JSON + ForLlm=简短确认）</returns>
    [ToolDescription("show_widget", IsSystem = false,
        Triggers = "widget,svg,visual widget,visualize,draw a diagram",
        AssistantTriggers = "visual widget,visualize,diagram",
        ReadOnly = true)]
    [DisplayName("Visual Widget")]
    [Description("Render an SVG or HTML fragment inside the chat bubble. Use it for timelines and relationship diagrams (SVG), form previews and data cards (HTML), and statistical charts (SVG). Do not use it for Mermaid diagrams; output Mermaid as a ```mermaid code block in the reply instead. Limits: 200KB or less; <script src=...> only from trusted CDNs. Visual style: choose the information architecture (Bento/card flow/Grid), type hierarchy, and color system based on the topic. Use cool sans-serif tones for technical discussions, neutral colors with highlighted numbers for business reports, and gradients/glassmorphism/micro-animations for creative work. Always set font-family and color explicitly; do not rely on browser defaults.")]
    public ToolResult ShowWidget(
        [Description("Widget title (up to 30 characters), e.g. Order Status Flow")] String title,
        [Description("SVG must start with <svg; HTML fragments must start with the root element and must not include <html><body>. Maximum 200KB; <script src=...> only from trusted CDNs. Do not pass Mermaid syntax (flowchart/graph/sequenceDiagram, etc.); output Mermaid as a ```mermaid code block in the reply")] String content,
        [Description("Placeholder text shown while loading (optional)")] String? loadingMessage = null,
        [Description("Theme hint: light / dark / auto (follow system). The generated HTML should pick background and text colors accordingly; not passed to the front-end renderer")] String? theme = null,
        [Description("Initial widget height in px (80-1200). Default 480. Use 300-400 for compact cards and 600-800 for large charts")] Int32? initialHeight = null,
        [Description("Background mode: solid (default) / transparent (for overlaying screenshots on a PPT template)")] String? background = null,
        [Description("PPT slide mode. When true the card renders at a 16:9 aspect ratio, ready to paste into a presentation. Default false, which keeps the content-driven width")] Boolean? slideMode = null,
        ToolCallContext? context = null)
    {
        if (content.IsNullOrEmpty())
            throw new ToolException("Invalid parameter: content cannot be empty", "Provide SVG or HTML content and try again, or reply to the user that the widget cannot be rendered.");

        // 部分模型在多轮工具调用场景下会对 content 做 HTML 实体编码（&lt;svg ...&gt;）
        // 检测到首字符为实体编码时，解码还原为原始标签，确保 kind 判定和安全校验正常工作
        {
            var head = content.TrimStart();
            if (head.StartsWith("&lt;", StringComparison.OrdinalIgnoreCase) ||
                head.StartsWith("&#60;", StringComparison.OrdinalIgnoreCase))
                content = System.Net.WebUtility.HtmlDecode(content);
        }

        // 尺寸校验：按 UTF-8 字节数（与浏览器解析、网络传输的实际成本对齐）
        var byteCount = System.Text.Encoding.UTF8.GetByteCount(content);
        if (byteCount > MaxWidgetBytes)
            throw new ToolException(Localization.Translate("content exceeds the {0} KB limit (actual {1} KB)", MaxWidgetBytes / 1024, byteCount / 1024), "Trim the content and try again, split long text across multiple cards, or reply directly to the user.");

        // 远程脚本白名单校验：仅允许受信任 CDN 的 <script src=...>，其余域名一律拒绝（防供应链投毒）
        // 注意：必须在 HtmlDecode 之后执行，防止实体编码绕过白名单检测
        foreach (Match m in _scriptSrcRegex.Matches(content))
        {
            var src = m.Groups[1].Value;
            var isTrusted = Uri.TryCreate(src, UriKind.Absolute, out var uri) && _trustedCdnHosts.Contains(uri.Host);
            if (!isTrusted)
                throw new ToolException(Localization.Translate("Loading remote script {0} is forbidden", src), "Remove the untrusted remote script and try again with an inline script or a trusted CDN.");
        }

        // 类型判定：SVG 优先（去除首部空白后以 <svg 开头）
        var trimmed = content.TrimStart();
        var kind = trimmed.StartsWith("<svg", StringComparison.OrdinalIgnoreCase) ? "svg" : "html";

        // widgetId 复用工具调用 ID，便于前端去重
        var widgetId = context?.ToolCallId;
        if (widgetId.IsNullOrEmpty()) widgetId = $"widget_{Guid.NewGuid():N}";

        log.Info("[Widget] Rendering {0}: {1}, {2} bytes", kind, title, byteCount);

        var resultJson = new { widgetId, kind, title, code = content, loadingMessage, initialHeight, background, slideMode }.ToJson();
        return ToolResult.ForAudiences(resultJson, $"[已渲染Widget到客户端：{title}]");
    }

    #endregion
}
