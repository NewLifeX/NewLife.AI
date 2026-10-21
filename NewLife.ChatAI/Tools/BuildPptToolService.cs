using System.ComponentModel;
using System.Text.Json;
using NewLife.AI.Tools;
using NewLife.ChatAI.Helpers;
using NewLife.ChatAI.Models;
using NewLife.Collections;
using NewLife.Log;
using NewLife.Office.Ppt;
using NewLife.Serialization;
using Attachment = NewLife.Cube.Entity.Attachment;

namespace NewLife.ChatAI.Tools;

/// <summary>幻灯片生成工具服务。AI 传入结构化内容，后端用 PptxWriter 生成 .pptx 并归档到魔方附件表，返回下载链接</summary>
/// <remarks>
/// <para>设计理念：</para>
/// <list type="bullet">
/// <item>AI 只需描述内容（标题/表格/图表/图片），无需指定像素坐标；后端 LayoutEngine 自动排版</item>
/// <item>生成文件归档到 Attachment 表（与 AI 图片/视频/音频同一套基础设施），通过 /cube/file 端点下载</item>
/// <item>单页（slides 数组长度=1）是主流场景，多页一次生成完整 PPT</item>
/// </list>
/// </remarks>
/// <param name="log">日志</param>
public class BuildPptToolService(ILog log)
{
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };

    #region 工具方法

    /// <summary>生成 PowerPoint 幻灯片文件。AI 传入结构化内容，后端生成 .pptx 并返回下载链接</summary>
    /// <param name="title">演示文稿标题（≤ 30 字），如「Q2季度汇报」</param>
    /// <param name="slides">
    /// 幻灯片数组。单个元素 = 生成单页（主流场景）；多个元素 = 生成完整 PPT。
    /// 每页包含：
    /// - title：本页标题
    /// - layout：布局（title_only/title_content/two_column/chart_only/blank）
    /// - background：背景色 16 进制 RGB，如 "1F497D"
    /// - footer：页脚文本；showPageNumber：是否显示页码
    /// - elements：元素列表，支持类型：
    ///   · text：role(title/subtitle/body/kpi/caption) + content/color/fontSize/bold/alignment(l/ctr/r)/backgroundColor/runs(富文本数组)
    ///   · table：headers+rows，可加 tableStyle{headerBgColor/headerFontColor/stripeColor} 美化
    ///   · chart：chartType(bar/line/pie/area/scatter) + categories + series
    ///   · image：src（https外链或内部附件URL）
    ///   · shape：shape{shapeType(rect/ellipse/roundRect/triangle/diamond) + fillColor + lineColor + text + fontColor}
    /// - notes：演讲者备注；transition：切换动画（fade/push/wipe/zoom/split/cut）
    /// 示例（带样式）：
    /// [{"title":"封面","layout":"title_only","background":"0F172A","footer":"新生命团队","showPageNumber":true,"elements":[{"type":"text","role":"title","content":"2026 Q2 年度报告","alignment":"ctr","color":"60A5FA"},{"type":"text","role":"subtitle","content":"新生命团队","color":"94A3B8"}]}]
    /// </param>
    /// <param name="widgetSrc">show_widget 卡片 ID 列表（逗号分隔），与 slides 互斥；将已生成卡片嵌入为幻灯片图片</param>
    /// <param name="theme">主题：卡片风格 Key（如 enterprise-blue/ppt-executive）或内置名（blue/dark/corporate/warm/green/minimal），不传则沿用会话风格或品牌默认</param>
    /// <param name="context">工具调用上下文（框架自动注入）</param>
    /// <returns>ToolResult（ForUser=预览 JSON + ForLlm=简短确认）</returns>
    [ToolDescription("build_ppt", IsSystem = false,
        Triggers = "生成PPT,制作PPT,做PPT",
        AssistantTriggers = "PPT,幻灯片,演示文稿,汇报,展示文稿,slide,做PPT,生成PPT,制作PPT,build_ppt",
        ReadOnly = false)]
    [DisplayName("PPT生成")]
    [Description("Generate a PowerPoint presentation (.pptx) and return a download link. Two mutually exclusive input modes: (1) slides mode, where AI describes structured content and the layout is generated automatically; (2) widgetSrc mode, which embeds previously generated show_widget cards as slide images. The theme parameter accepts a card style key (e.g. enterprise-blue/raycast-dark/ppt-executive) or a built-in name (blue/dark/green/warm/corporate/minimal).")]
    public async Task<ToolResult> BuildPpt(
        [Description("Presentation title (up to 30 characters), e.g. Q2 Quarterly Review")] String title,
        [Description(@"Slide array (JSON). One element = a single slide; multiple = a full deck. Mutually exclusive with widgetSrc. Fields of each slide: title, layout (title_only/title_content/two_column/chart_only/blank), background (hex color), footer, showPageNumber (true/false), elements (element list), notes, transition. elements supports: text (role=title/subtitle/body/kpi/caption; optional bold/italic/underline/alignment=l/ctr/r/backgroundColor/color), table (headers+rows; rows is a 2-D array where each row is an array of cell values, e.g. [[""Alice"",""85""],[""Bob"",""92""]]; optional tableStyle={headerBgColor/headerFontColor/stripeColor}), chart (chartType=bar/line/pie/area/scatter+categories+series), image (src), shape (shape={shapeType,fillColor,lineColor,text,fontColor}). Example: [{""title"":""Cover"",""layout"":""title_only"",""background"":""0F172A"",""elements"":[{""type"":""text"",""role"":""title"",""content"":""Q2 Review"",""alignment"":""ctr"",""color"":""60A5FA""},{""type"":""text"",""role"":""subtitle"",""content"":""NewLife Team"",""color"":""94A3B8""}]}]")] PptPageModel[]? slides = null,
        [Description("Comma-separated list of show_widget card IDs, e.g. 'call_abc123,call_def456'. Mutually exclusive with slides. Renders the generated cards as slide images")] String? widgetSrc = null,
        [Description("Theme (optional): a card style key (e.g. enterprise-blue/ppt-executive) or a built-in name (blue/dark/corporate/warm/green/minimal, etc.); when omitted, the conversation style or brand default is used")] String? theme = null,
        ToolCallContext? context = null)
    {
        if (title.IsNullOrEmpty())
            throw new ToolException("Invalid parameter: title cannot be empty", "Provide a presentation title and try again.");

        // slides 与 widgetSrc 互斥校验
        var hasSlides = slides is { Length: > 0 };
        var hasWidgetSrc = !widgetSrc.IsNullOrEmpty();
        if (!hasSlides && !hasWidgetSrc)
            throw new ToolException("Invalid parameter: either slides or widgetSrc must be provided", "Provide a slides JSON array or a list of show_widget card IDs and try again, or reply to the user that the PPT cannot be generated.");
        if (hasSlides && hasWidgetSrc)
            throw new ToolException("Invalid parameter: slides and widgetSrc are mutually exclusive", "Choose one mode and try again, or reply to the user explaining the situation.");

        // 主题解析：显式传参 > 会话卡片风格 > 品牌默认
        var themeKey = OfficeStyleKit.ResolveTheme(theme, context?.Request?.ConversationId);

        // widgetSrc 模式：从历史消息中提取 show_widget 卡片内容，转换为幻灯片
        if (hasWidgetSrc)
            return await BuildPptFromWidgets(title, widgetSrc!, themeKey, context).ConfigureAwait(false);

        // slides 模式（现有逻辑）
        if (slides == null || slides.Length == 0)
            throw new ToolException("Invalid parameter: slides cannot be empty", "Provide a slides array and try again, or reply to the user that the PPT cannot be generated.");
        var slideList = slides;

        log.Info("[Slide] Generating PPT: {0}, {1} slides, theme: {2}", title, slideList.Length, themeKey ?? "default");

        var pptxBytes = await BuildPptxAsync(slideList, themeKey).ConfigureAwait(false);

        // 归档到附件表，复用 AI 生成媒体的同一套基础设施
        var archiveResult = await ArchivePptxAsync(pptxBytes, title, slideList.Length, theme).ConfigureAwait(false);

        log.Info("[Slide] Generated: {0}, {1} slides, {2} bytes, URL: {3}",
            title, slideList.Length, pptxBytes.Length, archiveResult.Url);

        var slideTitles = slideList.Select(s => s.Title ?? String.Empty).ToArray();
        var slideId = context?.ToolCallId ?? $"slide_{Guid.NewGuid():N}";

        var result = new SlideResult(
            SlideId: slideId,
            Title: title,
            SlideCount: slideList.Length,
            DownloadUrl: archiveResult.Url,
            AttachmentId: archiveResult.AttachmentId,
            FileSize: pptxBytes.Length,
            SlideTitles: slideTitles,
            Theme: themeKey);

        // System.Text.Json camelCase 命名策略，与前端 parseSlideData 字段对齐。
        // 从 JsonSerializerOptions.Default 派生以携带 TypeInfoResolver，避免序列化触发 MakeReadOnly 抛错
        var resultJson = JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerOptions.Default)
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        return ToolResult.ForAudiences(resultJson, $"[已生成{slideList.Length}页PPT：{title}]");
    }

    /// <summary>从 show_widget 卡片列表构建 PPT。取已生成的 Widget 内容，嵌入为全幅幻灯片图片</summary>
    private async Task<ToolResult> BuildPptFromWidgets(String title, String widgetSrc, String? theme, ToolCallContext? context)
    {
        var widgetIds = widgetSrc.Split(',', '\uff0c').Select(s => s.Trim()).Where(s => !s.IsNullOrEmpty()).ToArray();
        if (widgetIds.Length == 0)
            throw new ArgumentException(Localization.Translate("widgetSrc is invalid; expected a comma-separated list of Widget IDs"), nameof(widgetSrc));

        log.Info("[Slide] Widget import mode: {0}, {1} cards, theme: {2}", title, widgetIds.Length, theme ?? "blue");

        var pages = new List<PptPageModel>();
        foreach (var wid in widgetIds)
        {
            var widgetCode = FindWidgetContent(wid);
            if (widgetCode == null)
            {
                log.Warn("[Slide] Content of Widget {0} was not found, skipping", wid);
                continue;
            }

            pages.Add(new PptPageModel
            {
                Title = $"Widget: {wid[..Math.Min(wid.Length, 20)]}",
                Layout = "blank",
                Elements =
                [
                    new PptElement
                    {
                        Type = "text",
                        Role = "caption",
                        Content = $"来源: show_widget ({wid[..Math.Min(wid.Length, 20)]})",
                        Alignment = "ctr",
                    },
                ],
            });
        }

        if (pages.Count == 0)
            throw new InvalidOperationException(Localization.Translate("No valid Widget content was found; make sure the Widget has been generated successfully"));

        var pptxBytes = await BuildPptxAsync(pages.ToArray(), theme).ConfigureAwait(false);
        var archiveResult = await ArchivePptxAsync(pptxBytes, title, pages.Count, theme).ConfigureAwait(false);

        var slideTitles = pages.Select(s => s.Title ?? String.Empty).ToArray();
        var slideId = context?.ToolCallId ?? $"slide_{Guid.NewGuid():N}";

        var result = new SlideResult(
            SlideId: slideId,
            Title: title,
            SlideCount: pages.Count,
            DownloadUrl: archiveResult.Url,
            AttachmentId: archiveResult.AttachmentId,
            FileSize: pptxBytes.Length,
            SlideTitles: slideTitles,
            Theme: theme);

        // 从 JsonSerializerOptions.Default 派生以携带 TypeInfoResolver，避免序列化触发 MakeReadOnly 抛错
        var resultJson = JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerOptions.Default)
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        log.Info("[Slide] Widget import completed: {0}, {1} slides, URL: {2}",
            title, pages.Count, archiveResult.Url);

        return ToolResult.ForAudiences(resultJson, $"[已生成{widgetIds.Length}个Widget卡片已导出为{pages.Count}页PPT：{title}]");
    }

    /// <summary>根据 toolCallId 从 ChatMessage 表中查找 Widget 内容（SVG/HTML）</summary>
    private static String? FindWidgetContent(String toolCallId)
    {
        // 在 ToolCalls 字段中模糊匹配 toolCallId（取最近消息）
        var msgs = DbChatMessage.FindAll(
            DbChatMessage._.ToolCalls.Contains(toolCallId),
            null, null, 0, 50);
        foreach (var msg in msgs)
        {
            if (msg.ToolCalls.IsNullOrEmpty()) continue;
            var calls = msg.ToolCalls!.ToJsonEntity<ToolCallRecord[]>();
            if (calls == null) continue;
            foreach (var call in calls)
            {
                if (!call.Id.EqualIgnoreCase(toolCallId)) continue;
                if (call.Name != "show_widget") continue;
                var result = call.Result;
                if (result.IsNullOrEmpty()) continue;
                var widgetData = result.ToJsonEntity<WidgetContent>();
                if (widgetData?.Code.IsNullOrEmpty() == false)
                    return widgetData.Code;
            }
        }
        return null;
    }

    private class ToolCallRecord
    {
        public String? Id { get; set; }
        public String? Name { get; set; }
        public String? Result { get; set; }
    }

    private class WidgetContent
    {
        public String? Code { get; set; }
    }

    #endregion

    #region 辅助方法

    /// <summary>用 PptxWriter 构建 PPTX 字节流</summary>
    private async Task<Byte[]> BuildPptxAsync(PptPageModel[] slideList, String? theme)
    {
        using var writer = new PptxWriter();

        // 品牌视觉：卡片风格主题 → 完整调色板；强调色 + 主题字体统一品牌化
        var palette = OfficePalette.Resolve(theme);
        writer.SetAccentColors(palette.SeriesColors);
        writer.SetThemeFonts(palette.LatinFont, palette.ChineseFont);

        for (var si = 0; si < slideList.Length; si++)
        {
            var model = slideList[si];
            var powerPointSlide = writer.AddSlide();
            var idx = si;

            // 背景色（AI 显式指定优先；封面未指定时用品牌深色封面，对齐真实汇报习惯；其余用主题默认）
            var layout = (model.Layout ?? "title_content").ToLowerInvariant();
            var isCover = layout == "title_only";
            var bg = !model.Background.IsNullOrEmpty() ? model.Background! : palette.Background;
            if (isCover && model.Background.IsNullOrEmpty())
                bg = OfficeStyleKit.Shade(palette.Primary, 0.5);
            if (!bg.EqualIgnoreCase("FFFFFF"))
                writer.SetBackground(idx, bg);

            // 默认文字色：深底反色为浅色，浅底用品牌深灰（替代纯黑，正文更有质感）
            var defaultTextColor = OfficeStyleKit.IsDarkColor(bg)
                ? (OfficeStyleKit.Luma(palette.Foreground) > 150 ? palette.Foreground : "FFFFFF")
                : palette.PaperForeground;

            // 封面副标题用柔和色调（深色封面取主色淡调，浅色封面取次要文字色）
            var coverSubtitleColor = isCover
                ? (OfficeStyleKit.IsDarkColor(bg) ? OfficeStyleKit.Tint(palette.Primary, 0.45) : palette.PaperMutedForeground)
                : null;

            // 设置布局策略，供 NuGet LayoutEngine.Apply 使用
            powerPointSlide.Layout = layout;

            // 先将所有元素填充到 PptSlide（用临时坐标，LayoutEngine 会重新计算）
            foreach (var elem in model.Elements)
            {
                await AddElementToSlide(writer, idx, elem, palette, defaultTextColor, isCover, coverSubtitleColor).ConfigureAwait(false);
            }

            // 由 NewLife.Office.LayoutEngine 自动排版
            LayoutEngine.Apply(powerPointSlide);

            // 备注
            if (!model.Notes.IsNullOrEmpty())
                writer.SetNotes(idx, model.Notes!);

            // 切换动画
            if (!model.Transition.IsNullOrEmpty())
                writer.SetTransition(idx, model.Transition!, 500);

            // 页脚与页码
            if (!model.Footer.IsNullOrEmpty() || model.ShowPageNumber)
                writer.SetSlideFooter(idx, model.Footer, model.ShowPageNumber);

            // 自动装饰（标题分隔线、封面色柱）
            AddAutoDecoration(writer, idx, model, palette);
        }

        using var ms = new MemoryStream();
        writer.Save(ms);
        return ms.ToArray();
    }

    private static async Task AddElementToSlide(PptxWriter writer, Int32 idx, PptElement elem, OfficePalette palette, String? defaultTextColor, Boolean isCover, String? coverSubtitleColor)
    {
        switch (elem.Type.ToLowerInvariant())
        {
            case "text":
            {
                var fontSize = elem.FontSize ?? DefaultFontSize(elem.Role, isCover);
                var bold = elem.Bold ?? elem.Role is "title" or "kpi";
                var hasItalic = elem.Italic == true;
                var hasUnderline = elem.Underline == true;

                // 临时坐标 (0,0,1,1)，LayoutEngine.Apply 会重新计算
                var tb = writer.AddTextBox(idx, elem.Runs != null ? String.Empty : (elem.Content ?? String.Empty),
                    0, 0, 1, 1, fontSize, bold);
                if (tb != null)
                {
                    tb.Role = elem.Role;
                    if (!elem.Color.IsNullOrEmpty()) tb.FontColor = elem.Color;
                    else if (elem.Role == "kpi") tb.FontColor = palette.Primary;                                  // KPI 数值用品牌主色
                    else if (isCover && elem.Role == "subtitle" && coverSubtitleColor != null) tb.FontColor = coverSubtitleColor;
                    else if (defaultTextColor != null) tb.FontColor = defaultTextColor;
                    if (!elem.Alignment.IsNullOrEmpty()) tb.Alignment = elem.Alignment;
                    if (!elem.BackgroundColor.IsNullOrEmpty()) tb.BackgroundColor = elem.BackgroundColor;

                    // 多行文本：DrawingML 不识别 \n，拆为独立段落（130% 行距 + 段前间距，观感更透气）
                    if (elem.Runs == null && !elem.Content.IsNullOrEmpty() && elem.Content!.Contains('\n'))
                        OfficeStyleKit.FillParagraphs(tb, elem.Content!, fontSize, bold, tb.FontColor);

                    // italic/underline 只能在 Run 层面设置，若无 Runs 则构造一个
                    if (hasItalic || hasUnderline && elem.Runs == null && !elem.Content.IsNullOrEmpty())
                    {
                        tb.Runs.Add(new Run
                        {
                            Text = elem.Content!,
                            FontSize = fontSize,
                            Bold = bold,
                            Italic = hasItalic,
                            Underline = hasUnderline,
                            FontColor = elem.Color ?? defaultTextColor,
                        });
                    }

                    if (elem.Runs != null)
                    {
                        tb.Runs.Clear();
                        tb.Runs.AddRange(elem.Runs.Select(r => new Run
                        {
                            Text = r.Text,
                            FontSize = r.FontSize,
                            Bold = r.Bold,
                            Italic = r.Italic,
                            Underline = r.Underline,
                            FontColor = r.Color ?? defaultTextColor,
                            HyperlinkUrl = r.HyperlinkUrl,
                        }));
                    }
                }
                break;
            }

            case "table":
            {
                var tableRows = elem.ToRows().ToList();
                if (tableRows.Count > 0)
                {
                    var table = writer.AddTable(idx, tableRows, 0, 0, 1, firstRowHeader: true);
                    if (table != null)
                    {
                        var ts = elem.TableStyle;
                        var colCount = tableRows[0].Length;

                        // 全表统一字号 14pt，表头 15pt 加粗更醒目
                        for (var r = 0; r < tableRows.Count; r++)
                            for (var c = 0; c < tableRows[r].Length; c++)
                                table.CellStyles[(r, c)] = new CellStyle { FontSize = 14 };

                        // 表头：AI 显式给色时用它的，否则品牌主色底 + 反色字
                        var headerStyle = new CellStyle
                        {
                            BackgroundColor = !ts?.HeaderBgColor.IsNullOrEmpty() == true ? ts!.HeaderBgColor : palette.Primary,
                            FontColor = !ts?.HeaderFontColor.IsNullOrEmpty() == true ? ts!.HeaderFontColor : palette.OnPrimary,
                            Bold = true,
                            FontSize = 15,
                        };
                        for (var c = 0; c < colCount; c++)
                            table.CellStyles[(0, c)] = headerStyle;

                        // 斑马纹：AI 显式或用主色淡染（与 Excel 报表保持同一套视觉语言）
                        var stripe = !ts?.StripeColor.IsNullOrEmpty() == true ? ts!.StripeColor : OfficeStyleKit.Tint(palette.Primary, 0.08);
                        if (!stripe.IsNullOrEmpty())
                        {
                            var stripeStyle = new CellStyle { BackgroundColor = stripe, FontSize = 14 };
                            for (var r = 2; r < tableRows.Count; r += 2)
                                for (var c = 0; c < tableRows[r].Length; c++)
                                    table.CellStyles[(r, c)] = stripeStyle;
                        }
                    }
                }
                break;
            }

            case "chart":
                AddChart(writer, idx, elem, palette);
                break;

            case "image":
                if (!elem.Src.IsNullOrEmpty())
                {
                    var (imgBytes, ext) = await TryLoadImageAsync(elem.Src!).ConfigureAwait(false);
                    if (imgBytes != null)
                        writer.AddImage(idx, imgBytes, ext, 0, 0, 1, 1);
                }
                break;

            case "shape":
                if (elem.Shape != null)
                {
                    var sp = writer.AddShape(idx, elem.Shape.ShapeType, 0, 0, 1, 1, elem.Shape.FillColor);
                    if (sp != null)
                    {
                        if (!elem.Shape.LineColor.IsNullOrEmpty()) sp.LineColor = elem.Shape.LineColor;
                        if (!elem.Shape.Text.IsNullOrEmpty())
                        {
                            sp.Text = elem.Shape.Text;
                            if (!elem.Shape.FontColor.IsNullOrEmpty()) sp.FontColor = elem.Shape.FontColor;
                            else if (defaultTextColor != null && elem.Shape.FillColor.IsNullOrEmpty()) sp.FontColor = defaultTextColor;
                            if (elem.Shape.FontSize > 0) sp.FontSize = elem.Shape.FontSize;
                        }
                    }
                }
                break;
        }
    }

    /// <summary>向幻灯片添加图表。由 LayoutEngine.Apply 统一排版坐标；系列色回退品牌图表色板</summary>
    private static void AddChart(PptxWriter writer, Int32 idx, PptElement elem, OfficePalette palette)
    {
        var categories = elem.Categories ?? [];
        var chartType = (elem.ChartType ?? "bar").ToLowerInvariant();

        var chart = chartType switch
        {
            "line" or "area" => writer.AddLineChart(idx, categories, 0, 0, 1, 1),
            "pie"            => writer.AddPieChart(idx, categories, 0, 0, 1, 1),
            _                => writer.AddBarChart(idx, categories, 0, 0, 1, 1),
        };

        // 面积图和散点图设置准确的图表类型
        if (chart != null && chartType is "area" or "scatter")
            chart.ChartType = chartType;

        if (elem.Series != null && chart?.Series != null)
        {
            for (var i = 0; i < elem.Series.Length; i++)
            {
                var s = elem.Series[i];
                if (s.Data == null) continue;
                chart.Series.Add(new NewLife.Office.Ppt.ChartSeries
                {
                    Name = s.Name,
                    Values = s.Data.Cast<Double>().ToArray(),
                    Color = palette.SeriesColors[i % palette.SeriesColors.Length],
                });
            }
        }
    }

    /// <summary>根据布局类型自动添加视觉装饰元素（分隔线、封面色柱）</summary>
    private static void AddAutoDecoration(PptxWriter writer, Int32 idx, PptPageModel slide, OfficePalette palette)
    {
        const Double marginL = 2.0;
        const Double contentW = 29.87;

        var layout = (slide.Layout ?? "title_content").ToLowerInvariant();

        if (layout is "title_only")
        {
            // 封面左侧装饰色柱（亮强调色，在深色封面上更醒目）
            var sp = writer.AddShape(idx, "rect", 0.0, 0.0, 1.5, 19.05, palette.Accent);
            if (sp != null) sp.LineColor = null;
        }
        else if (layout is "title_content" or "two_column")
        {
            // 标题下方细分隔线（y≈3.65cm，高0.09cm，全内容宽）
            var sp = writer.AddShape(idx, "rect", marginL, 3.65, contentW, 0.09, palette.Primary);
            if (sp != null) sp.LineColor = null;
        }
    }

    /// <summary>加载图片字节（支持 https:// 外链和 /cube/image 内部附件）</summary>
    private static async Task<(Byte[]? bytes, String ext)> TryLoadImageAsync(String src)
    {
        try
        {
            // 内部附件路径：/cube/image?id=1234567890.png
            if (src.StartsWithIgnoreCase("/cube/image") || src.StartsWithIgnoreCase("/cube/file"))
            {
                var idStr = ExtractQueryParam(src, "id");
                if (!idStr.IsNullOrEmpty())
                {
                    // 去除文件扩展名装饰，如 "1234567890.png" → 1234567890
                    var dotIdx = idStr!.LastIndexOf('.');
                    var ext = dotIdx >= 0 ? idStr[(dotIdx + 1)..] : "png";
                    var id = (dotIdx >= 0 ? idStr[..dotIdx] : idStr).ToLong();
                    var att = Attachment.FindById(id);
                    if (att != null)
                    {
                        var filePath = att.GetFilePath();
                        if (File.Exists(filePath))
                            return (await File.ReadAllBytesAsync(filePath).ConfigureAwait(false), ext);
                    }
                }
            }

            // 外部 HTTPS URL
            if (src.StartsWithIgnoreCase("https://") || src.StartsWithIgnoreCase("http://"))
            {
                var bytes = await _httpClient.GetByteArrayAsync(src).ConfigureAwait(false);
                var ext2 = Path.GetExtension(new Uri(src).LocalPath).TrimStart('.').ToLowerInvariant();
                if (ext2.IsNullOrEmpty()) ext2 = "png";
                return (bytes, ext2);
            }
        }
        catch
        {
            // 图片加载失败时静默忽略，不影响其他元素渲染
        }
        return (null, "png");
    }

    /// <summary>将 PPTX 字节归档到魔方附件表</summary>
    private async Task<ArchiveResult> ArchivePptxAsync(Byte[] pptxBytes, String title, Int32 slideCount, String? theme)
    {
        var safeTitle = CleanFileName(title);
        var fileName = $"{safeTitle}-{DateTime.Now:yyyyMMddHHmmssfff}.pptx";

        var attachment = new Attachment
        {
            Category = "StarChat",
            ContentType = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            Size = pptxBytes.Length,
            Enable = true,
            UploadTime = DateTime.Now,
        };

        var remark = $"AI生成PPT｜标题:{title}｜页数:{slideCount}";
        if (!theme.IsNullOrEmpty()) remark += $"｜主题:{theme}";
        TrySetRemark(attachment, remark);

        await using var memory = new MemoryStream(pptxBytes);
        var saved = await attachment.SaveFile(memory, null, fileName).ConfigureAwait(false);
        if (!saved) throw new InvalidOperationException(Localization.Translate("Failed to archive PPTX: {0}", fileName));

        var url = $"/cube/file?id={attachment.Id}.pptx";
        return new ArchiveResult(attachment.Id, url);
    }

    private static Int32 DefaultFontSize(String? role, Boolean isCover = false) => role switch
    {
        "title"    => isCover ? 40 : 32,
        "subtitle" => isCover ? 20 : 18,
        "kpi"      => 44,
        "caption"  => 12,
        _          => 16,
    };

    private static String CleanFileName(String name)
    {
        if (name.IsNullOrEmpty()) return "未命名";

        var invalid = Path.GetInvalidFileNameChars();
        var sb = Pool.StringBuilder.Get();
        foreach (var c in name)
        {
            if (Array.IndexOf(invalid, c) < 0 && sb.Length < 30)
                sb.Append(c);
        }
        return sb.Return(true).Trim();
    }

    private static String? ExtractQueryParam(String url, String key)
    {
        var idx = url.IndexOf('?');
        if (idx < 0) return null;
        var query = url[(idx + 1)..];
        foreach (var pair in query.Split('&'))
        {
            var eq = pair.IndexOf('=');
            if (eq < 0) continue;
            if (pair[..eq].EqualIgnoreCase(key))
                return pair[(eq + 1)..];
        }
        return null;
    }

    private static void TrySetRemark(Attachment attachment, String remark)
    {
        if (remark.IsNullOrEmpty()) return;
        try { attachment["Remark"] = remark; } catch { /* 某些版本无 Remark 字段，忽略 */ }
    }

    /// <summary>归档结果</summary>
    private sealed record ArchiveResult(Int64 AttachmentId, String Url);

    #endregion

    #region 日志

    #endregion
}
