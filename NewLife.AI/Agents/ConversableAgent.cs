using NewLife.AI.Models;
using NewLife.AI.Providers;

namespace NewLife.AI.Agents;

/// <summary>可对话代理。持有 IChatClient，将消息历史转换为 ChatRequest 并获取 LLM 响应</summary>
/// <remarks>
/// 支持工具调用自动循环（Auto-tool-loop）：
/// <list type="number">
/// <item>将历史消息转换为 ChatMessage 列表（含 SystemPrompt）</item>
/// <item>调用 chatClient.CompleteAsync 获取响应</item>
/// <item>若响应含 tool_calls，则产出 ToolCallMessage（由调用方决定是否执行）</item>
/// <item>否则，将文本内容作为 TextMessage 产出</item>
/// </list>
/// </remarks>
public class ConversableAgent : IAgent
{
    #region 属性

    /// <inheritdoc/>
    public String Name { get; }

    /// <inheritdoc/>
    public String? Description { get; }

    /// <summary>底层 IChatClient。执行实际 LLM 调用</summary>
    public IChatClient ChatClient { get; }

    /// <summary>系统提示词。在每次请求前作为 system 消息注入</summary>
    public String? SystemPrompt { get; set; }

    /// <summary>可用工具列表。为 null 时不启用函数调用</summary>
    public IList<ChatTool>? Tools { get; set; }

    /// <summary>最大自动回复次数（单轮 HandleAsync 内）。防止工具调用死循环</summary>
    public Int32 MaxAutoReply { get; set; } = 5;

    /// <summary>请求参数模板（Model、Temperature 等）</summary>
    public ChatOptions? RequestOptions { get; set; }

    #endregion

    #region 构造

    /// <summary>初始化可对话代理</summary>
    /// <param name="name">代理名称</param>
    /// <param name="chatClient">底层 IChatClient</param>
    /// <param name="systemPrompt">系统提示词，可为 null</param>
    public ConversableAgent(String name, IChatClient chatClient, String? systemPrompt = null)
    {
        if (String.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
        if (chatClient == null) throw new ArgumentNullException(nameof(chatClient));

        Name = name;
        ChatClient = chatClient;
        SystemPrompt = systemPrompt;
        Description = null;
    }

    /// <summary>初始化可对话代理（含描述）</summary>
    /// <param name="name">代理名称</param>
    /// <param name="description">代理描述（供调度器参考）</param>
    /// <param name="chatClient">底层 IChatClient</param>
    /// <param name="systemPrompt">系统提示词</param>
    public ConversableAgent(String name, String? description, IChatClient chatClient, String? systemPrompt = null)
        : this(name, chatClient, systemPrompt)
    {
        Description = description;
    }

    #endregion

    #region 方法

    /// <summary>处理历史消息，返回 LLM 响应消息流</summary>
    /// <param name="history">完整消息历史</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async IAsyncEnumerable<AgentMessage> HandleAsync(
        IList<AgentMessage> history,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (history == null) throw new ArgumentNullException(nameof(history));

        var messages = new List<ChatMessage>();

        // 注入系统提示
        if (!String.IsNullOrEmpty(SystemPrompt))
            messages.Add(new ChatMessage { Role = "system", Content = SystemPrompt });

        // 追加历史消息（过滤掉不可转换的 Stop 等）
        foreach (var msg in AgentMessageHelper.ToChatMessages(history))
            messages.Add(msg);

        var chatOptions = new ChatOptions
        {
            Model = RequestOptions?.Model,
            Temperature = RequestOptions?.Temperature,
            MaxTokens = RequestOptions?.MaxTokens,
            Tools = Tools,
            ToolChoice = Tools != null && Tools.Count > 0 ? "auto" : null,
        };

        cancellationToken.ThrowIfCancellationRequested();

        var response = await ChatClient.GetResponseAsync(messages, chatOptions, cancellationToken).ConfigureAwait(false);
        var choice = response?.Messages?.Count > 0 ? response.Messages[0] : null;
        if (choice?.Message == null) yield break;

        var toolCalls = choice.Message.ToolCalls;
        if (toolCalls != null && toolCalls.Count > 0)
        {
            // 产出工具调用消息，由 GroupChat / 调用方决定如何执行
            foreach (var tc in toolCalls)
            {
                if (tc?.Function == null) continue;
                yield return new ToolCallMessage
                {
                    Source = Name,
                    ToolName = tc.Function.Name ?? String.Empty,
                    Arguments = tc.Function.Arguments,
                    CallId = tc.Id ?? Guid.NewGuid().ToString("N"),
                };
            }
        }
        else
        {
            // 普通文本响应
            var content = choice.Message.Content?.ToString() ?? String.Empty;
            yield return new TextMessage
            {
                Source = Name,
                Role = "assistant",
                Content = content,
            };
        }
    }

    #endregion
}
