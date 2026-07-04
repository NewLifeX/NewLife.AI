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
        Triggers = "画一张,绘制,可视化,时间线,关系图,SVG,图表",
        AssistantTriggers = "画一张,绘制,可视化,时间线,关系图,SVG,图表,展示",
        ReadOnly = true)]
    [DisplayName("可视化Widget")]
    [Description("将 SVG 或 HTML 片段渲染到对话气泡中。适用：时间线/关系图（SVG）、表单预览/数据卡片（HTML）、统计图表（SVG）。禁止用于 Mermaid 图表，Mermaid 请在正文以 ```mermaid 代码块输出。约束：≤ 200KB，<script src=...> 仅限受信任 CDN。🎨 视觉风格：请根据对话主题为生成的 HTML/SVG 选择信息架构（Bento/卡片流/Grid）、字体层级和色彩体系。技术讨论用冷色无衬线、商业报告用中性色+关键数字突出、创意设计可用渐变/玻璃态/微动效。显式指定 font-family 和 color，禁止浏览器默认样式。")]
    public ToolResult ShowWidget(
        [Description("Widget 标题（≤ 30 字），如『订单状态流程图』")] String title,
        [Description("SVG 以 <svg 开头；HTML 片段以根标签开头，不含 <html><body>。最大 200KB，<script src=...> 仅限受信任 CDN。禁止传入 Mermaid 语法（flowchart/graph/sequenceDiagram 等），Mermaid 应在正文以 ```mermaid 代码块输出")] String content,
        [Description("加载占位文本，可选。渲染完成前展示")] String? loadingMessage = null,
        [Description("主题提示：light（浅色）/ dark（深色）/ auto（跟随系统）。AI 生成 HTML 时应据此选择背景色和文字色，不传入前端渲染")] String? theme = null,
        [Description("Widget 初始高度（px，80~1200），默认 480。紧凑卡片建议 300-400，大图表建议 600-800")] Int32? initialHeight = null,
        [Description("背景模式：solid（纯色背景，默认）/ transparent（透明背景，截图后可叠加到 PPT 模板底图上）")] String? background = null,
        [Description("PPT 幻灯片模式。true 时卡片按 16:9 宽高比渲染，适合直接粘贴到演示文稿。默认 false 沿用自适应内容宽度")] Boolean? slideMode = null,
        ToolCallContext? context = null)
    {
        if (content.IsNullOrEmpty()) throw new ArgumentException("content 不能为空", nameof(content));

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
            throw new ArgumentException($"content 超出 {MaxWidgetBytes / 1024} KB 限制（实际 {byteCount / 1024} KB）", nameof(content));

        // 远程脚本白名单校验：仅允许受信任 CDN 的 <script src=...>，其余域名一律拒绝（防供应链投毒）
        // 注意：必须在 HtmlDecode 之后执行，防止实体编码绕过白名单检测
        foreach (Match m in _scriptSrcRegex.Matches(content))
        {
            var src = m.Groups[1].Value;
            var isTrusted = Uri.TryCreate(src, UriKind.Absolute, out var uri) && _trustedCdnHosts.Contains(uri.Host);
            if (!isTrusted)
                throw new ArgumentException($"禁止加载远程脚本 <script src=\"{src}\">，只允许受信任 CDN（{String.Join("、", _trustedCdnHosts)}）", nameof(content));
        }

        // 类型判定：SVG 优先（去除首部空白后以 <svg 开头）
        var trimmed = content.TrimStart();
        var kind = trimmed.StartsWith("<svg", StringComparison.OrdinalIgnoreCase) ? "svg" : "html";

        // widgetId 复用工具调用 ID，便于前端去重
        var widgetId = context?.ToolCallId;
        if (widgetId.IsNullOrEmpty()) widgetId = $"widget_{Guid.NewGuid():N}";

        log.Info("[Widget] 渲染 {0}：{1}，{2} 字节", kind, title, byteCount);

        var resultJson = new { widgetId, kind, title, code = content, loadingMessage, initialHeight, background, slideMode }.ToJson();
        return new ToolResult(
            ToolContent.ForUser(resultJson),
            ToolContent.ForLlm($"[已渲染Widget到客户端：{title}]"));
    }

    #endregion
}
