using System.ComponentModel;
using System.Text.Json;
using NewLife.AI.Tools;
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
    /// <param name="theme">主题：blue（科技蓝，默认）/ dark（深色）/ corporate（商务灰）/ warm（暖橙）/ green（清新绿）/ minimal（极简白）</param>
    /// <param name="context">工具调用上下文（框架自动注入）</param>
    /// <returns>ToolResult（ForUser=预览 JSON + ForLlm=简短确认）</returns>
    [ToolDescription("build_ppt", IsSystem = false,
        Triggers = "生成PPT,制作PPT,做PPT",
        AssistantTriggers = "PPT,幻灯片,演示文稿,汇报,展示文稿,slide,做PPT,生成PPT,制作PPT,build_ppt",
        ReadOnly = false)]
    [DisplayName("PPT生成")]
    [Description("生成 PowerPoint 演示文稿（.pptx）并返回下载链接。两种输入模式互斥：①slides模式=AI描述结构化内容自动排版；②widgetSrc模式=嵌入已生成的show_widget卡片为幻灯片图片。theme参数使用卡片风格 Key（如 tech-blue/dark-mode/corporate）或内置名（blue/dark/green/warm/corporate/minimal）。")]
    public async Task<ToolResult> BuildPpt(
        [Description("演示文稿标题（≤ 30 字），如「Q2季度汇报」")] String title,
        [Description(@"幻灯片数组（JSON）。1个元素=单页；多个=完整PPT。与widgetSrc互斥，二选一。每页字段：title、layout(title_only/title_content/two_column/chart_only/blank)、background(背景色16进制)、footer(页脚)、showPageNumber(显示页码true/false)、elements(元素列表)、notes(备注)、transition(切换)。elements支持：text(role=title/subtitle/body/kpi/caption，可选bold/italic/underline/alignment=l/ctr/r/backgroundColor/color)、table(headers+rows，rows为二维数组每行一个单元格值数组如 [[""张三"",""85""],[""李四"",""92""]]，可加tableStyle={headerBgColor/headerFontColor/stripeColor})、chart(chartType=bar/line/pie/area/scatter+categories+series)、image(src)、shape(shape={shapeType,fillColor,lineColor,text,fontColor})。示例：[{""title"":""封面"",""layout"":""title_only"",""background"":""0F172A"",""elements"":[{""type"":""text"",""role"":""title"",""content"":""Q2汇报"",""alignment"":""ctr"",""color"":""60A5FA""},{""type"":""text"",""role"":""subtitle"",""content"":""新生命团队"",""color"":""94A3B8""}]}]")] String? slides = null,
        [Description("show_widget 卡片 ID 列表（逗号分隔），如 'call_abc123,call_def456'。与slides互斥，二选一。将已生成的卡片渲染结果嵌入为幻灯片图片")] String? widgetSrc = null,
        [Description("主题（可选）：blue（科技蓝，默认）/ dark（深色紫）/ corporate（商务灰）/ warm（暖橙）/ green（清新绿）/ minimal（极简白）/ ocean（深海蓝）/ sunset（日落橙紫）/ forest（翠绿森林）/ slate（高级石板）/ amber（琥珀金）")] String? theme = null,
        ToolCallContext? context = null)
    {
        if (title.IsNullOrEmpty())
            throw new ToolException("参数错误：title 不能为空", "请提供演示文稿标题后重试。");

        // slides 与 widgetSrc 互斥校验
        var hasSlides = !slides.IsNullOrEmpty();
        var hasWidgetSrc = !widgetSrc.IsNullOrEmpty();
        if (!hasSlides && !hasWidgetSrc)
            throw new ToolException("参数错误：slides 与 widgetSrc 必须提供其一", "请提供幻灯片 JSON 数组或 show_widget 卡片 ID 列表后重试，或直接回复用户说明无法生成 PPT。");
        if (hasSlides && hasWidgetSrc)
            throw new ToolException("参数错误：slides 与 widgetSrc 互斥，只能提供其一", "请选择一种模式后重试，或直接回复用户说明情况。");

        // widgetSrc 模式：从历史消息中提取 show_widget 卡片内容，转换为幻灯片
        if (hasWidgetSrc)
            return await BuildPptFromWidgets(title, widgetSrc!, theme, context).ConfigureAwait(false);

        // slides 模式（现有逻辑）
        if (slides.IsNullOrEmpty())
            throw new ToolException("参数错误：slides 不能为空", "请提供幻灯片 JSON 数组后重试，或直接回复用户说明无法生成 PPT。");

        PptPageModel[] slideList;
        try
        {
            slideList = slides.ToJsonEntity<PptPageModel[]>();
            if (slideList == null)
                throw new ToolException("slides JSON 格式错误", "请检查 JSON 语法后重试，或直接回复用户说明无法生成 PPT。");
        }
        catch (Exception ex) when (ex is not ArgumentException)
        {
            throw new ToolException($"slides JSON 解析失败：{ex.Message}", $"请检查 JSON 语法后重试，或直接回复用户说明情况。");
        }

        if (slideList.Length == 0)
            throw new ToolException("slides 不能为空数组", "请提供至少一页幻灯片后重试，或直接回复用户说明无法生成 PPT。");

        log.Info("[Slide] 开始生成 PPT：{0}，{1} 页，主题：{2}", title, slideList.Length, theme ?? "blue");

        var pptxBytes = await BuildPptxAsync(slideList, theme).ConfigureAwait(false);

        // 归档到附件表，复用 AI 生成媒体的同一套基础设施
        var archiveResult = await ArchivePptxAsync(pptxBytes, title, slideList.Length, theme).ConfigureAwait(false);

        log.Info("[Slide] 生成完成：{0}，{1} 页，{2} 字节，URL：{3}",
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
            Theme: theme ?? "blue");

        // System.Text.Json camelCase 命名策略，与前端 parseSlideData 字段对齐
        var resultJson = JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        return new ToolResult(
            ToolContent.ForUser(resultJson),
            ToolContent.ForLlm($"[已生成{slideList.Length}页PPT：{title}]"));
    }

    /// <summary>从 show_widget 卡片列表构建 PPT。取已生成的 Widget 内容，嵌入为全幅幻灯片图片</summary>
    private async Task<ToolResult> BuildPptFromWidgets(String title, String widgetSrc, String? theme, ToolCallContext? context)
    {
        var widgetIds = widgetSrc.Split(',', '\uff0c').Select(s => s.Trim()).Where(s => !s.IsNullOrEmpty()).ToArray();
        if (widgetIds.Length == 0)
            throw new ArgumentException("widgetSrc \u683c\u5f0f\u9519\u8bef\uff0c\u5e94\u4e3a\u9017\u53f7\u5206\u9694\u7684 Widget ID \u5217\u8868", nameof(widgetSrc));

        log.Info("[Slide] Widget \u5bfc\u5165\u6a21\u5f0f\uff1a{0}\uff0c{1} \u4e2a\u5361\u7247\uff0c\u4e3b\u9898\uff1a{2}", title, widgetIds.Length, theme ?? "blue");

        var pages = new List<PptPageModel>();
        foreach (var wid in widgetIds)
        {
            var widgetCode = FindWidgetContent(wid);
            if (widgetCode == null)
            {
                log.Warn("[Slide] \u672a\u627e\u5230 Widget {0} \u7684\u5185\u5bb9\uff0c\u8df3\u8fc7", wid);
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
            throw new InvalidOperationException("\u672a\u627e\u5230\u4efb\u4f55\u6709\u6548\u7684 Widget \u5185\u5bb9\uff0c\u8bf7\u786e\u8ba4 Widget \u5df2\u6210\u529f\u751f\u6210");

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
            Theme: theme ?? "blue");

        var resultJson = JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        log.Info("[Slide] Widget \u5bfc\u5165\u5b8c\u6210\uff1a{0}\uff0c{1} \u9875\uff0cURL\uff1a{2}",
            title, pages.Count, archiveResult.Url);

        return new ToolResult(
            ToolContent.ForUser(resultJson),
            ToolContent.ForLlm($"[{widgetIds.Length}\u4e2aWidget\u5361\u7247\u5df2\u5bfc\u51fa\u4e3a{pages.Count}\u9875PPT\uff1a{title}]"));
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

        // 应用主题强调色
        var accentColors = ThemeColors.Get(theme);
        writer.SetAccentColors(accentColors);

        for (var si = 0; si < slideList.Length; si++)
        {
            var model = slideList[si];
            var powerPointSlide = writer.AddSlide();
            var idx = si;

            // 背景色（优先使用 model 指定，否则用主题默认）
            var bg = !model.Background.IsNullOrEmpty() ? model.Background! : ThemeColors.GetBackground(theme);
            if (!bg.EqualIgnoreCase("FFFFFF"))
                writer.SetBackground(idx, bg);

            // 设置布局策略，供 NuGet LayoutEngine.Apply 使用
            powerPointSlide.Layout = model.Layout ?? "title_content";

            // 先将所有元素填充到 PptSlide（用临时坐标，LayoutEngine 会重新计算）
            foreach (var elem in model.Elements)
            {
                await AddElementToSlide(writer, idx, elem).ConfigureAwait(false);
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
            AddAutoDecoration(writer, idx, model, ThemeColors.GetPrimary(theme));
        }

        using var ms = new MemoryStream();
        writer.Save(ms);
        return ms.ToArray();
    }

    private static async Task AddElementToSlide(PptxWriter writer, Int32 idx, PptElement elem)
    {
        switch (elem.Type.ToLowerInvariant())
        {
            case "text":
            {
                var fontSize = elem.FontSize ?? DefaultFontSize(elem.Role);
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
                    if (!elem.Alignment.IsNullOrEmpty()) tb.Alignment = elem.Alignment;
                    if (!elem.BackgroundColor.IsNullOrEmpty()) tb.BackgroundColor = elem.BackgroundColor;

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
                            FontColor = elem.Color,
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
                            FontColor = r.Color,
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
                    if (table != null && elem.TableStyle != null)
                    {
                        var ts = elem.TableStyle;
                        if (!ts.HeaderBgColor.IsNullOrEmpty())
                        {
                            var colCount = tableRows[0].Length;
                            var headerStyle = new CellStyle
                            {
                                BackgroundColor = ts.HeaderBgColor,
                                FontColor = ts.HeaderFontColor,
                                Bold = true,
                            };
                            for (var c = 0; c < colCount; c++)
                                table.CellStyles[(0, c)] = headerStyle;
                        }
                        if (!ts.StripeColor.IsNullOrEmpty())
                        {
                            var stripeStyle = new CellStyle { BackgroundColor = ts.StripeColor };
                            for (var r = 2; r < tableRows.Count; r += 2)
                                for (var c = 0; c < tableRows[r].Length; c++)
                                    table.CellStyles[(r, c)] = stripeStyle;
                        }
                    }
                }
                break;
            }

            case "chart":
                AddChart(writer, idx, elem);
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
                            if (elem.Shape.FontSize > 0) sp.FontSize = elem.Shape.FontSize;
                        }
                    }
                }
                break;
        }
    }

    /// <summary>向幻灯片添加图表。由 LayoutEngine.Apply 统一排版坐标</summary>
    private static void AddChart(PptxWriter writer, Int32 idx, PptElement elem)
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
            foreach (var s in elem.Series)
            {
                chart.Series.Add(new NewLife.Office.Ppt.ChartSeries
                {
                    Name = s.Name,
                    Values = s.Data!.Cast<Double>().ToArray(),
                });
            }
        }
    }

    /// <summary>根据布局类型自动添加视觉装饰元素（分隔线、封面色柱）</summary>
    private static void AddAutoDecoration(PptxWriter writer, Int32 idx, PptPageModel slide, String accentColor)
    {
        const Double marginL = 2.0;
        const Double contentW = 29.87;

        var layout = (slide.Layout ?? "title_content").ToLowerInvariant();

        if (layout is "title_only")
        {
            // 封面左侧装饰色柱（x=0, y=0, 宽1.5cm, 全高）
            var sp = writer.AddShape(idx, "rect", 0.0, 0.0, 1.5, 19.05, accentColor);
            if (sp != null) sp.LineColor = null;
        }
        else if (layout is "title_content" or "two_column")
        {
            // 标题下方细分隔线（y≈3.65cm，高0.09cm，全内容宽）
            var sp = writer.AddShape(idx, "rect", marginL, 3.65, contentW, 0.09, accentColor);
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
        if (!saved) throw new InvalidOperationException($"PPTX 归档失败：{fileName}");

        var url = $"/cube/file?id={attachment.Id}.pptx";
        return new ArchiveResult(attachment.Id, url);
    }

    private static Int32 DefaultFontSize(String? role) => role switch
    {
        "title"    => 28,
        "subtitle" => 18,
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
