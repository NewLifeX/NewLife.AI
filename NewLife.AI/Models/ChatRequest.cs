namespace NewLife.AI.Models;

/// <summary>内部对话请求。继承自 ChatOptions，新增消息列表与流式标志，作为内部管道统一传输对象</summary>
/// <remarks>
/// 职责分离设计：
/// <list type="bullet">
/// <item><see cref="ChatCompletionRequest"/>：OpenAI 协议 Wire-Format DTO，含 [DataMember] 注解，仅用于 Controller [FromBody] 接收前端请求</item>
/// <item><see cref="ChatRequest"/>：内部统一传输模型，继承 ChatOptions 所有参数，增加 Messages 和 Stream，贯穿过滤器链到协议层</item>
/// </list>
/// </remarks>
public class ChatRequest : ChatOptions
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

        if (options.Items != null && options.Items.Count > 0)
        {
            if (request.Items == null || request.Items.Count == 0)
                request.Items = options.Items;
            else
            {
                foreach (var kv in options.Items)
                    request.Items[kv.Key] = kv.Value;
            }
        }

        return request;
    }
    #endregion

    #region 转换
    /// <summary>从 OpenAI 协议 DTO 创建内部请求。用于 GatewayController 接收前端请求后的唯一转换入口</summary>
    /// <param name="req">前端传入的 OpenAI 格式对话请求</param>
    /// <returns>内部传输模型实例</returns>
    public static ChatRequest From(ChatCompletionRequest req) => new()
    {
        Model = req.Model,
        Messages = req.Messages,
        Stream = req.Stream,
        Temperature = req.Temperature,
        TopP = req.TopP,
        TopK = req.TopK,
        MaxTokens = req.MaxTokens,
        Stop = req.Stop,
        PresencePenalty = req.PresencePenalty,
        FrequencyPenalty = req.FrequencyPenalty,
        Tools = req.Tools,
        ToolChoice = req.ToolChoice,
        User = req.User,
        EnableThinking = req.EnableThinking,
        ResponseFormat = req.ResponseFormat,
        ParallelToolCalls = req.ParallelToolCalls,
        Items = req.Items,
    };
    #endregion
}
