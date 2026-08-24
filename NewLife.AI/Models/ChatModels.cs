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
    Fast = 2,

    /// <summary>澄清。先与用户确认意图与关键参数，再执行复杂任务；通常配合 ask_user 工具实现交互闭环</summary>
    [Description("澄清")]
    Clarify = 3
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

/// <summary>客服入口位置。控制客服文本/链接在页面中的展示位置，None=不展示</summary>
public enum SupportPosition
{
    /// <summary>不显示。客服入口不展示</summary>
    [Description("不显示")]
    None = 0,

    /// <summary>侧边栏底部。显示在侧边栏底部</summary>
    [Description("侧边栏底部")]
    SidebarBottom = 1,

    /// <summary>新对话按钮下方。显示在新对话按钮下方</summary>
    [Description("新对话按钮下方")]
    BelowNewChat = 2,

    /// <summary>右下角悬浮球。显示为右下角悬浮球</summary>
    [Description("右下角悬浮球")]
    FloatingButton = 3,
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

    /// <summary>按语音合成字符数。SpeechPrice 单位：元/千字符</summary>
    [Description("按语音合成")]
    Speech = 4,
}
