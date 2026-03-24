namespace NewLife.AI.Providers;

/// <summary>声明该类为 AI 对话客户端，供 <see cref="AiClientRegistry"/> 反射扫描自动注册服务商描述符</summary>
/// <remarks>
/// 同一类上可标注多个此特性（AllowMultiple = true），每个对应一个服务商注册。<br/>
/// 例如 <c>OpenAiChatClient</c> 上可标注 OpenAI、DeepSeek、AzureAI 等所有兼容 OpenAI 协议的服务商。<br/>
/// 配套使用 <c>AiClientModelAttribute</c> 可声明各服务商的默认模型列表。
/// <code>
/// [AiClient("OpenAI", "OpenAI", "https://api.openai.com")]
/// [AiClientModel("gpt-4o", "GPT-4o", Code = "OpenAI", Vision = true)]
/// [AiClient("DeepSeek", "深度求索", "https://api.deepseek.com", Order = 2)]
/// [AiClientModel("deepseek-reasoner", "DeepSeek R1", Code = "DeepSeek", Thinking = true)]
/// public class OpenAiChatClient : AiClientBase { ... }
/// </code>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class AiClientAttribute : Attribute
{
    /// <summary>服务商编码，唯一标识。如 OpenAI、DashScope</summary>
    public String Code { get; }

    /// <summary>服务商显示名称，用于界面展示</summary>
    public String DisplayName { get; }

    /// <summary>默认 API 地址</summary>
    public String DefaultEndpoint { get; }

    /// <summary>协议名称。默认 OpenAI；特殊协议填 AnthropicMessages / Gemini / DashScope / Ollama</summary>
    public String Protocol { get; set; } = "OpenAI";

    /// <summary>服务商描述</summary>
    public String? Description { get; set; }

    /// <summary>对话路径覆盖。为空时客户端使用内置默认路径（如 /v1/chat/completions）</summary>
    public String? ChatPath { get; set; }

    /// <summary>排序序号。控制在列表中的显示顺序，数值小的在前；0 表示最高优先级</summary>
    public Int32 Order { get; set; }

    /// <summary>声明此类实现指定服务商的 AI 对话客户端</summary>
    /// <param name="code">服务商编码</param>
    /// <param name="displayName">显示名称</param>
    /// <param name="defaultEndpoint">默认 API 地址</param>
    public AiClientAttribute(String code, String displayName, String defaultEndpoint)
    {
        Code = code;
        DisplayName = displayName;
        DefaultEndpoint = defaultEndpoint;
    }
}
