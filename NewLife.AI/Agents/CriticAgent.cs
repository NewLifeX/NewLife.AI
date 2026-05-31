using NewLife.AI.Clients;

namespace NewLife.AI.Agents;

/// <summary>评审代理。专门对草稿回复进行批判性审查，输出评审意见或批准信号</summary>
/// <remarks>
/// 评审代理在反思循环（<see cref="ReflectionAgent"/>）中扮演"批评者"角色：
/// <list type="number">
/// <item>收到草稿回复后，按照 SystemPrompt 中定义的评审标准进行审核</item>
/// <item>若草稿满足标准，在回复末尾输出 <c>APPROVED</c> 信号（大写，独占一行或跟在句号后）</item>
/// <item>若草稿需要改进，给出具体修改意见，不输出 APPROVED</item>
/// </list>
/// <para>默认 SystemPrompt 要求评审者检查事实准确性、逻辑连贯性和回复完整性。
/// 可通过赋值 <see cref="ConversableAgent"/> 的 SystemPrompt 属性替换为领域专用评审标准。</para>
/// </remarks>
public class CriticAgent : ConversableAgent
{
    #region 常量

    /// <summary>批准信号。评审代理在回复中包含此字符串表示草稿已通过审核</summary>
    public const String ApprovalSignal = "APPROVED";

    #endregion

    #region 属性

    /// <summary>评审代理的默认系统提示词</summary>
    private const String DefaultCriticPrompt =
        "你是一位严格但公正的评审专家。你的任务是审核助手草稿回复的质量。\n\n" +
        "评审标准：\n" +
        "1. 事实准确性：内容是否符合已知事实，有无明显错误\n" +
        "2. 逻辑连贯性：论证是否清晰，推理是否合理\n" +
        "3. 回复完整性：是否充分回答了用户的问题\n" +
        "4. 表达清晰度：是否易于理解，有无歧义\n\n" +
        "若草稿质量达标，请在回复末尾单独一行输出：APPROVED\n" +
        "若需要改进，请给出具体、可操作的修改建议，不要输出 APPROVED。";

    #endregion

    #region 构造

    /// <summary>初始化评审代理</summary>
    /// <param name="name">代理名称</param>
    /// <param name="chatClient">底层 IChatClient</param>
    /// <param name="systemPrompt">自定义评审提示词；为 null 时使用默认评审提示词</param>
    public CriticAgent(String name, IChatClient chatClient, String? systemPrompt = null)
        : base(name, "评审代理：审核草稿质量，输出改进意见或 APPROVED 批准信号", chatClient, systemPrompt ?? DefaultCriticPrompt)
    {
    }

    #endregion

    #region 方法

    /// <summary>判断评审回复是否包含批准信号</summary>
    /// <param name="content">评审代理输出的文本内容</param>
    /// <returns>若内容包含 APPROVED（独立行或词）则返回 true</returns>
    public static Boolean IsApproved(String? content)
    {
        if (String.IsNullOrWhiteSpace(content)) return false;
        // 检查是否包含独立的 APPROVED 标记（整词匹配，防止误判如 "NOT APPROVED"）
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var trimmed = line.Trim(' ', '.', '!', '。', '！');
            if (String.Equals(trimmed, ApprovalSignal, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    #endregion
}
