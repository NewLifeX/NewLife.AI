using System.ComponentModel;
using System.Text.Json;
using NewLife;
using NewLife.AI.Tools;
using NewLife.ChatAI.Models;
using NewLife.Collections;
using NewLife.Log;
using NewLife.Office;
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
    [Description("生成 Word 文档（.docx）并返回下载链接。支持标题/段落/列表/表格/图片/分页。sections JSON 数组描述每个节，每节包含 heading（节标题）、content（正文）、elements（结构化元素）。建议配合 build-doc 技能使用。")]
    public async Task<ToolResult> BuildDoc(
        [Description("文档标题（≤ 30 字），如「Q2季度总结报告」")] String title,
        [Description(@"文档节数组（JSON）。每节字段：heading（节标题文字）、headingLevel（级别1~3，默认1）、content（正文段落，可选）、elements（元素数组，可选）。elements 元素类型：paragraph（{type,text}）、bullet_list（{type,items}）、ordered_list（{type,items}）、table（{type,headers,rows}）、image（{type,src,widthCm?,heightCm?}）、page_break（{type}）。示例：[{""heading"":""本周完成"",""headingLevel"":1,""elements"":[{""type"":""bullet_list"",""items"":[""功能A上线"",""Bug修复3项""]}]},{""heading"":""下周计划"",""content"":""下周重点工作："",""elements"":[{""type"":""ordered_list"",""items"":[""继续推进功能B"",""性能优化""]}]}]")] String sections,
        [Description("主题（可选）：卡片风格 Key 或内置名（blue/dark/corporate/warm/green/minimal）")] String? theme = null,
        ToolCallContext? context = null)
    {
        if (sections.IsNullOrEmpty())
            throw new ToolException("参数错误：sections 不能为空", "请提供文档节 JSON 数组后重试，或直接回复用户说明无法生成文档。");
        var sectionList = sections.ToJsonEntity<DocSectionModel[]>();
        if (sectionList == null)
            throw new ToolException("sections JSON 格式错误", "请检查 JSON 语法后重试，或直接回复用户说明无法生成文档。");
        if (sectionList.Length == 0)
            throw new ToolException("sections 不能为空数组", "请提供至少一个节后重试，或直接回复用户说明无法生成文档。");

        log.Info("[BuildDoc] 开始生成：{0}，{1} 节，主题：{2}", title, sectionList.Length, theme ?? "none");

        var docxBytes = await BuildDocxAsync(sectionList, title, theme);
        var archiveResult = await ArchiveFileAsync(docxBytes, title,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document", ".docx", "Word");

        log.Info("[BuildDoc] 生成完成：{0}，{1} 字节，URL：{2}", title, docxBytes.Length, archiveResult.Url);

        var buildId = context?.ToolCallId ?? $"docx_{Guid.NewGuid():N}";
        var result = new DocResult(buildId, title, sectionList.Length,
            archiveResult.Url, archiveResult.AttachmentId, docxBytes.Length, theme);

        var resultJson = JsonSerializer.Serialize(result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        return new ToolResult(ToolContent.ForUser(resultJson),
            ToolContent.ForLlm($"[已生成{sectionList.Length}节文档：{title}]"));
    }

    #endregion

    #region Word 构建

    private async Task<Byte[]> BuildDocxAsync(DocSectionModel[] sectionList, String title, String? theme)
    {
        using var ms = new MemoryStream();
        var writer = new WordWriter();
        writer.DocumentProperties = new WordDocumentProperties { Title = title };

        // 从主题取 Accent1 用于表格表头背景色
        var tableHeaderBg = ThemeColors.GetPrimary(theme);

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
                writer.AppendParagraph(section.Content!, WordParagraphStyle.Normal);

            // 结构化元素
            if (section.Elements == null) continue;
            foreach (var elem in section.Elements)
            {
                switch (elem.Type?.ToLowerInvariant())
                {
                    case "paragraph":
                        if (!elem.Text.IsNullOrEmpty())
                            writer.AppendParagraph(elem.Text!, WordParagraphStyle.Normal);
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
                        if (elem.Headers is { Length: > 0 } && elem.Rows is { Length: > 0 })
                        {
                            var allRows = new List<IEnumerable<String>> { elem.Headers };
                            allRows.AddRange(elem.Rows.Select(r => r.AsEnumerable()));
                            var tblStyle = !tableHeaderBg.IsNullOrEmpty()
                                ? new WordTableStyle { HeaderBgColor = tableHeaderBg }
                                : null;
                            writer.AppendTable(allRows, firstRowHeader: true, tblStyle);
                        }
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
                }
            }
        }

        writer.Save(ms);
        return ms.ToArray();
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
        var fileName = $"ai-{kind.ToLower()}-{safeTitle}-{DateTime.Now:yyyyMMddHHmmssfff}{extension}";
        var att = new Attachment { Category = "StarChat", ContentType = contentType, Size = fileBytes.Length, Enable = true, UploadTime = DateTime.Now };
        try { att["Remark"] = $"AI生成{kind}｜标题:{title}"; } catch { }
        await using var ms = new MemoryStream(fileBytes);
        if (!await att.SaveFile(ms, null, fileName)) throw new InvalidOperationException($"{kind} 归档失败");
        return new ArchiveResult(att.Id, $"/cube/file?id={att.Id}{extension}");
    }

    private static String CleanFileName(String name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = Pool.StringBuilder.Get();
        foreach (var c in name) { if (Array.IndexOf(invalid, c) < 0 && sb.Length < 30) sb.Append(c); }
        return sb.Return(true).Trim();
    }

    private sealed record ArchiveResult(Int64 AttachmentId, String Url);

    #endregion
}
