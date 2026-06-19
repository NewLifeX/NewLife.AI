using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using NewLife.AI.Tools;
using NewLife.Collections;
using NewLife.Log;
using NewLife.Office;
using NewLife.Serialization;
using Attachment = NewLife.Cube.Entity.Attachment;

namespace NewLife.ChatAI.Helpers;

/// <summary>附件内容构建器。将附件 ID 列表与文本内容组合为多模态 <see cref="AiChatMessage"/>。
/// 从 MessageService 提取为独立静态工具类，供 BuildHistoryMessage 覆盖点共用</summary>
public static class AttachmentContentBuilder
{
    private static readonly String[] _imageKeywords = ["图片", "照片"];
    private static readonly Regex _urlRegex = new(@"https?://[^\s<>""]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly HttpClient _httpClient = ToolHelper.CreateDefaultHttpClient();

    /// <summary>将附件 JSON 与文本内容构建为多模态用户消息</summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item>图片附件 → <see cref="ImageContent"/>（base64 内嵌）</item>
    ///   <item>文档附件 → <see cref="NewLife.Office"/> 提取 Markdown，前置注入文本</item>
    ///   <item>无图片时退化为纯文本 <see cref="AiChatMessage"/></item>
    /// </list>
    /// </remarks>
    /// <param name="attachmentsJson">附件 ID 列表 JSON（Int64 或字符串数组）</param>
    /// <param name="textContent">用户文本内容</param>
    /// <param name="inlineImages">内联图片列表（可选），用于直接传入图片内容而非附件 ID</param>
    /// <returns>多模态或纯文本 AiChatMessage</returns>
    public static AiChatMessage Build(String attachmentsJson, String? textContent, IList<ImageContent>? inlineImages = null)
    {
        var contents = new List<AIContent>();
        var docParts = new List<String>();

        var ids = ParseAttachmentIds(attachmentsJson);
        if (ids != null)
        {
            foreach (var id in ids)
            {
                try
                {
                    var att = Attachment.FindById(id);
                    if (att == null || !att.Enable) continue;

                    var filePath = att.GetFilePath();
                    if (filePath.IsNullOrEmpty() || !File.Exists(filePath)) continue;

                    if (!att.ContentType.IsNullOrEmpty() && att.ContentType.StartsWithIgnoreCase("image/"))
                        contents.Add(new ImageContent { Data = File.ReadAllBytes(filePath), MediaType = att.ContentType });
                    else
                    {
                        // 非图片文件：用 NewLife.Office 提取文本，作为上下文注入消息
                        var docText = ExtractDocumentAsMarkdown(filePath!, att.FileName);
                        if (!docText.IsNullOrEmpty())
                            docParts.Add($"【附件：{att.FileName}】\n{docText}");
                    }
                }
                catch (Exception ex)
                {
                    XTrace.WriteException(ex);
                }
            }
        }

        // 文本中提取的内联图片（HTTP 下载后保存为附件）
        if (inlineImages != null)
        {
            foreach (var img in inlineImages)
                contents.Add(img);
        }

        // 将文档内容前置注入到用户文本
        if (docParts.Count > 0)
        {
            var docContext = String.Join("\n\n---\n\n", docParts);
            textContent = docContext + (textContent.IsNullOrEmpty() ? String.Empty : $"\n\n---\n\n{textContent}");
        }

        if (!textContent.IsNullOrEmpty())
            contents.Add(new TextContent(textContent));

        // 无图片附件时退化为纯文本
        if (contents.Count == 0 || (contents.Count == 1 && contents[0] is TextContent))
            return new AiChatMessage { Role = "user", Content = textContent };

        return new AiChatMessage { Role = "user", Contents = contents };
    }

    /// <summary>解析附件 ID 列表 JSON。兼容字符串数组和整数数组两种格式</summary>
    /// <param name="json">附件 ID 列表 JSON（如 <c>[123,456]</c> 或 <c>["123","456"]</c>）</param>
    /// <returns>ID 列表；解析失败或为空时返回 null</returns>
    public static IList<Int64>? ParseAttachmentIds(String json)
    {
        // 优先尝试 Int64 数组
        var ids = json.ToJsonEntity<List<Int64>>();
        if (ids != null && ids.Count > 0 && ids[0] != 0) return ids;

        // 前端 attachmentIds.map(String) 产生字符串数组 ["123","456"]
        var strIds = json.ToJsonEntity<List<String>>();
        if (strIds != null && strIds.Count > 0)
            return strIds.Select(s => s.ToLong()).Where(v => v > 0).ToList();

        return null;
    }

    /// <summary>使用 NewLife.Office 将文档文件提取为 Markdown 文本。支持 docx/doc/pdf/xlsx/xls/pptx/ppt/txt/csv/md</summary>
    /// <param name="filePath">文件在磁盘上的完整路径</param>
    /// <param name="fileName">原始文件名（用于按扩展名路由及错误提示）</param>
    /// <returns>提取的 Markdown 文本；无法识别格式时返回 null</returns>
    public static String? ExtractDocumentAsMarkdown(String filePath, String? fileName)
    {
        var ext = Path.GetExtension(fileName ?? filePath).ToLowerInvariant();
        try
        {
            switch (ext)
            {
                case ".docx":
                case ".doc":
                    {
                        using var reader = new WordReader(filePath);
                        var sb = Pool.StringBuilder.Get();
                        foreach (var para in reader.ReadParagraphs())
                        {
                            sb.AppendLine(para);
                        }
                        // 将表格格式化为 markdown
                        foreach (var table in reader.ReadTables())
                        {
                            if (table.Length == 0) continue;
                            sb.AppendLine();
                            foreach (var row in table)
                            {
                                sb.Append("| ");
                                sb.Append(String.Join(" | ", row.Select(c => (c ?? String.Empty).Replace("|", "\\|"))));
                                sb.AppendLine(" |");
                            }
                            sb.AppendLine();
                        }
                        return sb.Return(true);
                    }
                case ".pdf":
                    {
                        using var reader = new PdfReader(filePath);
                        return reader.ExtractText();
                    }
                case ".xlsx":
                case ".xls":
                    {
                        using var reader = new ExcelReader(filePath);
                        var sb = Pool.StringBuilder.Get();
                        var sheets = reader.Sheets;
                        if (sheets != null)
                        {
                            foreach (var sheet in sheets)
                            {
                                sb.AppendLine($"## {sheet}");
                                sb.AppendLine();
                                var rows = reader.ReadRows(sheet).ToList();
                                for (var i = 0; i < rows.Count; i++)
                                {
                                    var row = rows[i];
                                    var cells = row.Select(c => Convert.ToString(c) ?? String.Empty);
                                    sb.Append("| ");
                                    sb.Append(String.Join(" | ", cells.Select(c => c.Replace("|", "\\|"))));
                                    sb.AppendLine(" |");
                                    // 首行后插入分隔线
                                    if (i == 0)
                                    {
                                        sb.Append("| ");
                                        sb.Append(String.Join(" | ", row.Select(_ => "---")));
                                        sb.AppendLine(" |");
                                    }
                                }
                                sb.AppendLine();
                            }
                        }
                        return sb.Return(true);
                    }
                case ".pptx":
                case ".ppt":
                    {
                        using var reader = new PptxReader(filePath);
                        return reader.ReadAllText();
                    }
                case ".txt":
                case ".csv":
                case ".md":
                    return File.ReadAllText(filePath, Encoding.UTF8);
                default:
                    return null;
            }
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
            return null;
        }
    }

    /// <summary>从文本中提取内联图片 URL，下载后保存为附件。同时满足含关键词、URL 可匹配、能下载、ContentType 为 image/* 四个条件才生效</summary>
    /// <param name="text">用户消息文本</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>成功下载并保存的 ImageContent 列表；条件不满足时返回 null</returns>
    public static async Task<List<ImageContent>?> ExtractAndSaveInlineImagesAsync(String? text, CancellationToken cancellationToken)
    {
        if (text.IsNullOrEmpty()) return null;

        // 检查是否含图片关键词
        var hasKeyword = false;
        foreach (var kw in _imageKeywords)
        {
            if (text.Contains(kw))
            {
                hasKeyword = true;
                break;
            }
        }
        if (!hasKeyword) return null;

        var matches = _urlRegex.Matches(text);
        if (matches.Count == 0) return null;

        List<ImageContent>? result = null;
        foreach (Match match in matches)
        {
            var url = match.Value.TrimEnd('.', ',', '!', '?', ')', ']', ';');
            Uri uri;
            try { uri = new Uri(url); }
            catch { continue; }

            // SSRF 防护：阻止访问内网 / 回环地址
            if (ToolHelper.IsSsrfRisk(uri.Host)) continue;

            Byte[]? bytes = null;
            String? mediaType = null;
            try
            {
                using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (!response.IsSuccessStatusCode) continue;

                mediaType = response.Content.Headers.ContentType?.MediaType;
                if (mediaType.IsNullOrEmpty() || !mediaType.StartsWithIgnoreCase("image/")) continue;

                bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            }
            catch (Exception ex) { XTrace.WriteException(ex); continue; }

            if (bytes == null || bytes.Length == 0) continue;

            // 从 URL 路径取文件名；无扩展时根据内容类型推断
            var fileName = Path.GetFileName(uri.LocalPath);
            if (fileName.IsNullOrEmpty() || !Path.HasExtension(fileName))
            {
                var ext = mediaType switch
                {
                    "image/jpeg" => ".jpg",
                    "image/png" => ".png",
                    "image/gif" => ".gif",
                    "image/webp" => ".webp",
                    _ => ".bin",
                };
                fileName = $"inline_{DateTime.Now:yyyyMMddHHmmssfff}{ext}";
            }

            // 保存为附件记录，同用户上传
            try
            {
                var att = new Attachment
                {
                    Category = "ChatAI",
                    ContentType = mediaType,
                    Size = bytes.Length,
                    Enable = true,
                    UploadTime = DateTime.Now,
                };
                using var ms = new MemoryStream(bytes);
                var saved = await att.SaveFile(ms, null, fileName);
                if (!saved) continue;
            }
            catch (Exception ex) { XTrace.WriteException(ex); continue; }

            result ??= [];
            result.Add(new ImageContent { Data = bytes, MediaType = mediaType });
        }

        return result;
    }
}
