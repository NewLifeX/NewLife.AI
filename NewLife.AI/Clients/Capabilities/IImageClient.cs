using NewLife.AI.Clients.OpenAI;

namespace NewLife.AI.Clients.Capabilities;

/// <summary>图像生成能力接口。支持文生图与图像编辑</summary>
/// <remarks>
/// 按官方协议支持情况，仅在能力可用的客户端上实现。调用方通过 <c>client is IImageClient img</c> 模式匹配判断。
/// 已实现：OpenAI、DashScope、NewLifeAI、Azure；不实现：DeepSeek、Anthropic、Ollama。
/// </remarks>
public interface IImageClient
{
    /// <summary>文生图。POST 至各服务商图像生成端点</summary>
    /// <param name="request">图像生成请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>图像生成响应，失败时返回 null</returns>
    Task<ImageGenerationResponse?> TextToImageAsync(ImageGenerationRequest request, CancellationToken cancellationToken = default);

    /// <summary>图像编辑（含 Inpainting）。POST 至各服务商图像编辑端点，multipart/form-data</summary>
    /// <param name="request">图像编辑请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>图像生成响应，失败时返回 null</returns>
    Task<ImageGenerationResponse?> EditImageAsync(ImageEditsRequest request, CancellationToken cancellationToken = default);
}
