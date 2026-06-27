namespace NewLife.AI.Models;

/// <summary>中性模型列表响应。統一表示各服务商 /v1/models 或等效接口的返回结构</summary>
public class ModelListResponse
{
    /// <summary>对象类型，通常为 "list"</summary>
    public String? Object { get; set; }

    /// <summary>模型信息数组</summary>
    public ModelInfo[]? Data { get; set; }
}

/// <summary>中性模型信息。表示服务商模型列表中的单个模型条目</summary>
public class ModelInfo
{
    /// <summary>模型唯一标识，即请求时 model 字段的值</summary>
    public String? Id { get; set; }

    /// <summary>模型显示名称</summary>
    public String? Name { get; set; }

    /// <summary>对象类型，通常为 "model"</summary>
    public String? Object { get; set; }

    /// <summary>模型创建时间（Unix 时间戳反序列化后的本地时间）</summary>
    public DateTime Created { get; set; }

    /// <summary>模型归属方，通常为提供商编码</summary>
    public String? OwnedBy { get; set; }

    /// <summary>模型上下文窗口长度（令牌数）。部分 OpenAI 兼容平台（如 OpenRouter）会返回此扩展字段</summary>
    public Int32 ContextLength { get; set; }

    /// <summary>是否支持思考模式（Chain-of-Thought）</summary>
    public Boolean SupportThinking { get; set; }

    /// <summary>是否支持 Function Calling / Tool Use</summary>
    public Boolean SupportFunction { get; set; }

    /// <summary>是否支持图片输入（视觉多模态）</summary>
    public Boolean SupportVision { get; set; }

    /// <summary>是否支持语音识别/音频输入（ASR）</summary>
    public Boolean SupportAudio { get; set; }

    /// <summary>是否支持语音合成/音频输出（TTS）</summary>
    public Boolean SupportSpeech { get; set; }

    /// <summary>是否支持文生图（图像生成）</summary>
    public Boolean SupportImage { get; set; }

    /// <summary>是否支持文生视频（视频生成）</summary>
    public Boolean SupportVideo { get; set; }

    /// <summary>是否支持嵌入向量</summary>
    public Boolean SupportEmbedding { get; set; }

    /// <summary>是否支持重排序</summary>
    public Boolean SupportRerank { get; set; }

    /// <summary>计费模式。Token/Image/Video/Embedding/Speech</summary>
    public String? PricingMode { get; set; }

    /// <summary>输入价格，元/百万Token。非Token模式此字段承载单价</summary>
    public Decimal InputPrice { get; set; }

    /// <summary>输出价格，元/百万Token</summary>
    public Decimal OutputPrice { get; set; }

    /// <summary>缓存命中价格，元/百万Token</summary>
    public Decimal CachedInputPrice { get; set; }

    /// <summary>显式缓存创建价格，元/百万Token。0时回退到InputPrice</summary>
    public Decimal CacheCreationInputPrice { get; set; }

    /// <summary>非Token模式的单价</summary>
    public Decimal UnitPrice { get; set; }

    /// <summary>价格单位标签。张/秒/千字符/百万Token</summary>
    public String? Unit { get; set; }
}
