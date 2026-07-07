using NewLife.Data;

namespace NewLife.AI.Models;

/// <summary>嵌入向量模型的定制参数。存储在 ModelConfig.Settings 字段</summary>
/// <remarks>
/// 可用设置项：
/// <list type="bullet">
///   <item><c>encodingFormat</c> — 编码格式，"float"（默认）或 "base64"</item>
///   <item><c>dimensions</c> — 向量维度，部分模型支持降维，null 表示使用模型最大维度</item>
/// </list>
/// 其它模型定制化参数通过 <see cref="IExtend.Items"/> 字典携带。
/// 示例：{"encodingFormat":"float","dimensions":null}
/// </remarks>
public class EmbeddingModelSetting : IExtend
{
    /// <summary>编码格式。如 "float"（默认）或 "base64"</summary>
    public String? EncodingFormat { get; set; }

    /// <summary>向量维度。支持降维的模型可指定，null 表示使用模型最大维度</summary>
    public Int32? Dimensions { get; set; }

    /// <summary>扩展参数。无法识别的额外设置项，用于兼容未来模型的新参数</summary>
    public IDictionary<String, Object?> Items { get; set; } = new Dictionary<String, Object?>();

    /// <summary>索引器，方便访问扩展数据</summary>
    public Object? this[String key] { get => Items.TryGetValue(key, out var value) ? value : null; set => Items[key] = value; }
}
