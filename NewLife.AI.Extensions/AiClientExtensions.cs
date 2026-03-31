using NewLife.AI.Clients;
using NewLife.AI.Clients.Anthropic;
using NewLife.AI.Clients.Bedrock;
using NewLife.AI.Clients.DashScope;
using NewLife.AI.Clients.Gemini;
using NewLife.AI.Clients.Ollama;
using NewLife.AI.Clients.OpenAI;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>IChatClient 的依赖注入扩展方法。适用于 ASP.NET Core 等 Web 项目的标准 DI 注册场景</summary>
/// <remarks>
/// 典型用法：
/// <code>
/// // 通用配置驱动注册
/// services.AddAiClient(opts => { opts.Code = "DashScope"; opts.ApiKey = "sk-xxx"; opts.Model = "qwen3.5-flash"; });
///
/// // 服务商专属快捷注册
/// services.AddDashScope("sk-xxx", "qwen3.5-flash");
/// services.AddOpenAI("sk-xxx", "gpt-4o");
///
/// // 多服务商（.NET 8+）
/// services.AddKeyedDashScope("fast",   "sk-xxx", "qwen3.5-flash");
/// services.AddKeyedOpenAI  ("strong", "sk-xxx", "gpt-4o");
/// // 注入：[FromKeyedServices("fast")] IChatClient client
/// </code>
/// </remarks>
public static class AiClientExtensions
{
    #region 通用注册

    /// <summary>注册默认 <see cref="IChatClient"/> 单例。通过 <paramref name="configure"/> 配置服务商编码、密钥和模型</summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">选项配置委托；必须设置 <see cref="AiClientOptions.Code"/> 以指定服务商</param>
    /// <returns>服务集合（支持链式调用）</returns>
    /// <exception cref="ArgumentNullException">configure 为 null 时抛出</exception>
    /// <exception cref="InvalidOperationException">Code 未设置或服务商未注册时抛出</exception>
    public static IServiceCollection AddAiClient(this IServiceCollection services, Action<AiClientOptions> configure)
    {
        if (configure == null) throw new ArgumentNullException(nameof(configure));

        services.AddSingleton(_ =>
        {
            var opts = new AiClientOptions();
            configure(opts);
            if (String.IsNullOrWhiteSpace(opts.Code))
                throw new InvalidOperationException("必须通过 opts.Code 指定服务商编码，如 \"DashScope\"、\"OpenAI\"");
            return AiClientRegistry.Default.CreateClient(opts.Code, opts);
        });

        return services;
    }

    #endregion

    #region 服务商专属注册

    /// <summary>注册 OpenAI 兼容协议 <see cref="IChatClient"/> 单例</summary>
    /// <param name="services">服务集合</param>
    /// <param name="apiKey">API 密钥</param>
    /// <param name="model">默认模型；为空时由每次请求自行指定</param>
    /// <param name="endpoint">API 地址覆盖；为空时使用内置默认地址</param>
    /// <returns>服务集合（支持链式调用）</returns>
    public static IServiceCollection AddOpenAI(this IServiceCollection services, String apiKey, String? model = null, String? endpoint = null)
        => services.AddSingleton<IChatClient>(_ => new OpenAIChatClient(apiKey, model, endpoint));

    /// <summary>注册阿里百炼 DashScope <see cref="IChatClient"/> 单例</summary>
    /// <param name="services">服务集合</param>
    /// <param name="apiKey">阿里云 API Key</param>
    /// <param name="model">默认模型</param>
    /// <param name="endpoint">API 地址覆盖；为空时使用默认 DashScope 地址</param>
    /// <returns>服务集合（支持链式调用）</returns>
    public static IServiceCollection AddDashScope(this IServiceCollection services, String apiKey, String? model = null, String? endpoint = null)
        => services.AddSingleton<IChatClient>(_ => new DashScopeChatClient(apiKey, model, endpoint));

    /// <summary>注册 Anthropic Claude <see cref="IChatClient"/> 单例</summary>
    /// <param name="services">服务集合</param>
    /// <param name="apiKey">Anthropic API Key</param>
    /// <param name="model">默认模型</param>
    /// <param name="endpoint">API 地址覆盖</param>
    /// <returns>服务集合（支持链式调用）</returns>
    public static IServiceCollection AddAnthropic(this IServiceCollection services, String apiKey, String? model = null, String? endpoint = null)
        => services.AddSingleton<IChatClient>(_ => new AnthropicChatClient(apiKey, model, endpoint));

    /// <summary>注册 Google Gemini <see cref="IChatClient"/> 单例</summary>
    /// <param name="services">服务集合</param>
    /// <param name="apiKey">Google API Key</param>
    /// <param name="model">默认模型</param>
    /// <param name="endpoint">API 地址覆盖</param>
    /// <returns>服务集合（支持链式调用）</returns>
    public static IServiceCollection AddGemini(this IServiceCollection services, String apiKey, String? model = null, String? endpoint = null)
        => services.AddSingleton<IChatClient>(_ => new GeminiChatClient(apiKey, model, endpoint));

    /// <summary>注册 Ollama <see cref="IChatClient"/> 单例</summary>
    /// <param name="services">服务集合</param>
    /// <param name="apiKey">API 密钥；本地部署可传 null 或空字符串</param>
    /// <param name="model">默认模型</param>
    /// <param name="endpoint">Ollama 地址；为空时使用默认 http://localhost:11434</param>
    /// <returns>服务集合（支持链式调用）</returns>
    public static IServiceCollection AddOllama(this IServiceCollection services, String? apiKey = null, String? model = null, String? endpoint = null)
        => services.AddSingleton<IChatClient>(_ => new OllamaChatClient(apiKey, model, endpoint));

    /// <summary>注册新生命 AI 网关 <see cref="IChatClient"/> 单例</summary>
    /// <param name="services">服务集合</param>
    /// <param name="apiKey">新生命 AI 网关 API Key</param>
    /// <param name="model">默认模型</param>
    /// <param name="endpoint">网关地址覆盖；为空时使用内置默认地址</param>
    /// <returns>服务集合（支持链式调用）</returns>
    public static IServiceCollection AddNewLifeAI(this IServiceCollection services, String apiKey, String? model = null, String? endpoint = null)
        => services.AddSingleton<IChatClient>(_ => new NewLifeAIChatClient(apiKey, model, endpoint));

    /// <summary>注册 Azure OpenAI <see cref="IChatClient"/> 单例</summary>
    /// <param name="services">服务集合</param>
    /// <param name="apiKey">Azure API Key</param>
    /// <param name="model">deployment 名称（对应 Azure 中的模型部署）</param>
    /// <param name="endpoint">Azure OpenAI 完整地址，如 https://myresource.openai.azure.com</param>
    /// <returns>服务集合（支持链式调用）</returns>
    public static IServiceCollection AddAzureAI(this IServiceCollection services, String apiKey, String? model = null, String? endpoint = null)
        => services.AddSingleton<IChatClient>(_ => new AzureAIChatClient(apiKey, model, endpoint));

    /// <summary>注册 AWS Bedrock <see cref="IChatClient"/> 单例</summary>
    /// <param name="services">服务集合</param>
    /// <param name="accessKeyId">AWS Access Key ID</param>
    /// <param name="secretAccessKey">AWS Secret Access Key</param>
    /// <param name="model">默认模型 ID，如 anthropic.claude-sonnet-4-20250514-v1:0</param>
    /// <param name="region">AWS 区域，默认 us-east-1</param>
    /// <returns>服务集合（支持链式调用）</returns>
    public static IServiceCollection AddBedrock(this IServiceCollection services, String accessKeyId, String secretAccessKey, String? model = null, String? region = null)
        => services.AddSingleton<IChatClient>(_ => new BedrockChatClient(accessKeyId, secretAccessKey, model, region));

    #endregion

#if NET8_0_OR_GREATER
    #region Keyed 注册（.NET 8+）

    /// <summary>注册 Keyed <see cref="IChatClient"/> 单例，适用于同一项目使用多个服务商的场景</summary>
    /// <param name="services">服务集合</param>
    /// <param name="serviceKey">服务键，注入时通过 [FromKeyedServices] 区分</param>
    /// <param name="configure">选项配置委托；必须设置 <see cref="AiClientOptions.Code"/> 以指定服务商</param>
    /// <returns>服务集合（支持链式调用）</returns>
    /// <exception cref="ArgumentNullException">configure 为 null 时抛出</exception>
    /// <exception cref="InvalidOperationException">Code 未设置或服务商未注册时抛出</exception>
    public static IServiceCollection AddKeyedAiClient(this IServiceCollection services, String serviceKey, Action<AiClientOptions> configure)
    {
        if (configure == null) throw new ArgumentNullException(nameof(configure));

        services.AddKeyedSingleton(serviceKey, (_, _) =>
        {
            var opts = new AiClientOptions();
            configure(opts);
            if (String.IsNullOrWhiteSpace(opts.Code))
                throw new InvalidOperationException("必须通过 opts.Code 指定服务商编码，如 \"DashScope\"、\"OpenAI\"");
            return AiClientRegistry.Default.CreateClient(opts.Code, opts);
        });

        return services;
    }

    /// <summary>注册 Keyed OpenAI 兼容协议 <see cref="IChatClient"/> 单例</summary>
    /// <param name="services">服务集合</param>
    /// <param name="serviceKey">服务键</param>
    /// <param name="apiKey">API 密钥</param>
    /// <param name="model">默认模型</param>
    /// <param name="endpoint">API 地址覆盖</param>
    /// <returns>服务集合（支持链式调用）</returns>
    public static IServiceCollection AddKeyedOpenAI(this IServiceCollection services, String serviceKey, String apiKey, String? model = null, String? endpoint = null)
        => services.AddKeyedSingleton<IChatClient>(serviceKey, (_, _) => new OpenAIChatClient(apiKey, model, endpoint));

    /// <summary>注册 Keyed 阿里百炼 DashScope <see cref="IChatClient"/> 单例</summary>
    /// <param name="services">服务集合</param>
    /// <param name="serviceKey">服务键</param>
    /// <param name="apiKey">阿里云 API Key</param>
    /// <param name="model">默认模型</param>
    /// <param name="endpoint">API 地址覆盖</param>
    /// <returns>服务集合（支持链式调用）</returns>
    public static IServiceCollection AddKeyedDashScope(this IServiceCollection services, String serviceKey, String apiKey, String? model = null, String? endpoint = null)
        => services.AddKeyedSingleton<IChatClient>(serviceKey, (_, _) => new DashScopeChatClient(apiKey, model, endpoint));

    /// <summary>注册 Keyed Anthropic Claude <see cref="IChatClient"/> 单例</summary>
    /// <param name="services">服务集合</param>
    /// <param name="serviceKey">服务键</param>
    /// <param name="apiKey">Anthropic API Key</param>
    /// <param name="model">默认模型</param>
    /// <param name="endpoint">API 地址覆盖</param>
    /// <returns>服务集合（支持链式调用）</returns>
    public static IServiceCollection AddKeyedAnthropic(this IServiceCollection services, String serviceKey, String apiKey, String? model = null, String? endpoint = null)
        => services.AddKeyedSingleton<IChatClient>(serviceKey, (_, _) => new AnthropicChatClient(apiKey, model, endpoint));

    /// <summary>注册 Keyed Google Gemini <see cref="IChatClient"/> 单例</summary>
    /// <param name="services">服务集合</param>
    /// <param name="serviceKey">服务键</param>
    /// <param name="apiKey">Google API Key</param>
    /// <param name="model">默认模型</param>
    /// <param name="endpoint">API 地址覆盖</param>
    /// <returns>服务集合（支持链式调用）</returns>
    public static IServiceCollection AddKeyedGemini(this IServiceCollection services, String serviceKey, String apiKey, String? model = null, String? endpoint = null)
        => services.AddKeyedSingleton<IChatClient>(serviceKey, (_, _) => new GeminiChatClient(apiKey, model, endpoint));

    /// <summary>注册 Keyed Ollama <see cref="IChatClient"/> 单例</summary>
    /// <param name="services">服务集合</param>
    /// <param name="serviceKey">服务键</param>
    /// <param name="apiKey">API 密钥；本地部署可传 null 或空字符串</param>
    /// <param name="model">默认模型</param>
    /// <param name="endpoint">Ollama 地址；为空时使用默认 http://localhost:11434</param>
    /// <returns>服务集合（支持链式调用）</returns>
    public static IServiceCollection AddKeyedOllama(this IServiceCollection services, String serviceKey, String? apiKey = null, String? model = null, String? endpoint = null)
        => services.AddKeyedSingleton<IChatClient>(serviceKey, (_, _) => new OllamaChatClient(apiKey, model, endpoint));

    /// <summary>注册 Keyed 新生命 AI 网关 <see cref="IChatClient"/> 单例</summary>
    /// <param name="services">服务集合</param>
    /// <param name="serviceKey">服务键</param>
    /// <param name="apiKey">新生命 AI 网关 API Key</param>
    /// <param name="model">默认模型</param>
    /// <param name="endpoint">网关地址覆盖</param>
    /// <returns>服务集合（支持链式调用）</returns>
    public static IServiceCollection AddKeyedNewLifeAI(this IServiceCollection services, String serviceKey, String apiKey, String? model = null, String? endpoint = null)
        => services.AddKeyedSingleton<IChatClient>(serviceKey, (_, _) => new NewLifeAIChatClient(apiKey, model, endpoint));

    /// <summary>注册 Keyed Azure OpenAI <see cref="IChatClient"/> 单例</summary>
    /// <param name="services">服务集合</param>
    /// <param name="serviceKey">服务键</param>
    /// <param name="apiKey">Azure API Key</param>
    /// <param name="model">deployment 名称</param>
    /// <param name="endpoint">Azure OpenAI 完整地址</param>
    /// <returns>服务集合（支持链式调用）</returns>
    public static IServiceCollection AddKeyedAzureAI(this IServiceCollection services, String serviceKey, String apiKey, String? model = null, String? endpoint = null)
        => services.AddKeyedSingleton<IChatClient>(serviceKey, (_, _) => new AzureAIChatClient(apiKey, model, endpoint));

    /// <summary>注册 Keyed AWS Bedrock <see cref="IChatClient"/> 单例</summary>
    /// <param name="services">服务集合</param>
    /// <param name="serviceKey">服务键</param>
    /// <param name="accessKeyId">AWS Access Key ID</param>
    /// <param name="secretAccessKey">AWS Secret Access Key</param>
    /// <param name="model">默认模型 ID</param>
    /// <param name="region">AWS 区域</param>
    /// <returns>服务集合（支持链式调用）</returns>
    public static IServiceCollection AddKeyedBedrock(this IServiceCollection services, String serviceKey, String accessKeyId, String secretAccessKey, String? model = null, String? region = null)
        => services.AddKeyedSingleton<IChatClient>(serviceKey, (_, _) => new BedrockChatClient(accessKeyId, secretAccessKey, model, region));

    #endregion
#endif
}
