using System.Runtime.Serialization;
using NewLife.AI.Models;
using NewLife.Data;

namespace NewLife.AI.Clients.DashScope;

/// <summary>阿里百炼 DashScope 原生协议请求体。兼容 https://help.aliyun.com/zh/model-studio/qwen-api-via-dashscope 协议，同时实现 IChatRequest 可直接作为统一请求传递</summary>
/// <remarks>
/// 与 OpenAI Chat Completions 的主要差异：
/// <list type="bullet">
/// <item>消息列表放在 input.messages 嵌套字段，而非顶级 messages</item>
/// <item>推理参数放在 parameters 嵌套字段，而非顶级</item>
/// <item>多模态内容块格式为 <c>[{"text":"..."}]</c>，纯文本请求为 <c>[{"type":"text","text":"..."}]</c></item>
/// <item>工具定义支持 function/mcp/web_search/code_interpreter 多种类型</item>
/// </list>
/// </remarks>
public class DashScopeRequest : IChatRequest
{
    #region 属性
    /// <summary>模型编码</summary>
    [DataMember(Name = "model")]
    public String? Model { get; set; }

    /// <summary>输入容器。包含 messages 列表</summary>
    [DataMember(Name = "input")]
    public DashScopeInput Input { get; set; } = new();

    /// <summary>推理参数</summary>
    [DataMember(Name = "parameters")]
    public DashScopeParameters Parameters { get; set; } = new();
    #endregion

    #region IChatRequest 适配
    /// <summary>消息列表适配。将 DashScopeMessage 转换为 ChatMessage</summary>
    [IgnoreDataMember]
    private IList<ChatMessage>? _chatMessages;

    /// <summary>消息列表适配</summary>
    [IgnoreDataMember]
    IList<ChatMessage> IChatRequest.Messages
    {
        get
        {
            if (_chatMessages == null)
            {
                var messages = new List<ChatMessage>();
                foreach (var msg in Input.Messages)
                {
                    var cm = new ChatMessage { Role = msg.Role, Content = msg.Content, Name = msg.Name, ToolCallId = msg.ToolCallId };
                    if (msg.ToolCalls?.Count > 0)
                    {
                        var toolCalls = new List<ToolCall>(msg.ToolCalls.Count);
                        foreach (var tc in msg.ToolCalls)
                        {
                            toolCalls.Add(new ToolCall
                            {
                                Id = tc.Id,
                                Type = tc.Type,
                                Function = tc.Function != null ? new FunctionCall { Name = tc.Function.Name ?? "", Arguments = tc.Function.Arguments } : null,
                            });
                        }
                        cm.ToolCalls = toolCalls;
                    }
                    messages.Add(cm);
                }
                _chatMessages = messages;
            }
            return _chatMessages;
        }
        set => _chatMessages = value;
    }

    /// <summary>是否流式输出</summary>
    [IgnoreDataMember]
    Boolean IChatRequest.Stream
    {
        get => Parameters.Stream == true;
        set => Parameters.Stream = value;
    }

    /// <summary>温度适配</summary>
    [IgnoreDataMember]
    Double? IChatRequest.Temperature
    {
        get => Parameters.Temperature;
        set => Parameters.Temperature = value;
    }

    /// <summary>核采样适配</summary>
    [IgnoreDataMember]
    Double? IChatRequest.TopP
    {
        get => Parameters.TopP;
        set => Parameters.TopP = value;
    }

    /// <summary>Top-K 采样适配</summary>
    [IgnoreDataMember]
    Int32? IChatRequest.TopK
    {
        get => Parameters.TopK;
        set => Parameters.TopK = value;
    }

    /// <summary>最大生成令牌数适配</summary>
    [IgnoreDataMember]
    Int32? IChatRequest.MaxTokens
    {
        get => Parameters.MaxTokens;
        set => Parameters.MaxTokens = value;
    }

    /// <summary>停止词列表适配</summary>
    [IgnoreDataMember]
    IList<String>? IChatRequest.Stop
    {
        get => Parameters.Stop;
        set => Parameters.Stop = value;
    }

    /// <summary>存在惩罚适配</summary>
    [IgnoreDataMember]
    Double? IChatRequest.PresencePenalty
    {
        get => Parameters.PresencePenalty;
        set => Parameters.PresencePenalty = value;
    }

    /// <summary>频率惩罚适配</summary>
    [IgnoreDataMember]
    Double? IChatRequest.FrequencyPenalty
    {
        get => Parameters.FrequencyPenalty;
        set => Parameters.FrequencyPenalty = value;
    }

    /// <summary>可用工具列表适配</summary>
    [IgnoreDataMember]
    IList<ChatTool>? IChatRequest.Tools { get; set; }

    /// <summary>工具选择策略适配</summary>
    [IgnoreDataMember]
    Object? IChatRequest.ToolChoice
    {
        get => Parameters.ToolChoice;
        set => Parameters.ToolChoice = value;
    }

    /// <summary>是否启用思考模式适配</summary>
    [IgnoreDataMember]
    Boolean? IChatRequest.EnableThinking
    {
        get => Parameters.EnableThinking;
        set => Parameters.EnableThinking = value;
    }

    /// <summary>响应格式适配</summary>
    [IgnoreDataMember]
    Object? IChatRequest.ResponseFormat
    {
        get => Parameters.ResponseFormat;
        set => Parameters.ResponseFormat = value;
    }

    /// <summary>是否允许并行工具调用适配</summary>
    [IgnoreDataMember]
    Boolean? IChatRequest.ParallelToolCalls
    {
        get => Parameters.ParallelToolCalls;
        set => Parameters.ParallelToolCalls = value;
    }

    /// <summary>用户标识</summary>
    [IgnoreDataMember]
    public String? User { get; set; }

    /// <summary>用户编号。内部管道传递</summary>
    [IgnoreDataMember]
    public String? UserId { get; set; }

    /// <summary>会话编号。内部管道传递</summary>
    [IgnoreDataMember]
    public String? ConversationId { get; set; }

    /// <summary>扩展数据</summary>
    [IgnoreDataMember]
    public IDictionary<String, Object?> Items { get; set; } = new Dictionary<String, Object?>();

    /// <summary>索引器</summary>
    [IgnoreDataMember]
    public Object? this[String key] { get => Items.TryGetValue(key, out var value) ? value : null; set => Items[key] = value; }
    #endregion

    #region 转换
    /// <summary>从内部统一 ChatRequest 构建 DashScope 协议请求</summary>
    /// <param name="request">内部统一请求</param>
    /// <param name="isMultimodal">是否为多模态模型。控制消息内容块格式</param>
    /// <returns>可直接 ToJson 序列化的 DashScope 协议请求</returns>
    public static DashScopeRequest FromChatRequest(IChatRequest request, Boolean isMultimodal = false)
    {
        var result = new DashScopeRequest
        {
            Model = request.Model ?? "",
        };

        result.Input.Messages = BuildMessages(request.Messages, isMultimodal);

        var p = result.Parameters;
        p.ResultFormat = "message";
        if (request.Temperature != null) p.Temperature = request.Temperature;
        if (request.TopP != null) p.TopP = request.TopP;
        if (request.TopK != null) p.TopK = request.TopK;
        if (request.MaxTokens != null) p.MaxTokens = request.MaxTokens;
        if (request.Stop != null && request.Stop.Count > 0) p.Stop = request.Stop;
        if (request.PresencePenalty != null) p.PresencePenalty = request.PresencePenalty;
        if (request.FrequencyPenalty != null) p.FrequencyPenalty = request.FrequencyPenalty;
        if (request.EnableThinking != null) p.EnableThinking = request.EnableThinking;
        if (request.ResponseFormat != null) p.ResponseFormat = request.ResponseFormat;
        if (request.Tools != null && request.Tools.Count > 0)
            p.Tools = BuildTools(request.Tools);
        if (request.ToolChoice != null) p.ToolChoice = request.ToolChoice;
        if (request.ParallelToolCalls != null) p.ParallelToolCalls = request.ParallelToolCalls;

        // DashScope 专属扩展参数
        var seed = request["Seed"] as Int32?;
        if (seed != null) p.Seed = seed;
        var repetitionPenalty = request["RepetitionPenalty"] as Double?;
        if (repetitionPenalty != null) p.RepetitionPenalty = repetitionPenalty;
        var n = request["N"] as Int32?;
        if (n != null) p.N = n;
        var thinkingBudget = request["ThinkingBudget"] as Int32?;
        if (thinkingBudget != null) p.ThinkingBudget = thinkingBudget;
        var enableCodeInterpreter = request["EnableCodeInterpreter"] as Boolean?;
        if (enableCodeInterpreter != null) p.EnableCodeInterpreter = enableCodeInterpreter;
        var logprobs = request["Logprobs"] as Boolean?;
        if (logprobs != null) p.Logprobs = logprobs;
        var topLogprobs = request["TopLogprobs"] as Int32?;
        if (topLogprobs != null) p.TopLogprobs = topLogprobs;
        var enableSearch = request["EnableSearch"] as Boolean?;
        if (enableSearch != null) p.EnableSearch = enableSearch;

        var searchOptions = new Dictionary<String, Object>();
        var searchStrategy = request["SearchStrategy"] as String;
        if (!String.IsNullOrEmpty(searchStrategy)) searchOptions["search_strategy"] = searchStrategy;
        var enableSource = request["EnableSource"] as Boolean?;
        if (enableSource != null) searchOptions["enable_source"] = enableSource.Value;
        var forcedSearch = request["ForcedSearch"] as Boolean?;
        if (forcedSearch != null) searchOptions["forced_search"] = forcedSearch.Value;
        if (searchOptions.Count > 0) p.SearchOptions = searchOptions;

        if (request.Stream)
        {
            p.Stream = true;
            p.IncrementalOutput = true;
        }
        else
        {
            // 显式传 stream=false，避免多模态端点默认进入流式模式
            p.Stream = false;
        }

        return result;
    }
    #endregion

    #region 辅助
    /// <summary>构建原生协议 messages 数组</summary>
    /// <param name="messages">内部消息列表</param>
    /// <param name="isMultimodal">是否多模态模型</param>
    internal static IList<DashScopeMessage> BuildMessages(IList<ChatMessage> messages, Boolean isMultimodal)
    {
        var result = new List<DashScopeMessage>(messages.Count);
        foreach (var msg in messages)
        {
            var m = new DashScopeMessage { Role = msg.Role };

            if (msg.Contents != null && msg.Contents.Count > 0)
                m.Content = BuildContent(msg.Contents, isMultimodal);
            else if (isMultimodal)
                m.Content = new List<Object> { new { text = msg.Content ?? "" } };
            else
                m.Content = msg.Content;

            if (msg.Name != null) m.Name = msg.Name;
            if (msg.ToolCallId != null) m.ToolCallId = msg.ToolCallId;

            if (msg.ToolCalls != null && msg.ToolCalls.Count > 0)
            {
                var toolCalls = new List<DashScopeToolCall>(msg.ToolCalls.Count);
                foreach (var tc in msg.ToolCalls)
                {
                    var tcObj = new DashScopeToolCall { Id = tc.Id, Type = tc.Type };
                    if (tc.Function != null)
                    {
                        tcObj.Function = new DashScopeToolCallFunction
                        {
                            Name = tc.Function.Name,
                            Arguments = String.IsNullOrEmpty(tc.Function.Arguments) ? "{}" : tc.Function.Arguments,
                        };
                    }
                    toolCalls.Add(tcObj);
                }
                m.ToolCalls = toolCalls;
            }

            result.Add(m);
        }
        return result;
    }

    /// <summary>构建多模态内容数组（DashScope 原生格式）</summary>
    /// <param name="contents">内容块列表</param>
    /// <param name="isMultimodal">是否多模态模型</param>
    internal static Object BuildContent(IList<AIContent> contents, Boolean isMultimodal)
    {
        if (!isMultimodal && contents.Count == 1 && contents[0] is TextContent singleText)
            return singleText.Text!;

        var parts = new List<Object>(contents.Count);
        foreach (var item in contents)
        {
            if (item is TextContent text)
            {
                parts.Add(isMultimodal
                    ? (Object)new { text = text.Text }
                    : new { type = "text", text = text.Text });
            }
            else if (item is ImageContent img)
            {
                String url;
                if (img.Data != null && img.Data.Length > 0)
                    url = $"data:{img.MediaType ?? "image/jpeg"};base64,{Convert.ToBase64String(img.Data)}";
                else
                    url = img.Uri ?? "";

                parts.Add(isMultimodal
                    ? (Object)new { image = url }
                    : new { type = "image_url", image_url = new { url } });
            }
        }
        return parts;
    }

    /// <summary>构建原生协议 tools 参数数组，支持 function/mcp/web_search/code_interpreter</summary>
    /// <param name="tools">工具列表</param>
    internal static IList<DashScopeTool> BuildTools(IList<ChatTool> tools)
    {
        var result = new List<DashScopeTool>(tools.Count);
        foreach (var tool in tools)
        {
            var t = new DashScopeTool { Type = tool.Type };

            if (tool.Type == "function" && tool.Function != null)
            {
                t.Function = new DashScopeToolFunction
                {
                    Name = tool.Function.Name,
                    Description = tool.Function.Description,
                    Parameters = tool.Function.Parameters,
                };
            }
            else if (tool.Type == "mcp" && tool.Mcp != null)
            {
                t.Mcp = new DashScopeMcpTool
                {
                    ServerUrl = tool.Mcp.ServerUrl,
                    ServerId = tool.Mcp.ServerId,
                    AllowedTools = tool.Mcp.AllowedTools,
                    Authorization = tool.Mcp.Authorization != null
                        ? new DashScopeToolAuthorization
                        {
                            Type = tool.Mcp.Authorization.Type,
                            Token = tool.Mcp.Authorization.Token,
                        }
                        : null,
                };
            }
            else if (tool.Config != null)
                t.Config = tool.Config;

            result.Add(t);
        }
        return result;
    }
    #endregion
}

/// <summary>DashScope 请求输入容器</summary>
public class DashScopeInput
{
    /// <summary>消息列表</summary>
    [DataMember(Name = "messages")]
    public IList<DashScopeMessage> Messages { get; set; } = [];
}

/// <summary>DashScope 请求推理参数</summary>
public class DashScopeParameters
{
    /// <summary>结果格式。固定 "message"</summary>
    [DataMember(Name = "result_format")]
    public String ResultFormat { get; set; } = "message";

    /// <summary>温度。0~2，控制随机性</summary>
    [DataMember(Name = "temperature")]
    public Double? Temperature { get; set; }

    /// <summary>核采样。0~1，与 Temperature 搭配使用</summary>
    [DataMember(Name = "top_p")]
    public Double? TopP { get; set; }

    /// <summary>Top-K 采样</summary>
    [DataMember(Name = "top_k")]
    public Int32? TopK { get; set; }

    /// <summary>最大生成令牌数</summary>
    [DataMember(Name = "max_tokens")]
    public Int32? MaxTokens { get; set; }

    /// <summary>停止序列</summary>
    [DataMember(Name = "stop")]
    public IList<String>? Stop { get; set; }

    /// <summary>话题新鲜度惩罚</summary>
    [DataMember(Name = "presence_penalty")]
    public Double? PresencePenalty { get; set; }

    /// <summary>频率惩罚</summary>
    [DataMember(Name = "frequency_penalty")]
    public Double? FrequencyPenalty { get; set; }

    /// <summary>是否启用深度思考</summary>
    [DataMember(Name = "enable_thinking")]
    public Boolean? EnableThinking { get; set; }

    /// <summary>响应格式。如 {"type":"json_object"}</summary>
    [DataMember(Name = "response_format")]
    public Object? ResponseFormat { get; set; }

    /// <summary>工具列表</summary>
    [DataMember(Name = "tools")]
    public IList<DashScopeTool>? Tools { get; set; }

    /// <summary>工具选择策略。"auto"/"none"/"required" 或指定工具名</summary>
    [DataMember(Name = "tool_choice")]
    public Object? ToolChoice { get; set; }

    /// <summary>是否并行工具调用</summary>
    [DataMember(Name = "parallel_tool_calls")]
    public Boolean? ParallelToolCalls { get; set; }

    /// <summary>是否流式输出</summary>
    [DataMember(Name = "stream")]
    public Boolean? Stream { get; set; }

    /// <summary>是否增量输出。流式时配合使用，每个 chunk 只含本次新增内容</summary>
    [DataMember(Name = "incremental_output")]
    public Boolean? IncrementalOutput { get; set; }

    // DashScope 专属参数

    /// <summary>随机种子。固定种子可在相同参数下复现输出</summary>
    [DataMember(Name = "seed")]
    public Int32? Seed { get; set; }

    /// <summary>重复惩罚。大于 1 则抑制已出现的 Token，默认 1.1</summary>
    [DataMember(Name = "repetition_penalty")]
    public Double? RepetitionPenalty { get; set; }

    /// <summary>返回候选数量。同一输入独立生成 N 条不同输出，默认 1</summary>
    [DataMember(Name = "n")]
    public Int32? N { get; set; }

    /// <summary>思考预算（Token 数）。0=关闭深度思考，-1=不限制</summary>
    [DataMember(Name = "thinking_budget")]
    public Int32? ThinkingBudget { get; set; }

    /// <summary>是否启用代码解释器</summary>
    [DataMember(Name = "enable_code_interpreter")]
    public Boolean? EnableCodeInterpreter { get; set; }

    /// <summary>是否返回对数概率</summary>
    [DataMember(Name = "logprobs")]
    public Boolean? Logprobs { get; set; }

    /// <summary>返回对数概率的 top-K Token 数。需同时设置 Logprobs=true</summary>
    [DataMember(Name = "top_logprobs")]
    public Int32? TopLogprobs { get; set; }

    /// <summary>是否启用联网搜索</summary>
    [DataMember(Name = "enable_search")]
    public Boolean? EnableSearch { get; set; }

    /// <summary>联网搜索选项。包含 search_strategy/enable_source/forced_search 等</summary>
    [DataMember(Name = "search_options")]
    public IDictionary<String, Object>? SearchOptions { get; set; }
}

/// <summary>DashScope 消息</summary>
public class DashScopeMessage
{
    /// <summary>角色。user/assistant/system/tool</summary>
    [DataMember(Name = "role")]
    public String Role { get; set; } = "";

    /// <summary>消息内容。纯文本时为字符串；多模态或多内容块时为对象数组</summary>
    [DataMember(Name = "content")]
    public Object? Content { get; set; }

    /// <summary>消息名称。可选，用于标识对话角色</summary>
    [DataMember(Name = "name")]
    public String? Name { get; set; }

    /// <summary>工具调用 ID。角色为 tool 时使用，标识响应哪个工具调用</summary>
    [DataMember(Name = "tool_call_id")]
    public String? ToolCallId { get; set; }

    /// <summary>工具调用列表。角色为 assistant 且有工具调用时填充</summary>
    [DataMember(Name = "tool_calls")]
    public IList<DashScopeToolCall>? ToolCalls { get; set; }
}

/// <summary>DashScope 工具定义</summary>
public class DashScopeTool
{
    /// <summary>工具类型。function/mcp/web_search/code_interpreter</summary>
    [DataMember(Name = "type")]
    public String? Type { get; set; }

    /// <summary>函数工具定义。type="function" 时使用</summary>
    [DataMember(Name = "function")]
    public DashScopeToolFunction? Function { get; set; }

    /// <summary>MCP 工具定义。type="mcp" 时使用</summary>
    [DataMember(Name = "mcp")]
    public DashScopeMcpTool? Mcp { get; set; }

    /// <summary>其他类型工具配置（如 web_search/code_interpreter）</summary>
    [DataMember(Name = "config")]
    public Object? Config { get; set; }
}

/// <summary>DashScope 函数工具定义</summary>
public class DashScopeToolFunction
{
    /// <summary>函数名称</summary>
    [DataMember(Name = "name")]
    public String? Name { get; set; }

    /// <summary>函数描述</summary>
    [DataMember(Name = "description")]
    public String? Description { get; set; }

    /// <summary>参数 JSON Schema</summary>
    [DataMember(Name = "parameters")]
    public Object? Parameters { get; set; }
}

/// <summary>DashScope MCP 工具定义</summary>
public class DashScopeMcpTool
{
    /// <summary>MCP 服务地址</summary>
    [DataMember(Name = "server_url")]
    public String? ServerUrl { get; set; }

    /// <summary>MCP 服务 ID</summary>
    [DataMember(Name = "server_id")]
    public String? ServerId { get; set; }

    /// <summary>允许调用的工具列表。为空则允许全部</summary>
    [DataMember(Name = "allowed_tools")]
    public IList<String>? AllowedTools { get; set; }

    /// <summary>授权配置</summary>
    [DataMember(Name = "authorization")]
    public DashScopeToolAuthorization? Authorization { get; set; }
}

/// <summary>DashScope 工具授权配置</summary>
public class DashScopeToolAuthorization
{
    /// <summary>授权类型</summary>
    [DataMember(Name = "type")]
    public String? Type { get; set; }

    /// <summary>授权 Token</summary>
    [DataMember(Name = "token")]
    public String? Token { get; set; }
}

/// <summary>DashScope 消息中的工具调用</summary>
public class DashScopeToolCall
{
    /// <summary>工具调用编号</summary>
    [DataMember(Name = "id")]
    public String? Id { get; set; }

    /// <summary>工具类型。固定 "function"</summary>
    [DataMember(Name = "type")]
    public String? Type { get; set; }

    /// <summary>函数调用信息</summary>
    [DataMember(Name = "function")]
    public DashScopeToolCallFunction? Function { get; set; }
}

/// <summary>DashScope 工具调用函数信息</summary>
public class DashScopeToolCallFunction
{
    /// <summary>函数名称</summary>
    [DataMember(Name = "name")]
    public String? Name { get; set; }

    /// <summary>函数参数（JSON 字符串）</summary>
    [DataMember(Name = "arguments")]
    public String? Arguments { get; set; }
}
