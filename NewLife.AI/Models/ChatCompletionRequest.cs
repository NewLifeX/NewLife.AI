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
