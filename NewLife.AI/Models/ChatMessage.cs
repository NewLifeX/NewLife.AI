using System.Runtime.Serialization;

namespace NewLife.AI.Models;

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
    [DataMember(Name = "tool_calls")]
    public IList<ToolCall>? ToolCalls { get; set; }

    /// <summary>工具调用编号。tool 角色回传时关联的调用编号</summary>
    [DataMember(Name = "tool_call_id")]
    public String? ToolCallId { get; set; }

    /// <summary>思考内容。部分模型返回的推理链路（reasoning_content）</summary>
    [DataMember(Name = "reasoning_content")]
    public String? ReasoningContent { get; set; }

    /// <summary>类型化内容片段列表（MEAI 兼容）。非空时优先于 <see cref="Content"/> 使用，支持多模态消息</summary>
    /// <remarks>
    /// 与 <see cref="Content"/>（Object?）的关系：两者并存以保持向后兼容。
    /// 新代码建议使用 Contents 以获得更强的类型安全性；旧代码无需修改。
    /// 序列化时不输出此属性：由各协议客户端在构建请求时将 Contents 转为协议格式赋值给 Content。
    /// </remarks>
    [IgnoreDataMember]
    public IList<AIContent>? Contents { get; set; }
    #endregion
}
