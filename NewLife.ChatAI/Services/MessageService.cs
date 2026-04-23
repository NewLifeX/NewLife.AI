using System.Text;
using NewLife.Collections;
using NewLife.Log;
using NewLife.Office;
using NewLife.Serialization;
using Attachment = NewLife.Cube.Entity.Attachment;
using ILog = NewLife.Log.ILog;

namespace NewLife.ChatAI.Services;

/// <summary>消息生成服务（ChatAI 社区版）。直接继承 <see cref="MessageFlow"/> 模板方法基类，
/// 仅在基类降级实现上补充 <b>Cube 部门信息</b> 与 <b>NewLife.Office 文档解析</b> 两项增强。</summary>
/// <remarks>
/// <para>核心流程（Validate → Prepare → Execute → Persist → PostProcess 五段式模板、4 大 public 入口）完全由基类提供，本类只需 override 扩展点。</para>
/// <para>业务数据模型 <see cref="MessageDto"/> / <see cref="SendMessageRequest"/> / <see cref="ToolCallDto"/> 位于 <c>NewLife.ChatAI.Models</c>。</para>
/// </remarks>
/// <param name="pipeline">已装配好三层能力的对话执行管道</param>
/// <param name="modelService">模型服务（用于模型解析和客户端创建）</param>
/// <param name="backgroundService">后台生成服务</param>
/// <param name="usageService">用量统计服务</param>
/// <param name="setting">AI对话系统配置</param>
/// <param name="tracer">追踪器</param>
/// <param name="log">日志</param>
/// <param name="enrichers">上下文增强器（可选，DI 自动注入）</param>
/// <param name="postProcessors">消息流后处理器（可选，DI 自动注入）</param>
public class MessageService(IChatPipeline pipeline, ModelService modelService, BackgroundGenerationService? backgroundService, UsageService? usageService, ChatSetting setting, ITracer tracer, ILog log, IEnumerable<IContextEnricher>? enrichers = null, IEnumerable<IMessageFlowPostProcessor>? postProcessors = null)
    : MessageFlow(pipeline, modelService, backgroundService, usageService, setting, tracer, log, enrichers, postProcessors)
{
    #region 覆盖：完整多模态（图片 + Office 文档）

    /// <inheritdoc />
    protected override AiChatMessage BuildMultimodalUserMessage(String attachmentsJson, String? textContent)
    {
        var contents = new List<AIContent>();
        var docParts = new List<String>();

        // 前端发送的 attachmentIds 为字符串数组 ["123","456"]，兼容 Int64 数组 [123,456]
        var ids = ParseAttachmentIdsEx(attachmentsJson);
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

    #endregion

    #region 辅助

    /// <summary>解析附件ID列表 JSON。兼容字符串数组和整数数组两种格式</summary>
    /// <param name="json">附件ID列表 JSON</param>
    /// <returns>ID 列表，解析失败返回 null</returns>
    private static IList<Int64>? ParseAttachmentIdsEx(String json)
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
    /// <returns>提取的 markdown 文本，无法识别格式时返回 null</returns>
    internal static String? ExtractDocumentAsMarkdown(String filePath, String? fileName)
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

    #endregion
}
