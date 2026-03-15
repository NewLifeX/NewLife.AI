using NewLife.AI.Filters;
using NewLife.AI.Models;
using NewLife.AI.Providers;

namespace NewLife.AI.Tools;

/// <summary>原生工具对话客户端。将 <see cref="ToolRegistry"/> 中的工具自动注入请求并自动处理工具调用回路</summary>
/// <remarks>
/// 工作流：
/// <list type="number">
/// <item>CompleteAsync 前，将 <see cref="ToolRegistry.Tools"/> 合并到请求的 <c>Tools</c> 列表</item>
/// <item>调用内层客户端获取响应</item>
/// <item>若响应含 <c>tool_calls</c>，依次通过 <see cref="ToolRegistry.TryInvokeAsync"/> 执行并追加 tool 消息</item>
/// <item>循环重新调用模型，直到无更多工具调用（最多 <see cref="MaxIterations"/> 轮）</item>
/// </list>
/// 使用方式：
/// <code>
/// var registry = new ToolRegistry();
/// registry.AddTools(new WeatherService());
///
/// var client = new ChatClientBuilder(provider, options)
///     .UseNativeTools(registry)
///     .Build();
/// </code>
/// </remarks>
public class NativeToolChatClient : DelegatingChatClient
{
    #region 属性

    /// <summary>工具注册表</summary>
    public ToolRegistry Registry { get; }

    /// <summary>最大工具调用循环次数，防止无限递归。默认 10</summary>
    public Int32 MaxIterations { get; set; } = 10;

    #endregion

    #region 构造

    /// <summary>初始化原生工具客户端</summary>
    /// <param name="innerClient">内层客户端</param>
    /// <param name="registry">工具注册表</param>
    public NativeToolChatClient(IChatClient innerClient, ToolRegistry registry) : base(innerClient)
    {
        if (registry == null) throw new ArgumentNullException(nameof(registry));
        Registry = registry;
    }

    #endregion

    #region 方法

    /// <summary>非流式对话完成。注入工具定义并自动处理工具调用回路</summary>
    /// <param name="request">对话请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public override async Task<ChatCompletionResponse> CompleteAsync(ChatCompletionRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (Registry.Tools.Count == 0)
            return await InnerClient.CompleteAsync(request, cancellationToken).ConfigureAwait(false);

        // 合并工具定义到请求（不修改调用方的原始请求，通过浅拷贝隔离）
        var workRequest = ShallowCloneWithTools(request);

        ChatCompletionResponse response;
        var iterations = 0;
        var messages = workRequest.Messages;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            response = await InnerClient.CompleteAsync(workRequest, cancellationToken).ConfigureAwait(false);

            // 从第一个 Choice 中获取工具调用
            var assistantMessage = response.Choices?.FirstOrDefault()?.Message;
            var toolCalls = assistantMessage?.ToolCalls;
            if (toolCalls == null || toolCalls.Count == 0) break;
            if (++iterations > MaxIterations) break;

            // 追加 assistant 消息（含工具调用）
            messages.Add(new ChatMessage
            {
                Role = "assistant",
                Content = assistantMessage?.Content,
                ToolCalls = toolCalls.Select(tc => new ToolCall
                {
                    Id = tc.Id,
                    Type = tc.Type,
                    Function = tc.Function
                }).ToList<ToolCall>()
            });

            // 依次执行所有工具调用
            foreach (var tc in toolCalls)
            {
                if (tc.Function == null) continue;
                var result = await Registry.TryInvokeAsync(tc.Function.Name, tc.Function.Arguments, cancellationToken).ConfigureAwait(false);
                messages.Add(new ChatMessage
                {
                    Role = "tool",
                    ToolCallId = tc.Id,
                    Content = result
                });
            }

            // 在下一轮请求里继续带上工具定义
            workRequest = ShallowCloneWithTools(request, messages);
        }

        return response;
    }

    #endregion

    #region 辅助

    private ChatCompletionRequest ShallowCloneWithTools(ChatCompletionRequest src, IList<ChatMessage>? messages = null)
    {
        var tools = new List<ChatTool>(Registry.Tools);
        if (src.Tools != null)
        {
            foreach (var t in src.Tools)
                tools.Add(t);
        }

        return new ChatCompletionRequest
        {
            Model = src.Model,
            Messages = messages ?? src.Messages.ToList(),
            Temperature = src.Temperature,
            TopP = src.TopP,
            MaxTokens = src.MaxTokens,
            Stream = src.Stream,
            Stop = src.Stop,
            PresencePenalty = src.PresencePenalty,
            FrequencyPenalty = src.FrequencyPenalty,
            Tools = tools,
            ToolChoice = src.ToolChoice ?? "auto",
            User = src.User,
            EnableThinking = src.EnableThinking
        };
    }

    #endregion
}
