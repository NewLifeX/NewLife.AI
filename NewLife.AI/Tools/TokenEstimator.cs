using NewLife.AI.Models;
using NewLife.Serialization;

namespace NewLife.AI.Tools;

/// <summary>Token 估算与上下文预算帮助类。提供字符级启发式 Token 计数与按预算的内容截断</summary>
/// <remarks>
/// <para>估算策略：中文按1字/token，英文及其它字符按4字符/token。这是粗略估算，不同模型的分词器差异较大，
/// 保守估算宁可多估不冒险。多模态二进制内容（图片/音频/文档）按 base64 字符数/4 粗略估算。</para>
/// <para>单一事实源：<c>ToolChatClient</c> 工具循环守卫、<c>TokenBudgetFilter</c> 均复用本类，
/// 避免多处重复实现导致估算口径漂移。</para>
/// </remarks>
public static class TokenEstimator
{
    /// <summary>内容截断时保留的最少字符数。低于此长度不再截断</summary>
    const Int32 MinContentChars = 200;

    /// <summary>估算单条消息的 Token 数。含 Content/推理/工具调用/名称与多模态二进制内容</summary>
    /// <param name="message">对话消息</param>
    /// <returns>Token 估算值</returns>
    public static Int32 EstimateTokens(ChatMessage message)
    {
        var total = 1; // role

        if (message.Content is String text)
            total += EstimateTokens(text);
        else if (message.Content != null)
            total += EstimateTokens(message.Content.ToString());
        if (!message.ReasoningContent.IsNullOrEmpty())
            total += EstimateTokens(message.ReasoningContent);
        if (message.ToolCalls != null)
        {
            foreach (var tc in message.ToolCalls)
            {
                total += EstimateTokens(tc.Function?.Name);
                total += EstimateTokens(tc.Function?.Arguments);
            }
        }
        if (!message.ToolCallId.IsNullOrEmpty())
            total += 2;
        if (!message.Name.IsNullOrEmpty())
            total += EstimateTokens(message.Name);

        // 多模态二进制内容（图片/音频/文档）：按固定 token 估算（每图/每音频约 1-3K token），
        // 真实模型按张/时长计费而非字节比例——按 base64 字节线性估算（原 字节/4）会使 500KB 图估出
        // 17 万 token 直接击穿 128K 预算，视觉/音频会话首轮被守卫误杀（B-16/T-3）
        if (message.Contents != null)
        {
            foreach (var c in message.Contents)
            {
                if (c is ImageContent img && img.Data != null)
                    total += 1500;
                else if (c is AudioContent au && au.Data != null)
                    total += 1500;
                else if (c is DataContent dc)
                    total += dc.Data.Length > 0 ? 1500 : 0;
            }
        }
        return total;
    }

    /// <summary>估算消息列表的总 Token 数</summary>
    /// <param name="messages">消息列表</param>
    /// <returns>Token 估算值</returns>
    public static Int32 EstimateTokens(IList<ChatMessage> messages)
    {
        if (messages == null || messages.Count == 0) return 0;

        var total = 0;
        foreach (var msg in messages)
        {
            total += EstimateTokens(msg);
        }
        return total;
    }

    /// <summary>估算工具定义（schema）的 Token 数。工具参数 JSON、名称与描述均占用上下文窗口</summary>
    /// <param name="tools">工具定义列表</param>
    /// <returns>Token 估算值</returns>
    public static Int32 EstimateTokens(IList<ChatTool>? tools)
    {
        if (tools == null || tools.Count == 0) return 0;

        var total = 0;
        foreach (var t in tools)
        {
            total += 10; // 固定开销（type/name/description 等字段）
            total += EstimateTokens(t.Function?.Name);
            total += EstimateTokens(t.Function?.Description);
            total += EstimateTokens(t.Function?.Parameters?.ToJson());
        }
        return total;
    }

    /// <summary>估算单段文本的 Token 数（粗略：中文按1字/token，英文按4字符/token）</summary>
    /// <param name="text">文本内容</param>
    /// <returns>Token 估算值</returns>
    public static Int32 EstimateTokens(String? text)
    {
        if (text.IsNullOrEmpty()) return 0;

        var chineseCount = 0;
        var otherCount = 0;
        foreach (var ch in text)
        {
            if (ch >= 0x4E00 && ch <= 0x9FFF)
                chineseCount++;
            else
                otherCount++;
        }

        // 保守估算：中文按 1 字/token（qwen/gpt 等主流分词器约 1 字/token），宁可多估不冒险
        return (Int32)(chineseCount / 1.0 + otherCount / 4.0);
    }

    /// <summary>将消息列表内容截断到 Token 预算内。只截断超长 Content（不删除消息，保持 assistant-tool 配对完整），
    /// 从最早的消息开始按需缩短；全部消息均截到最短仍超预算时返回 false，调用方应中断循环</summary>
    /// <param name="messages">消息列表（Content 会被原地缩短）</param>
    /// <param name="maxTokens">Token 预算</param>
    /// <returns>截断后满足预算返回 true，否则返回 false</returns>
    public static Boolean TryTruncateToBudget(IList<ChatMessage> messages, Int32 maxTokens)
    {
        if (messages == null || messages.Count == 0) return true;
        if (EstimateTokens(messages) <= maxTokens) return true;

        // 从最早的消息开始，将超长 Content 按超额比例截短
        for (var i = 0; i < messages.Count; i++)
        {
            var msg = messages[i];
            if (msg.Content is not String text || text.Length <= MinContentChars) continue;

            var total = EstimateTokens(messages);
            if (total <= maxTokens) return true;

            // 按超额 Token 估算需截掉的字符数（每 token ≈ 4 字符，保守），保留至少 MinContentChars
            var excessChars = (total - maxTokens) * 4;
            var keepChars = Math.Max(MinContentChars, text.Length - excessChars);
            msg.Content = text[..keepChars] + "\n...(内容已因上下文窗口限制而截断)";
        }
        return EstimateTokens(messages) <= maxTokens;
    }
}
