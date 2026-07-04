using NewLife.AI.Tools;
using NewLife.Collections;

namespace NewLife.ChatAI.Services;

/// <summary>工具上下文目录构建器。生成可供 <see cref="Handlers.ToolContextHandler"/> 注入到 system 消息的工具摘要文本</summary>
public class ToolDiscoveryProvider
{
    #region 方法
    /// <summary>判断是否应启用渐进式发现模式</summary>
    /// <param name="allProviders">所有工具提供者</param>
    /// <param name="threshold">工具数量阈值；小于等于 0 时始终禁用</param>
    /// <returns>工具总数超过阈值时返回 true</returns>
    public static Boolean ShouldAdvertise(IEnumerable<IToolProvider> allProviders, Int32 threshold)
    {
        if (threshold <= 0) return false;

        var count = 0;
        foreach (var p in allProviders)
        {
            count += p.GetTools().Count;
            if (count > threshold) return true;
        }

        return false;
    }

    /// <summary>构建工具目录提示词。列出所有（或排除已选中）工具的名称和描述摘要</summary>
    /// <param name="allProviders">所有工具提供者</param>
    /// <param name="excludeTools">要排除的工具名称（已选中工具）；为 null 时列出全部</param>
    /// <returns>工具目录文本，可注入系统提示词；没有可列工具时返回空字符串</returns>
    public static String BuildToolCatalog(IEnumerable<IToolProvider> allProviders, ISet<String>? excludeTools = null)
    {
        var sb = Pool.StringBuilder.Get();
        sb.AppendLine("你可以使用以下工具，需要时请在回复中用 @工具名 引用来激活工具：");
        //sb.AppendLine();

        var hasAny = false;
        foreach (var p in allProviders)
        {
            foreach (var tool in p.GetTools())
            {
                var func = tool.Function;
                if (func == null) continue;
                var name = func.Name;
                if (name.IsNullOrEmpty()) continue;

                if (excludeTools != null && excludeTools.Contains(name)) continue;

                var desc = func.Description;
                if (desc.IsNullOrEmpty()) desc = "无描述";

                // 截断过长描述
                if (desc.Length > 80) desc = desc[..80] + "...";

                // 注意：此处不输出参数名。带参数签名（如 @foo(a,b)）会被 LLM 视为可直接 tool_call 的函数定义，
                // 导致 AI 在工具 schema 尚未注入的情况下发起调用，参数残缺出错。纯名称+描述格式使 AI
                // 仅在文本中 @引用，由服务端在下一轮按需注入完整 schema。
                sb.AppendLine($"- @{name}：{desc}");
                hasAny = true;
            }
        }

        if (!hasAny)
        {
            Pool.StringBuilder.Return(sb);
            return String.Empty;
        }

        return sb.Return(true);
    }

    /// <summary>从工具目录中提取被引用的工具名称。解析消息文本中的 @ToolName 模式</summary>
    /// <param name="content">消息内容</param>
    /// <param name="availableTools">所有可用工具名称集合</param>
    /// <returns>被引用的工具名称集合</returns>
    public static ISet<String> ExtractReferencedTools(String? content, IEnumerable<String> availableTools)
    {
        var result = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
        if (content.IsNullOrEmpty()) return result;

        foreach (var toolName in availableTools)
        {
            if (content.Contains($"@{toolName}", StringComparison.OrdinalIgnoreCase))
                result.Add(toolName);
        }

        return result;
    }

    #endregion
}
