using System.Runtime.Serialization;
using NewLife.AI.Models;
using NewLife.Serialization;

namespace NewLife.AI.Clients.Anthropic;

/// <summary>Anthropic Messages API 请求体。兼容 https://docs.anthropic.com/en/api/messages 协议（snake_case 格式），同时实现 IChatRequest 可直接作为统一请求在管道中传递</summary>
/// <remarks>
/// 与 OpenAI Chat Completions 的主要差异：
/// <list type="bullet">
/// <item>system 作为独立的顶级字段，而非 messages 中的一条</item>
/// <item>停止词字段名为 stop_sequences（OpenAI 为 stop）</item>
/// <item>额外支持 top_k 采样参数</item>
/// <item>消息内容可以是字符串或内容块数组（text / image / tool_use / tool_result）</item>
/// </list>
/// </remarks>
public class AnthropicRequest : IChatRequest
{
    #region 属性
    /// <summary>模型编码</summary>
    public String? Model { get; set; }

    /// <summary>消息列表。role 为 user / assistant</summary>
    public IList<AnthropicMessage> Messages { get; set; } = [];

    /// <summary>系统提示词。Anthropic 将 system 作为独立顶级字段，转换时放入 messages 首条</summary>
    public String? System { get; set; }

    /// <summary>最大生成令牌数。Anthropic 中为必填字段</summary>
    public Int32? MaxTokens { get; set; }

    /// <summary>温度。0~1</summary>
    public Double? Temperature { get; set; }

    /// <summary>核采样。0~1，与 Temperature 二选一</summary>
    public Double? TopP { get; set; }

    /// <summary>Top-K 采样</summary>
    public Int32? TopK { get; set; }

    /// <summary>是否流式输出</summary>
    public Boolean Stream { get; set; }

    /// <summary>停止序列。对应 OpenAI 的 stop 字段</summary>
    public IList<String>? StopSequences { get; set; }

    /// <summary>可用工具列表。Anthropic 格式：name/description/input_schema</summary>
    public IList<Object>? Tools { get; set; }

    /// <summary>思考模式配置。EnableThinking=true 时输出 {type:"enabled",budget_tokens:N}，false 时 {type:"disabled"}；adaptive 模式经 Items["ThinkingMode"]="adaptive" 启用</summary>
    public AnthropicThinkingConfig? Thinking { get; set; }

    /// <summary>输出配置。adaptive 思考模式下控制推理深度（如 {"effort":"high"}），对应请求体 output_config</summary>
    public AnthropicOutputConfig? OutputConfig { get; set; }

    /// <summary>元数据。Anthropic 支持 metadata.user_id 用于滥用追踪与用量归属</summary>
    public IDictionary<String, Object>? Metadata { get; set; }
    #endregion

    #region IChatRequest 适配
    /// <summary>消息列表适配。合并 System 首条系统消息与 AnthropicMessage 列表转换为 ChatMessage</summary>
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
                if (!String.IsNullOrEmpty(System))
                    messages.Add(new ChatMessage { Role = "system", Content = System });
                foreach (var msg in Messages)
                    messages.Add(new ChatMessage { Role = msg.Role, Content = msg.Content });
                _chatMessages = messages;
            }
            return _chatMessages;
        }
        set => _chatMessages = value;
    }

    /// <summary>停止词列表适配。委托到 StopSequences</summary>
    [IgnoreDataMember]
    IList<String>? IChatRequest.Stop { get => StopSequences; set => StopSequences = value; }

    /// <summary>可用工具列表缓存。入站时从原生 Tools 数组惰性转换</summary>
    [IgnoreDataMember]
    private IList<ChatTool>? _chatTools;

    /// <summary>可用工具列表适配。入站时从原生 Tools（name/description/input_schema）惰性转换为 ChatTool（type/function），与 Ollama 的 _chatTools 模式一致</summary>
    [IgnoreDataMember]
    IList<ChatTool>? IChatRequest.Tools
    {
        get
        {
            // A-106：自动属性在入站时保持 null（IgnoreDataMember），工具定义在网关统一化 ToChatRequest 转换中丢失。
            // 与 Ollama 一致改为惰性转换：原生 Tools 数组 → ChatTool 列表，仅在无缓存且存在工具时构建。
            if (_chatTools == null && Tools != null && Tools.Count > 0)
            {
                var list = new List<ChatTool>(Tools.Count);
                foreach (var tool in Tools)
                {
                    // 复用 CollectionHelper.ToDictionary：统一处理 JsonElement（System.Text.Json，ASP.NET Core 入站）/
                    // Dictionary（NewLife SystemJson）/ POCO，JsonElement 递归转换且返回大小写不敏感字典。
                    // 工具元素必须是对象，String/数组等非对象形态跳过（ToDictionary 对基础类型抛 InvalidDataException）。
                    if (tool == null || tool is String || tool is System.Collections.IList) continue;
                    var dic = tool.ToDictionary();

                    var name = dic["name"] as String;
                    if (name.IsNullOrEmpty()) continue;

                    list.Add(new ChatTool
                    {
                        Type = "function",
                        Function = new FunctionDefinition
                        {
                            Name = name,
                            Description = dic.TryGetValue("description", out var desc) ? desc as String : null,
                            // Anthropic 的 input_schema 对应 OpenAI 的 parameters
                            Parameters = dic.TryGetValue("input_schema", out var schema) ? schema : null,
                        },
                    });
                }
                _chatTools = list;
            }
            return _chatTools;
        }
        set => _chatTools = value;
    }

    /// <summary>存在惩罚</summary>
    [IgnoreDataMember]
    public Double? PresencePenalty { get; set; }

    /// <summary>频率惩罚</summary>
    [IgnoreDataMember]
    public Double? FrequencyPenalty { get; set; }

    /// <summary>工具选择策略</summary>
    [IgnoreDataMember]
    public Object? ToolChoice { get; set; }

    /// <summary>用户标识</summary>
    [IgnoreDataMember]
    public String? User { get; set; }

    /// <summary>推理强度</summary>
    [IgnoreDataMember]
    public String? ReasoningEffort { get; set; }

    /// <summary>是否启用思考模式</summary>
    [IgnoreDataMember]
    public Boolean? EnableThinking { get; set; }

    /// <summary>响应格式</summary>
    [IgnoreDataMember]
    public Object? ResponseFormat { get; set; }

    /// <summary>是否允许并行工具调用</summary>
    [IgnoreDataMember]
    public Boolean? ParallelToolCalls { get; set; }

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
    /// <summary>从内部统一 ChatRequest 构建 Anthropic 协议请求</summary>
    /// <param name="request">内部统一请求</param>
    /// <returns>可直接 ToJson 序列化的 Anthropic 协议请求</returns>
    public static AnthropicRequest FromChatRequest(IChatRequest request)
    {
        var result = new AnthropicRequest
        {
            Model = request.Model ?? "",
            MaxTokens = request.MaxTokens ?? 4096, // Anthropic 中为必填项
            Temperature = request.Temperature,
            TopP = request.TopP,
            TopK = request.TopK,
            Stream = request.Stream,
        };

        if (request.Stop != null && request.Stop.Count > 0)
            result.StopSequences = request.Stop;

        // 用户标识 → metadata.user_id（Anthropic 用于滥用追踪与用量归属）
        if (!request.User.IsNullOrEmpty())
            result.Metadata = new Dictionary<String, Object> { ["user_id"] = request.User! };

        // 思考模式：EnableThinking → thinking: {type, budget_tokens}，或 adaptive（新模型前向兼容）
        // Anthropic 官方约束：
        // 1) enabled 模式 budget_tokens 最小 1024 且必须小于 max_tokens，此处自动 clamp + 兜底提升
        // 2) 思考开启时 temperature/top_k 与思考不兼容（老模型 400），top_p 仅允许 0.95~1，此处自动剥离/收敛
        // 3) 4.6+ 模型 enabled 已弃用、4.7+ 直接 400，经 Items["ThinkingMode"]="adaptive" 切换 adaptive + output_config.effort
        if (request.EnableThinking != null)
        {
            var thinkingMode = request["ThinkingMode"] as String;
            var isAdaptive = request.EnableThinking.Value && !thinkingMode.IsNullOrEmpty() && thinkingMode.EqualIgnoreCase("adaptive");
            if (isAdaptive)
            {
                var thinking = new AnthropicThinkingConfig { Type = "adaptive" };
                // display：summarized=返回思考摘要，omitted=仅返回签名（新模型默认，缩短流式 TTFT）
                var display = request["ThinkingDisplay"] as String;
                if (!display.IsNullOrEmpty()) thinking.Display = display;
                result.Thinking = thinking;
                // ReasoningEffort → output_config.effort（adaptive 模式控制整体推理深度）
                if (!request.ReasoningEffort.IsNullOrEmpty())
                    result.OutputConfig = new AnthropicOutputConfig { Effort = request.ReasoningEffort };
            }
            else
            {
                var thinking = new AnthropicThinkingConfig { Type = request.EnableThinking.Value ? "enabled" : "disabled" };
                if (request.EnableThinking.Value)
                {
                    var budget = request["ThinkingBudget"] as Int32? ?? 1024;
                    // 官方约束：budget_tokens 最小 1024，小于该值 API 拒绝
                    if (budget < 1024) budget = 1024;
                    thinking.BudgetTokens = budget;
                    if (result.MaxTokens == null || result.MaxTokens.Value <= budget)
                        result.MaxTokens = budget + 2048;

                    // 思考开启时剥离冲突采样参数：temperature/top_k 与思考不兼容（老模型 400），top_p 收敛到 0.95~1
                    result.Temperature = null;
                    result.TopK = null;
                    if (result.TopP != null)
                    {
                        if (result.TopP.Value < 0.95) result.TopP = 0.95;
                        if (result.TopP.Value > 1.0) result.TopP = 1.0;
                    }
                }
                result.Thinking = thinking;
            }
        }

        // 分离 system 消息和普通消息
        var messages = new List<AnthropicMessage>();
        foreach (var msg in request.Messages)
        {
            if (msg.Role.Equals("system", StringComparison.OrdinalIgnoreCase))
            {
                // 多条 system 消息合并（调用方可能分段注入），避免静默覆盖
                var text = msg.Content?.ToString();
                if (!String.IsNullOrEmpty(text))
                    result.System = String.IsNullOrEmpty(result.System) ? text : result.System + "\n\n" + text;
                continue;
            }

            var role = msg.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase) ? "assistant" : "user";
            var am = new AnthropicMessage { Role = role };

            // assistant 消息：回传思考块（含签名）与 redacted_thinking 块。
            // Anthropic 官方约束：工具轮次必须将 thinking 块（含签名）原样回传，否则 API 返回 400；
            // 普通多轮建议回传（新模型自动裁剪不需要的旧思考块，无需自行清理）。
            List<Object>? contentBlocks = null;
            if (role == "assistant")
            {
                var thinkingText = msg.ReasoningContent;
                var signature = msg["Signature"] as String;
                var redacted = (msg["RedactedThinking"] as IList<String>)
                    ?? (msg["RedactedThinking"] as IList<Object>)?.Select(x => x + "").ToList();
                var hasThinking = !thinkingText.IsNullOrEmpty();
                var hasRedacted = redacted is { Count: > 0 };
                if (hasThinking || hasRedacted)
                {
                    contentBlocks = [];
                    // redacted_thinking 块：加密数据原样回传，顺序保持
                    if (hasRedacted)
                    {
                        foreach (var data in redacted!)
                            contentBlocks.Add(new Dictionary<String, Object> { ["type"] = "redacted_thinking", ["data"] = data });
                    }
                    // thinking 块需携带签名（API 校验完整性）；无签名时降级为纯文本块（普通多轮可接受）
                    if (hasThinking)
                    {
                        var thinkingBlock = new Dictionary<String, Object?> { ["type"] = "thinking", ["thinking"] = thinkingText };
                        if (!signature.IsNullOrEmpty()) thinkingBlock["signature"] = signature;
                        contentBlocks.Add(thinkingBlock);
                    }
                }
            }

            if (msg.ToolCallId != null)
            {
                // 工具结果消息 → tool_result 内容块
                am.Role = "user";
                am.Content = new List<Object>
                {
                    new Dictionary<String, Object?>
                    {
                        ["type"] = "tool_result",
                        ["tool_use_id"] = msg.ToolCallId,
                        ["content"] = msg.Content?.ToString() ?? "",
                    }
                };
            }
            else if (msg.ToolCalls != null && msg.ToolCalls.Count > 0)
            {
                // assistant 工具调用 → tool_use 内容块（思考块位于 text 之前，保持官方块顺序）
                contentBlocks ??= [];
                if (msg.Content != null)
                    contentBlocks.Add(new Dictionary<String, Object> { ["type"] = "text", ["text"] = msg.Content.ToString()! });
                foreach (var tc in msg.ToolCalls)
                {
                    Object input = tc.Function?.Arguments != null
                        ? (JsonParser.Decode(tc.Function.Arguments) ?? new Dictionary<String, Object?>())
                        : new Dictionary<String, Object?>();
                    contentBlocks.Add(new Dictionary<String, Object?>
                    {
                        ["type"] = "tool_use",
                        ["id"] = tc.Id,
                        ["name"] = tc.Function?.Name ?? "",
                        ["input"] = input,
                    });
                }
                am.Content = contentBlocks;
            }
            else
            {
                // 普通文本消息；含思考块时使用内容块数组（thinking + text）
                if (contentBlocks != null)
                {
                    if (msg.Content != null)
                        contentBlocks.Add(new Dictionary<String, Object> { ["type"] = "text", ["text"] = msg.Content.ToString()! });
                    am.Content = contentBlocks;
                }
                else
                {
                    // 多模态图片输入：Anthropic 通过 image 内容块接收 base64 / URL 图片
                    var imageBlocks = BuildImageBlocks(msg);
                    if (imageBlocks != null)
                        am.Content = imageBlocks;
                    else
                        am.Content = msg.Content;
                }
            }

            messages.Add(am);
        }
        result.Messages = messages;

        // 转换工具定义：OpenAI function → Anthropic tool（name/description/input_schema）
        if (request.Tools != null && request.Tools.Count > 0)
        {
            var tools = new List<Object>();
            foreach (var tool in request.Tools)
            {
                if (tool.Function == null) continue;
                tools.Add(new Dictionary<String, Object?>
                {
                    ["name"] = tool.Function.Name,
                    ["description"] = tool.Function.Description,
                    ["input_schema"] = tool.Function.Parameters ?? new Dictionary<String, Object> { ["type"] = "object" },
                });
            }
            result.Tools = tools;
        }

        return result;
    }

    /// <summary>构建 Anthropic 图片内容块。Contents 中含图片时返回 [text? + image] 块列表，否则返回 null</summary>
    /// <param name="msg">待转换消息</param>
    /// <returns>内容块列表，无图片时返回 null</returns>
    private static List<Object>? BuildImageBlocks(ChatMessage msg)
    {
        if (msg.Contents is not { Count: > 0 }) return null;

        var blocks = new List<Object>();
        var hasImage = false;
        foreach (var item in msg.Contents)
        {
            if (item is TextContent text)
            {
                if (!String.IsNullOrEmpty(text.Text))
                    blocks.Add(new Dictionary<String, Object> { ["type"] = "text", ["text"] = text.Text });
            }
            else if (item is ImageContent img)
            {
                hasImage = true;
                var data = img.Data;
                var b64 = data is { Length: > 0 } ? Convert.ToBase64String(data) : AIContentHelper.ParseDataUri(img.Uri);
                if (b64 != null)
                {
                    blocks.Add(new Dictionary<String, Object>
                    {
                        ["type"] = "image",
                        ["source"] = new Dictionary<String, Object>
                        {
                            ["type"] = "base64",
                            ["media_type"] = img.MediaType ?? "image/jpeg",
                            ["data"] = b64,
                        }
                    });
                }
                else if (!String.IsNullOrEmpty(img.Uri))
                {
                    blocks.Add(new Dictionary<String, Object>
                    {
                        ["type"] = "image",
                        ["source"] = new Dictionary<String, Object>
                        {
                            ["type"] = "url",
                            ["url"] = img.Uri!,
                        }
                    });
                }
            }
        }
        return hasImage ? blocks : null;
    }

    /// <summary>转换为内部统一的 ChatRequest。从内容块数组恢复 tool_use/thinking/tool_result/redacted_thinking（与 FromChatRequest 对称）</summary>
    /// <returns>等效的 ChatRequest 实例</returns>
    public ChatRequest ToChatRequest()
    {
        var messages = new List<ChatMessage>();

        // 将顶级 system 字段转为首条系统消息
        if (!String.IsNullOrEmpty(System))
            messages.Add(new ChatMessage { Role = "system", Content = System });

        foreach (var msg in Messages)
        {
            var cm = new ChatMessage { Role = msg.Role };

            // 内容块数组（text/thinking/redacted_thinking/tool_use/tool_result）逐块恢复；纯字符串内容直接透传
            if (msg.Content is IList<Object> blocks)
            {
                var textParts = new List<String>();
                var reasoningParts = new List<String>();
                String? signature = null;
                var toolCalls = new List<ToolCall>();
                String? toolResultId = null;

                foreach (var block in blocks)
                {
                    // 内容块为反序列化后的字典（Dictionary<String,Object> 或 Dictionary<String,Object?>，
                    // 可空标注编译期擦除运行时同类型，统一按 IDictionary<String,Object?> 匹配）
                    if (block is not IDictionary<String, Object?> dic) continue;
                    var type = dic["type"] as String;
                    switch (type)
                    {
                        case "text":
                            if (dic.TryGetValue("text", out var t) && t != null) textParts.Add(t + "");
                            break;
                        case "thinking":
                            if (dic.TryGetValue("thinking", out var th) && th != null) reasoningParts.Add(th + "");
                            if (dic.TryGetValue("signature", out var sig) && sig != null) signature = sig + "";
                            break;
                        case "redacted_thinking":
                            // 加密数据无法还原原文，提取 data 存回 Items 供原样回传
                            if (dic.TryGetValue("data", out var data) && data != null)
                            {
                                cm["RedactedThinking"] ??= new List<String>();
                                (cm["RedactedThinking"] as List<String>)!.Add(data + "");
                            }
                            break;
                        case "tool_use":
                            toolCalls.Add(new ToolCall
                            {
                                Id = dic.TryGetValue("id", out var id) ? id + "" : "",
                                Type = "function",
                                Function = new FunctionCall
                                {
                                    Name = dic.TryGetValue("name", out var n) ? n + "" : "",
                                    Arguments = dic.TryGetValue("input", out var input) && input != null ? input.ToJson() : null,
                                },
                            });
                            break;
                        case "tool_result":
                            toolResultId = dic.TryGetValue("tool_use_id", out var tid) ? tid + "" : null;
                            if (dic.TryGetValue("content", out var content) && content != null)
                                textParts.Add(content + "");
                            break;
                    }
                }

                // 回填恢复的字段（仅非空时写入，保持与原始消息等效）
                if (signature != null) cm["Signature"] = signature;
                if (toolResultId != null) cm.ToolCallId = toolResultId;
                if (toolCalls.Count > 0) cm.ToolCalls = toolCalls;
                var reasoning = String.Join("", reasoningParts);
                if (!reasoning.IsNullOrEmpty()) cm.ReasoningContent = reasoning;
                var text = String.Join("", textParts);
                if (!text.IsNullOrEmpty()) cm.Content = text;
            }
            else
            {
                cm.Content = msg.Content;
            }

            messages.Add(cm);
        }

        return new ChatRequest
        {
            Model = Model,
            Messages = messages,
            MaxTokens = MaxTokens,
            Temperature = Temperature,
            TopP = TopP,
            TopK = TopK,
            Stream = Stream,
            Stop = StopSequences,
            PresencePenalty = PresencePenalty,
            FrequencyPenalty = FrequencyPenalty,
            EnableThinking = EnableThinking,
            ResponseFormat = ResponseFormat,
            ToolChoice = ToolChoice,
            ParallelToolCalls = ParallelToolCalls,
            Tools = ((IChatRequest)this).Tools,
            User = User,
            UserId = UserId,
            ConversationId = ConversationId,
            Items = Items,
        };
    }
    #endregion
}

/// <summary>Anthropic 思考模式配置。对应请求体 thinking 字段</summary>
/// <remarks>
/// <code>
/// {
///   "thinking": { "type": "enabled", "budget_tokens": 1024 }
/// }
/// </code>
/// </remarks>
public class AnthropicThinkingConfig
{
    /// <summary>思考模式。enabled / disabled / adaptive（新模型前向兼容）</summary>
    public String Type { get; set; } = "enabled";

    /// <summary>思考预算（Token 数）。仅 enabled 时有效，需小于 max_tokens 且最小 1024</summary>
    public Int32? BudgetTokens { get; set; }

    /// <summary>思考展示方式。summarized=返回思考摘要，omitted=仅返回签名（新模型默认，缩短流式 TTFT）；仅 adaptive 模式有效</summary>
    public String? Display { get; set; }
}

/// <summary>Anthropic 输出配置。对应请求体 output_config 字段，adaptive 思考模式用 effort 控制推理深度</summary>
public class AnthropicOutputConfig
{
    /// <summary>推理强度。low/medium/high/max（模型相关，见官方 effort 文档）</summary>
    public String? Effort { get; set; }
}

/// <summary>Anthropic 消息</summary>
public class AnthropicMessage
{
    /// <summary>角色。user / assistant</summary>
    public String Role { get; set; } = "";

    /// <summary>消息内容。可以是字符串或内容块数组（text / image_url 等）</summary>
    public Object? Content { get; set; }
}
