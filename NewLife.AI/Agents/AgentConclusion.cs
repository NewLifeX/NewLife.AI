using NewLife.Collections;

namespace NewLife.AI.Agents;

/// <summary>子代理结构化结论。子代理完成子任务后向主代理汇报时使用，保持主链上下文精简</summary>
/// <remarks>
/// 子代理结论汇聚协议：子代理只向主代理回传约 100 字的结构化摘要，主代理不直接接收完整执行轨迹。
/// 典型用途：并行代理 impact analysis，将主链上下文大幅压缩，消除错误重复。
/// 配合 <see cref="ParallelGroupChat.MaxWorkerConclusionLength"/> 使用，可在聚合前自动截断工作代理输出。
/// </remarks>
public class AgentConclusion
{
    /// <summary>结论摘要（建议不超过 100 字）</summary>
    public String Conclusion { get; set; } = String.Empty;

    /// <summary>置信度（0~1）。1 表示完全确定，0.5 表示不确定</summary>
    public Single Confidence { get; set; } = 1f;

    /// <summary>影响范围描述（可为空）</summary>
    public String? Impact { get; set; }

    /// <summary>格式化为给主代理汇报的简洁文本</summary>
    /// <returns>结构化摘要文本，供 <see cref="ParallelGroupChat"/> 构建聚合输入</returns>
    public String ToSummaryText()
    {
        var sb = Pool.StringBuilder.Get();
        sb.Append("结论：");
        sb.Append(Conclusion);
        if (Confidence < 1f)
            sb.Append($"（置信度 {Confidence:P0}）");
        if (!Impact.IsNullOrEmpty())
            sb.Append($"，影响范围：{Impact}");
        return sb.Return(true);
    }

    /// <summary>从完整文本中提取结构化结论（简单截断实现）</summary>
    /// <param name="fullText">完整响应文本</param>
    /// <param name="maxLength">摘要最大字符数，默认 300</param>
    public static AgentConclusion ExtractFrom(String fullText, Int32 maxLength = 300)
    {
        if (fullText.IsNullOrEmpty()) return new AgentConclusion { Conclusion = String.Empty };
        var conclusion = fullText.Length <= maxLength ? fullText : fullText.Substring(0, maxLength) + "...";
        return new AgentConclusion { Conclusion = conclusion };
    }
}
