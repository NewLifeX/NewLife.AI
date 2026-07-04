using NewLife.AI.Embedding;
using NewLife.AI.Tools;
using NewLife.ChatAI.Tools;

namespace NewLife.ChatAI.Handlers;

/// <summary>工具上下文处理器。在 SkillActivationHandler 之后执行，负责按仓位上限完成工具选择，
/// 并将超出仓位的工具以纯文本目录形式注入 <see cref="IChatContext.SystemSegments"/></summary>
/// <remarks>
/// <para>仓位填充优先级（高 → 低）：</para>
/// <list type="number">
/// <item>SkillActivationHandler 已填入的技能引用工具（本处理器执行前已就绪）</item>
/// <item>用户消息中的 @工具名 显式引用</item>
/// <item>用户消息触发词命中（NativeTool + MCP）</item>
/// <item>AI 上一轮回复触发词命中</item>
/// <item>历史 tool_call 补全（防路由缺失）</item>
/// <item>系统工具（IsSystem=true）补满剩余仓位</item>
/// <item>向量语义排名：对剩余未选工具按用户消息余弦相似度排序，取 Top-N 填满剩余仓位</item>
/// </list>
/// <para>工具总数 ≤ <see cref="ChatSetting.ToolSlotLimit"/> 时全量加载，不注入目录。
/// 超出时将剩余工具以纯名称+描述格式注入目录（不含参数签名，防止 LLM 直接 tool_call）。</para>
/// </remarks>
/// <param name="toolProviders">工具提供者集合</param>
/// <param name="skillService">技能服务（可为 null）</param>
/// <param name="chatSetting">对话配置（读取 <see cref="ChatSetting.ToolSlotLimit"/>）</param>
/// <param name="localEmbedder">本地文本向量化器；不为 null 时启用向量语义排名补位</param>
[ChatHandlerOrder(Before = 100)]
public class ToolContextHandler(IEnumerable<IToolProvider> toolProviders, SkillService? skillService, ChatSetting chatSetting, ILocalTextEmbedder? localEmbedder = null) : ChatHandlerBase
{
    /// <inheritdoc/>
    public override ChatHandlerCapabilities Capabilities => ChatHandlerCapabilities.Before;

    /// <inheritdoc/>
    public override Task OnBefore(IChatContext context, CancellationToken cancellationToken)
    {
        // 网关透传模式：工具调用由客户端自行管理，服务端不进行工具选择与激活
        if (context.Source == ChatFlowSource.Gateway)
            return Task.CompletedTask;

        var providers = toolProviders.ToArray();
        var messages = context.ContextMessages;
        var lastUserContent = context.UserMessage?.Content;
        var lastAssistantContent = context.AssistantMessage?.Content;
        lastUserContent ??= messages.LastOrDefault(m => m.Role == "user")?.Content as String;
        lastAssistantContent ??= messages.LastOrDefault(m => m.Role == "assistant")?.Content as String;

        // @引用激活（最高优先级）：解析用户消息中的 @工具名，直接加入 SelectedTools
        var allToolNames = providers.SelectMany(p => p.GetTools(null) ?? [])
            .Select(t => t.Function?.Name)
            .OfType<String>();
        var atReferenced = ToolDiscoveryProvider.ExtractReferencedTools(lastUserContent, allToolNames);
        foreach (var n in atReferenced)
            context.SelectedTools.Add(n);

        // 触发词命中（NativeTool — 用户消息）
        if (skillService != null)
        {
            var matchedNative = skillService.MatchNativeToolNamesByContent(lastUserContent);
            foreach (var n in matchedNative)
                context.SelectedTools.Add(n);

            // 助手输出触发词命中（NativeTool — AI 上一轮回复）
            var matchedAssistant = skillService.MatchNativeToolNamesByAssistantContent(lastAssistantContent);
            foreach (var n in matchedAssistant)
            {
                context.SelectedTools.Add(n);
                // 向 system 注入激活提示，引导模型主动调用已激活的工具
                context.SystemSegments.Add($"[系统提示] 因 AI 上一轮回复触发词命中，工具 {n} 已激活，请在本轮按需调用。");
            }
        }

        // 触发词命中（MCP）
        foreach (var mcp in providers.OfType<McpClientService>())
        {
            var matchedMcp = mcp.MatchToolNamesByContent(lastUserContent);
            foreach (var n in matchedMcp)
                context.SelectedTools.Add(n);
        }

        // 历史工具调用补全：扫描上下文历史中所有已用的 tool_call，将其工具名自动加入 SelectedTools
        // 部分模型（DeepSeek / Qwen 等）会从历史 context 中重新调用已使用过的工具，
        // 若未重新注册到路由表则触发 "Tool not found" 异常；此处确保历史用过的工具始终可路由
        foreach (var m in messages)
        {
            if (m.Role != "assistant" || m.ToolCalls == null) continue;
            foreach (var tc in m.ToolCalls)
            {
                if (tc.Function?.Name is { } name)
                    context.SelectedTools.Add(name);
            }
        }

        // 获取全量工具列表（null=不过滤）用于总数判断
        var allTools = providers.SelectMany(p => p.GetTools(null) ?? []).ToList();
        var threshold = chatSetting.ToolSlotLimit;

        // 全量模式：工具总数未超过仓位（或阈值 ≤ 0 表示无限制），直接全量加入 SelectedTools，不注入目录
        if (threshold <= 0 || allTools.Count <= threshold)
        {
            foreach (var t in allTools)
            {
                if (t.Function?.Name is { } n)
                    context.SelectedTools.Add(n);
            }

            InjectToolGuidanceIfNeeded(context);
            return Task.CompletedTask;
        }

        // 渐进式仓位填充：高优先级工具已占位，用系统工具补满剩余仓位
        // GetTools(emptySet) 语义：系统工具（IsSystem=true）∪ emptySet = 仅系统工具
        var emptySet = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
        var systemToolNames = providers
            .SelectMany(p => p.GetTools(emptySet) ?? [])
            .Select(t => t.Function?.Name)
            .OfType<String>();
        var remaining = threshold - context.SelectedTools.Count;
        foreach (var sysName in systemToolNames)
        {
            if (remaining <= 0) break;
            if (context.SelectedTools.Add(sysName))
                remaining--;
        }

        // 向量语义排名补位：用本地向量化器对未选工具按与用户消息的余弦相似度降序排列，取 Top-remaining
        // 无 embedder 或用户消息为空时跳过，仓位留空（降级为纯文本目录曝光）
        if (remaining > 0 && localEmbedder != null && !lastUserContent.IsNullOrWhiteSpace())
        {
            var queryVec = localEmbedder.Embed(lastUserContent!);
            var ranked = allTools
                .Where(t => t.Function?.Name is { } n && !context.SelectedTools.Contains(n, StringComparer.OrdinalIgnoreCase))
                .Select(t =>
                {
                    var toolText = $"{t.Function!.Name}：{t.Function.Description ?? ""}".Trim();
                    var score = CosineSimilarity(queryVec, localEmbedder.Embed(toolText));
                    return (Name: t.Function!.Name!, Score: score);
                })
                .OrderByDescending(x => x.Score);

            foreach (var item in ranked)
            {
                if (remaining <= 0) break;
                if (context.SelectedTools.Add(item.Name))
                    remaining--;
        }
        }

        // 超出仓位的工具以纯文本目录注入 system（不含参数签名，防止 LLM 在无完整 schema 时直接 tool_call）
        var catalog = ToolDiscoveryProvider.BuildToolCatalog(providers, context.SelectedTools);
        if (!catalog.IsNullOrWhiteSpace())
            context.SystemSegments.Add(catalog);

        InjectToolGuidanceIfNeeded(context);
        return Task.CompletedTask;
    }

    #region 辅助

    /// <summary>当上下文中存在 search_table 或 query_sql 时，注入工具选择引导</summary>
    private static void InjectToolGuidanceIfNeeded(IChatContext context)
    {
        var hasDbTool = context.SelectedTools.Contains("search_table", StringComparer.OrdinalIgnoreCase) ||
                        context.SelectedTools.Contains("query_sql", StringComparer.OrdinalIgnoreCase);
        if (!hasDbTool) return;

        context.SystemSegments.Add("""
            ## 工具使用原则
            - 优先使用业务专用查询工具获取数据，它们已封装了表结构和业务逻辑
            - search_table 和 query_sql 是底层数据库工具，仅在需要探索不熟悉的表结构或执行专用工具无法覆盖的自定义查询时使用
            - 不要用 search_table + query_sql 替代已有的专用查询工具
            """);
    }

    /// <summary>计算两个向量的余弦相似度（-1 ~ 1，归一化向量返回 0 ~ 1）</summary>
    /// <param name="a">向量 a</param>
    /// <param name="b">向量 b</param>
    private static Double CosineSimilarity(Single[] a, Single[] b)
    {
        var len = Math.Min(a.Length, b.Length);
        Double dot = 0, normA = 0, normB = 0;
        for (var i = 0; i < len; i++)
        {
            dot += (Double)a[i] * b[i];
            normA += (Double)a[i] * a[i];
            normB += (Double)b[i] * b[i];
        }
        var denom = Math.Sqrt(normA) * Math.Sqrt(normB);
        if (denom < 1e-10) return 0;
        return dot / denom;
    }

    #endregion
}
