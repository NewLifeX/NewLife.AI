using NewLife.AI.Models;

namespace NewLife.AI.Tools;

/// <summary>上下文预算帮助类。提供消息 Token 估算与内容截断，供 <see cref="ToolChatClient"/> 工具循环逐轮守卫复用</summary>
/// <remarks>
/// 工具循环内每轮追加工具结果后，消息列表可能超过请求级 Token 预算（<c>MaxInputTokens</c>）。
/// 与过滤器链（只作用于初始消息）不同，本类在工具循环内逐轮生效，负责把超限的工具结果内容截短，
/// 使对话得以继续而非直接中断。截断只缩短 Content、不删除消息，保持 assistant(tool_calls)→tool 配对完整，
/// 避免下一轮 LLM 调用因工具对缺失返回 400。
/// </remarks>
public static class ContextBudgetHelper
{
    /// <summary>内容截断时保留的最少字符数。低于此长度不再截断</summary>
    const Int32 MinContentChars = 200;

    /// <summary>估算消息列表的 Token 数（粗略：中文按1字/token，英文按4字符/token）。含多模态二进制内容粗略估算</summary>
    /// <param name="messages">消息列表</param>
    /// <returns>Token 估算值</returns>
    public static Int32 EstimateTokens(IList<ChatMessage> messages)
    {
        if (messages == null || messages.Count == 0) return 0;

        var total = 0;
        foreach (var msg in messages)
        {
            total += 1; // role
            if (msg.Content is String text)
                total += EstimateTokens(text);
            else if (msg.Content != null)
                total += EstimateTokens(msg.Content.ToString());
            if (!msg.ReasoningContent.IsNullOrEmpty())
                total += EstimateTokens(msg.ReasoningContent);
            if (msg.ToolCalls != null)
            {
                foreach (var tc in msg.ToolCalls)
                {
                    total += EstimateTokens(tc.Function?.Name);
                    total += EstimateTokens(tc.Function?.Arguments);
                }
            }
            if (!msg.ToolCallId.IsNullOrEmpty())
                total += 2;
            if (!msg.Name.IsNullOrEmpty())
                total += EstimateTokens(msg.Name);

            // 多模态二进制内容（图片/音频/文档）：视觉模型按图计 token，粗略按 base64 字符数/4 估算，
            // 避免 Contents 完全不计入导致窗口守卫对多模态请求失明
            if (msg.Contents != null)
            {
                foreach (var c in msg.Contents)
                {
                    if (c is ImageContent img && img.Data != null)
                        total += img.Data.Length * 4 / 3 / 4;
                    else if (c is AudioContent au && au.Data != null)
                        total += au.Data.Length * 4 / 3 / 4;
                    else if (c is DataContent dc)
                        total += dc.Data.Length * 4 / 3 / 4;
                }
            }
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
