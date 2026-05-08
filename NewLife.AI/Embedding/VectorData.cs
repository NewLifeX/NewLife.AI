using NewLife.Serialization;

namespace NewLife.AI.Embedding;

/// <summary>向量数据序列化包装。存储模型名称、维度和 Base64 编码的 Single[] 数据，用于持久化 KnowledgeArticle.Vector 字段</summary>
/// <remarks>
/// 序列化格式（JSON）：<c>{"model":"local-hash-v1-512","dims":512,"data":"&lt;base64&gt;"}</c>
/// 其中 data 为 Single[] 的字节序列（Buffer.BlockCopy），以 Base64 编码存储。
/// 通过 <see cref="IsStale"/> 判断当前存储的向量是否与活跃模型匹配，不匹配则需要重新计算。
/// </remarks>
public class VectorData
{
    #region 属性

    /// <summary>生成向量所用的模型名称，含维度编码，如 local-hash-v1-512 或 text-embedding-3-small</summary>
    public String Model { get; set; } = "";

    /// <summary>向量维度数</summary>
    public Int32 Dims { get; set; }

    /// <summary>Base64 编码的 Single[] 字节序列</summary>
    public String Data { get; set; } = "";

    #endregion

    #region 静态工厂

    /// <summary>从 Single[] 向量创建 VectorData，对字节序列进行 Base64 编码</summary>
    /// <param name="model">模型名称（含维度）</param>
    /// <param name="vector">原始向量</param>
    /// <returns>序列化包装对象</returns>
    public static VectorData FromVector(String model, Single[] vector)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));
        if (vector == null) throw new ArgumentNullException(nameof(vector));

        var bytes = new Byte[vector.Length * sizeof(Single)];
        Buffer.BlockCopy(vector, 0, bytes, 0, bytes.Length);

        return new VectorData
        {
            Model = model,
            Dims = vector.Length,
            Data = Convert.ToBase64String(bytes),
        };
    }

    /// <summary>从 JSON 字符串解析 VectorData，解析失败返回 null</summary>
    /// <param name="json">JSON 字符串（可为 null 或空）</param>
    /// <returns>解析结果，失败时返回 null</returns>
    public static VectorData? Parse(String? json)
    {
        if (json.IsNullOrWhiteSpace()) return null;
        // 兼容旧格式：纯 JSON 数组 [0.1, 0.2, ...] 不是 VectorData 格式
        var trimmed = json.TrimStart();
        if (trimmed.StartsWith("[")) return null;

        try
        {
            return json.ToJsonEntity<VectorData>();
        }
        catch
        {
            return null;
        }
    }

    #endregion

    #region 方法

    /// <summary>解码为原始 Single[] 向量</summary>
    /// <returns>向量数组，Data 为空时返回空数组</returns>
    public Single[] ToVector()
    {
        if (Data.IsNullOrEmpty()) return [];

        var bytes = Convert.FromBase64String(Data);
        var result = new Single[bytes.Length / sizeof(Single)];
        Buffer.BlockCopy(bytes, 0, result, 0, bytes.Length);
        return result;
    }

    /// <summary>判断此向量是否与当前活跃模型不匹配（即需要重新计算）</summary>
    /// <param name="currentModel">当前活跃模型名称</param>
    /// <returns>true 表示模型不匹配，需要重新生成向量</returns>
    public Boolean IsStale(String currentModel)
    {
        if (currentModel.IsNullOrEmpty()) return false;
        return !Model.EqualIgnoreCase(currentModel);
    }

    /// <summary>序列化为 JSON 字符串</summary>
    /// <returns>JSON 字符串</returns>
    public String ToJson() => this.ToJson(false, false, false);

    #endregion
}
