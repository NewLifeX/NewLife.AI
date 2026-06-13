namespace NewLife.AI.Tools;

/// <summary>工具内容块。描述工具返回的一个内容片段，含类型、数据和受众</summary>
/// <remarks>
/// 对齐 MCP CallToolResult.content[] + annotations.audience 设计。<br/>
/// 单类 + Type 枚举而非子类继承：所有内容类型共享相同数据形状（Data + MimeType），
/// 与 MCP 的 type 字符串区分机制一致，序列化更简单。
/// </remarks>
public sealed class ToolContent
{
    #region 属性
    /// <summary>内容类型</summary>
    public ToolContentType Type { get; }

    /// <summary>内容数据。Text 时为字符串；Image 时为 Base64 或 SVG 源码</summary>
    public String Data { get; }

    /// <summary>受众标记。决定此块发给 LLM、前端还是双方</summary>
    public ToolAudience Audience { get; }

    /// <summary>MIME 类型。可选，如 "image/svg+xml"、"application/json"</summary>
    public String? MimeType { get; }
    #endregion

    #region 构造
    /// <summary>实例化工具内容块</summary>
    /// <param name="data">内容数据</param>
    /// <param name="audience">受众</param>
    /// <param name="type">内容类型</param>
    /// <param name="mimeType">MIME 类型</param>
    public ToolContent(String data, ToolAudience audience = ToolAudience.Both,
        ToolContentType type = ToolContentType.Text, String? mimeType = null)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
        Audience = audience;
        Type = type;
        MimeType = mimeType;
    }
    #endregion

    #region 工厂方法
    /// <summary>创建同时发给 LLM 和前端的内容块</summary>
    public static ToolContent ForBoth(String text) => new(text);

    /// <summary>创建仅发给 LLM 的摘要内容块（不发前端）</summary>
    public static ToolContent ForLlm(String text) => new(text, ToolAudience.Llm);

    /// <summary>创建仅发给前端的内容块（不发 LLM）</summary>
    /// <param name="text">内容</param>
    /// <param name="mimeType">MIME 类型</param>
    public static ToolContent ForUser(String text, String? mimeType = null)
        => new(text, ToolAudience.User, mimeType: mimeType);

    /// <summary>创建前端 SVG 图片内容块</summary>
    public static ToolContent Svg(String svg)
        => new(svg, ToolAudience.User, ToolContentType.Image, "image/svg+xml");

    /// <summary>创建前端 Base64 图片内容块</summary>
    /// <param name="base64">Base64 编码的图片数据</param>
    /// <param name="mimeType">MIME 类型，如 "image/png"</param>
    public static ToolContent ImageB64(String base64, String mimeType)
        => new(base64, ToolAudience.User, ToolContentType.Image, mimeType);
    #endregion
}
