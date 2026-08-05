using NewLife.AI.Models;

namespace NewLife.AI.Clients.Ollama;

/// <summary>Ollama 生成响应</summary>
public class OllamaGenerateResponse
{
    /// <summary>模型名称</summary>
    public String? Model { get; set; }

    /// <summary>创建时间</summary>
    public String? CreatedAt { get; set; }

    /// <summary>响应文本</summary>
    public String? Response { get; set; }

    /// <summary>思考文本</summary>
    public String? Thinking { get; set; }

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

    /// <summary>从内部统一 ChatResponse 构建 Ollama 生成响应（非流式）。供网关等对外伪装 Ollama 协议的场景使用</summary>
    /// <param name="response">内部统一响应</param>
    /// <returns>Ollama 生成响应对象</returns>
    public static OllamaGenerateResponse From(ChatResponse response)
    {
        var result = new OllamaGenerateResponse
        {
            Model = response.Model,
            CreatedAt = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'"),
            Done = true,
            Response = response.Text,
        };

        // 完成原因映射：工具调用输出 tool_calls，其余为 stop
        var fr = response.Messages?.FirstOrDefault()?.FinishReason;
        if (fr == FinishReason.ToolCalls)
            result.DoneReason = "tool_calls";
        else
            result.DoneReason = "stop";

        var msg = response.Messages?.FirstOrDefault()?.Message;
        if (msg != null && !msg.ReasoningContent.IsNullOrEmpty())
            result.Thinking = msg.ReasoningContent;

        if (response.Usage != null)
        {
            result.PromptEvalCount = response.Usage.InputTokens;
            result.EvalCount = response.Usage.OutputTokens;
        }

        return result;
    }
}
