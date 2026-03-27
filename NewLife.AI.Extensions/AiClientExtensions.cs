using NewLife.AI.Providers;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>IChatClient 的依赖注入扩展方法。适用于 ASP.NET Core 等 Web 项目的标准 DI 注册场景</summary>
/// <remarks>
/// 典型用法：
/// <code>
/// // 单服务商
/// services.AddAiClient(opts =>
/// {
///     opts.Code    = "DashScope";
///     opts.ApiKey  = "sk-xxx";
///     opts.Model   = "qwen3.5-flash";
/// });
/// // 注入：IChatClient
///
/// // 多服务商（.NET 8+）
/// services.AddKeyedAiClient("fast",   opts => { opts.Code = "DashScope"; opts.ApiKey = "..."; opts.Model = "qwen3.5-flash"; });
/// services.AddKeyedAiClient("strong", opts => { opts.Code = "OpenAI";    opts.ApiKey = "..."; opts.Model = "gpt-4o"; });
/// // 注入：[FromKeyedServices("fast")] IChatClient client
/// </code>
/// </remarks>
public static class AiClientExtensions
{
    /// <summary>注册默认 <see cref="IChatClient"/> 单例。通过 <paramref name="configure"/> 配置服务商编码、密钥和模型</summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">选项配置委托；必须设置 <see cref="AiClientOptions.Code"/> 以指定服务商</param>
    /// <returns>服务集合（支持链式调用）</returns>
    /// <exception cref="ArgumentNullException">configure 为 null 时抛出</exception>
    /// <exception cref="InvalidOperationException">Code 未设置或服务商未注册时抛出</exception>
    public static IServiceCollection AddAiClient(this IServiceCollection services, Action<AiClientOptions> configure)
    {
        if (configure == null) throw new ArgumentNullException(nameof(configure));

        services.AddSingleton<IChatClient>(_ =>
        {
            var opts = new AiClientOptions();
            configure(opts);
            if (String.IsNullOrWhiteSpace(opts.Code))
                throw new InvalidOperationException("必须通过 opts.Code 指定服务商编码，如 \"DashScope\"、\"OpenAI\"");
            return AiClientRegistry.Default.CreateClient(opts.Code, opts);
        });

        return services;
    }

#if NET8_0_OR_GREATER
    /// <summary>注册 Keyed <see cref="IChatClient"/> 单例，适用于同一项目使用多个服务商的场景</summary>
    /// <param name="services">服务集合</param>
    /// <param name="serviceKey">服务键，如 "fast"、"strong"，注入时通过 [FromKeyedServices] 区分</param>
    /// <param name="configure">选项配置委托；必须设置 <see cref="AiClientOptions.Code"/> 以指定服务商</param>
    /// <returns>服务集合（支持链式调用）</returns>
    /// <exception cref="ArgumentNullException">configure 为 null 时抛出</exception>
    /// <exception cref="InvalidOperationException">Code 未设置或服务商未注册时抛出</exception>
    public static IServiceCollection AddKeyedAiClient(this IServiceCollection services, String serviceKey, Action<AiClientOptions> configure)
    {
        if (configure == null) throw new ArgumentNullException(nameof(configure));

        services.AddKeyedSingleton<IChatClient>(serviceKey, (_, _) =>
        {
            var opts = new AiClientOptions();
            configure(opts);
            if (String.IsNullOrWhiteSpace(opts.Code))
                throw new InvalidOperationException("必须通过 opts.Code 指定服务商编码，如 \"DashScope\"、\"OpenAI\"");
            return AiClientRegistry.Default.CreateClient(opts.Code, opts);
        });

        return services;
    }
#endif
}
