using NewLife.AI.Models;

namespace NewLife.AI.Providers;

/// <summary>AI 服务商统一接口。所有模型服务商均需实现此接口</summary>
/// <remarks>
/// 接口设计原则：
/// <list type="bullet">
/// <item>使用服务商名称（字符串）而非枚举标识，方便扩展自定义服务商</item>
/// <item>同时支持非流式和流式两种调用模式</item>
/// <item>接口不依赖数据库，可在任意 .NET 项目中独立使用</item>
/// </list>
/// </remarks>
public interface IAiProvider
{
    /// <summary>服务商编码。唯一标识，如 OpenAI、DashScope、DeepSeek 等</summary>
    String Code { get; }

    /// <summary>服务商名称。用于界面显示的友好名称，如"OpenAI"、"阿里百炼"、"深度求索"等</summary>
    String Name { get; }

    /// <summary>服务商描述。详细说明服务商特点、支持的模型系列等</summary>
    String? Description { get; }

    /// <summary>API 协议类型。ChatCompletions / AnthropicMessages / Gemini</summary>
    String ApiProtocol { get; }

    /// <summary>默认 API 地址</summary>
    String DefaultEndpoint { get; }

    /// <summary>默认能力信息。表示该服务商主力模型的典型能力，用户可在模型配置中按实际模型覆盖</summary>
    AiProviderCapabilities DefaultCapabilities { get; }

    /// <summary>非流式对话。发送请求并一次性返回完整响应</summary>
    /// <param name="request">对话请求</param>
    /// <param name="options">服务商连接选项（Endpoint、ApiKey 等）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>完整的对话响应</returns>
    Task<ChatCompletionResponse> ChatAsync(ChatCompletionRequest request, AiProviderOptions options, CancellationToken cancellationToken = default);

    /// <summary>流式对话。逐块返回生成内容</summary>
    /// <param name="request">对话请求（自动设置 Stream=true）</param>
    /// <param name="options">服务商连接选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>流式响应块的异步枚举</returns>
    IAsyncEnumerable<ChatCompletionResponse> ChatStreamAsync(ChatCompletionRequest request, AiProviderOptions options, CancellationToken cancellationToken = default);
}

/// <summary>AI 服务商默认能力信息。表示该服务商主力模型的典型能力</summary>
/// <remarks>这些是服务商级别的默认值，用户创建具体模型配置时可按实际模型覆盖</remarks>
/// <param name="SupportThinking">是否支持思考模式。如 DeepSeek-R1、Claude 的 extended thinking</param>
/// <param name="SupportVision">是否支持图片输入（视觉）。如 GPT-4V、Claude Vision、Qwen-VL</param>
/// <param name="SupportImageGeneration">是否支持文生图。如 DALL·E、Qwen 的图像生成</param>
/// <param name="SupportFunctionCalling">是否支持 Function Calling / Tool Use</param>
public record AiProviderCapabilities(
    Boolean SupportThinking = false,
    Boolean SupportVision = false,
    Boolean SupportImageGeneration = false,
    Boolean SupportFunctionCalling = false);

/// <summary>AI 服务商连接选项</summary>
public class AiProviderOptions
{
    /// <summary>API 地址。为空时使用服务商默认地址</summary>
    public String? Endpoint { get; set; }

    /// <summary>API 密钥</summary>
    public String? ApiKey { get; set; }

    /// <summary>组织编号。部分服务商需要（如 OpenAI）</summary>
    public String? Organization { get; set; }

    /// <summary>获取实际使用的 API 地址</summary>
    /// <param name="defaultEndpoint">默认地址</param>
    /// <returns></returns>
    public String GetEndpoint(String defaultEndpoint) =>
        String.IsNullOrWhiteSpace(Endpoint) ? defaultEndpoint : Endpoint;
}
