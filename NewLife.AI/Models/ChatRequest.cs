using NewLife.AI.Clients;
using NewLife.AI.Clients.OpenAI;

namespace NewLife.AI.Models;

/// <summary>内部对话请求。继承自 ChatOptions，新增消息列表与流式标志，作为内部管道统一传输对象</summary>
/// <remarks>
/// 职责分离设计：
/// <list type="bullet">
/// <item><see cref="ChatCompletionRequest"/>：OpenAI 协议 Wire-Format DTO，仅用于 Controller [FromBody] 接收前端请求</item>
/// <item><see cref="ChatRequest"/>：内部统一传输模型，继承 ChatOptions 所有参数，增加 Messages 和 Stream，贯穿过滤器链到协议层</item>
/// </list>
/// </remarks>
public class ChatRequest : ChatOptions, IChatRequest
{
    #region 属性
    /// <summary>消息列表</summary>
    public IList<ChatMessage> Messages { get; set; } = [];

    /// <summary>是否流式输出</summary>
    public Boolean Stream { get; set; }
    #endregion

    #region 方法
    /// <summary>根据消息列表和可选对话选项创建内部请求</summary>
    /// <param name="messages">消息列表</param>
    /// <param name="options">对话选项，null 字段不复制</param>
    /// <param name="stream">是否流式</param>
    /// <returns>内部请求实例</returns>
    public static ChatRequest Create(IList<ChatMessage> messages, ChatOptions? options = null, Boolean stream = false)
    {
        var request = new ChatRequest
        {
            Messages = messages,
            Stream = stream,
        };
        if (options == null) return request;

        request.Model = options.Model;
        request.Temperature = options.Temperature;
        request.TopP = options.TopP;
        request.TopK = options.TopK;
        request.MaxTokens = options.MaxTokens;
        request.Stop = options.Stop;
        request.PresencePenalty = options.PresencePenalty;
        request.FrequencyPenalty = options.FrequencyPenalty;
        request.User = options.User;
        request.ReasoningEffort = options.ReasoningEffort;
        request.EnableThinking = options.EnableThinking;
        request.ResponseFormat = options.ResponseFormat;
        request.ParallelToolCalls = options.ParallelToolCalls;
        request.UserId = options.UserId;
        request.ConversationId = options.ConversationId;
        request.ToolChoice = options.ToolChoice;

        if (options.Tools != null && options.Tools.Count > 0)
        {
            request.Tools ??= [];
            foreach (var t in options.Tools)
                request.Tools.Add(t);
        }

        // 直接引用共享 options.Items（故意设计）：request 与调用方 options 共享同一字典，
        // 协议层写入 request["xxx"]（如 DashScope 的 EnableWebExtractor）同步反映到调用方，
        // 多轮循环持续保留协议专属键值。勿改为逐项拷贝（A-53 曾误改，已恢复）
        if (options.Items != null && options.Items.Count > 0)
            request.Items = options.Items;

        return request;
    }
    #endregion
}
