using NewLife.Data;

namespace NewLife.AI.Models;

/// <summary>对话请求接口。统一所有协议请求的公共契约</summary>
/// <remarks>
/// 所有协议的原生请求类（ChatCompletionRequest、AnthropicRequest、GeminiRequest 等）均实现此接口，
/// 使得 IChatClient.GetResponseAsync 可直接接受任意协议的原生请求，无需在网关层做 ToChatRequest() 转换。
/// ChatRequest 作为内部统一传输模型，也实现此接口。
/// </remarks>
public interface IChatRequest : IExtend
{
    /// <summary>模型编码</summary>
    String? Model { get; set; }

    /// <summary>消息列表</summary>
    IList<ChatMessage> Messages { get; set; }

    /// <summary>是否流式输出</summary>
    Boolean Stream { get; set; }

    /// <summary>温度。0~2，越高越随机</summary>
    Double? Temperature { get; set; }

    /// <summary>核采样。0~1</summary>
    Double? TopP { get; set; }

    /// <summary>候选词数量</summary>
    Int32? TopK { get; set; }

    /// <summary>最大生成令牌数</summary>
    Int32? MaxTokens { get; set; }

    /// <summary>停止词列表</summary>
    IList<String>? Stop { get; set; }

    /// <summary>存在惩罚。-2~2</summary>
    Double? PresencePenalty { get; set; }

    /// <summary>频率惩罚。-2~2</summary>
    Double? FrequencyPenalty { get; set; }

    /// <summary>可用工具列表</summary>
    IList<ChatTool>? Tools { get; set; }

    /// <summary>工具选择策略</summary>
    Object? ToolChoice { get; set; }

    /// <summary>用户标识</summary>
    String? User { get; set; }

    /// <summary>是否启用思考模式</summary>
    Boolean? EnableThinking { get; set; }

    /// <summary>响应格式</summary>
    Object? ResponseFormat { get; set; }

    /// <summary>是否允许并行工具调用</summary>
    Boolean? ParallelToolCalls { get; set; }

    /// <summary>用户编号。内部管道传递</summary>
    String? UserId { get; set; }

    /// <summary>会话编号。内部管道传递</summary>
    String? ConversationId { get; set; }

    /// <summary>扩展数据</summary>
    IDictionary<String, Object?> Items { get; set; }
}
