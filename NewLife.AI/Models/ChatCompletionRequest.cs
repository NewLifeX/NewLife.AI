namespace NewLife.AI.Models;

/// <summary>对话完成请求。兼容 OpenAI ChatCompletion 标准</summary>
public class ChatCompletionRequest
{
    #region 属性
    /// <summary>模型编码</summary>
    public String? Model { get; set; }

    /// <summary>消息列表</summary>
    public IList<ChatMessage> Messages { get; set; } = [];

    /// <summary>温度。0~2，越高越随机，默认1</summary>
    public Double? Temperature { get; set; }

    /// <summary>核采样。0~1，与Temperature二选一</summary>
    public Double? TopP { get; set; }

    /// <summary>最大生成令牌数</summary>
    public Int32? MaxTokens { get; set; }

    /// <summary>是否流式输出</summary>
    public Boolean Stream { get; set; }

    /// <summary>停止词列表</summary>
    public IList<String>? Stop { get; set; }

    /// <summary>存在惩罚。-2~2</summary>
    public Double? PresencePenalty { get; set; }

    /// <summary>频率惩罚。-2~2</summary>
    public Double? FrequencyPenalty { get; set; }

    /// <summary>可用工具列表。用于函数调用</summary>
    public IList<ChatTool>? Tools { get; set; }

    /// <summary>工具选择策略。auto/none/required 或指定工具名</summary>
    public Object? ToolChoice { get; set; }

    /// <summary>用户标识。用于追踪和限流</summary>
    public String? User { get; set; }

    /// <summary>是否启用思考模式。null=不设置，true=开启，false=关闭。仅支持的模型有效（如 Qwen3 系列、QwQ 等）</summary>
    public Boolean? EnableThinking { get; set; }
    #endregion

    #region 方法
    /// <summary>应用对话选项。将 ChatOptions 中的非空字段合并到当前请求</summary>
    /// <param name="options">对话选项，null 字段不覆盖</param>
    /// <returns>当前请求实例（支持链式调用）</returns>
    public ChatCompletionRequest Apply(ChatOptions? options)
    {
        if (options == null) return this;

        Model ??= options.Model;
        Temperature ??= options.Temperature;
        TopP ??= options.TopP;
        MaxTokens ??= options.MaxTokens;
        Stop ??= options.Stop;
        PresencePenalty ??= options.PresencePenalty;
        FrequencyPenalty ??= options.FrequencyPenalty;
        User ??= options.User;
        EnableThinking ??= options.EnableThinking;

        if (options.Tools != null && options.Tools.Count > 0)
        {
            Tools ??= [];
            foreach (var t in options.Tools)
            {
                Tools.Add(t);
            }
        }
        ToolChoice ??= options.ToolChoice;

        return this;
    }

    /// <summary>根据消息列表和可选对话选项创建请求</summary>
    /// <param name="messages">消息列表</param>
    /// <param name="options">对话选项</param>
    /// <param name="stream">是否流式</param>
    /// <returns>对话请求实例</returns>
    public static ChatCompletionRequest Create(IList<ChatMessage> messages, ChatOptions? options = null, Boolean stream = false)
    {
        var request = new ChatCompletionRequest
        {
            Messages = messages,
            Stream = stream,
        };
        return request.Apply(options);
    }

    /// <summary>提取对话选项。将请求中的参数转换为 ChatOptions（不含 Messages 和 Stream）</summary>
    /// <returns>对话选项实例</returns>
    public ChatOptions ToChatOptions() => new()
    {
        Model = Model,
        Temperature = Temperature,
        TopP = TopP,
        MaxTokens = MaxTokens,
        Stop = Stop,
        PresencePenalty = PresencePenalty,
        FrequencyPenalty = FrequencyPenalty,
        Tools = Tools,
        ToolChoice = ToolChoice,
        User = User,
        EnableThinking = EnableThinking,
    };
    #endregion
}

/// <summary>对话消息</summary>
public class ChatMessage
{
    #region 属性
    /// <summary>角色。system/user/assistant/tool</summary>
    public String Role { get; set; } = null!;

    /// <summary>内容。文本内容或多模态内容数组</summary>
    public Object? Content { get; set; }

    /// <summary>名称。函数调用时的函数名</summary>
    public String? Name { get; set; }

    /// <summary>工具调用列表。assistant 角色发起的工具调用</summary>
    public IList<ToolCall>? ToolCalls { get; set; }

    /// <summary>工具调用编号。tool 角色回传时关联的调用编号</summary>
    public String? ToolCallId { get; set; }

    /// <summary>思考内容。部分模型返回的推理链路（reasoning_content）</summary>
    public String? ReasoningContent { get; set; }

    /// <summary>类型化内容片段列表（MEAI 兼容）。非空时优先于 <see cref="Content"/> 使用，支持多模态消息</summary>
    /// <remarks>
    /// 与 <see cref="Content"/>（Object?）的关系：两者并存以保持向后兼容。
    /// 新代码建议使用 Contents 以获得更强的类型安全性；旧代码无需修改。
    /// </remarks>
    public IList<AIContent>? Contents { get; set; }
    #endregion
}

/// <summary>多模态内容块</summary>
public class ContentPart
{
    /// <summary>类型。text/image_url</summary>
    public String Type { get; set; } = null!;

    /// <summary>文本内容</summary>
    public String? Text { get; set; }

    /// <summary>图片URL</summary>
    public ImageUrl? ImageUrl { get; set; }
}

/// <summary>图片URL</summary>
public class ImageUrl
{
    /// <summary>URL地址。支持 http/https/base64 data URI</summary>
    public String Url { get; set; } = null!;

    /// <summary>细节级别。auto/low/high</summary>
    public String? Detail { get; set; }
}

/// <summary>工具调用</summary>
public class ToolCall
{
    /// <summary>流式下标。流式输出时用于标识属于哪个工具调用，非流式场景可忽略</summary>
    public Int32? Index { get; set; }

    /// <summary>调用编号</summary>
    public String Id { get; set; } = null!;

    /// <summary>调用类型。固定 function</summary>
    public String Type { get; set; } = "function";

    /// <summary>函数调用详情</summary>
    public FunctionCall? Function { get; set; }
}

/// <summary>函数调用详情</summary>
public class FunctionCall
{
    /// <summary>函数名称</summary>
    public String Name { get; set; } = null!;

    /// <summary>调用参数。JSON 字符串</summary>
    public String? Arguments { get; set; }
}

/// <summary>聊天工具定义</summary>
public class ChatTool
{
    /// <summary>类型。固定 function</summary>
    public String Type { get; set; } = "function";

    /// <summary>函数定义</summary>
    public FunctionDefinition? Function { get; set; }
}

/// <summary>函数定义</summary>
public class FunctionDefinition
{
    /// <summary>名称</summary>
    public String Name { get; set; } = null!;

    /// <summary>描述</summary>
    public String? Description { get; set; }

    /// <summary>参数。JSON Schema 格式</summary>
    public Object? Parameters { get; set; }
}
