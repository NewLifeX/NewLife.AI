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
