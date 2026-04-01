using System.Runtime.Serialization;
using NewLife.AI.Models;
using NewLife.Collections;

namespace NewLife.AI.Clients.DashScope;

/// <summary>阿里百炼 DashScope 原生协议非流式响应。兼容 https://help.aliyun.com/zh/model-studio/qwen-api-via-dashscope 协议</summary>
/// <remarks>
/// 与 OpenAI ChatCompletionResponse 的主要差异：
/// <list type="bullet">
/// <item>响应内容在 output.choices 嵌套字段，而非顶级 choices</item>
/// <item>唯一请求标识为顶级 request_id 字段</item>
/// <item>Usage 使用 input_tokens/output_tokens/total_tokens 命名，并扩展了多模态 Token 字段</item>
/// <item>错误时在顶级返回 code/message 字段</item>
/// </list>
/// </remarks>
public class DashScopeResponse : IChatResponse
{
    #region 属性
    /// <summary>请求编号</summary>
    [DataMember(Name = "request_id")]
    public String? RequestId { get; set; }

    /// <summary>错误码。非空时表示请求失败</summary>
    public String? Code { get; set; }

    /// <summary>错误消息</summary>
    public String? Message { get; set; }

    /// <summary>输出容器。包含 choices 列表</summary>
    public DashScopeOutput? Output { get; set; }

    /// <summary>令牌用量统计</summary>
    public DashScopeUsageData? Usage { get; set; }
    #endregion

    #region IChatResponse 适配
    /// <summary>响应标识。映射到 RequestId</summary>
    [IgnoreDataMember]
    String? IChatResponse.Id { get => RequestId; set => RequestId = value; }

    /// <summary>对象类型</summary>
    [IgnoreDataMember]
    String? IChatResponse.Object { get; set; }

    /// <summary>创建时间。DashScope 原生不返回此字段</summary>
    [IgnoreDataMember]
    public DateTimeOffset Created { get; set; }

    /// <summary>模型编码。DashScope 原生响应无顶级 model 字段</summary>
    [IgnoreDataMember]
    public String? Model { get; set; }

    /// <summary>响应消息列表适配。将 Output.Choices 转换为 ChatChoice 列表</summary>
    [IgnoreDataMember]
    private IList<ChatChoice>? _messages;

    /// <summary>消息列表适配</summary>
    [IgnoreDataMember]
    IList<ChatChoice>? IChatResponse.Messages
    {
        get
        {
            if (_messages == null && Output?.Choices != null)
            {
                var list = new List<ChatChoice>(Output.Choices.Count);
                for (var i = 0; i < Output.Choices.Count; i++)
                {
                    var choiceData = Output.Choices[i];
                    list.Add(new DashScopeChoice
                    {
                        Index = i,
                        FinishReason = FinishReasonHelper.Parse(choiceData.FinishReason),
                        Message = choiceData.Message?.ToChatMessage(),
                        Delta = (choiceData.Delta ?? choiceData.Message)?.ToChatMessage(),
                        Logprobs = choiceData.Logprobs,
                    });
                }
                _messages = list;
            }
            return _messages;
        }
        set => _messages = value;
    }

    /// <summary>用量统计适配</summary>
    [IgnoreDataMember]
    private UsageDetails? _usageDetails;

    /// <summary>用量统计适配</summary>
    [IgnoreDataMember]
    UsageDetails? IChatResponse.Usage
    {
        get
        {
            if (_usageDetails == null && Usage != null)
                _usageDetails = Usage.ToUsageDetails();
            return _usageDetails;
        }
        set => _usageDetails = value;
    }

    /// <summary>首条回复文本</summary>
    [IgnoreDataMember]
    public String? Text
    {
        get
        {
            if (Output?.Choices == null || Output.Choices.Count == 0) return null;
            var msg = Output.Choices[0].Message ?? Output.Choices[0].Delta;
            return msg?.ToChatMessage()?.Content as String;
        }
    }
    #endregion

    #region 转换
    /// <summary>转换为内部统一 ChatResponse</summary>
    /// <param name="model">模型编码（DashScope 原生响应无顶级 model 字段，从请求回填）</param>
    /// <returns>等效的 ChatResponse 实例</returns>
    public ChatResponse ToChatResponse(String? model = null)
    {
        if (!String.IsNullOrEmpty(Code))
            throw new HttpRequestException($"[DashScope] 错误 {Code}: {Message}");

        var response = new ChatResponse
        {
            Object = "chat.completion",
            Id = RequestId,
            Model = model,
        };

        if (Output?.Choices != null)
        {
            var choices = new List<ChatChoice>(Output.Choices.Count);
            for (var i = 0; i < Output.Choices.Count; i++)
            {
                var choiceData = Output.Choices[i];
                choices.Add(new DashScopeChoice
                {
                    Index = i,
                    FinishReason = FinishReasonHelper.Parse(choiceData.FinishReason),
                    Message = choiceData.Message?.ToChatMessage(),
                    Logprobs = choiceData.Logprobs,
                });
            }
            response.Messages = choices;
        }

        if (Usage != null)
            response.Usage = Usage.ToUsageDetails();

        return response;
    }

    /// <summary>转换流式 chunk 为内部统一 ChatResponse</summary>
    /// <param name="model">模型编码</param>
    /// <returns>等效的 ChatResponse 流式 chunk</returns>
    public ChatResponse ToChunkResponse(String? model = null)
    {
        var response = new ChatResponse
        {
            Object = "chat.completion.chunk",
            Id = RequestId,
            Model = model,
        };

        if (Output?.Choices != null)
        {
            var choices = new List<ChatChoice>(Output.Choices.Count);
            for (var i = 0; i < Output.Choices.Count; i++)
            {
                var choiceData = Output.Choices[i];
                var choice = new DashScopeChoice
                {
                    Index = i,
                    FinishReason = FinishReasonHelper.Parse(choiceData.FinishReason),
                    Logprobs = choiceData.Logprobs,
                };
                // 流式 chunk 优先用 Delta，无 Delta 则降级用 Message
                var msgData = choiceData.Delta ?? choiceData.Message;
                choice.Delta = msgData?.ToChatMessage();
                choices.Add(choice);
            }
            response.Messages = choices;
        }

        if (Usage != null)
            response.Usage = Usage.ToUsageDetails();

        return response;
    }
    #endregion
}

/// <summary>DashScope 响应输出容器</summary>
public class DashScopeOutput
{
    /// <summary>选择项列表</summary>
    public IList<DashScopeChoiceData>? Choices { get; set; }
}

/// <summary>DashScope 响应选择项数据</summary>
public class DashScopeChoiceData
{
    /// <summary>结束原因。stop/length/tool_calls/null</summary>
    [DataMember(Name = "finish_reason")]
    public String? FinishReason { get; set; }

    /// <summary>消息内容（非流式）</summary>
    public DashScopeMessageData? Message { get; set; }

    /// <summary>增量消息内容（流式）</summary>
    public DashScopeMessageData? Delta { get; set; }

    /// <summary>对数概率信息。请求参数 logprobs=true 时返回</summary>
    public Object? Logprobs { get; set; }
}

/// <summary>DashScope 响应消息数据</summary>
public class DashScopeMessageData
{
    /// <summary>角色。user/assistant/tool</summary>
    public String? Role { get; set; }

    /// <summary>消息内容。纯文本时为字符串；多模态响应时可能为数组</summary>
    public Object? Content { get; set; }

    /// <summary>思考内容。Qwen 推理模型返回的推理过程（reasoning_content）</summary>
    [DataMember(Name = "reasoning_content")]
    public String? ReasoningContent { get; set; }

    /// <summary>工具调用列表</summary>
    [DataMember(Name = "tool_calls")]
    public IList<DashScopeResponseToolCall>? ToolCalls { get; set; }

    /// <summary>工具调用 ID（角色为 tool 时使用）</summary>
    [DataMember(Name = "tool_call_id")]
    public String? ToolCallId { get; set; }

    /// <summary>转换为内部统一 ChatMessage</summary>
    /// <returns>等效的 ChatMessage 实例</returns>
    public ChatMessage ToChatMessage()
    {
        var msg = new ChatMessage { Role = Role, ReasoningContent = ReasoningContent };

        // 多模态响应：content 为 [{text: "..."}] 数组，归一化为字符串
        if (Content is IList<Object> contentList)
        {
            var sb = Pool.StringBuilder.Get();
            foreach (var item in contentList)
            {
                if (item is IDictionary<String, Object> d && d.TryGetValue("text", out var t))
                    sb.Append(t);
            }
            msg.Content = sb.Return(true);
        }
        else
        {
            msg.Content = Content;
        }

        if (ToolCallId != null) msg.ToolCallId = ToolCallId;

        if (ToolCalls != null && ToolCalls.Count > 0)
        {
            var toolCalls = new List<ToolCall>(ToolCalls.Count);
            foreach (var tc in ToolCalls)
            {
                toolCalls.Add(new ToolCall
                {
                    Id = tc.Id ?? "",
                    Type = tc.Type ?? "function",
                    Function = tc.Function != null
                        ? new FunctionCall
                        {
                            Name = tc.Function.Name,
                            Arguments = tc.Function.Arguments ?? "{}",
                        }
                        : null,
                });
            }
            msg.ToolCalls = toolCalls;
        }

        return msg;
    }
}

/// <summary>DashScope 响应中的工具调用</summary>
public class DashScopeResponseToolCall
{
    /// <summary>工具调用编号</summary>
    public String? Id { get; set; }

    /// <summary>工具类型。固定 "function"</summary>
    public String? Type { get; set; }

    /// <summary>函数调用信息</summary>
    public DashScopeResponseFunction? Function { get; set; }
}

/// <summary>DashScope 响应工具调用函数信息</summary>
public class DashScopeResponseFunction
{
    /// <summary>函数名称</summary>
    public String? Name { get; set; }

    /// <summary>函数参数（JSON 字符串）</summary>
    public String? Arguments { get; set; }
}

/// <summary>DashScope 专属选择项。继承 <see cref="ChatChoice"/> 并扩展 logprobs 字段</summary>
public class DashScopeChoice : ChatChoice
{
    /// <summary>对数概率信息。当请求参数 logprobs=true 时返回，包含输出 Token 的概率分布</summary>
    public Object? Logprobs { get; set; }
}

/// <summary>DashScope 专属用量统计。继承 <see cref="UsageDetails"/> 并扩展多模态 Token 字段</summary>
public class DashScopeUsage : UsageDetails
{
    /// <summary>图像 Token 数。多模态请求中图像输入消耗的 Token 数</summary>
    public Int32 ImageTokens { get; set; }

    /// <summary>视频 Token 数。多模态请求中视频输入消耗的 Token 数</summary>
    public Int32 VideoTokens { get; set; }

    /// <summary>音频 Token 数。多模态请求中音频输入消耗的 Token 数</summary>
    public Int32 AudioTokens { get; set; }
}

/// <summary>DashScope 响应令牌用量统计</summary>
public class DashScopeUsageData
{
    /// <summary>输入令牌数</summary>
    [DataMember(Name = "input_tokens")]
    public Int32 InputTokens { get; set; }

    /// <summary>输出令牌数</summary>
    [DataMember(Name = "output_tokens")]
    public Int32 OutputTokens { get; set; }

    /// <summary>总令牌数</summary>
    [DataMember(Name = "total_tokens")]
    public Int32 TotalTokens { get; set; }

    /// <summary>图像 Token 数。多模态请求中图像消耗的 Token 数</summary>
    [DataMember(Name = "image_tokens")]
    public Int32 ImageTokens { get; set; }

    /// <summary>视频 Token 数</summary>
    [DataMember(Name = "video_tokens")]
    public Int32 VideoTokens { get; set; }

    /// <summary>音频 Token 数</summary>
    [DataMember(Name = "audio_tokens")]
    public Int32 AudioTokens { get; set; }

    /// <summary>转换为内部统一 UsageDetails</summary>
    /// <returns>等效的 UsageDetails 实例</returns>
    public UsageDetails ToUsageDetails() => new DashScopeUsage
    {
        InputTokens = InputTokens,
        OutputTokens = OutputTokens,
        TotalTokens = TotalTokens,
        ImageTokens = ImageTokens,
        VideoTokens = VideoTokens,
        AudioTokens = AudioTokens,
    };
}
