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
}
