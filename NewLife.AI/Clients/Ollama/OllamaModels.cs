using System.Runtime.Serialization;

namespace NewLife.AI.Clients.Ollama;


/// <summary>Ollama 模型列表响应</summary>
public class OllamaTagsResponse
{
    /// <summary>模型列表</summary>
    public OllamaModelInfo[]? Models { get; set; }
}

/// <summary>Ollama 模型信息</summary>
public class OllamaModelInfo
{
    /// <summary>模型名称</summary>
    public String? Name { get; set; }

    /// <summary>模型标识</summary>
    public String? Model { get; set; }

    /// <summary>修改时间</summary>
    [DataMember(Name = "modified_at")]
    public DateTime ModifiedAt { get; set; }

    /// <summary>模型大小（字节）</summary>
    public Int64 Size { get; set; }

    /// <summary>摘要哈希</summary>
    public String? Digest { get; set; }

    /// <summary>模型详细信息</summary>
    public OllamaModelDetails? Details { get; set; }
}

/// <summary>Ollama 模型详细信息</summary>
public class OllamaModelDetails
{
    /// <summary>格式</summary>
    public String? Format { get; set; }

    /// <summary>模型家族</summary>
    public String? Family { get; set; }

    /// <summary>模型家族列表</summary>
    public String[]? Families { get; set; }

    /// <summary>参数规模</summary>
    [DataMember(Name = "parameter_size")]
    public String? ParameterSize { get; set; }

    /// <summary>量化级别</summary>
    [DataMember(Name = "quantization_level")]
    public String? QuantizationLevel { get; set; }
}

/// <summary>Ollama 运行中模型列表响应</summary>
public class OllamaPsResponse
{
    /// <summary>运行中的模型列表</summary>
    public OllamaRunningModel[]? Models { get; set; }
}

/// <summary>Ollama 运行中模型信息</summary>
public class OllamaRunningModel
{
    /// <summary>模型名称</summary>
    public String? Name { get; set; }

    /// <summary>模型标识</summary>
    public String? Model { get; set; }

    /// <summary>模型大小（字节）</summary>
    public Int64 Size { get; set; }

    /// <summary>摘要哈希</summary>
    public String? Digest { get; set; }

    /// <summary>模型详细信息</summary>
    public OllamaModelDetails? Details { get; set; }

    /// <summary>过期时间</summary>
    [DataMember(Name = "modified_at")]
    public DateTime ExpiresAt { get; set; }

    /// <summary>显存占用（字节）</summary>
    [DataMember(Name = "size_vram")]
    public Int64 SizeVram { get; set; }

    /// <summary>上下文长度</summary>
    [DataMember(Name = "context_length")]
    public Int64 ContextLength { get; set; }
}

/// <summary>Ollama 模型详情请求</summary>
public class OllamaShowRequest
{
    /// <summary>模型名称</summary>
    public String? Model { get; set; }

    /// <summary>是否返回详细信息</summary>
    public Boolean? Verbose { get; set; }
}

/// <summary>Ollama 模型详情响应</summary>
public class OllamaShowResponse
{
    /// <summary>参数信息</summary>
    public String? Parameters { get; set; }

    /// <summary>许可证</summary>
    public String? License { get; set; }

    /// <summary>修改时间</summary>
    public String? ModifiedAt { get; set; }

    /// <summary>模型详细信息</summary>
    public OllamaModelDetails? Details { get; set; }

    /// <summary>模型模板</summary>
    public String? Template { get; set; }

    /// <summary>模型能力列表</summary>
    public String[]? Capabilities { get; set; }

    /// <summary>模型元信息</summary>
    public IDictionary<String, Object>? ModelInfo { get; set; }
}

/// <summary>Ollama 版本响应</summary>
public class OllamaVersionResponse
{
    /// <summary>版本号</summary>
    public String? Version { get; set; }
}

/// <summary>Ollama 拉取模型请求</summary>
public class OllamaPullRequest
{
    /// <summary>模型名称</summary>
    public String? Model { get; set; }

    /// <summary>是否流式输出。默认 true，设为 false 时等待完成后返回单条结果</summary>
    public Boolean? Stream { get; set; }

    /// <summary>是否允许拉取未经验证的镜像</summary>
    public Boolean? Insecure { get; set; }
}

/// <summary>Ollama 拉取模型状态（流式每帧 / 非流式最终帧）</summary>
public class OllamaPullStatus
{
    /// <summary>状态描述，如 "pulling manifest"、"downloading"、"success"</summary>
    public String? Status { get; set; }

    /// <summary>当前层的文件摘要</summary>
    public String? Digest { get; set; }

    /// <summary>文件总大小（字节）</summary>
    public Int64 Total { get; set; }

    /// <summary>已完成大小（字节）</summary>
    public Int64 Completed { get; set; }
}
