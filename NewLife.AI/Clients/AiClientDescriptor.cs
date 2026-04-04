namespace NewLife.AI.Clients;

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

/// <summary>AI 客户端连接选项</summary>
public class AiClientOptions
{
    /// <summary>服务商编码。配置驱动场景下用于指定服务商，如 OpenAI、DashScope</summary>
    public String? Code { get; set; }

    /// <summary>API 地址。为空时使用服务商默认地址</summary>
    public String? Endpoint { get; set; }

    /// <summary>API 密钥</summary>
    public String? ApiKey { get; set; }

    /// <summary>组织编号。部分服务商需要（如 OpenAI）</summary>
    public String? Organization { get; set; }

    /// <summary>默认模型编码。客户端每次调用时若未指定模型则使用此值</summary>
    public String? Model { get; set; }

    /// <summary>协议覆盖。DashScope 等双协议服务商可通过此字段切换"DashScope"原生协议或"ChatCompletions"兼容协议</summary>
    public String? Protocol { get; set; }

    /// <summary>获取实际使用的 API 地址</summary>
    /// <param name="defaultEndpoint">默认地址</param>
    /// <returns></returns>
    public String GetEndpoint(String defaultEndpoint) => String.IsNullOrWhiteSpace(Endpoint) ? defaultEndpoint : Endpoint;
}

/// <summary>AI 客户端描述符。描述一个 AI 服务商的元数据及客户端创建工厂。替代原 IAiProvider 接口</summary>
/// <remarks>
/// <para>设计原则：纯数据对象，描述服务商元数据并持有创建 IChatClient 的工厂委托。</para>
/// <para>原 34 个 xxxProvider 类全部替换为 <see cref="AiClientDescriptor"/> 实例，注册在 AiClientRegistry 中。</para>
/// </remarks>
public class AiClientDescriptor
{
    /// <summary>服务商编码。唯一标识，如 OpenAI、DashScope、DeepSeek 等</summary>
    public String Code { get; set; } = "";

    /// <summary>服务商显示名称。用于界面展示，如"OpenAI"、"阿里百炼"</summary>
    public String DisplayName { get; set; } = "";

    /// <summary>服务商描述</summary>
    public String? Description { get; set; }

    /// <summary>默认 API 地址</summary>
    public String DefaultEndpoint { get; set; } = "";

    /// <summary>API 协议类型。OpenAI / AnthropicMessages / Gemini / DashScope / Ollama</summary>
    public String Protocol { get; set; } = "OpenAI";

    /// <summary>主流模型列表。该服务商下各主流模型及其能力描述，供用户选择配置时参考</summary>
    public AiModelInfo[] Models { get; set; } = [];

    /// <summary>客户端工厂。根据连接选项创建 IChatClient 实例</summary>
    /// <remarks>每次调用均创建新实例，调用方负责释放（using）</remarks>
    public Func<AiClientOptions, IChatClient> Factory { get; set; } = _ => throw new InvalidOperationException("未配置 Factory");

    /// <summary>按模型 ID 查找已注册的模型能力信息</summary>
    /// <remarks>
    /// 优先精确匹配（大小写不敏感），未命中时尝试前缀匹配以覆盖带日期版本后缀的变体（如 qwen3-max-2025-01-01 → qwen3-max）
    /// </remarks>
    /// <param name="modelId">模型标识</param>
    /// <returns>匹配的能力信息，未找到返回 null</returns>
    public AiProviderCapabilities? FindModelCapabilities(String? modelId)
    {
        if (String.IsNullOrEmpty(modelId) || Models.Length == 0) return null;

        // 精确匹配
        foreach (var m in Models)
        {
            if (String.Equals(m.Model, modelId, StringComparison.OrdinalIgnoreCase))
                return m.Capabilities;
        }

        // 前缀匹配：已注册模型作为前缀匹配远端返回的带版本后缀变体
        foreach (var m in Models)
        {
            if (modelId.StartsWith(m.Model, StringComparison.OrdinalIgnoreCase) &&
                modelId.Length > m.Model.Length &&
                (modelId[m.Model.Length] == '-' || modelId[m.Model.Length] == ':'))
                return m.Capabilities;
        }

        return null;
    }

    /// <inheritdoc/>
    public override String ToString() => $"{Code} ({DisplayName})";
}
