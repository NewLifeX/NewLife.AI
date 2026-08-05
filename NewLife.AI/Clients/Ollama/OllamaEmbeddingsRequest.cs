namespace NewLife.AI.Clients.Ollama;

/// <summary>Ollama 旧版嵌入请求（/api/embeddings，已废弃但保留兼容）。与新版 /api/embed 的差异：输入为单个 prompt 字段，响应为单条 embedding</summary>
public class OllamaEmbeddingsRequest
{
    /// <summary>模型名称</summary>
    public String? Model { get; set; }

    /// <summary>提示文本</summary>
    public String? Prompt { get; set; }
}
