namespace NewLife.AI.Clients.Ollama;

/// <summary>Ollama 生成请求</summary>
public class OllamaGenerateRequest
{
    /// <summary>模型名称</summary>
    public String? Model { get; set; }

    /// <summary>提示文本</summary>
    public String? Prompt { get; set; }

    /// <summary>后缀文本</summary>
    public String? Suffix { get; set; }

    /// <summary>图片（Base64 编码数组）</summary>
    public String[]? Images { get; set; }

    /// <summary>系统提示</summary>
    public String? System { get; set; }

    /// <summary>是否流式输出。默认 true</summary>
    public Boolean? Stream { get; set; }

    /// <summary>输出格式，如 json</summary>
    public Object? Format { get; set; }

    /// <summary>是否启用思考</summary>
    public Boolean? Think { get; set; }

    /// <summary>保持模型加载的时长</summary>
    public String? KeepAlive { get; set; }

    /// <summary>模型参数选项</summary>
    public OllamaOptions? Options { get; set; }
}
