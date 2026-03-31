namespace NewLife.AI.Models;

/// <summary>对话响应接口。统一所有协议响应的公共契约</summary>
/// <remarks>
/// 所有协议的原生响应类（ChatCompletionResponse、AnthropicResponse、GeminiResponse 等）均可实现此接口，
/// 使得 IChatClient.GetResponseAsync 返回统一的 IChatResponse，上层无需关心具体协议。
/// ChatResponse 作为内部统一响应模型，也实现此接口。
/// </remarks>
public interface IChatResponse
{
    /// <summary>响应编号</summary>
    String? Id { get; set; }

    /// <summary>对象类型</summary>
    String? Object { get; set; }

    /// <summary>创建时间戳</summary>
    DateTimeOffset Created { get; set; }

    /// <summary>模型编码</summary>
    String? Model { get; set; }

    /// <summary>消息选择列表</summary>
    IList<ChatChoice>? Messages { get; set; }

    /// <summary>令牌用量统计</summary>
    UsageDetails? Usage { get; set; }

    /// <summary>获取回复文本</summary>
    String? Text { get; }
}
