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
/// var response = await client.CompleteAsync("你好，请介绍一下你自己");
/// Console.WriteLine(response.Text);
/// </code>
/// </remarks>
public interface IChatClient : IDisposable
{
    /// <summary>非流式对话完成。发送消息列表并一次性返回完整响应</summary>
    /// <param name="messages">消息列表</param>
    /// <param name="options">对话选项，可覆盖客户端默认参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>完整的对话响应</returns>
    Task<ChatResponse> GetResponseAsync(IList<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>流式对话完成。逐块返回生成内容</summary>
    /// <param name="messages">消息列表</param>
    /// <param name="options">对话选项，可覆盖客户端默认参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>流式响应块的异步枚举</returns>
    IAsyncEnumerable<ChatResponse> GetStreamingResponseAsync(IList<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default);
}

/// <summary>IChatClient 扩展方法。提供常用便捷调用方式</summary>
public static class ChatClientExtensions
{
    /// <summary>发送单条文本消息并获取完整响应</summary>
    /// <param name="client">对话客户端</param>
    /// <param name="prompt">用户消息文本</param>
    /// <param name="options">对话选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>完整的对话响应</returns>
    public static Task<ChatResponse> GetResponseAsync(this IChatClient client, String prompt, ChatOptions? options = null, CancellationToken cancellationToken = default)
        => client.GetResponseAsync([new ChatMessage { Role = "user", Content = prompt }], options, cancellationToken);

    /// <summary>发送单条文本消息并获取流式响应</summary>
    /// <param name="client">对话客户端</param>
    /// <param name="prompt">用户消息文本</param>
    /// <param name="options">对话选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>流式响应块的异步枚举</returns>
    public static IAsyncEnumerable<ChatResponse> GetStreamingResponseAsync(this IChatClient client, String prompt, ChatOptions? options = null, CancellationToken cancellationToken = default)
        => client.GetStreamingResponseAsync([new ChatMessage { Role = "user", Content = prompt }], options, cancellationToken);

    /// <summary>发送单条文本消息并直接返回回复文本。最简单的一行调用方式</summary>
    /// <param name="client">对话客户端</param>
    /// <param name="prompt">用户消息文本</param>
    /// <param name="options">对话选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>模型回复文本，失败时返回 null</returns>
    public static async Task<String?> AskAsync(this IChatClient client, String prompt, ChatOptions? options = null, CancellationToken cancellationToken = default)
        => (await client.GetResponseAsync(prompt, options, cancellationToken).ConfigureAwait(false)).Text;

    /// <summary>以元组形式传入多条消息并直接返回回复文本。对标 Python dashscope 的 messages 列表写法，无需构造 ChatMessage 对象</summary>
    /// <param name="client">对话客户端</param>
    /// <param name="messages">消息元组数组，每项为 (role, content)，如 ("system", "你是助手"), ("user", "你好")</param>
    /// <param name="options">对话选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>模型回复文本，失败时返回 null</returns>
    public static async Task<String?> AskAsync(this IChatClient client, (String Role, String Content)[] messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var chatMessages = new List<ChatMessage>(messages.Length);
        foreach (var (role, content) in messages)
            chatMessages.Add(new ChatMessage { Role = role, Content = content });
        return (await client.GetResponseAsync(chatMessages, options, cancellationToken).ConfigureAwait(false)).Text;
    }

    /// <summary>将客户端包装为 <see cref="ChatClientBuilder"/>，以便链式添加中间件</summary>
    /// <param name="client">当前客户端（将作为管道的最内层）</param>
    /// <returns>以 client 为内层的构建器</returns>
    /// <example>
    /// <code>
    /// var client = new DashScopeProvider()
    ///     .CreateClient(apiKey, "qwen3.5-flash")
    ///     .AsBuilder()
    ///     .UseMcp(mcpProvider)
    ///     .UseFilters(new LogFilter())
    ///     .Build();
    /// </code>
    /// </example>
    public static ChatClientBuilder AsBuilder(this IChatClient client) => new(client);
}
