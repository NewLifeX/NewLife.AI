namespace NewLife.AI.Clients.OpenAI;

/// <summary>OpenAI 兼容的模型列表响应。对应 GET /v1/models 返回结构</summary>
public class OpenAiModelListResponse
{
    /// <summary>对象类型，固定为 "list"</summary>
    public String? Object { get; set; }

    /// <summary>模型对象数组</summary>
    public OpenAiModelObject[]? Data { get; set; }
}

/// <summary>OpenAI 兼容的模型对象。对应 /v1/models 列表中的单个条目</summary>
public class OpenAiModelObject
{
    /// <summary>模型唯一标识，即请求时 model 字段的值</summary>
    public String? Id { get; set; }

    /// <summary>名称</summary>
    public String? Name { get; set; }

    /// <summary>对象类型，固定为 "model"</summary>
    public String? Object { get; set; }

    /// <summary>模型创建时间（Unix 时间戳，秒）</summary>
    public DateTime Created { get; set; }

    /// <summary>模型归属方，通常为提供商编码</summary>
    public String? OwnedBy { get; set; }

    /// <summary>模型上下文窗口长度（令牌数）。部分 OpenAI 兼容平台（如 OpenRouter）会返回此扩展字段</summary>
    public Int32 ContextLength { get; set; }

    /// <summary>是否支持思考模式（Chain-of-Thought）</summary>
    public Boolean SupportThinking { get; set; }

    /// <summary>是否支持 Function Calling / Tool Use</summary>
    public Boolean SupportFunctionCalling { get; set; }

    /// <summary>是否支持图片输入（视觉多模态）</summary>
    public Boolean SupportVision { get; set; }

    /// <summary>是否支持音频输入输出</summary>
    public Boolean SupportAudio { get; set; }

    /// <summary>是否支持文生图（图像生成）</summary>
    public Boolean SupportImageGeneration { get; set; }

    /// <summary>是否支持文生视频（视频生成）</summary>
    public Boolean SupportVideoGeneration { get; set; }
}
