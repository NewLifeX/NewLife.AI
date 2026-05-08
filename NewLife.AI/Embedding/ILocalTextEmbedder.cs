namespace NewLife.AI.Embedding;

/// <summary>本地文本向量化接口。无需 API 调用，在进程内直接生成嵌入向量</summary>
public interface ILocalTextEmbedder
{
    /// <summary>模型名称，不含维度信息的纯标识符，如 local-hash-v1 或 text-embedding-3-small</summary>
    String ModelName { get; }

    /// <summary>向量维度数</summary>
    Int32 Dimensions { get; }

    /// <summary>将文本转换为归一化浮点向量</summary>
    /// <param name="text">待嵌入文本</param>
    /// <returns>L2 归一化的 Single[] 向量</returns>
    Single[] Embed(String text);
}
