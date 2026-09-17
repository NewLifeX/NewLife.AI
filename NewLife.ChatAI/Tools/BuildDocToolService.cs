using System.ComponentModel;
using System.Text.Json;
using NewLife.AI.Tools;
using NewLife.ChatAI.Helpers;
using NewLife.ChatAI.Models;
using NewLife.Collections;
using NewLife.Log;
using NewLife.Office.Word;
using NewLife.Serialization;
using Attachment = NewLife.Cube.Entity.Attachment;

namespace NewLife.ChatAI.Tools;

/// <summary>Word 文档生成工具服务。接收结构化节 JSON，用 WordWriter 生成 .docx 并归档到附件表</summary>
/// <param name="log">日志</param>
public class BuildDocToolService(ILog log)
{
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };

    #region 工具方法

    [ToolDescription("build_doc", IsSystem = false,
        Triggers = "生成Word,生成文档,制作文档,写报告",
        AssistantTriggers = "Word,docx,文档,报告,方案书,周报,会议纪要,生成文档,生成Word,报告文档,build_doc",
        ReadOnly = false)]
    [DisplayName("Word文档生成")]
    [Description("生成 Word 文档（.docx）并返回下载链接。支持标题/段落/列表/表格/图片/分页/分隔线/高亮框/KPI/引用/代码块。sections JSON 数组描述每个节，每节包含 heading（节标题）、content（正文）、elements（结构化元素）")]
    public async Task<ToolResult> BuildDoc(
        [Description("文档标题（≤ 30 字），如「Q2季度总结报告」")] String title,
        [Description(@"文档节数组（JSON）。每节字段：heading（节标题文字）、headingLevel（级别1~3，默认1）、content（正文段落，可选）、elements（元素数组，可选）。elements 元素类型：paragraph（{type,text,可选bold/italic/underline/fontSize/color/alignment/backgroundColor}）、bullet_list（{type,items}）、ordered_list（{type,items}）、table（{type,headers,rows,可选tableStyle}，rows为二维数组每行一个单元格值数组如 [[""张三"",""85""],[""李四"",""92""]]）、image（{type,src,widthCm?,heightCm?}）、page_break（{type}）、divider（{type}分隔线）、callout（{type,text,variant?=info/success/warning/danger}高亮提示框）、kpi（{type,kpiValue,kpiLabel?,kpiTrend?=up/down/flat}大数字指标）、quote（{type,text,quoteSource?}引用块）、code（{type,text,codeLanguage?}代码块）")] DocSectionModel[]? sections,
        [Description("主题（可选）：卡片风格 Key（如 enterprise-blue）或内置名（blue/dark/corporate/warm/green/minimal），不传则沿用会话风格或品牌默认")] String? theme = null,
        ToolCallContext? context = null)
    {
        if (title.IsNullOrEmpty())
            throw new ToolException("参数错误：title 不能为空", "请提供文档标题后重试。");

        if (sections == null || sections.Length == 0)
            throw new ToolException("参数错误：sections 不能为空", "请提供文档节数组后重试，或直接回复用户说明无法生成文档。");
        var sectionList = sections;

        // 主题解析：显式传参 > 会话卡片风格 > 品牌默认
        var themeKey = OfficeStyleKit.ResolveTheme(theme, context?.Request?.ConversationId);
        log.Info("[BuildDoc] 开始生成：{0}，{1} 节，主题：{2}", title, sectionList.Length, themeKey ?? "default");

        var docxBytes = await BuildDocxAsync(sectionList, title, themeKey);
        var archiveResult = await ArchiveFileAsync(docxBytes, title,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document", ".docx", "Word");

        log.Info("[BuildDoc] 生成完成：{0}，{1} 字节，URL：{2}", title, docxBytes.Length, archiveResult.Url);

        var buildId = context?.ToolCallId ?? $"docx_{Guid.NewGuid():N}";
        var result = new DocResult(buildId, title, sectionList.Length,
            archiveResult.Url, archiveResult.AttachmentId, docxBytes.Length, themeKey);

        // 从 JsonSerializerOptions.Default 派生以携带 TypeInfoResolver，避免序列化触发 MakeReadOnly 抛错
        var resultJson = JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerOptions.Default) { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        return ToolResult.ForAudiences(resultJson, $"[已生成{sectionList.Length}节文档：{title}]");
    }

    #endregion

    #region Word 构建

    private async Task<Byte[]> BuildDocxAsync(DocSectionModel[] sectionList, String title, String? theme)
    {
        using var ms = new MemoryStream();
        var writer = new WordWriter();
        writer.DocumentProperties = new DocumentProperties { Title = title };

        // 品牌视觉：卡片风格主题 → 完整调色板 → 样式表注入（字体/行距/标题配色/表格边框）
        var palette = OfficePalette.Resolve(theme);
        writer.StylesXml = OfficeStyleKit.BuildWordStylesXml(palette);

        // 页面：四边 2.5cm，页脚显示标题与页码
        var page = writer.PageSettings;
        page.MarginTop = page.MarginBottom = 1417;
        page.MarginLeft = page.MarginRight = 1417;
        page.FooterText = title;

        foreach (var section in sectionList)
        {
            // 节标题
            if (!section.Heading.IsNullOrEmpty())
            {
                var level = section.HeadingLevel is >= 1 and <= 6 ? section.HeadingLevel : 1;
                writer.AppendHeading(section.Heading!, level);
            }

            // 正文段落
            if (!section.Content.IsNullOrEmpty())
                writer.AppendParagraph(section.Content!, ParagraphStyle.Normal);

            // 结构化元素
            if (section.Elements == null) continue;
            foreach (var elem in section.Elements)
            {
                var runProps = BuildRunProperties(elem);
                switch (elem.Type?.ToLowerInvariant())
                {
                    case "paragraph":
                        if (!elem.Text.IsNullOrEmpty())
                            WriteParagraph(writer, elem, runProps);
                        break;
                    case "bullet_list":
                        if (elem.Items is { Length: > 0 })
                            writer.AppendBulletList(elem.Items);
                        break;
                    case "ordered_list":
                        if (elem.Items is { Length: > 0 })
                            writer.AppendOrderedList(elem.Items);
                        break;
                    case "table":
                        WriteTable(writer, elem, palette);
                        break;
                    case "image":
                        if (!elem.Src.IsNullOrEmpty())
                        {
                            var imgBytes = await TryLoadImageAsync(elem.Src!);
                            if (imgBytes != null)
                            {
                                var w = elem.WidthCm ?? 14.0;
                                var h = elem.HeightCm ?? 10.0;
                                writer.InsertImage(imgBytes, "png", w, h);
                            }
                        }
                        break;
                    case "page_break":
                        writer.AppendPageBreak();
                        break;
                    case "divider":
                        writer.AppendHorizontalRule(palette.PaperBorder);
                        break;
                    case "callout":
                        WriteCallout(writer, elem, runProps);
                        break;
                    case "kpi":
                        WriteKpi(writer, elem, palette);
                        break;
                    case "quote":
                        WriteQuote(writer, elem, palette);
                        break;
                    case "code":
                        WriteCodeBlock(writer, elem, palette);
                        break;
                }
            }
        }

        writer.Save(ms);
        return ms.ToArray();
    }

    /// <summary>从 DocElement 构建 RunProperties（文本格式）</summary>
    private static RunProperties? BuildRunProperties(DocElement elem)
    {
        if (elem.Bold == null && elem.Italic == null && elem.Underline == null
            && elem.FontSize == null && elem.Color.IsNullOrEmpty())
            return null;

        return new RunProperties
        {
            Bold = elem.Bold ?? false,
            Italic = elem.Italic ?? false,
            Underline = elem.Underline ?? false,
            FontSize = elem.FontSize,
            ForeColor = elem.Color,
        };
    }

    /// <summary>渲染段落（支持格式属性）</summary>
    private static void WriteParagraph(WordWriter writer, DocElement elem, RunProperties? runProps)
    {
        var para = writer.AppendParagraph(elem.Text!, ParagraphStyle.Normal, runProps ?? new RunProperties());
        if (!elem.Alignment.IsNullOrEmpty())
            para.Alignment = elem.Alignment;
        if (!elem.BackgroundColor.IsNullOrEmpty())
            para.BackgroundColor = elem.BackgroundColor;
    }

    /// <summary>渲染表格（AI 显式样式优先，缺项用品牌默认补齐）</summary>
    private static void WriteTable(WordWriter writer, DocElement elem, OfficePalette palette)
    {
        if (elem.Headers is not { Length: > 0 } || elem.Rows is not { Length: > 0 }) return;

        var allRows = new List<IEnumerable<String>> { elem.Headers };
        allRows.AddRange(elem.Rows.Select(r => r.AsEnumerable()));

        var brand = OfficeStyleKit.CreateWordTableStyle(palette);
        if (elem.TableStyle != null)
        {
            // 显式背景色优先；表头字色未传时按底色亮度自动选黑/白，避免白字浅底看不清
            if (!elem.TableStyle.HeaderBgColor.IsNullOrEmpty())
            {
                brand.HeaderBgColor = elem.TableStyle.HeaderBgColor;
                brand.HeaderFontColor = !elem.TableStyle.HeaderFontColor.IsNullOrEmpty()
                    ? elem.TableStyle.HeaderFontColor
                    : OfficeStyleKit.Luma(elem.TableStyle.HeaderBgColor) < 160 ? "FFFFFF" : "262626";
            }
            if (!elem.TableStyle.StripeColor.IsNullOrEmpty())
                brand.StripeColor = elem.TableStyle.StripeColor;
        }

        writer.AppendTable(allRows, firstRowHeader: true, brand);
    }

    /// <summary>渲染高亮提示框（左侧彩色边框 + 浅色背景）</summary>
    private static void WriteCallout(WordWriter writer, DocElement elem, RunProperties? runProps)
    {
        var (bgColor, borderColor) = OfficeStyleKit.GetCalloutColors(elem.Variant);

        var para = new Paragraph
        {
            BackgroundColor = bgColor,
            Borders = new ParagraphBorders
            {
                Left = new Border
                {
                    Style = BorderStyle.Single,
                    Color = borderColor,
                    Width = 18, // ~2.25pt 醒目
                },
            },
        };
        para.Runs.Add(new Run { Text = elem.Text, Properties = runProps ?? new RunProperties() });
        writer.AppendParagraph(para);
    }

    /// <summary>渲染 KPI 大数字指标</summary>
    private static void WriteKpi(WordWriter writer, DocElement elem, OfficePalette palette)
    {
        if (elem.KpiValue.IsNullOrEmpty()) return;

        // 趋势箭头
        var prefix = elem.KpiTrend?.ToLowerInvariant() switch
        {
            "up" => "▲ ",
            "down" => "▼ ",
            _ => null,
        };
        var displayValue = prefix != null ? $"{prefix}{elem.KpiValue}" : elem.KpiValue;

        // 数值行（大字号加粗、品牌主色）
        writer.AppendParagraph(displayValue, ParagraphStyle.Normal,
            new RunProperties { Bold = true, FontSize = 28, ForeColor = palette.Primary });

        // 标签行（小字号次要色）
        if (!elem.KpiLabel.IsNullOrEmpty())
            writer.AppendParagraph(elem.KpiLabel, ParagraphStyle.Normal,
                new RunProperties { FontSize = 11, ForeColor = palette.PaperMutedForeground });
    }

    /// <summary>渲染引用块（斜体 + 左边框）</summary>
    private static void WriteQuote(WordWriter writer, DocElement elem, OfficePalette palette)
    {
        if (elem.Text.IsNullOrEmpty()) return;

        var para = new Paragraph
        {
            Alignment = "left",
            Borders = new ParagraphBorders
            {
                Left = new Border
                {
                    Style = BorderStyle.Single,
                    Color = palette.PaperBorder,
                    Width = 12,
                },
            },
        };
        para.Runs.Add(new Run
        {
            Text = elem.Text,
            Properties = new RunProperties { Italic = true, FontSize = elem.FontSize, ForeColor = elem.Color ?? palette.PaperMutedForeground },
        });
        writer.AppendParagraph(para);

        // 来源行（小字号次要色，右对齐）
        if (!elem.QuoteSource.IsNullOrEmpty())
        {
            var srcPara = new Paragraph { Alignment = "right" };
            srcPara.Runs.Add(new Run
            {
                Text = $"—— {elem.QuoteSource}",
                Properties = new RunProperties { FontSize = 10, ForeColor = palette.PaperMutedForeground },
            });
            writer.AppendParagraph(srcPara);
        }
    }

    /// <summary>渲染代码块（等宽字体 + 灰底）</summary>
    private static void WriteCodeBlock(WordWriter writer, DocElement elem, OfficePalette palette)
    {
        if (elem.Text.IsNullOrEmpty()) return;

        var para = new Paragraph { BackgroundColor = "F5F5F5" };
        para.Runs.Add(new Run
        {
            Text = elem.Text,
            Properties = new RunProperties
            {
                FontName = palette.MonoFont,
                EastAsiaFontName = palette.ChineseFont,
                FontSize = elem.FontSize ?? 9.5f,
                ForeColor = elem.Color ?? "333333",
            },
        });
        writer.AppendParagraph(para);
    }

    private async Task<Byte[]?> TryLoadImageAsync(String src)
    {
        try
        {
            if (src.StartsWithIgnoreCase("/cube/image") || src.StartsWithIgnoreCase("/cube/file"))
            {
                var idx = src.IndexOf('?');
                if (idx >= 0)
                {
                    var idPart = src[(idx + 1)..].Split('&').FirstOrDefault(p => p.StartsWith("id="))?[3..];
                    if (!idPart.IsNullOrEmpty())
                    {
                        var dot = idPart!.LastIndexOf('.');
                        var id = (dot >= 0 ? idPart[..dot] : idPart).ToLong();
                        var att = Attachment.FindById(id);
                        if (att != null) { var p = att.GetFilePath(); if (File.Exists(p)) return await File.ReadAllBytesAsync(p); }
                    }
                }
            }
            if (src.StartsWithIgnoreCase("https://") || src.StartsWithIgnoreCase("http://"))
                return await _httpClient.GetByteArrayAsync(src);
        }
        catch { }
        return null;
    }

    #endregion

    #region 归档

    private static async Task<ArchiveResult> ArchiveFileAsync(Byte[] fileBytes, String title,
        String contentType, String extension, String kind)
    {
        var safeTitle = CleanFileName(title);
        var fileName = $"{safeTitle}-{DateTime.Now:yyyyMMddHHmmssfff}{extension}";
        var att = new Attachment { Category = "StarChat", ContentType = contentType, Size = fileBytes.Length, Enable = true, UploadTime = DateTime.Now };
        try { att["Remark"] = $"AI生成{kind}｜标题:{title}"; } catch { }
        await using var ms = new MemoryStream(fileBytes);
        if (!await att.SaveFile(ms, null, fileName)) throw new InvalidOperationException($"{kind} 归档失败");
        return new ArchiveResult(att.Id, $"/cube/file?id={att.Id}{extension}");
    }

    private static String CleanFileName(String name)
    {
        if (name.IsNullOrEmpty()) return "未命名";

        var invalid = Path.GetInvalidFileNameChars();
        var sb = Pool.StringBuilder.Get();
        foreach (var c in name) { if (Array.IndexOf(invalid, c) < 0 && sb.Length < 30) sb.Append(c); }
        return sb.Return(true).Trim();
    }

    private sealed record ArchiveResult(Int64 AttachmentId, String Url);

    #endregion
}
