using System.ComponentModel;

namespace NewLife.ChatAI.Models;

/// <summary>Word 文档节定义</summary>
public class DocSectionModel
{
    [Description("节标题文本，如「本周完成」")]
    public String? Heading { get; set; }

    [Description("标题级别：1=一级（最大），2=二级，3=三级，默认1")]
    public Int32 HeadingLevel { get; set; } = 1;

    [Description("正文段落文本（可选，与 elements 配合使用）")]
    public String? Content { get; set; }

    [Description("结构化元素数组（可选）：paragraph/bullet_list/ordered_list/table/image/page_break")]
    public DocElement[]? Elements { get; set; }
}

/// <summary>Word 文档元素（段落/列表/表格/图片/分页）</summary>
public class DocElement
{
    [Description("元素类型：paragraph（段落）/ bullet_list（无序列表）/ ordered_list（有序列表）/ table（表格）/ image（图片）/ page_break（分页）")]
    public String? Type { get; set; }

    [Description("段落文本（type=paragraph 时使用）")]
    public String? Text { get; set; }

    [Description("列表条目数组（type=bullet_list 或 ordered_list 时使用）")]
    public String[]? Items { get; set; }

    [Description("表格列标题（type=table 时必填）")]
    public String[]? Headers { get; set; }

    [Description("表格数据行（type=table 时必填），按列顺序平铺的单元格值，自动按表头数量分组")]
    public String[]? Rows { get; set; }

    [Description("图片 URL（type=image 时必填），支持 https:// 外链或 /cube/image?id=xxx 内部附件")]
    public String? Src { get; set; }

    [Description("图片宽度（厘米），type=image 时有效，默认 14cm")]
    public Double? WidthCm { get; set; }

    [Description("图片高度（厘米），type=image 时有效，默认 10cm")]
    public Double? HeightCm { get; set; }
}

/// <summary>build_doc 工具结果 JSON</summary>
public sealed record DocResult(
    String BuildId,
    String Title,
    Int32 SectionCount,
    String DownloadUrl,
    Int64 AttachmentId,
    Int64 FileSize,
    String? Theme);
