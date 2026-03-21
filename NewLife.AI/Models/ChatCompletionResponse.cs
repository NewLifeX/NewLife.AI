namespace NewLife.AI.Models;

/// <summary>对话完成响应。兼容 OpenAI ChatCompletion 标准</summary>
public class ChatCompletionResponse
{
    #region 属性
    /// <summary>响应编号</summary>
    public String? Id { get; set; }

    /// <summary>对象类型。chat.completion 或 chat.completion.chunk</summary>
    public String? Object { get; set; }

    /// <summary>创建时间戳（Unix 秒）</summary>
    public Int64 Created { get; set; }

    /// <summary>模型编码</summary>
    public String? Model { get; set; }

    /// <summary>选择列表</summary>
    public IList<ChatChoice>? Choices { get; set; }

    /// <summary>令牌用量统计</summary>
    public ChatUsage? Usage { get; set; }

    /// <summary>系统指纹</summary>
    public String? SystemFingerprint { get; set; }
    #endregion

    #region 便捷属性
    /// <summary>获取回复文本。返回第一个选择项的消息内容</summary>
    public String? Text => Choices?.FirstOrDefault()?.Message?.Content?.ToString()
        ?? Choices?.FirstOrDefault()?.Delta?.Content?.ToString();
    #endregion
}

/// <summary>对话选择项</summary>
public class ChatChoice
{
    /// <summary>序号</summary>
    public Int32 Index { get; set; }

    /// <summary>消息内容（非流式）</summary>
    public ChatMessage? Message { get; set; }

    /// <summary>增量内容（流式）</summary>
    public ChatMessage? Delta { get; set; }

    /// <summary>结束原因。stop/length/tool_calls/content_filter</summary>
    public String? FinishReason { get; set; }
}

/// <summary>令牌用量统计</summary>
public class ChatUsage
{
    /// <summary>提示令牌数</summary>
    public Int32 PromptTokens { get; set; }

    /// <summary>回复令牌数</summary>
    public Int32 CompletionTokens { get; set; }

    /// <summary>总令牌数</summary>
    public Int32 TotalTokens { get; set; }

    /// <summary>耗时。本次LLM调用的端到端毫秒数</summary>
    public Int32 ElapsedMs { get; set; }
}
