using System.Runtime.Serialization;
using NewLife.AI.Models;

namespace NewLife.AI.Clients.OpenAI;

/// <summary>OpenAI Chat Completion 响应。兼容 v1/chat/completions 和 v1/responses 协议</summary>
/// <remarks>
/// 与内部统一 <see cref="ChatResponse"/> 的主要差异：
/// <list type="bullet">
/// <item>属性名 Choices（对应协议 choices），而非 Messages</item>
/// <item>Usage 使用 prompt_tokens/completion_tokens 命名（OpenAI 标准），而非 input_tokens/output_tokens</item>
/// <item>流式块 object 为 chat.completion.chunk，使用 delta 而非 message</item>
/// </list>
/// </remarks>
public class ChatCompletionResponse
{
    #region 属性
    /// <summary>响应编号</summary>
    public String? Id { get; set; }

    /// <summary>对象类型。chat.completion 或 chat.completion.chunk</summary>
    public String? Object { get; set; }

    /// <summary>创建时间戳（Unix 秒）</summary>
    public Int64 Created { get; set; }

    /// <summary>模型编码</summary>
    public String? Model { get; set; }

    /// <summary>回复选择列表</summary>
    public IList<CompletionChoice>? Choices { get; set; }

    /// <summary>令牌用量统计</summary>
    public CompletionUsage? Usage { get; set; }
    #endregion

    #region 转换
    /// <summary>转换为内部统一 ChatResponse</summary>
    /// <returns>等效的 ChatResponse 实例</returns>
    public ChatResponse ToChatResponse()
    {
        var response = new ChatResponse
        {
            Id = Id,
            Object = Object,
            Created = Created > 0 ? DateTimeOffset.FromUnixTimeSeconds(Created) : DateTimeOffset.UtcNow,
            Model = Model,
        };

        if (Choices != null)
        {
            var choices = new List<ChatChoice>();
            foreach (var choice in Choices)
            {
                choices.Add(new ChatChoice
                {
                    Index = choice.Index,
                    Message = choice.Message,
                    Delta = choice.Delta,
                    FinishReason = choice.FinishReason,
                });
            }
            response.Messages = choices;
        }

        if (Usage != null)
        {
            response.Usage = new UsageDetails
            {
                InputTokens = Usage.PromptTokens,
                OutputTokens = Usage.CompletionTokens,
                TotalTokens = Usage.TotalTokens,
            };
        }

        return response;
    }

    /// <summary>从内部统一响应转换为 OpenAI 非流式响应</summary>
    /// <param name="response">内部统一响应</param>
    /// <returns>OpenAI 格式响应</returns>
    public static ChatCompletionResponse From(ChatResponse response)
    {
        var result = new ChatCompletionResponse
        {
            Id = response.Id,
            Object = response.Object ?? "chat.completion",
            Created = response.Created.ToUnixTimeSeconds(),
            Model = response.Model,
        };

        if (response.Messages != null)
        {
            var choices = new List<CompletionChoice>();
            foreach (var choice in response.Messages)
            {
                choices.Add(new CompletionChoice
                {
                    Index = choice.Index,
                    Message = choice.Message,
                    FinishReason = choice.FinishReason,
                });
            }
            result.Choices = choices;
        }

        if (response.Usage != null)
            result.Usage = CompletionUsage.From(response.Usage);

        return result;
    }

    /// <summary>从内部统一流式块转换为 OpenAI 流式响应块</summary>
    /// <param name="chunk">内部统一流式块</param>
    /// <returns>OpenAI 格式流式块</returns>
    public static ChatCompletionResponse FromChunk(ChatResponse chunk)
    {
        var result = new ChatCompletionResponse
        {
            Id = chunk.Id,
            Object = "chat.completion.chunk",
            Created = chunk.Created.ToUnixTimeSeconds(),
            Model = chunk.Model,
        };

        if (chunk.Messages != null)
        {
            var choices = new List<CompletionChoice>();
            foreach (var choice in chunk.Messages)
            {
                choices.Add(new CompletionChoice
                {
                    Index = choice.Index,
                    Delta = choice.Delta,
                    FinishReason = choice.FinishReason,
                });
            }
            result.Choices = choices;
        }

        if (chunk.Usage != null)
            result.Usage = CompletionUsage.From(chunk.Usage);

        return result;
    }
    #endregion
}

/// <summary>OpenAI 回复选择项</summary>
public class CompletionChoice
{
    /// <summary>序号</summary>
    public Int32 Index { get; set; }

    /// <summary>消息内容（非流式）</summary>
    public ChatMessage? Message { get; set; }

    /// <summary>增量内容（流式）</summary>
    public ChatMessage? Delta { get; set; }

    /// <summary>结束原因。stop/length/tool_calls/content_filter</summary>
    [DataMember(Name = "finish_reason")]
    public String? FinishReason { get; set; }
}

/// <summary>OpenAI 令牌用量统计。使用 prompt_tokens/completion_tokens 命名</summary>
public class CompletionUsage
{
    /// <summary>提示令牌数</summary>
    [DataMember(Name = "prompt_tokens")]
    public Int32 PromptTokens { get; set; }

    /// <summary>回复令牌数</summary>
    [DataMember(Name = "completion_tokens")]
    public Int32 CompletionTokens { get; set; }

    /// <summary>总令牌数</summary>
    [DataMember(Name = "total_tokens")]
    public Int32 TotalTokens { get; set; }

    /// <summary>从内部用量统计转换</summary>
    /// <param name="usage">内部用量统计</param>
    /// <returns>OpenAI 格式用量</returns>
    public static CompletionUsage From(UsageDetails usage) => new()
    {
        PromptTokens = usage.InputTokens,
        CompletionTokens = usage.OutputTokens,
        TotalTokens = usage.TotalTokens,
    };
}