namespace NewLife.AI.Agents;

/// <summary>反思代理。通过"起草→评审→修订"迭代循环提升回复质量</summary>
/// <remarks>
/// 反思循环流程（每次 <see cref="HandleAsync"/> 调用）：
/// <list type="number">
/// <item>调用 <see cref="Primary"/> 代理生成初始草稿</item>
/// <item>将草稿交给 <see cref="Critic"/> 代理评审</item>
/// <item>若评审返回 <see cref="CriticAgent.ApprovalSignal"/>，或已达到 <see cref="MaxIterations"/> 上限，结束迭代，产出当前草稿</item>
/// <item>否则将评审意见拼接到历史，重新调用 Primary 生成修订版，回到步骤 2</item>
/// </list>
/// <para>
/// 所有迭代过程消息（草稿 + 评审）仅在内部流转，最终仅将末轮草稿的 <see cref="TextMessage"/> 作为输出。
/// 使用 <see cref="EmitIterationMessages"/> = true 可将每轮中间消息也产出，便于调试与观测。
/// </para>
/// <example>
/// <code>
/// var primary = new ConversableAgent("Drafter", chatClient, "你是一位资深技术写作者");
/// var critic = new CriticAgent("Critic", reviewChatClient);
/// var agent = new ReflectionAgent(primary, critic) { MaxIterations = 3 };
///
/// var history = new List&lt;AgentMessage&gt;
/// {
///     new TextMessage { Source = "User", Role = "user", Content = "帮我解释量子纠缠" }
/// };
/// await foreach (var msg in agent.HandleAsync(history))
///     Console.WriteLine(msg);
/// </code>
/// </example>
/// </remarks>
public class ReflectionAgent : IAgent
{
    #region 属性

    /// <inheritdoc/>
    public String Name { get; }

    /// <inheritdoc/>
    public String? Description { get; }

    /// <summary>起草代理。负责生成初始回复及根据评审意见进行修订</summary>
    public IAgent Primary { get; }

    /// <summary>评审代理。负责审核草稿质量并给出改进意见或批准信号。
    /// 通常使用 <see cref="CriticAgent"/>；也可传入任何 <see cref="IAgent"/> 并配合 <see cref="IsApproved"/> 委托</summary>
    public IAgent Critic { get; }

    /// <summary>判断评审回复是否为批准信号的委托。默认使用 <see cref="CriticAgent.IsApproved"/>。
    /// 可替换为自定义逻辑（如关键词匹配、分数阈值）</summary>
    public Func<String?, Boolean> IsApproved { get; set; } = CriticAgent.IsApproved;

    /// <summary>最大迭代轮次（含首次起草）。默认 3，即最多生成 3 份草稿。
    /// 设为 1 时退化为无评审的单次调用</summary>
    public Int32 MaxIterations { get; set; } = 3;

    /// <summary>是否将每轮迭代的中间消息（草稿 + 评审）产出给调用方。
    /// 默认 false（仅产出最终草稿）；设为 true 时便于调试与可观测性</summary>
    public Boolean EmitIterationMessages { get; set; }

    #endregion

    #region 构造

    /// <summary>初始化反思代理</summary>
    /// <param name="primary">起草代理</param>
    /// <param name="critic">评审代理（通常为 <see cref="CriticAgent"/>）</param>
    /// <param name="name">代理名称；为 null 时默认使用 "Reflection:{primary.Name}"</param>
    public ReflectionAgent(IAgent primary, IAgent critic, String? name = null)
    {
        if (primary == null) throw new ArgumentNullException(nameof(primary));
        if (critic == null) throw new ArgumentNullException(nameof(critic));

        Primary = primary;
        Critic = critic;
        Name = name ?? $"Reflection:{primary.Name}";
        Description = $"反思代理：{primary.Description ?? primary.Name} + {critic.Description ?? critic.Name} 迭代评审";
    }

    #endregion

    #region 方法

    /// <summary>执行反思迭代循环，最终产出经过评审的草稿消息流</summary>
    /// <param name="history">完整消息历史（含 user 请求）</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async IAsyncEnumerable<AgentMessage> HandleAsync(
        IList<AgentMessage> history,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (history == null) throw new ArgumentNullException(nameof(history));

        // 工作副本：包含原始历史 + 本轮迭代累积的草稿/评审消息
        var workHistory = new List<AgentMessage>(history);

        TextMessage? lastDraft = null;
        var iterations = MaxIterations > 0 ? MaxIterations : 1;

        for (var i = 0; i < iterations; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // ── 步骤 1：起草 ───────────────────────────────────────────
            var draftMessages = new List<AgentMessage>();
            await foreach (var msg in Primary.HandleAsync(workHistory, cancellationToken).ConfigureAwait(false))
            {
                draftMessages.Add(msg);
                if (EmitIterationMessages)
                    yield return msg;
            }

            // 从本轮产出中找最后一条文本消息作为草稿
            TextMessage? draft = null;
            for (var j = draftMessages.Count - 1; j >= 0; j--)
            {
                if (draftMessages[j] is TextMessage tm)
                {
                    draft = tm;
                    break;
                }
            }

            if (draft != null)
                lastDraft = draft;

            // 非最后一轮且起草成功 → 进入评审
            if (draft != null && i < iterations - 1)
            {
                // 将草稿追加到工作历史（role = assistant）
                workHistory.Add(draft);

                // ── 步骤 2：评审 ──────────────────────────────────────
                var criticMessages = new List<AgentMessage>();
                await foreach (var msg in Critic.HandleAsync(workHistory, cancellationToken).ConfigureAwait(false))
                {
                    criticMessages.Add(msg);
                    if (EmitIterationMessages)
                        yield return msg;
                }

                // 提取评审文本
                String? criticContent = null;
                for (var j = criticMessages.Count - 1; j >= 0; j--)
                {
                    if (criticMessages[j] is TextMessage cm)
                    {
                        criticContent = cm.Content;
                        break;
                    }
                }

                // ── 步骤 3：判断是否已批准 ────────────────────────────
                if (IsApproved(criticContent))
                    break;

                // 将评审意见以 user 身份追加（模拟用户追问/反馈），驱动下一轮修订
                if (!String.IsNullOrWhiteSpace(criticContent))
                {
                    workHistory.Add(new TextMessage
                    {
                        Source = Critic.Name,
                        Role = "user",
                        Content = $"[评审意见] {criticContent}\n\n请根据以上意见修订你的回复。",
                    });
                }
            }
            else
            {
                // 最后一轮或无草稿，退出
                break;
            }
        }

        // 产出最终草稿（若 EmitIterationMessages 已流出则无需重复）
        if (!EmitIterationMessages && lastDraft != null)
            yield return lastDraft;
    }

    #endregion
}
