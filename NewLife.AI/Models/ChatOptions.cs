using NewLife.Data;

namespace NewLife.AI.Models;

/// <summary>对话选项。面向用户的简洁参数集合，可在每次调用时覆盖客户端默认值</summary>
/// <remarks>
/// 与 <see cref="ChatCompletionRequest"/> 的关系：ChatOptions 是面向 SDK 用户的简洁 API，
/// ChatCompletionRequest 是面向协议层的完整 DTO。客户端内部将两者合并后发送给服务商。
/// 所有属性均为 nullable，null 表示沿用客户端默认值。
/// </remarks>
public class ChatOptions : IExtend
{
    /// <summary>模型编码。覆盖客户端默认模型</summary>
    public String? Model { get; set; }

    /// <summary>温度。0~2，越高越随机</summary>
    public Double? Temperature { get; set; }

    /// <summary>核采样。0~1，与Temperature二选一</summary>
    public Double? TopP { get; set; }

    /// <summary>最大生成令牌数</summary>
    public Int32? MaxTokens { get; set; }

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

    /// <summary>是否启用思考模式。null=不设置，true=开启，false=关闭</summary>
    public Boolean? EnableThinking { get; set; }

    /// <summary>响应格式。用于结构化输出，如 {"type":"json_schema","json_schema":{...}}</summary>
    public Object? ResponseFormat { get; set; }

    /// <summary>是否允许并行工具调用。null=不设置，true=允许，false=禁止</summary>
    public Boolean? ParallelToolCalls { get; set; }

    /// <summary>当前请求的用户编号。传递给过滤器链，供 LearningFilter 等中间件读取</summary>
    public Int32 UserId { get; set; }

    /// <summary>当前请求的会话编号。传递给过滤器链，供 LearningFilter 等中间件读取</summary>
    public Int64 ConversationId { get; set; }

    /// <summary>扩展数据。用于在中间件管道中传递非结构化的自定义上下文</summary>
    public IDictionary<String, Object?> Items { get; set; } = new Dictionary<String, Object?>();

    /// <summary>索引器，方便访问扩展数据</summary>
    public Object? this[String key] { get => Items.TryGetValue(key, out var value) ? value : null; set => Items[key] = value; }
}
