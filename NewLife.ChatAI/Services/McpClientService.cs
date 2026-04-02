using System.Net.Http.Headers;
using NewLife.AI.ModelContextProtocol;
using NewLife.AI.Models;
using NewLife.AI.Tools;
using NewLife.ChatAI.Entity;
using NewLife.Log;
using NewLife.Serialization;

namespace NewLife.ChatAI.Services;

/// <summary>MCP 客户端服务。连接远程 MCP Server，发现工具并执行工具调用。实现 <see cref="IToolProvider"/> 以供 <c>ToolChatClient</c> 直接集成</summary>
/// <remarks>实例化 MCP 客户端服务</remarks>
/// <param name="httpClientFactory">HTTP 客户端工厂</param>
/// <param name="log">日志</param>
public class McpClientService(IHttpClientFactory httpClientFactory, ILog log) : IToolProvider
{
    #region 工具发现
    /// <summary>发现指定 MCP Server 的可用工具列表，并更新到数据库</summary>
    /// <param name="serverId">MCP 服务配置编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>发现的工具列表</returns>
    public async Task<IList<ToolDefinition>> DiscoverToolsAsync(Int32 serverId, CancellationToken cancellationToken = default)
    {
        var config = McpServerConfig.FindById(serverId);
        if (config == null) throw new ArgumentException($"MCP 服务配置 {serverId} 不存在");

        if (!config.Enable) throw new InvalidOperationException($"MCP 服务 '{config.Name}' 未启用");

        // 先初始化
        await InitializeAsync(config, cancellationToken).ConfigureAwait(false);

        // 发送 tools/list 请求
        var request = new JsonRpcRequest("2.0", "tools/list", null, 2);
        var response = await SendRequestAsync(config, request, cancellationToken).ConfigureAwait(false);

        if (response.Error != null)
        {
            var error = response.Error.ToJson().ToJsonEntity<JsonRpcError>();
            throw new InvalidOperationException($"工具发现失败: {error?.Message}");
        }

        // 解析工具列表
        var result = response.Result?.ToJson().ToJsonEntity<ToolListResult>();
        var tools = result?.Tools ?? [];

        // 更新数据库
        config.AvailableTools = tools.ToJson();
        config.Update();

        log?.Info("MCP Server '{0}' 发现 {1} 个工具", config.Name, tools.Count);

        return tools;
    }

    /// <summary>获取所有已启用 MCP Server 的工具列表</summary>
    /// <returns>工具列表，包含服务名称</returns>
    public IList<McpToolInfo> GetAllTools()
    {
        var list = new List<McpToolInfo>();
        var servers = McpServerConfig.FindAll();

        foreach (var server in servers)
        {
            if (!server.Enable) continue;
            if (server.AvailableTools.IsNullOrEmpty()) continue;

            var tools = server.AvailableTools.ToJsonEntity<IList<ToolDefinition>>();
            if (tools == null) continue;

            foreach (var tool in tools)
            {
                list.Add(new McpToolInfo
                {
                    ServerId = server.Id,
                    ServerName = server.Name,
                    Name = tool.Name,
                    Description = tool.Description,
                    InputSchema = tool.InputSchema,
                });
            }
        }

        return list;
    }

    /// <summary>实现 <see cref="IToolProvider.GetTools"/>。将已启用 MCP 工具转换为 <see cref="ChatTool"/> 列表</summary>
    /// <returns>工具定义列表，供注入 ChatCompletionRequest.Tools</returns>
    public IList<ChatTool> GetTools()
    {
        var mcpTools = GetAllTools();
        var tools = new List<ChatTool>(mcpTools.Count);
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
    #endregion

    #region 工具调用
    /// <summary>调用 MCP 工具</summary>
    /// <param name="serverId">MCP 服务编号</param>
    /// <param name="toolName">工具名称</param>
    /// <param name="arguments">调用参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>调用结果</returns>
    public async Task<ToolCallResult> CallToolAsync(Int32 serverId, String toolName, Dictionary<String, Object?> arguments, CancellationToken cancellationToken = default)
    {
        var config = McpServerConfig.FindById(serverId);
        if (config == null) throw new ArgumentException($"MCP 服务配置 {serverId} 不存在");

        if (!config.Enable) throw new InvalidOperationException($"MCP 服务 '{config.Name}' 未启用");

        var toolParams = new ToolCallParams(toolName, arguments, null);
        var request = new JsonRpcRequest("2.0", "tools/call", toolParams, 3);
        var response = await SendRequestAsync(config, request, cancellationToken).ConfigureAwait(false);

        if (response.Error != null)
        {
            var error = response.Error.ToJson().ToJsonEntity<JsonRpcError>();
            throw new InvalidOperationException($"工具调用失败: {error?.Message}");
        }

        var result = response.Result?.ToJson().ToJsonEntity<ToolCallResult>();
        return result ?? new ToolCallResult([], true);
    }

    /// <summary>实现 <see cref="IToolProvider.CallToolAsync"/>。按工具名在已启用服务中查找并调用</summary>
    /// <param name="toolName">工具名称</param>
    /// <param name="argumentsJson">参数 JSON 字符串</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>调用结果文本；工具未找到时抛 <see cref="KeyNotFoundException"/></returns>
    async Task<String> IToolProvider.CallToolAsync(String toolName, String? argumentsJson, CancellationToken cancellationToken)
    {
        var allTools = GetAllTools();
        var tool = allTools.FirstOrDefault(t => t.Name.EqualIgnoreCase(toolName));
        if (tool == null) throw new KeyNotFoundException($"MCP tool not found: '{toolName}'");

        var args = new Dictionary<String, Object?>();
        if (!argumentsJson.IsNullOrEmpty())
        {
            var parsed = argumentsJson.ToJsonEntity<Dictionary<String, Object?>>();
            if (parsed != null) args = parsed;
        }
        var result = await CallToolAsync(tool.ServerId, toolName, args, cancellationToken).ConfigureAwait(false);

        if (result.Content == null || result.Content.Count == 0) return String.Empty;
        return String.Join("\n", result.Content.Select(c => c.Text));
    }
    #endregion

    #region 辅助
    /// <summary>向 MCP Server 发送初始化请求</summary>
    /// <param name="config">服务配置</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    private async Task InitializeAsync(McpServerConfig config, CancellationToken cancellationToken)
    {
        var initParams = new InitializeParams("2025-06-18", new ClientInfo("NewLife.ChatAI", "1.0.0"));
        var request = new JsonRpcRequest("2.0", "initialize", initParams, 1);
        await SendRequestAsync(config, request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>发送 JSON-RPC 请求到 MCP Server</summary>
    /// <param name="config">服务配置</param>
    /// <param name="request">JSON-RPC 请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>JSON-RPC 响应</returns>
    private async Task<JsonRpcResponse> SendRequestAsync(McpServerConfig config, JsonRpcRequest request, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("McpClient");
        client.Timeout = TimeSpan.FromSeconds(30);

        // 设置认证
        if (!config.AuthToken.IsNullOrEmpty())
        {
            if (config.AuthType.EqualIgnoreCase("Bearer"))
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.AuthToken);
            else if (config.AuthType.EqualIgnoreCase("ApiKey"))
                client.DefaultRequestHeaders.Add("X-Api-Key", config.AuthToken);
        }

        var json = request.ToJson();
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        using var httpResponse = await client.PostAsync(config.Endpoint, content, cancellationToken).ConfigureAwait(false);

        httpResponse.EnsureSuccessStatusCode();

        var responseBody = await httpResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        var response = responseBody.ToJsonEntity<JsonRpcResponse>();
        if (response == null)
            throw new InvalidOperationException("MCP Server 返回了无效的 JSON-RPC 响应");

        return response;
    }
    #endregion
}

/// <summary>MCP 工具信息。包含工具所属的 MCP Server 信息</summary>
public class McpToolInfo
{
    /// <summary>MCP 服务编号</summary>
    public Int32 ServerId { get; set; }

    /// <summary>MCP 服务名称</summary>
    public String ServerName { get; set; } = null!;

    /// <summary>工具名称</summary>
    public String Name { get; set; } = null!;

    /// <summary>工具描述</summary>
    public String? Description { get; set; }

    /// <summary>输入参数 Schema</summary>
    public Object? InputSchema { get; set; }
}
