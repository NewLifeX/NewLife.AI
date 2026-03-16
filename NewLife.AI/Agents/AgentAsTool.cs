using NewLife.AI.Models;
using NewLife.AI.Tools;
using NewLife.Collections;

namespace NewLife.AI.Agents;

/// <summary>Agent 工具化桥接器。将 IAgent 注册为 ToolRegistry 中的函数调用工具</summary>
/// <remarks>
/// 使用场景：一个 Agent 需要调用另一个 Agent 的能力时，可将被调用 Agent 注册为工具。
/// <code>
/// var registry = new ToolRegistry();
/// AgentAsTool.Register(registry, translatorAgent);
/// // 注册后，其他 Agent 可通过工具调用来使用 translatorAgent 的能力
/// </code>
/// </remarks>
public static class AgentAsTool
{
    #region 方法

    /// <summary>将 Agent 注册到工具注册表。工具名称使用 Agent 名称（转换为 snake_case），描述使用 Agent 描述</summary>
    /// <param name="registry">目标工具注册表</param>
    /// <param name="agent">要注册的代理</param>
    /// <param name="toolName">自定义工具名称，为 null 时使用 Agent 名称</param>
    public static void Register(ToolRegistry registry, IAgent agent, String? toolName = null)
    {
        if (registry == null) throw new ArgumentNullException(nameof(registry));
        if (agent == null) throw new ArgumentNullException(nameof(agent));

        var name = toolName ?? ToSnakeCase(agent.Name);
        var description = agent.Description ?? $"调用 {agent.Name} 代理处理任务";

        registry.AddTool(name, (args, ct) => InvokeAgentAsync(agent, args, ct), description);
    }

    /// <summary>创建表示 Agent 能力的 ChatTool 定义（不注册到 ToolRegistry）</summary>
    /// <param name="agent">目标代理</param>
    /// <param name="toolName">自定义工具名称，为 null 时使用 Agent 名称</param>
    /// <returns>可直接添加到 ChatCompletionRequest.Tools 的 ChatTool</returns>
    public static ChatTool CreateToolDefinition(IAgent agent, String? toolName = null)
    {
        if (agent == null) throw new ArgumentNullException(nameof(agent));

        return new ChatTool
        {
            Function = new FunctionDefinition
            {
                Name = toolName ?? ToSnakeCase(agent.Name),
                Description = agent.Description ?? $"调用 {agent.Name} 代理处理任务",
                Parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        message = new
                        {
                            type = "string",
                            description = "发送给代理的消息内容"
                        }
                    },
                    required = new[] { "message" },
                },
            }
        };
    }

    #endregion

    #region 辅助

    /// <summary>调用 Agent 处理消息并收集文本结果</summary>
    private static async Task<String> InvokeAgentAsync(IAgent agent, String? arguments, CancellationToken cancellationToken)
    {
        // 解析输入消息
        var content = "请处理任务";
        if (!String.IsNullOrWhiteSpace(arguments))
        {
            // 简单 JSON 解析: {"message": "xxx"}
            try
            {
                var dic = NewLife.Serialization.JsonParser.Decode(arguments);
                if (dic != null && dic.TryGetValue("message", out var msgObj) && msgObj is String msg && !String.IsNullOrEmpty(msg))
                    content = msg;
            }
            catch
            {
                // 解析失败时直接使用原始参数作为消息
                content = arguments;
            }
        }

        var history = new List<AgentMessage>
        {
            new TextMessage { Source = "user", Role = "user", Content = content }
        };

        // 收集 Agent 响应文本
        var sb = Pool.StringBuilder.Get();
        await foreach (var msg in agent.HandleAsync(history, cancellationToken).ConfigureAwait(false))
        {
            if (msg is TextMessage textMsg && !String.IsNullOrEmpty(textMsg.Content))
            {
                if (sb.Length > 0) sb.AppendLine();
                sb.Append(textMsg.Content);
            }
        }

        return sb.Put(true);
    }

    /// <summary>转换为 snake_case</summary>
    private static String ToSnakeCase(String name)
    {
        if (String.IsNullOrEmpty(name)) return name;

        var sb = Pool.StringBuilder.Get();
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (Char.IsUpper(c))
            {
                if (i > 0) sb.Append('_');
                sb.Append(Char.ToLowerInvariant(c));
            }
            else if (c == ' ' || c == '-')
            {
                sb.Append('_');
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.Put(true);
    }

    #endregion
}
