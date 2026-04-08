namespace NewLife.ChatAI.Filters;

/// <summary>标记 Action 的 [FromBody] 参数应使用 camelCase 命名策略反序列化。适用于 Gemini 协议端点</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class CamelCaseBodyAttribute : Attribute { }
