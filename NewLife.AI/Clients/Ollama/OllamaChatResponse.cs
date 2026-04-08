using System.Runtime.Serialization;
using NewLife.AI.Models;

namespace NewLife.AI.Clients.Ollama;

/// <summary>Ollama /api/chat 对话响应（非流式和流式共用结构），同时实现 IChatResponse 可直接作为统一响应</summary>
/// <remarks>
/// 非流式响应包含完整消息和统计信息（done=true）。
/// 流式响应每帧包含部分消息（done=false），最后一帧包含统计信息（done=true）。
/// </remarks>
public class OllamaChatResponse : IChatResponse
{
    /// <summary>模型名称</summary>
    public String? Model { get; set; }

    /// <summary>创建时间</summary>
    public String? CreatedAt { get; set; }

    /// <summary>消息对象</summary>
    public OllamaChatMessage? Message { get; set; }

    /// <summary>是否完成</summary>
    public Boolean Done { get; set; }

    /// <summary>完成原因</summary>
    public String? DoneReason { get; set; }

    /// <summary>总耗时（纳秒）</summary>
    public Int64 TotalDuration { get; set; }

    /// <summary>模型加载耗时（纳秒）</summary>
    public Int64 LoadDuration { get; set; }

    /// <summary>输入 token 数</summary>
    public Int32 PromptEvalCount { get; set; }

    /// <summary>输入评估耗时（纳秒）</summary>
    public Int64 PromptEvalDuration { get; set; }

    /// <summary>输出 token 数</summary>
    public Int32 EvalCount { get; set; }

    /// <summary>输出评估耗时（纳秒）</summary>
    public Int64 EvalDuration { get; set; }

    #region IChatResponse 适配
    /// <summary>响应标识。未由 Ollama 返回时自动生成</summary>
    [IgnoreDataMember]
    public String? Id
    {
        get => _id ??= CreatedAt != null ? $"ollama-{CreatedAt}" : $"ollama-{DateTime.UtcNow.Ticks}";
        set => _id = value;
    }
    private String? _id;

    /// <summary>对象类型。非流式为 chat.completion，流式为 chat.completion.chunk</summary>
    [IgnoreDataMember]
    String? IChatResponse.Object
    {
        get => _object ??= Done ? "chat.completion" : "chat.completion.chunk";
        set => _object = value;
    }
    private String? _object;

    /// <summary>创建时间适配。从 CreatedAt 解析或使用当前时间</summary>
    [IgnoreDataMember]
    DateTimeOffset IChatResponse.Created
    {
        get => CreatedAt != null && DateTimeOffset.TryParse(CreatedAt, out var dt) ? dt : DateTimeOffset.UtcNow;
        set => CreatedAt = value.ToString("O");
    }

    /// <summary>响应消息列表适配</summary>
    [IgnoreDataMember]
    private IList<ChatChoice>? _messages;

    /// <summary>消息列表适配</summary>
    [IgnoreDataMember]
    IList<ChatChoice>? IChatResponse.Messages
    {
        get
        {
            if (_messages == null && Message != null)
            {
                var msg = Message.ToChatMessage();
                var fr = FinishReasonHelper.Parse(DoneReason);
                if (fr == null && Done) fr = FinishReason.Stop;
                _messages = [new ChatChoice { Index = 0, Message = msg, Delta = msg, FinishReason = fr }];
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
            if (_usageDetails == null && (PromptEvalCount > 0 || EvalCount > 0))
            {
                _usageDetails = new UsageDetails
                {
                    InputTokens = PromptEvalCount,
                    OutputTokens = EvalCount,
                    TotalTokens = PromptEvalCount + EvalCount,
                };
            }
            return _usageDetails;
        }
        set => _usageDetails = value;
    }

    /// <summary>首条回复文本</summary>
    [IgnoreDataMember]
    public String? Text => Message?.Content as String;
    #endregion

    /// <summary>转换为通用 ChatResponse（非流式）</summary>
    /// <returns>通用对话响应</returns>
    public ChatResponse ToChatResponse()
    {
        var response = new ChatResponse
        {
            Id = CreatedAt != null ? $"ollama-{CreatedAt}" : $"ollama-{DateTime.UtcNow.Ticks}",
            Object = "chat.completion",
            Model = Model,
        };

        if (Message != null)
        {
            var msg = Message.ToChatMessage();
            response.Messages = [new ChatChoice { Index = 0, Message = msg, FinishReason = FinishReasonHelper.Parse(DoneReason) }];
        }

        if (PromptEvalCount > 0 || EvalCount > 0)
        {
            response.Usage = new UsageDetails
            {
                InputTokens = PromptEvalCount,
                OutputTokens = EvalCount,
                TotalTokens = PromptEvalCount + EvalCount,
            };
        }

        return response;
    }

    /// <summary>转换为通用 ChatResponse（流式 chunk）</summary>
    /// <returns>流式 chunk 响应，解析失败返回 null</returns>
    public ChatResponse? ToStreamChunk()
    {
        var chunk = new ChatResponse
        {
            Id = CreatedAt != null ? $"ollama-{CreatedAt}" : $"ollama-{DateTime.UtcNow.Ticks}",
            Object = "chat.completion.chunk",
            Model = Model,
        };

        FinishReason? finishReason = null;
        if (Done) finishReason = FinishReasonHelper.Parse(DoneReason) ?? FinishReason.Stop;

        if (Message != null)
        {
            var msg = Message.ToChatMessage();
            chunk.Messages = [new ChatChoice { Index = 0, Delta = msg, FinishReason = finishReason }];
        }
        else if (Done)
        {
            chunk.Messages = [new ChatChoice { Index = 0, Delta = new ChatMessage { Role = "assistant" }, FinishReason = finishReason }];
        }

        if (Done && (PromptEvalCount > 0 || EvalCount > 0))
        {
            chunk.Usage = new UsageDetails
            {
                InputTokens = PromptEvalCount,
                OutputTokens = EvalCount,
                TotalTokens = PromptEvalCount + EvalCount,
            };
        }

        return chunk;
    }
}
