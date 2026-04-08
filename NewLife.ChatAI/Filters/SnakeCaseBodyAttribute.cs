namespace NewLife.ChatAI.Filters;

/// <summary>标记 Action 的 [FromBody] 参数应使用 snake_case 命名策略反序列化。适用于 OpenAI / Anthropic 协议端点</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class SnakeCaseBodyAttribute : Attribute { }
