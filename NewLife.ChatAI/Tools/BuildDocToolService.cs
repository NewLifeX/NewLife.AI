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

    /// <summary>生成 Word 文档（.docx）并归档到附件表，返回下载链接</summary>
    [ToolDescription("build_doc", IsSystem = false,
        Triggers = "build word,word document,generate doc",
        AssistantTriggers = "word,docx,document,report,proposal,minutes,build_doc",
        ReadOnly = false)]
    [DisplayName("Word Document")]
    [Description("Generate a Word document (.docx) and return a download link. Supports headings, paragraphs, lists, tables, images, page breaks, dividers, callouts, KPIs, quotes, and code blocks. The sections JSON array describes each section, with heading, content, and elements")]
    public async Task<ToolResult> BuildDoc(
        [Description("Document title (up to 30 characters), e.g. Q2 Quarterly Report")] String title,
        [Description(@"Section array (JSON). Fields of each section: heading (section title), headingLevel (1-3, default 1), content (body paragraph, optional), elements (element array, optional). Element types: paragraph ({type,text, optional bold/italic/underline/fontSize/color/alignment/backgroundColor}), bullet_list ({type,items}), ordered_list ({type,items}), table ({type,headers,rows, optional tableStyle}; rows is a 2-D array where each row is an array of cell values, e.g. [[""Alice"",""85""],[""Bob"",""92""]]), image ({type,src,widthCm?,heightCm?}), page_break ({type}), divider ({type}), callout ({type,text,variant?=info/success/warning/danger}), kpi ({type,kpiValue,kpiLabel?,kpiTrend?=up/down/flat}), quote ({type,text,quoteSource?}), code ({type,text,codeLanguage?})")] DocSectionModel[]? sections,
        [Description("Theme (optional): a card style key (e.g. enterprise-blue) or a built-in name (blue/dark/corporate/warm/green/minimal); when omitted, the conversation style or brand default is used")] String? theme = null,
        ToolCallContext? context = null)
    {
        if (title.IsNullOrEmpty())
            throw new ToolException("Invalid parameter: title cannot be empty", "Provide a document title and try again.");

        if (sections == null || sections.Length == 0)
            throw new ToolException("Invalid parameter: sections cannot be empty", "Provide a section array and try again, or reply to the user that the document cannot be generated.");
        var sectionList = sections;

        // 主题解析：显式传参 > 会话卡片风格 > 品牌默认
        var themeKey = OfficeStyleKit.ResolveTheme(theme, context?.Request?.ConversationId);
        log.Info("[BuildDoc] Generating: {0}, {1} sections, theme: {2}", title, sectionList.Length, themeKey ?? "default");

        var docxBytes = await BuildDocxAsync(sectionList, title, themeKey);
        var archiveResult = await ArchiveFileAsync(docxBytes, title,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document", ".docx", "Word");

        log.Info("[BuildDoc] Generated: {0}, {1} bytes, URL: {2}", title, docxBytes.Length, archiveResult.Url);

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
                                // 按图片原始比例适配：只给单边尺寸时按比例推算另一边，避免拉伸变形
                                var (fitW, fitH) = FitImageSize(imgBytes);
                                Double w, h;
                                if (elem.WidthCm.HasValue && elem.HeightCm.HasValue) { w = elem.WidthCm.Value; h = elem.HeightCm.Value; }
                                else if (elem.WidthCm.HasValue) { w = elem.WidthCm.Value; h = Math.Round(w * fitH / fitW, 2); }
                                else if (elem.HeightCm.HasValue) { h = elem.HeightCm.Value; w = Math.Round(h * fitW / fitH, 2); }
                                else { w = fitW; h = fitH; }
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
            // 显式样式优先：底色/字色可单独覆盖；只给底色时自动配对比字色
            if (!elem.TableStyle.HeaderBgColor.IsNullOrEmpty())
                brand.HeaderBgColor = elem.TableStyle.HeaderBgColor;
            if (!elem.TableStyle.HeaderFontColor.IsNullOrEmpty())
                brand.HeaderFontColor = elem.TableStyle.HeaderFontColor;
            else if (!elem.TableStyle.HeaderBgColor.IsNullOrEmpty())
                brand.HeaderFontColor = OfficeStyleKit.Luma(elem.TableStyle.HeaderBgColor) < 160 ? "FFFFFF" : "262626";
            if (!elem.TableStyle.StripeColor.IsNullOrEmpty())
                brand.StripeColor = elem.TableStyle.StripeColor;
        }

        // 列宽：按内容宽度比例分配页面可用宽度（替代均分，宽窄列更协调）
        var availW = writer.PageSettings.PageWidth - writer.PageSettings.MarginLeft - writer.PageSettings.MarginRight;
        var weights = OfficeStyleKit.EstimateColumnWidths(elem.Headers, elem.Rows);
        var total = weights.Sum();
        if (total > 0 && availW > 0)
        {
            var widths = new Int32[weights.Length];
            for (var i = 0; i < weights.Length; i++)
                widths[i] = availW * weights[i] / total;
            brand.ColumnWidths = widths;
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
        para.Runs.Add(new Run { Text = elem.Text ?? String.Empty, Properties = runProps ?? new RunProperties() });
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

    /// <summary>按图片原始宽高比计算排版尺寸。宽 14cm 基准、高不超 17cm，解析失败回退 14×10</summary>
    private static (Double WidthCm, Double HeightCm) FitImageSize(Byte[] bytes)
    {
        const Double maxW = 14.0;
        const Double maxH = 17.0;

        var (px, py) = TryGetImageSize(bytes);
        if (px <= 0 || py <= 0) return (maxW, 10.0);

        var ratio = (Double)py / px;
        var w = maxW;
        var h = w * ratio;
        if (h > maxH)
        {
            h = maxH;
            w = h / ratio;
        }
        return (Math.Round(w, 2), Math.Round(h, 2));
    }

    /// <summary>轻量解析 PNG/GIF/JPEG 原始像素尺寸（读取文件头，失败返回 0）</summary>
    private static (Int32 Width, Int32 Height) TryGetImageSize(Byte[] bytes)
    {
        try
        {
            // PNG：8 字节签名 + 8 字节块头后为 IHDR 宽高（大端）
            if (bytes.Length > 24 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
            {
                var w = (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19];
                var h = (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23];
                return (w, h);
            }
            // GIF：宽高在 6-11 字节（小端）
            if (bytes.Length > 10 && bytes[0] == (Byte)'G' && bytes[1] == (Byte)'I' && bytes[2] == (Byte)'F')
            {
                var w = bytes[6] | (bytes[7] << 8);
                var h = bytes[8] | (bytes[9] << 8);
                return (w, h);
            }
            // JPEG：扫描 SOF 段中的高宽
            if (bytes.Length > 4 && bytes[0] == 0xFF && bytes[1] == 0xD8)
            {
                var i = 2;
                while (i + 9 < bytes.Length)
                {
                    if (bytes[i] != 0xFF) { i++; continue; }
                    var marker = bytes[i + 1];
                    if (marker == 0xD8 || marker == 0x01 || (marker >= 0xD0 && marker <= 0xD7)) { i += 2; continue; }
                    var len = (bytes[i + 2] << 8) | bytes[i + 3];
                    if (marker >= 0xC0 && marker <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC)
                    {
                        var h = (bytes[i + 5] << 8) | bytes[i + 6];
                        var w = (bytes[i + 7] << 8) | bytes[i + 8];
                        return (w, h);
                    }
                    i += 2 + len;
                }
            }
        }
        catch { /* 解析失败按未知尺寸处理 */ }
        return (0, 0);
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
        if (!await att.SaveFile(ms, null, fileName)) throw new InvalidOperationException(Localization.Translate("{0} archiving failed", kind));
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
