namespace NewLife.AI.Agents;

/// <summary>Agent 消息类型</summary>
public enum AgentMessageType
{
    /// <summary>普通文本消息</summary>
    Text,

    /// <summary>系统消息（用于初始化代理角色）</summary>
    System,

    /// <summary>终止信号。任何代理返回此消息时 GroupChat 停止循环</summary>
    Stop,

    /// <summary>工具调用请求消息</summary>
    ToolCall,

    /// <summary>工具调用结果消息</summary>
    ToolCallResult,
}

/// <summary>Agent 消息基类。AgentChat 协议的基础传输单元</summary>
public abstract class AgentMessage
{
    /// <summary>消息来源（发送方 Agent 名称）</summary>
    public String Source { get; set; } = String.Empty;

    /// <summary>消息类型</summary>
    public abstract AgentMessageType Type { get; }

    /// <summary>消息时间戳（UTC）</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>转换为 ChatMessage（system/user/assistant/tool）。供 IChatClient 使用</summary>
    /// <returns>可用于 ChatCompletionRequest.Messages 的消息，若本消息类型不适合转换则返回 null</returns>
    public abstract Models.ChatMessage? ToChatMessage();
}

/// <summary>普通文本消息</summary>
public sealed class TextMessage : AgentMessage
{
    /// <summary>文本内容</summary>
    public String Content { get; set; } = String.Empty;

    /// <summary>消息角色（user/assistant）。默认 user</summary>
    public String Role { get; set; } = "user";

    /// <inheritdoc/>
    public override AgentMessageType Type => AgentMessageType.Text;

    /// <inheritdoc/>
    public override Models.ChatMessage? ToChatMessage() => new() { Role = Role, Content = Content };
}

/// <summary>系统消息。用于注入 Agent 的角色设定</summary>
public sealed class SystemMessage : AgentMessage
{
    /// <summary>系统提示内容</summary>
    public String Content { get; set; } = String.Empty;

    /// <inheritdoc/>
    public override AgentMessageType Type => AgentMessageType.System;

    /// <inheritdoc/>
    public override Models.ChatMessage? ToChatMessage() => new() { Role = "system", Content = Content };
}

/// <summary>终止信号消息。GroupChat 收到此消息时停止轮询</summary>
public sealed class StopMessage : AgentMessage
{
    /// <summary>终止原因</summary>
    public String Reason { get; set; } = String.Empty;

    /// <inheritdoc/>
    public override AgentMessageType Type => AgentMessageType.Stop;

    /// <inheritdoc/>
    public override Models.ChatMessage? ToChatMessage() => null;
}

/// <summary>工具调用请求消息</summary>
public sealed class ToolCallMessage : AgentMessage
{
    /// <summary>工具名称</summary>
    public String ToolName { get; set; } = String.Empty;

    /// <summary>调用参数（JSON 字符串）</summary>
    public String? Arguments { get; set; }

    /// <summary>调用 Id（对应 ToolCallResultMessage.CallId）</summary>
    public String CallId { get; set; } = String.Empty;

    /// <inheritdoc/>
    public override AgentMessageType Type => AgentMessageType.ToolCall;

    /// <inheritdoc/>
    public override Models.ChatMessage? ToChatMessage() => new()
    {
        Role = "assistant",
        ToolCalls =
        [
            new Models.ToolCall
            {
                Id = CallId,
                Function = new Models.FunctionCall { Name = ToolName, Arguments = Arguments },
            }
        ]
    };
}

/// <summary>工具调用结果消息</summary>
public sealed class ToolCallResultMessage : AgentMessage
{
    /// <summary>关联的调用 Id</summary>
    public String CallId { get; set; } = String.Empty;

    /// <summary>工具名称</summary>
    public String ToolName { get; set; } = String.Empty;

    /// <summary>工具执行结果（文本或 JSON）</summary>
    public String Result { get; set; } = String.Empty;

    /// <inheritdoc/>
    public override AgentMessageType Type => AgentMessageType.ToolCallResult;

    /// <inheritdoc/>
    public override Models.ChatMessage? ToChatMessage() => new()
    {
        Role = "tool",
        Name = ToolName,
        Content = Result,
    };
}

/// <summary>Agent 消息工具方法</summary>
public static class AgentMessageHelper
{
    /// <summary>将 AgentMessage 列表转换为 ChatMessage 列表（过滤掉不支持转换的消息类型）</summary>
    /// <param name="messages">Agent 消息列表</param>
    /// <returns>可追加到 ChatCompletionRequest.Messages 的消息列表</returns>
    public static IList<Models.ChatMessage> ToChatMessages(IList<AgentMessage> messages)
    {
        if (messages == null) throw new ArgumentNullException(nameof(messages));

        var result = new List<Models.ChatMessage>(messages.Count);
        foreach (var m in messages)
        {
            var cm = m.ToChatMessage();
            if (cm != null) result.Add(cm);
        }
        return result;
    }
}
