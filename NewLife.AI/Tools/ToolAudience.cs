namespace NewLife.AI.Tools;

/// <summary>工具内容受众。标记一个内容块应该发送给谁</summary>
[Flags]
public enum ToolAudience
{
    /// <summary>发给大模型（追加 role=tool 消息参与上下文推理）</summary>
    Llm = 1,

    /// <summary>发给前端（SSE 事件 + DB 持久化，供界面渲染）</summary>
    User = 2,

    /// <summary>同时发给 LLM 和前端（默认）</summary>
    Both = Llm | User,
}
