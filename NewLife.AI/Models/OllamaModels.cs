using System;
using System.Collections.Generic;

namespace NewLife.AI.Models;

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

/// <summary>Ollama 模型参数选项</summary>
public class OllamaOptions
{
    /// <summary>温度</summary>
    public Double? Temperature { get; set; }

    /// <summary>Top P</summary>
    public Double? TopP { get; set; }

    /// <summary>Top K</summary>
    public Int32? TopK { get; set; }

    /// <summary>最大生成 token 数</summary>
    public Int32? NumPredict { get; set; }

    /// <summary>停止序列</summary>
    public List<String>? Stop { get; set; }

    /// <summary>随机种子</summary>
    public Int32? Seed { get; set; }

    /// <summary>重复惩罚</summary>
    public Double? RepeatPenalty { get; set; }

    /// <summary>存在惩罚</summary>
    public Double? PresencePenalty { get; set; }

    /// <summary>频率惩罚</summary>
    public Double? FrequencyPenalty { get; set; }
}

/// <summary>Ollama 生成响应</summary>
public class OllamaGenerateResponse
{
    /// <summary>模型名称</summary>
    public String? Model { get; set; }

    /// <summary>创建时间</summary>
    public String? CreatedAt { get; set; }

    /// <summary>响应文本</summary>
    public String? Response { get; set; }

    /// <summary>思考文本</summary>
    public String? Thinking { get; set; }

    /// <summary>是否完成</summary>
    public Boolean Done { get; set; }

    /// <summary>完成原因</summary>
    public String? DoneReason { get; set; }

    /// <summary>总耗时（纳秒）</summary>
    public Int64 TotalDuration { get; set; }

    /// <summary>模型加载耗时（纳秒）</summary>
    public Int64 LoadDuration { get; set; }

    /// <summary>输入 token 数</summary>
    public Int32 PromptEvalCount { get; set; }

    /// <summary>输入评估耗时（纳秒）</summary>
    public Int64 PromptEvalDuration { get; set; }

    /// <summary>输出 token 数</summary>
    public Int32 EvalCount { get; set; }

    /// <summary>输出评估耗时（纳秒）</summary>
    public Int64 EvalDuration { get; set; }
}

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
    public String? ModifiedAt { get; set; }

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
    public String? ParameterSize { get; set; }

    /// <summary>量化级别</summary>
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
    public String? ExpiresAt { get; set; }

    /// <summary>显存占用（字节）</summary>
    public Int64 SizeVram { get; set; }

    /// <summary>上下文长度</summary>
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
