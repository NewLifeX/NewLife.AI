using NewLife.AI.Models;

namespace NewLife.AI.Providers;

/// <summary>AI 服务商统一接口。描述服务商身份与能力，并作为创建对话客户端的工厂</summary>
/// <remarks>
/// 接口设计原则：
/// <list type="bullet">
/// <item>纯工厂 + 描述者职责：仅暴露服务商元数据与 <see cref="CreateClient"/> 工厂方法</item>
/// <item>对话执行委托给 <see cref="IChatClient"/>，对标 MEAI 的 IChatClient 设计</item>
/// <item>使用服务商名称（字符串）而非枚举标识，方便扩展自定义服务商</item>
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

    /// <summary>主流模型列表。该服务商下各主流模型及其能力描述，供用户选择配置时参考</summary>
    AiModelInfo[] Models { get; }

    /// <summary>创建已绑定连接参数的对话客户端（MEAI 兼容入口）</summary>
    /// <remarks>
    /// 返回的 <see cref="IChatClient"/> 已将 Endpoint 和 ApiKey 绑定，无需每次调用传入 options。
    /// 可与 <see cref="ChatClientBuilder"/> 组合，通过中间件管道添加日志、追踪、用量统计等横切行为。
    /// </remarks>
    /// <param name="options">连接选项（Endpoint、ApiKey 等）</param>
    /// <returns>已配置的 IChatClient 实例</returns>
    IChatClient CreateClient(AiProviderOptions options);
}

/// <summary>支持列出可用模型的 AI 服务商接口。对应 OpenAI GET /v1/models 端点</summary>
public interface IModelListProvider
{
    /// <summary>获取该服务商当前可用的模型列表</summary>
    /// <param name="options">连接选项（Endpoint、ApiKey 等）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>模型列表，服务不可用时返回 null</returns>
    Task<OpenAiModelListResponse?> ListModelsAsync(AiProviderOptions options, CancellationToken cancellationToken = default);
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

/// <summary>AI 模型信息。描述服务商旗下某具体模型的标识与能力</summary>
/// <param name="Model">模型标识，即 API 请求中 model 字段的值，如 "gpt-4o"</param>
/// <param name="DisplayName">模型显示名称，用于界面展示，如 "GPT-4o"</param>
/// <param name="Capabilities">该模型支持的能力</param>
public record AiModelInfo(String Model, String DisplayName, AiProviderCapabilities Capabilities);

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
