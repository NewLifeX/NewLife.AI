namespace NewLife.AI.Models;

/// <summary>AI 消息内容基类。表示消息中的一个内容片段</summary>
/// <remarks>
/// 参考 MEAI 的 AIContent 类型层次，提供比 <c>Object? Content</c> 更强的类型安全性。
/// 在 <see cref="ChatMessage.Contents"/> 中组合多个内容片段，可表达多模态消息。
/// 旧的 <see cref="ChatMessage.Content"/> 属性保持不变，向后兼容；
/// <see cref="ChatMessage.Contents"/> 非空时优先使用。
/// </remarks>
public abstract class AIContent { }

/// <summary>文本内容片段</summary>
/// <remarks>初始化文本内容</remarks>
/// <param name="text">文本字符串</param>
public class TextContent(String text) : AIContent
{
    /// <summary>文本内容</summary>
    public String Text { get; set; } = text;

    /// <summary>返回文本内容</summary>
    public override String ToString() => Text;
}

/// <summary>图片内容片段。支持 URL 引用和二进制数据两种方式</summary>
public class ImageContent : AIContent
{
    /// <summary>图片地址。http/https URL 或 data URI（优先级低于 Data）</summary>
    public String? Uri { get; set; }

    /// <summary>图片二进制数据。设置后自动编码为 base64 data URI，优先于 Uri</summary>
    public Byte[]? Data { get; set; }

    /// <summary>媒体类型。如 image/jpeg、image/png，Data 不为空时生效；默认 image/jpeg</summary>
    public String? MediaType { get; set; }

    /// <summary>细节级别。auto / low / high，控制 Token 消耗；默认 auto</summary>
    public String? Detail { get; set; }
}

/// <summary>函数调用内容片段（assistant 角色发起的工具调用请求）</summary>
public class FunctionCallContent : AIContent
{
    /// <summary>调用编号。与对应 <see cref="FunctionResultContent.CallId"/> 匹配</summary>
    public String CallId { get; set; } = null!;

    /// <summary>函数名称</summary>
    public String Name { get; set; } = null!;

    /// <summary>调用参数。JSON 字符串格式</summary>
    public String? Arguments { get; set; }
}

/// <summary>函数调用结果内容片段（tool 角色回传的工具执行结果）</summary>
public class FunctionResultContent : AIContent
{
    /// <summary>关联的调用编号，与 <see cref="FunctionCallContent.CallId"/> 对应</summary>
    public String CallId { get; set; } = null!;

    /// <summary>函数名称</summary>
    public String? Name { get; set; }

    /// <summary>调用结果。字符串或可序列化对象</summary>
    public Object? Result { get; set; }
}

/// <summary>任意二进制数据内容片段。用于音频、文档等非图片媒体</summary>
/// <remarks>初始化数据内容</remarks>
/// <param name="data">二进制数据</param>
/// <param name="mediaType">媒体类型</param>
public class DataContent(Byte[] data, String mediaType) : AIContent
{
    /// <summary>二进制数据</summary>
    public Byte[] Data { get; set; } = data;

    /// <summary>媒体类型。如 audio/wav、application/pdf</summary>
    public String MediaType { get; set; } = mediaType;
}
