namespace NewLife.AI.Tools;

/// <summary>工具响应路由。决定工具执行结果的输出目标</summary>
[Flags]
public enum ToolResponseRouting
{
    /// <summary>结果回传给 LLM（追加 role=tool 消息），供模型继续推理</summary>
    Llm = 1,

    /// <summary>结果推送给前端（SSE ToolCallEvent），供客户端渲染</summary>
    Frontend = 2,

    /// <summary>同时发给 LLM 和前端（默认值）</summary>
    Both = Llm | Frontend,
}
