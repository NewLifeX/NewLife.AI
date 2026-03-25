using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using NewLife.AI.Models;
using NewLife.AI.Providers;
using NewLife.AI.Tools;
using NewLife.ChatAI.Entity;
using NewLife.Log;
using AiChatMessage = NewLife.AI.Models.ChatMessage;
using ChatResponse = NewLife.AI.Models.ChatResponse;
using ChatStreamEvent = NewLife.AI.Models.ChatStreamEvent;
using UsageDetails = NewLife.AI.Models.UsageDetails;

namespace NewLife.ChatAI.Services;

/// <summary>ChatAI 对话执行管道。将工具调用、技能注入、知识进化等中间件组装为统一的执行入口</summary>
/// <remarks>
/// 流式管道为单路径：
/// <code>
/// FilteredChatClient(LearningFilter + AgentTriggerFilter)
///   → ToolChatClient（工具调用循环）
///     → 服务商 RawClient（HTTP 调用）
/// </code>
/// 过滤器包在最外层，仅在全部工具调用完成后触发一次 OnStreamCompletedAsync。
/// </remarks>
public class ChatAIPipeline(GatewayService gatewayService, IEnumerable<IToolProvider> toolProviders, ITracer tracer) : IChatPipeline
{
    #region IChatPipeline

    /// <inheritdoc/>
    public async IAsyncEnumerable<ChatStreamEvent> StreamAsync(
        IList<AiChatMessage> contextMessages,
        ModelConfig modelConfig,
        ThinkingMode thinkingMode,
        ChatPipelineContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // 2. 获取服务商客户端
        var descriptor = gatewayService.GetDescriptor(modelConfig);
        if (descriptor == null)
        {
            yield return ChatStreamEvent.ErrorEvent("MODEL_UNAVAILABLE", $"未找到服务商 '{modelConfig.GetEffectiveProvider()}'");
            yield break;
        }

        var providerOptions = GatewayService.BuildOptions(modelConfig);
        using var rawClient = descriptor.Factory(providerOptions);

        // 3. 组装中间件管道：过滤器在外层（仅触发一次回调），工具循环在内层靠近 RawClient
        var clientBuilder = rawClient.AsBuilder();
        var providers = BuildScopedProviders(context.SelectedTools).ToArray();
        if (providers.Length > 0) clientBuilder = clientBuilder.UseTools(providers);

        // 4. 构建 ChatOptions
        var chatOptions = new ChatOptions
        {
            Model = modelConfig.Code,
            EnableThinking = thinkingMode switch
            {
                ThinkingMode.Think => true,
                ThinkingMode.Fast => false,
                _ => modelConfig.SupportThinking ? true : null,
            },
            UserId = context.UserId,
            ConversationId = context.ConversationId,
        };
        if (context.Items.Count > 0) chatOptions.Items = context.Items;

        // 5. 流式调用并转换为 SSE 事件
        using var streamClient = clientBuilder.Build();

        var thinkingBuilder = new StringBuilder();
        UsageDetails? lastUsage = null;
        Int64 thinkingStart = 0;
        var streamSw = Stopwatch.StartNew();

        await foreach (var chunk in streamClient.GetStreamingResponseAsync(contextMessages, chatOptions, cancellationToken).ConfigureAwait(false))
        {
            if (chunk.Usage != null) lastUsage = chunk.Usage;

            var choice = chunk.Messages?.FirstOrDefault();
            if (choice == null) continue;

            var delta = choice.Delta;
            if (delta == null) continue;

            if (!String.IsNullOrEmpty(delta.ReasoningContent))
            {
                if (thinkingStart == 0) thinkingStart = Runtime.TickCount64;
                thinkingBuilder.Append(delta.ReasoningContent);
                yield return ChatStreamEvent.ThinkingDelta(delta.ReasoningContent);
            }

            var text = delta.Content as String;
            if (!String.IsNullOrEmpty(text))
                yield return ChatStreamEvent.ContentDelta(text);
        }

        if (thinkingBuilder.Length > 0)
            yield return ChatStreamEvent.ThinkingDone((Int32)(Runtime.TickCount64 - thinkingStart));

        streamSw.Stop();
        lastUsage ??= new UsageDetails();
        lastUsage.ElapsedMs = (Int32)streamSw.ElapsedMilliseconds;

        yield return ChatStreamEvent.MessageDone(lastUsage);
    }

    /// <inheritdoc/>
    public async Task<ChatResponse> CompleteAsync(
        IList<AiChatMessage> contextMessages,
        ModelConfig modelConfig,
        ChatPipelineContext context,
        CancellationToken cancellationToken)
    {
        var descriptor = gatewayService.GetDescriptor(modelConfig);
        if (descriptor == null)
            return new ChatResponse { Messages = [new ChatChoice { Message = new AiChatMessage { Role = "assistant", Content = "未找到服务商" } }] };

        var providerOptions = GatewayService.BuildOptions(modelConfig);
        using var rawClient = descriptor.Factory(providerOptions);

        var clientBuilder = rawClient.AsBuilder();
        var providers2 = BuildScopedProviders(context.SelectedTools).ToArray();
        if (providers2.Length > 0) clientBuilder = clientBuilder.UseTools(providers2);

        var chatOptions = new ChatOptions
        {
            Model = modelConfig.Code,
            UserId = context.UserId,
            ConversationId = context.ConversationId,
        };

        using var chatClient = clientBuilder.Build();
        return await chatClient.GetResponseAsync(contextMessages, chatOptions, cancellationToken).ConfigureAwait(false);
    }

    #endregion

    #region 辅助
    /// <summary>将 DbToolProvider 包装为携带 selectedTools 的作用域版本</summary>
    private IEnumerable<IToolProvider> BuildScopedProviders(ISet<String> selectedTools)
    {
        foreach (var p in toolProviders)
        {
            if (p is DbToolProvider dbTool)
                yield return new ScopedDbToolProvider(dbTool, selectedTools);
            else
                yield return p;
        }
    }

    /// <summary>携带 SelectedTools 的轻量 DbToolProvider 包装器</summary>
    private sealed class ScopedDbToolProvider(DbToolProvider inner, ISet<String> selectedTools) : IToolProvider
    {
        /// <inheritdoc/>
        public IList<ChatTool> GetTools() => inner.GetFilteredTools(selectedTools);

        /// <inheritdoc/>
        public Task<String> CallToolAsync(String toolName, String? argumentsJson, CancellationToken cancellationToken = default)
            => inner.CallToolAsync(toolName, argumentsJson, cancellationToken);
    }

    #endregion

    #region 日志
    #endregion
}
