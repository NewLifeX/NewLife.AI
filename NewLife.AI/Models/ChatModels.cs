using System.ComponentModel;

namespace NewLife.AI.Models;

/// <summary>思考模式</summary>
public enum ThinkingMode
{
    /// <summary>自动</summary>
    [Description("自动")]
    Auto = 0,

    /// <summary>深度思考</summary>
    [Description("深度思考")]
    Think = 1,

    /// <summary>快速</summary>
    [Description("快速")]
    Fast = 2
}

/// <summary>反馈类型</summary>
public enum FeedbackType
{
    /// <summary>无反馈</summary>
    None = 0,

    /// <summary>点赞</summary>
    [Description("点赞")]
    Like = 1,

    /// <summary>点踩</summary>
    [Description("点踩")]
    Dislike = 2
}

/// <summary>消息状态</summary>
public enum MessageStatus
{
    /// <summary>流式输出中</summary>
    Streaming = 0,

    /// <summary>已完成</summary>
    Done = 1,

    /// <summary>出错</summary>
    Error = 2
}

/// <summary>工具调用状态</summary>
public enum ToolCallStatus
{
    /// <summary>调用中</summary>
    Calling = 0,

    /// <summary>已完成</summary>
    Done = 1,

    /// <summary>出错</summary>
    Error = 2
}

/// <summary>MCP传输类型</summary>
public enum McpTransportType
{
    /// <summary>HTTP</summary>
    Http = 0,

    /// <summary>SSE（Server-Sent Events）</summary>
    Sse = 1,

    /// <summary>标准输入输出</summary>
    Stdio = 2,
}

/// <summary>回应风格</summary>
public enum ResponseStyle
{
    /// <summary>均衡。平衡专业性与友好度，适合通用对话</summary>
    [Description("均衡")]
    Balanced = 0,

    /// <summary>精确。准确简洁，高确定性回答，适合代码和技术问答</summary>
    [Description("精确")]
    Precise = 1,

    /// <summary>生动。丰富表达，善用类比举例，适合学习和解释</summary>
    [Description("生动")]
    Vivid = 2,

    /// <summary>创意。发散思维，大胆联想，适合头脑风暴和写作</summary>
    [Description("创意")]
    Creative = 3,
}

/// <summary>计费模式。控制 ModelConfig 上价格字段的解释方式</summary>
public enum PricingMode
{
    /// <summary>按 Token。InputPrice/OutputPrice 单位：元/百万Token</summary>
    [Description("按Token")]
    Token = 0,

    /// <summary>按张图片。ImagePrice 单位：元/张</summary>
    [Description("按图片")]
    Image = 1,

    /// <summary>按视频秒数。VideoPrice 单位：元/秒，按 PriceTiers 不同分辨率区分</summary>
    [Description("按视频")]
    Video = 2,

    /// <summary>按 Embedding 调用。EmbeddingPrice 单位：元/百万Token</summary>
    [Description("按Embedding")]
    Embedding = 3,
}
