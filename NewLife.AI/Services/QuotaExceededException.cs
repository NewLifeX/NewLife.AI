namespace NewLife.AI.Services;

/// <summary>配额超限异常。由 GatewayService 在硬拒绝时抛出，控制器转换为 OpenAI 兼容的 429 响应</summary>
/// <param name="message">错误消息</param>
/// <param name="owner">触发拒绝的维度，如"User#123.日Token"</param>
public class QuotaExceededException(String message, String? owner = null) : Exception(message)
{
    /// <summary>触发拒绝的维度，如"User#123.日Token"</summary>
    public String? Owner { get; } = owner;
}
