using System.Runtime.Serialization;
using NewLife.AI.Models;
using NewLife.Serialization;

namespace NewLife.AI.Clients.Bedrock;

/// <summary>AWS Bedrock Converse API 请求体。兼容 https://docs.aws.amazon.com/bedrock/latest/userguide/conversation-api.html 协议</summary>
/// <remarks>
/// Bedrock Converse API 的主要特点：
/// <list type="bullet">
/// <item>支持 Claude、Llama、Mistral 等多种模型</item>
/// <item>使用 AWS SigV4 签名认证，而非 Bearer Token</item>
/// <item>system 消息为独立的顶级字段数组，不在 messages 中</item>
/// <item>推理配置通过顶级 inferenceConfig 字段传递</item>
/// <item>工具定义通过顶级 toolConfig 字段传递</item>
/// </list>
/// </remarks>
public class BedrockRequest
{
    #region 属性
    /// <summary>模型编码（不包括版本号）</summary>
    [IgnoreDataMember]
    public String? Model { get; set; }

    /// <summary>系统提示词数组。每项包含 text 字段</summary>
    public IList<BedrockSystemContent>? System { get; set; }

    /// <summary>消息列表。role 为 user / assistant</summary>
    public IList<BedrockMessage> Messages { get; set; } = [];

    /// <summary>推理配置</summary>
    [DataMember(Name = "inferenceConfig")]
    public BedrockInferenceConfig? InferenceConfig { get; set; }

    /// <summary>工具配置</summary>
    [DataMember(Name = "toolConfig")]
    public BedrockToolConfig? ToolConfig { get; set; }
    #endregion

    #region 转换
    /// <summary>从内部统一 ChatRequest 构建 Bedrock Converse API 请求</summary>
    /// <param name="request">内部统一请求</param>
    /// <returns>可直接 ToJson 序列化的 Bedrock 请求</returns>
    public static BedrockRequest FromChatRequest(IChatRequest request)
    {
        var result = new BedrockRequest { Model = request.Model };

        // 分离 system 消息和普通消息
        var messages = new List<BedrockMessage>();
        var systemContents = new List<BedrockSystemContent>();

        if (request.Messages != null)
        {
            foreach (var msg in request.Messages)
            {
                if (msg.Role == "system")
                {
                    var content = msg.Content?.ToString();
                    if (!String.IsNullOrEmpty(content))
                        systemContents.Add(new BedrockSystemContent { Text = content });
                    continue;
                }

                var role = msg.Role switch
                {
                    "assistant" => "assistant",
                    "tool" => "user",
                    _ => "user",
                };

                var bmsg = new BedrockMessage { Role = role };

                if (msg.ToolCallId != null)
                {
                    // 工具结果消息 → toolResult 内容块
                    bmsg.Role = "user";
                    bmsg.Content = [
                        new BedrockContentBlock
                        {
                            ToolResult = new BedrockToolResult
                            {
                                ToolUseId = msg.ToolCallId,
                                Content = [new BedrockContentBlock { Text = msg.Content?.ToString() ?? "" }],
                            }
                        }
                    ];
                }
                else if (msg.ToolCalls != null && msg.ToolCalls.Count > 0)
                {
                    // assistant 工具调用 → toolUse 内容块
                    var contentBlocks = new List<BedrockContentBlock>();
                    if (msg.Content != null)
                        contentBlocks.Add(new BedrockContentBlock { Text = msg.Content.ToString()! });

                    foreach (var tc in msg.ToolCalls)
                    {
                        Object input = tc.Function?.Arguments != null
                            ? (JsonParser.Decode(tc.Function.Arguments) ?? new Dictionary<String, Object?>())
                            : new Dictionary<String, Object?>();

                        contentBlocks.Add(new BedrockContentBlock
                        {
                            ToolUse = new BedrockToolUse
                            {
                                ToolUseId = tc.Id ?? "",
                                Name = tc.Function?.Name ?? "",
                                Input = input,
                            }
                        });
                    }
                    bmsg.Content = contentBlocks;
                }
                else
                {
                    // 普通文本消息
                    var textContent = msg.Content?.ToString();
                    if (!String.IsNullOrEmpty(textContent))
                    {
                        bmsg.Content = [new BedrockContentBlock { Text = textContent }];
                    }
                }

                messages.Add(bmsg);
            }
        }

        result.Messages = messages;
        if (systemContents.Count > 0)
            result.System = systemContents;

        // 推理配置
        var inferenceConfig = new BedrockInferenceConfig();
        if (request.MaxTokens > 0)
            inferenceConfig.MaxTokens = request.MaxTokens;
        if (request.Temperature != null)
            inferenceConfig.Temperature = request.Temperature.Value;
        if (request.TopP != null)
            inferenceConfig.TopP = request.TopP.Value;
        if (request.Stop != null && request.Stop.Count > 0)
            inferenceConfig.StopSequences = request.Stop;

        if (!inferenceConfig.IsEmpty())
            result.InferenceConfig = inferenceConfig;

        // 工具配置
        if (request.Tools != null && request.Tools.Count > 0)
        {
            var toolList = new List<BedrockToolSpec>();
            foreach (var tool in request.Tools)
            {
                if (tool.Function == null) continue;
                toolList.Add(new BedrockToolSpec
                {
                    ToolSpec = new BedrockToolSpecDef
                    {
                        Name = tool.Function.Name ?? "",
                        Description = tool.Function.Description ?? "",
                        InputSchema = tool.Function.Parameters != null
                            ? new Dictionary<String, Object> { ["json"] = tool.Function.Parameters }
                            : new Dictionary<String, Object> { ["json"] = new { type = "object" } },
                    }
                });
            }

            if (toolList.Count > 0)
                result.ToolConfig = new BedrockToolConfig { Tools = toolList };
        }

        return result;
    }

    /// <summary>转换为内部统一的 ChatRequest</summary>
    /// <returns>等效的 ChatRequest 实例</returns>
    public ChatRequest ToChatRequest()
    {
        var messages = new List<ChatMessage>();

        // 将顶级 system 字段转为首条系统消息
        if (System != null)
        {
            var systemText = String.Join("", System.Select(s => s.Text));
            if (!String.IsNullOrEmpty(systemText))
                messages.Add(new ChatMessage { Role = "system", Content = systemText });
        }

        foreach (var msg in Messages)
        {
            messages.Add(new ChatMessage
            {
                Role = msg.Role,
                Content = msg.Content != null
                    ? String.Join("", msg.Content.Select(c => c.Text ?? "").Where(t => !String.IsNullOrEmpty(t)))
                    : null,
            });
        }

        return new ChatRequest
        {
            Model = Model,
            Messages = messages,
            MaxTokens = InferenceConfig?.MaxTokens,
            Temperature = InferenceConfig?.Temperature,
            TopP = InferenceConfig?.TopP,
            Stop = InferenceConfig?.StopSequences,
        };
    }
    #endregion
}

/// <summary>Bedrock 系统内容块</summary>
public class BedrockSystemContent
{
    /// <summary>文本内容</summary>
    public String? Text { get; set; }
}

/// <summary>Bedrock 消息</summary>
public class BedrockMessage
{
    /// <summary>角色。user / assistant</summary>
    public String Role { get; set; } = "";

    /// <summary>消息内容块列表</summary>
    public IList<BedrockContentBlock>? Content { get; set; }
}

/// <summary>Bedrock 内容块。通用容器，包含 text / toolUse / toolResult</summary>
public class BedrockContentBlock
{
    /// <summary>文本内容</summary>
    public String? Text { get; set; }

    /// <summary>工具调用内容</summary>
    public BedrockToolUse? ToolUse { get; set; }

    /// <summary>工具结果内容</summary>
    public BedrockToolResult? ToolResult { get; set; }
}

/// <summary>Bedrock 工具调用</summary>
public class BedrockToolUse
{
    /// <summary>工具调用编号</summary>
    [DataMember(Name = "toolUseId")]
    public String? ToolUseId { get; set; }

    /// <summary>工具名称</summary>
    public String? Name { get; set; }

    /// <summary>工具调用输入参数。反序列化后为 IDictionary</summary>
    public Object? Input { get; set; }
}

/// <summary>Bedrock 工具结果</summary>
public class BedrockToolResult
{
    /// <summary>关联的工具调用编号</summary>
    [DataMember(Name = "toolUseId")]
    public String? ToolUseId { get; set; }

    /// <summary>工具结果内容</summary>
    public IList<BedrockContentBlock>? Content { get; set; }
}

/// <summary>Bedrock 推理配置</summary>
public class BedrockInferenceConfig
{
    /// <summary>最大生成令牌数</summary>
    [DataMember(Name = "maxTokens")]
    public Int32? MaxTokens { get; set; }

    /// <summary>温度。0~1</summary>
    public Double? Temperature { get; set; }

    /// <summary>核采样。0~1</summary>
    [DataMember(Name = "topP")]
    public Double? TopP { get; set; }

    /// <summary>停止序列</summary>
    [DataMember(Name = "stopSequences")]
    public IList<String>? StopSequences { get; set; }

    /// <summary>判断配置是否为空</summary>
    public Boolean IsEmpty() =>
        MaxTokens == null &&
        Temperature == null &&
        TopP == null &&
        (StopSequences == null || StopSequences.Count == 0);
}

/// <summary>Bedrock 工具配置</summary>
public class BedrockToolConfig
{
    /// <summary>工具列表</summary>
    public IList<BedrockToolSpec>? Tools { get; set; }
}

/// <summary>Bedrock 单个工具规范</summary>
public class BedrockToolSpec
{
    /// <summary>工具定义</summary>
    public BedrockToolSpecDef? ToolSpec { get; set; }
}

/// <summary>Bedrock 工具定义详情</summary>
public class BedrockToolSpecDef
{
    /// <summary>工具名称</summary>
    public String? Name { get; set; }

    /// <summary>工具描述</summary>
    public String? Description { get; set; }

    /// <summary>输入参数 Schema。键为 "json"，值为 JSON Schema 对象</summary>
    public Dictionary<String, Object>? InputSchema { get; set; }
}
