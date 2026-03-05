using System.Runtime.CompilerServices;
using NewLife.AI.Providers;
using NewLife.Log;
using NewLife.Serialization;
using AiChatMessage = NewLife.AI.Models.ChatMessage;
using ChatCompletionRequest = NewLife.AI.Models.ChatCompletionRequest;
using ChatStreamEvent = NewLife.AI.Models.ChatStreamEvent;
using ChatTool = NewLife.AI.Models.ChatTool;
using ChatUsage = NewLife.AI.Models.ChatUsage;
using FunctionCall = NewLife.AI.Models.FunctionCall;
using FunctionDefinition = NewLife.AI.Models.FunctionDefinition;
using ToolCall = NewLife.AI.Models.ToolCall;

namespace NewLife.ChatAI.Services;

/// <summary>工具调用编排服务。处理模型返回的 tool_calls，执行 MCP/函数调用，将结果回传模型继续生成</summary>
/// <remarks>
/// 编排流程：
/// 1. 模型流式输出中检测 FinishReason == tool_calls
/// 2. 收集完整的 ToolCall 列表
/// 3. 逐个执行工具调用（通过 McpClientService）
/// 4. 将工具结果追加到消息列表，重新调用模型
/// 5. 重复上述过程直到模型输出 stop
/// </remarks>
public class ToolCallService
{
    #region 属性
    private readonly McpClientService _mcpClient;
    private readonly GatewayService _gateway;
    private readonly UsageService? _usageService;
    private readonly ILog _log;

    /// <summary>最大工具调用轮次，防止无限循环</summary>
    private const Int32 MaxToolCallRounds = 10;
    #endregion

    #region 构造
    /// <summary>实例化工具调用编排服务</summary>
    /// <param name="mcpClient">MCP 客户端服务</param>
    /// <param name="gateway">网关服务</param>
    /// <param name="usageService">用量统计服务</param>
    /// <param name="log">日志</param>
    public ToolCallService(McpClientService mcpClient, GatewayService gateway, UsageService? usageService, ILog log)
    {
        _mcpClient = mcpClient;
        _gateway = gateway;
        _usageService = usageService;
        _log = log;
    }
    #endregion

    #region 流式编排
    /// <summary>执行带工具调用的流式对话。支持 think–tool–think 交错链路</summary>
    /// <param name="messages">对话消息列表（含历史上下文）</param>
    /// <param name="modelConfig">模型配置</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>SSE 事件流</returns>
    public async IAsyncEnumerable<ChatStreamEvent> StreamWithToolsAsync(
        IList<AiChatMessage> messages,
        Entity.ModelConfig modelConfig,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var setting = ChatSetting.Current;
        var tools = BuildToolDefinitions();

        for (var round = 0; round < MaxToolCallRounds; round++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 构建请求
            var request = new ChatCompletionRequest
            {
                Model = modelConfig.Code,
                Messages = messages,
                Stream = true,
            };

            // 仅在启用函数调用且有可用工具时传入工具定义
            if (setting.EnableFunctionCalling && tools.Count > 0)
            {
                request.Tools = tools;
                request.ToolChoice = "auto";
            }

            // 流式调用模型
            var provider = _gateway.GetProvider(modelConfig);
            if (provider == null)
            {
                yield return ChatStreamEvent.ErrorEvent("MODEL_UNAVAILABLE", $"未找到服务商 '{modelConfig.GetEffectiveProvider()}'");
                yield break;
            }

            var options = GatewayService.BuildOptions(modelConfig);
            var contentBuilder = new System.Text.StringBuilder();
            var thinkingBuilder = new System.Text.StringBuilder();
            var toolCallCollector = new List<ToolCall>();
            String? finishReason = null;
            ChatUsage? usage = null;

            await foreach (var chunk in provider.ChatStreamAsync(request, options, cancellationToken).ConfigureAwait(false))
            {
                if (chunk.Usage != null) usage = chunk.Usage;

                var choice = chunk.Choices?.FirstOrDefault();
                if (choice == null) continue;

                finishReason = choice.FinishReason ?? finishReason;
                var delta = choice.Delta;
                if (delta == null) continue;

                // 思考内容
                if (!String.IsNullOrEmpty(delta.ReasoningContent))
                {
                    thinkingBuilder.Append(delta.ReasoningContent);
                    yield return ChatStreamEvent.ThinkingDelta(delta.ReasoningContent);
                }

                // 正文内容
                var text = delta.Content as String;
                if (!String.IsNullOrEmpty(text))
                {
                    contentBuilder.Append(text);
                    yield return ChatStreamEvent.ContentDelta(text);
                }

                // 收集工具调用
                if (delta.ToolCalls != null)
                {
                    foreach (var tc in delta.ToolCalls)
                    {
                        MergeToolCall(toolCallCollector, tc);
                    }
                }
            }

            // 思考完成
            if (thinkingBuilder.Length > 0)
            {
                yield return ChatStreamEvent.ThinkingDone(0);
                thinkingBuilder.Clear();
            }

            // 如果不是工具调用结束，则流程完成
            if (!finishReason.EqualIgnoreCase("tool_calls") || toolCallCollector.Count == 0)
            {
                if (usage != null)
                    yield return ChatStreamEvent.MessageDone(usage);

                yield break;
            }

            // 有工具调用需要执行
            // 将 assistant 的工具调用消息追加到上下文
            var assistantMsg = new AiChatMessage
            {
                Role = "assistant",
                Content = contentBuilder.Length > 0 ? contentBuilder.ToString() : null,
                ToolCalls = toolCallCollector.ToList(),
            };
            messages.Add(assistantMsg);

            // 逐个执行工具调用
            foreach (var toolCall in toolCallCollector)
            {
                var fn = toolCall.Function;
                if (fn == null) continue;

                yield return ChatStreamEvent.ToolCallStart(toolCall.Id, fn.Name, fn.Arguments);

                String? toolResult = null;
                var success = false;
                try
                {
                    toolResult = await ExecuteToolAsync(fn.Name, fn.Arguments, cancellationToken).ConfigureAwait(false);
                    success = true;
                }
                catch (Exception ex)
                {
                    toolResult = ex.Message;
                    _log?.Error("工具调用 '{0}' 失败: {1}", fn.Name, ex.Message);
                }

                // yield 不能放在 try-catch 中，移到外面
                if (success)
                    yield return ChatStreamEvent.ToolCallDone(toolCall.Id, toolResult, true);
                else
                    yield return ChatStreamEvent.ToolCallError(toolCall.Id, toolResult ?? "未知错误");

                // 将工具结果追加到消息列表
                messages.Add(new AiChatMessage
                {
                    Role = "tool",
                    ToolCallId = toolCall.Id,
                    Content = toolResult ?? String.Empty,
                });
            }

            // 清空收集器，进入下一轮模型调用
            toolCallCollector.Clear();
            contentBuilder.Clear();
        }

        // 超过最大轮次
        _log?.Warn("工具调用超过最大轮次 {0}", MaxToolCallRounds);
        yield return ChatStreamEvent.ErrorEvent("TOOL_CALL_FAILED", "工具调用轮次超出限制");
    }
    #endregion

    #region 工具执行
    /// <summary>执行单个工具调用。优先在 MCP 工具中查找，未找到则报错</summary>
    /// <param name="toolName">工具名称</param>
    /// <param name="argumentsJson">参数 JSON 字符串</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>工具执行结果</returns>
    private async Task<String> ExecuteToolAsync(String toolName, String? argumentsJson, CancellationToken cancellationToken)
    {
        // 在 MCP 工具中查找
        var allTools = _mcpClient.GetAllTools();
        var tool = allTools.FirstOrDefault(t => t.Name.EqualIgnoreCase(toolName));
        if (tool == null)
            throw new InvalidOperationException($"未找到工具 '{toolName}'");

        // 解析参数
        var arguments = new Dictionary<String, Object?>();
        if (!String.IsNullOrEmpty(argumentsJson))
        {
            var parsed = argumentsJson.ToJsonEntity<Dictionary<String, Object?>>();
            if (parsed != null) arguments = parsed;
        }

        var result = await _mcpClient.CallToolAsync(tool.ServerId, toolName, arguments, cancellationToken).ConfigureAwait(false);

        // 拼接所有内容项
        if (result.Content == null || result.Content.Count == 0)
            return String.Empty;

        return String.Join("\n", result.Content.Select(c => c.Text));
    }
    #endregion

    #region 辅助
    /// <summary>构建可用工具定义列表。从 MCP 工具转换为 ChatTool 格式</summary>
    /// <returns></returns>
    private IList<ChatTool> BuildToolDefinitions()
    {
        var setting = ChatSetting.Current;
        if (!setting.EnableFunctionCalling && !setting.EnableMcp)
            return [];

        var tools = new List<ChatTool>();
        var mcpTools = _mcpClient.GetAllTools();

        foreach (var t in mcpTools)
        {
            tools.Add(new ChatTool
            {
                Type = "function",
                Function = new FunctionDefinition
                {
                    Name = t.Name,
                    Description = t.Description,
                    Parameters = t.InputSchema,
                },
            });
        }

        return tools;
    }

    /// <summary>合并流式工具调用增量。OpenAI 流式协议中 tool_calls 分块到达</summary>
    /// <param name="collector">已收集的工具调用列表</param>
    /// <param name="delta">增量工具调用</param>
    private static void MergeToolCall(List<ToolCall> collector, ToolCall delta)
    {
        if (delta == null) return;

        var existing = collector.FirstOrDefault(t => t.Id == delta.Id);
        if (existing == null && !String.IsNullOrEmpty(delta.Id))
        {
            // 新工具调用
            collector.Add(new ToolCall
            {
                Id = delta.Id,
                Type = delta.Type,
                Function = new FunctionCall
                {
                    Name = delta.Function?.Name ?? String.Empty,
                    Arguments = delta.Function?.Arguments ?? String.Empty,
                },
            });
            return;
        }

        if (existing?.Function != null && delta.Function != null)
        {
            // 追加参数增量
            if (!String.IsNullOrEmpty(delta.Function.Name))
                existing.Function.Name += delta.Function.Name;
            if (!String.IsNullOrEmpty(delta.Function.Arguments))
                existing.Function.Arguments += delta.Function.Arguments;
        }
    }
    #endregion
}
