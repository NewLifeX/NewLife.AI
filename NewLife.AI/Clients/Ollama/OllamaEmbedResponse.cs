namespace NewLife.AI.Clients.Ollama;

/// <summary>Ollama 嵌入响应</summary>
public class OllamaEmbedResponse
{
    /// <summary>模型名称</summary>
    public String? Model { get; set; }

    /// <summary>嵌入向量数组</summary>
    public Double[][]? Embeddings { get; set; }

    /// <summary>总耗时（纳秒）</summary>
    public Int64 TotalDuration { get; set; }

    /// <summary>模型加载耗时（纳秒）</summary>
    public Int64 LoadDuration { get; set; }

    /// <summary>输入 token 数</summary>
    public Int32 PromptEvalCount { get; set; }
}
