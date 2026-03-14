using NewLife.AI.Models;

namespace NewLife.AI.Providers;

/// <summary>AI 对话客户端接口。绑定连接参数，直接执行对话请求，无需每次传入 Options</summary>
/// <remarks>
/// 设计对标 Microsoft.Extensions.AI（MEAI）的 IChatClient，使熟悉 MEAI 的开发者可无缝迁移。
/// 与 <see cref="IAiProvider"/> 的关系：IAiProvider 是无状态的服务商描述与工厂，
/// 通过 <see cref="IAiProvider.CreateClient"/> 创建已绑定 Endpoint/ApiKey 的 IChatClient 实例。
/// 使用示例：
/// <code>
/// var client = provider.CreateClient(options);
/// // 或通过 Builder 组装中间件管道：
/// var client = new ChatClientBuilder(provider, options)
///     .UseLogging(log)
///     .UseTracing(tracer)
///     .Build();
/// var response = await client.CompleteAsync(request);
/// </code>
/// </remarks>
public interface IChatClient : IDisposable
{
    /// <summary>客户端元数据。描述所连接的服务商与默认模型</summary>
    ChatClientMetadata Metadata { get; }

    /// <summary>非流式对话完成。发送请求并一次性返回完整响应</summary>
    /// <param name="request">对话请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>完整的对话响应</returns>
    Task<ChatCompletionResponse> CompleteAsync(ChatCompletionRequest request, CancellationToken cancellationToken = default);

    /// <summary>流式对话完成。逐块返回生成内容</summary>
    /// <param name="request">对话请求（自动设置 Stream=true）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>流式响应块的异步枚举</returns>
    IAsyncEnumerable<ChatCompletionResponse> CompleteStreamingAsync(ChatCompletionRequest request, CancellationToken cancellationToken = default);
}

/// <summary>AI 对话客户端元数据。描述客户端连接的服务商与模型信息</summary>
public class ChatClientMetadata
{
    /// <summary>服务商名称，如 "OpenAI"、"阿里百炼"</summary>
    public String ProviderName { get; init; } = null!;

    /// <summary>API 地址</summary>
    public String? Endpoint { get; init; }

    /// <summary>默认模型编码，即 API 请求中 model 字段的值</summary>
    public String? DefaultModelId { get; init; }
}
