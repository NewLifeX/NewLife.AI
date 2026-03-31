using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using NewLife.Serialization;

namespace NewLife.AI.Models;

/// <summary>Ollama 生成请求</summary>
public class OllamaGenerateRequest
{
    /// <summary>模型名称</summary>
    public String? Model { get; set; }

    /// <summary>提示文本</summary>
    public String? Prompt { get; set; }

    /// <summary>后缀文本</summary>
    public String? Suffix { get; set; }

    /// <summary>图片（Base64 编码数组）</summary>
    public String[]? Images { get; set; }

    /// <summary>系统提示</summary>
    public String? System { get; set; }

    /// <summary>是否流式输出。默认 true</summary>
    public Boolean? Stream { get; set; }

    /// <summary>输出格式，如 json</summary>
    public Object? Format { get; set; }

    /// <summary>是否启用思考</summary>
    public Boolean? Think { get; set; }

    /// <summary>保持模型加载的时长</summary>
    public String? KeepAlive { get; set; }

    /// <summary>模型参数选项</summary>
    public OllamaOptions? Options { get; set; }
}

/// <summary>Ollama 模型参数选项</summary>
public class OllamaOptions
{
    /// <summary>温度</summary>
    public Double? Temperature { get; set; }

    /// <summary>Top P</summary>
    [DataMember(Name = "top_p")]
    public Double? TopP { get; set; }

    /// <summary>Top K</summary>
    [DataMember(Name = "top_k")]
    public Int32? TopK { get; set; }

    /// <summary>最大生成 token 数</summary>
    [DataMember(Name = "num_predict")]
    public Int32? NumPredict { get; set; }

    /// <summary>停止序列</summary>
    public List<String>? Stop { get; set; }

    /// <summary>随机种子</summary>
    public Int32? Seed { get; set; }

    /// <summary>重复惩罚</summary>
    [DataMember(Name = "repeat_penalty")]
    public Double? RepeatPenalty { get; set; }

    /// <summary>存在惩罚</summary>
    [DataMember(Name = "presence_penalty")]
    public Double? PresencePenalty { get; set; }

    /// <summary>频率惩罚</summary>
    [DataMember(Name = "frequency_penalty")]
    public Double? FrequencyPenalty { get; set; }
}

/// <summary>Ollama 生成响应</summary>
public class OllamaGenerateResponse
{
    /// <summary>模型名称</summary>
    public String? Model { get; set; }

    /// <summary>创建时间</summary>
    public String? CreatedAt { get; set; }

    /// <summary>响应文本</summary>
    public String? Response { get; set; }

    /// <summary>思考文本</summary>
    public String? Thinking { get; set; }

    /// <summary>是否完成</summary>
    public Boolean Done { get; set; }

    /// <summary>完成原因</summary>
    public String? DoneReason { get; set; }

    /// <summary>总耗时（纳秒）</summary>
    public Int64 TotalDuration { get; set; }

    /// <summary>模型加载耗时（纳秒）</summary>
    public Int64 LoadDuration { get; set; }

    /// <summary>输入 token 数</summary>
    public Int32 PromptEvalCount { get; set; }

    /// <summary>输入评估耗时（纳秒）</summary>
    public Int64 PromptEvalDuration { get; set; }

    /// <summary>输出 token 数</summary>
    public Int32 EvalCount { get; set; }

    /// <summary>输出评估耗时（纳秒）</summary>
    public Int64 EvalDuration { get; set; }
}

/// <summary>Ollama 嵌入请求</summary>
public class OllamaEmbedRequest
{
    /// <summary>模型名称</summary>
    public String? Model { get; set; }

    /// <summary>输入文本</summary>
    public Object? Input { get; set; }

    /// <summary>是否截断。默认 true</summary>
    public Boolean? Truncate { get; set; }

    /// <summary>向量维度</summary>
    public Int32? Dimensions { get; set; }

    /// <summary>保持模型加载的时长</summary>
    public String? KeepAlive { get; set; }

    /// <summary>模型参数选项</summary>
    public OllamaOptions? Options { get; set; }
}

/// <summary>Ollama 嵌入响应</summary>
public class OllamaEmbedResponse
{
    /// <summary>模型名称</summary>
    public String? Model { get; set; }

    /// <summary>嵌入向量数组</summary>
    public Double[][]? Embeddings { get; set; }

    /// <summary>总耗时（纳秒）</summary>
    public Int64 TotalDuration { get; set; }

    /// <summary>模型加载耗时（纳秒）</summary>
    public Int64 LoadDuration { get; set; }

    /// <summary>输入 token 数</summary>
    public Int32 PromptEvalCount { get; set; }
}

/// <summary>Ollama 模型列表响应</summary>
public class OllamaTagsResponse
{
    /// <summary>模型列表</summary>
    public OllamaModelInfo[]? Models { get; set; }
}

/// <summary>Ollama 模型信息</summary>
public class OllamaModelInfo
{
    /// <summary>模型名称</summary>
    public String? Name { get; set; }

    /// <summary>模型标识</summary>
    public String? Model { get; set; }

    /// <summary>修改时间</summary>
    [DataMember(Name = "modified_at")]
    public DateTime ModifiedAt { get; set; }

    /// <summary>模型大小（字节）</summary>
    public Int64 Size { get; set; }

    /// <summary>摘要哈希</summary>
    public String? Digest { get; set; }

    /// <summary>模型详细信息</summary>
    public OllamaModelDetails? Details { get; set; }
}

/// <summary>Ollama 模型详细信息</summary>
public class OllamaModelDetails
{
    /// <summary>格式</summary>
    public String? Format { get; set; }

    /// <summary>模型家族</summary>
    public String? Family { get; set; }

    /// <summary>模型家族列表</summary>
    public String[]? Families { get; set; }

    /// <summary>参数规模</summary>
    [DataMember(Name = "parameter_size")]
    public String? ParameterSize { get; set; }

    /// <summary>量化级别</summary>
    [DataMember(Name = "quantization_level")]
    public String? QuantizationLevel { get; set; }
}

/// <summary>Ollama 运行中模型列表响应</summary>
public class OllamaPsResponse
{
    /// <summary>运行中的模型列表</summary>
    public OllamaRunningModel[]? Models { get; set; }
}

/// <summary>Ollama 运行中模型信息</summary>
public class OllamaRunningModel
{
    /// <summary>模型名称</summary>
    public String? Name { get; set; }

    /// <summary>模型标识</summary>
    public String? Model { get; set; }

    /// <summary>模型大小（字节）</summary>
    public Int64 Size { get; set; }

    /// <summary>摘要哈希</summary>
    public String? Digest { get; set; }

    /// <summary>模型详细信息</summary>
    public OllamaModelDetails? Details { get; set; }

    /// <summary>过期时间</summary>
    [DataMember(Name = "modified_at")]
    public DateTime ExpiresAt { get; set; }

    /// <summary>显存占用（字节）</summary>
    [DataMember(Name = "size_vram")]
    public Int64 SizeVram { get; set; }

    /// <summary>上下文长度</summary>
    [DataMember(Name = "context_length")]
    public Int64 ContextLength { get; set; }
}

/// <summary>Ollama 模型详情请求</summary>
public class OllamaShowRequest
{
    /// <summary>模型名称</summary>
    public String? Model { get; set; }

    /// <summary>是否返回详细信息</summary>
    public Boolean? Verbose { get; set; }
}

/// <summary>Ollama 模型详情响应</summary>
public class OllamaShowResponse
{
    /// <summary>参数信息</summary>
    public String? Parameters { get; set; }

    /// <summary>许可证</summary>
    public String? License { get; set; }

    /// <summary>修改时间</summary>
    public String? ModifiedAt { get; set; }

    /// <summary>模型详细信息</summary>
    public OllamaModelDetails? Details { get; set; }

    /// <summary>模型模板</summary>
    public String? Template { get; set; }

    /// <summary>模型能力列表</summary>
    public String[]? Capabilities { get; set; }

    /// <summary>模型元信息</summary>
    public IDictionary<String, Object>? ModelInfo { get; set; }
}

/// <summary>Ollama 版本响应</summary>
public class OllamaVersionResponse
{
    /// <summary>版本号</summary>
    public String? Version { get; set; }
}

/// <summary>Ollama 拉取模型请求</summary>
public class OllamaPullRequest
{
    /// <summary>模型名称</summary>
    public String? Model { get; set; }

    /// <summary>是否流式输出。默认 true，设为 false 时等待完成后返回单条结果</summary>
    public Boolean? Stream { get; set; }

    /// <summary>是否允许拉取未经验证的镜像</summary>
    public Boolean? Insecure { get; set; }
}

/// <summary>Ollama 拉取模型状态（流式每帧 / 非流式最终帧）</summary>
public class OllamaPullStatus
{
    /// <summary>状态描述，如 "pulling manifest"、"downloading"、"success"</summary>
    public String? Status { get; set; }

    /// <summary>当前层的文件摘要</summary>
    public String? Digest { get; set; }

    /// <summary>文件总大小（字节）</summary>
    public Int64 Total { get; set; }

    /// <summary>已完成大小（字节）</summary>
    public Int64 Completed { get; set; }
}

// ── Ollama Chat API 模型 ──────────────────────────────────────────────────

/// <summary>Ollama /api/chat 对话请求</summary>
/// <remarks>
/// 对应 Ollama 原生 POST /api/chat 请求体。
/// 与 OpenAI ChatCompletionRequest 的差异：
/// <list type="bullet">
/// <item>模型参数放在 options 子对象中（非顶级），字段使用 snake_case</item>
/// <item>流式输出为 NDJSON 格式（非 SSE）</item>
/// <item>think 参数控制思考模式（Ollama 原生字段）</item>
/// </list>
/// </remarks>
public class OllamaChatRequest
{
    /// <summary>模型名称</summary>
    public String? Model { get; set; }

    /// <summary>是否流式输出</summary>
    public Boolean Stream { get; set; }

    /// <summary>是否启用思考。null 时不传，由模型自身决定</summary>
    public Boolean? Think { get; set; }

    /// <summary>对话消息列表</summary>
    public IList<OllamaChatMessage> Messages { get; set; } = [];

    /// <summary>模型参数选项</summary>
    public OllamaOptions? Options { get; set; }

    /// <summary>工具定义列表</summary>
    public IList<Object>? Tools { get; set; }

    /// <summary>从通用 ChatRequest 构建 Ollama 原生请求</summary>
    /// <param name="request">通用对话请求</param>
    /// <returns>Ollama 原生请求对象</returns>
    public static OllamaChatRequest FromChatRequest(IChatRequest request)
    {
        var result = new OllamaChatRequest
        {
            Model = request.Model ?? "",
            Stream = request.Stream,
        };

        // think 参数：显式 true/false 时才传给 Ollama；null（Auto）时不传，由模型自身决定
        // 注意：不能用 ?? false 兜底，否则 Auto 模式会意外关闭思考
        if (request.EnableThinking.HasValue)
            result.Think = request.EnableThinking.Value;

        // 转换消息
        var messages = new List<OllamaChatMessage>();
        foreach (var msg in request.Messages)
        {
            var m = new OllamaChatMessage
            {
                Role = msg.Role,
                Content = msg.Content ?? "",
            };

            if (msg.ToolCalls != null && msg.ToolCalls.Count > 0)
            {
                var toolCalls = new List<OllamaToolCall>();
                foreach (var tc in msg.ToolCalls)
                {
                    var otc = new OllamaToolCall { Id = tc.Id, Type = tc.Type };
                    if (tc.Function != null)
                    {
                        // 将 arguments JSON 字符串解析为对象，以便序列化时输出 JSON 对象而非字符串
                        Object? args;
                        var argsStr = tc.Function.Arguments;
                        if (!String.IsNullOrEmpty(argsStr))
                            args = JsonParser.Decode(argsStr) ?? (Object)argsStr;
                        else
                            args = new Dictionary<String, Object?>();

                        otc.Function = new OllamaFunctionCall
                        {
                            Name = tc.Function.Name,
                            Arguments = args,
                        };
                    }
                    toolCalls.Add(otc);
                }
                m.ToolCalls = toolCalls;
            }

            messages.Add(m);
        }
        result.Messages = messages;

        // Ollama 的生成参数放在 options 子对象里
        var hasOptions = request.MaxTokens != null || request.Temperature != null
            || request.TopP != null || (request.Stop != null && request.Stop.Count > 0);
        // 携带工具时限制思考 token 上限，防止 thinking 内容耗尽 context 导致工具调用 JSON 被截断
        var forceNumPredict = request.Tools != null && request.Tools.Count > 0 && request.MaxTokens == null;
        if (hasOptions || forceNumPredict)
        {
            var opts = new OllamaOptions();
            if (request.MaxTokens != null)
                opts.NumPredict = request.MaxTokens.Value;
            else if (forceNumPredict)
                opts.NumPredict = 4096;
            if (request.Temperature != null) opts.Temperature = request.Temperature.Value;
            if (request.TopP != null) opts.TopP = request.TopP.Value;
            if (request.Stop != null && request.Stop.Count > 0)
                opts.Stop = request.Stop is List<String> list ? list : new List<String>(request.Stop);
            result.Options = opts;
        }

        // 工具定义
        if (request.Tools != null && request.Tools.Count > 0)
        {
            var tools = new List<Object>();
            foreach (var tool in request.Tools)
            {
                var t = new Dictionary<String, Object> { ["type"] = tool.Type };
                if (tool.Function != null)
                {
                    var fn = new Dictionary<String, Object?> { ["name"] = tool.Function.Name };
                    if (tool.Function.Description != null) fn["description"] = tool.Function.Description;
                    if (tool.Function.Parameters != null) fn["parameters"] = tool.Function.Parameters;
                    t["function"] = fn;
                }
                tools.Add(t);
            }
            result.Tools = tools;
        }

        return result;
    }
}

/// <summary>Ollama 对话消息</summary>
public class OllamaChatMessage
{
    /// <summary>角色（user/assistant/system/tool）</summary>
    public String? Role { get; set; }

    /// <summary>消息内容</summary>
    public Object? Content { get; set; }

    /// <summary>思考内容（仅响应中使用）</summary>
    public String? Thinking { get; set; }

    /// <summary>工具调用列表</summary>
    [DataMember(Name = "tool_calls")]
    public IList<OllamaToolCall>? ToolCalls { get; set; }

    /// <summary>转换为通用 ChatMessage</summary>
    /// <returns>通用消息对象</returns>
    public ChatMessage ToChatMessage()
    {
        var msg = new ChatMessage
        {
            Role = Role ?? "assistant",
            Content = Content,
            // Ollama 原生思考字段为 thinking（与兼容模式的 reasoning 不同）
            ReasoningContent = Thinking,
        };

        if (ToolCalls != null && ToolCalls.Count > 0)
        {
            var toolCalls = new List<ToolCall>();
            foreach (var tc in ToolCalls)
            {
                var call = new ToolCall
                {
                    Id = tc.Id ?? "",
                    Type = tc.Type ?? "function",
                };

                if (tc.Function != null)
                {
                    var argsRaw = tc.Function.Arguments;
                    call.Function = new FunctionCall
                    {
                        Name = tc.Function.Name ?? "",
                        Arguments = argsRaw is String s ? s
                            : argsRaw is IDictionary<String, Object> argsDic ? argsDic.ToJson()
                            : "{}",
                    };
                }

                toolCalls.Add(call);
            }
            msg.ToolCalls = toolCalls;
        }

        return msg;
    }
}

/// <summary>Ollama 工具调用</summary>
public class OllamaToolCall
{
    /// <summary>调用标识。Ollama 响应中可能为空</summary>
    public String? Id { get; set; }

    /// <summary>调用类型。Ollama 响应中可能为空，默认 function</summary>
    public String? Type { get; set; }

    /// <summary>函数调用信息</summary>
    public OllamaFunctionCall? Function { get; set; }
}

/// <summary>Ollama 函数调用</summary>
public class OllamaFunctionCall
{
    /// <summary>函数名称</summary>
    public String? Name { get; set; }

    /// <summary>函数参数。请求中序列化为 JSON 对象，响应中反序列化为 IDictionary</summary>
    public Object? Arguments { get; set; }
}

/// <summary>Ollama /api/chat 对话响应（非流式和流式共用结构）</summary>
/// <remarks>
/// 非流式响应包含完整消息和统计信息（done=true）。
/// 流式响应每帧包含部分消息（done=false），最后一帧包含统计信息（done=true）。
/// </remarks>
public class OllamaChatResponse
{
    /// <summary>模型名称</summary>
    public String? Model { get; set; }

    /// <summary>创建时间</summary>
    [DataMember(Name = "created_at")]
    public String? CreatedAt { get; set; }

    /// <summary>消息对象</summary>
    public OllamaChatMessage? Message { get; set; }

    /// <summary>是否完成</summary>
    public Boolean Done { get; set; }

    /// <summary>完成原因</summary>
    [DataMember(Name = "done_reason")]
    public String? DoneReason { get; set; }

    /// <summary>总耗时（纳秒）</summary>
    [DataMember(Name = "total_duration")]
    public Int64 TotalDuration { get; set; }

    /// <summary>模型加载耗时（纳秒）</summary>
    [DataMember(Name = "load_duration")]
    public Int64 LoadDuration { get; set; }

    /// <summary>输入 token 数</summary>
    [DataMember(Name = "prompt_eval_count")]
    public Int32 PromptEvalCount { get; set; }

    /// <summary>输入评估耗时（纳秒）</summary>
    [DataMember(Name = "prompt_eval_duration")]
    public Int64 PromptEvalDuration { get; set; }

    /// <summary>输出 token 数</summary>
    [DataMember(Name = "eval_count")]
    public Int32 EvalCount { get; set; }

    /// <summary>输出评估耗时（纳秒）</summary>
    [DataMember(Name = "eval_duration")]
    public Int64 EvalDuration { get; set; }

    /// <summary>转换为通用 ChatResponse（非流式）</summary>
    /// <returns>通用对话响应</returns>
    public ChatResponse ToChatResponse()
    {
        var response = new ChatResponse
        {
            Id = CreatedAt != null ? $"ollama-{CreatedAt}" : $"ollama-{DateTime.UtcNow.Ticks}",
            Object = "chat.completion",
            Model = Model,
        };

        if (Message != null)
        {
            var msg = Message.ToChatMessage();
            response.Messages = [new ChatChoice { Index = 0, Message = msg, FinishReason = DoneReason }];
        }

        if (PromptEvalCount > 0 || EvalCount > 0)
        {
            response.Usage = new UsageDetails
            {
                InputTokens = PromptEvalCount,
                OutputTokens = EvalCount,
                TotalTokens = PromptEvalCount + EvalCount,
            };
        }

        return response;
    }

    /// <summary>转换为通用 ChatResponse（流式 chunk）</summary>
    /// <returns>流式 chunk 响应，解析失败返回 null</returns>
    public ChatResponse? ToStreamChunk()
    {
        var chunk = new ChatResponse
        {
            Id = CreatedAt != null ? $"ollama-{CreatedAt}" : $"ollama-{DateTime.UtcNow.Ticks}",
            Object = "chat.completion.chunk",
            Model = Model,
        };

        String? finishReason = null;
        if (Done) finishReason = DoneReason ?? "stop";

        if (Message != null)
        {
            var msg = Message.ToChatMessage();
            chunk.Messages = [new ChatChoice { Index = 0, Delta = msg, FinishReason = finishReason }];
        }
        else if (Done)
        {
            chunk.Messages = [new ChatChoice { Index = 0, Delta = new ChatMessage { Role = "assistant" }, FinishReason = finishReason }];
        }

        if (Done && (PromptEvalCount > 0 || EvalCount > 0))
        {
            chunk.Usage = new UsageDetails
            {
                InputTokens = PromptEvalCount,
                OutputTokens = EvalCount,
                TotalTokens = PromptEvalCount + EvalCount,
            };
        }

        return chunk;
    }
}
