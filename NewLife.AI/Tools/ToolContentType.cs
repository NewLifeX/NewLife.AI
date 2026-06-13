namespace NewLife.AI.Tools;

/// <summary>工具内容类型。对齐 MCP content[].type</summary>
public enum ToolContentType
{
    /// <summary>纯文本</summary>
    Text,

    /// <summary>图片。SVG 或 Base64 编码的位图</summary>
    Image,

    /// <summary>资源引用。对齐 MCP EmbeddedResource</summary>
    Resource,
}
