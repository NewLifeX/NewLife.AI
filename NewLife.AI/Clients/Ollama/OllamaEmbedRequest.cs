using System.Runtime.Serialization;

namespace NewLife.AI.Clients.Ollama;

/// <summary>Ollama 嵌入请求</summary>
public class OllamaEmbedRequest
{
    /// <summary>模型名称</summary>
    public String? Model { get; set; }

    /// <summary>输入文本</summary>
    public Object? Input { get; set; }

    /// <summary>是否截断。默认 true</summary>
    public Boolean? Truncate { get; set; }

    /// <summary>向量维度</summary>
    public Int32? Dimensions { get; set; }

    /// <summary>保持模型加载的时长</summary>
    public String? KeepAlive { get; set; }

    /// <summary>模型参数选项</summary>
    public OllamaOptions? Options { get; set; }
}
