namespace NewLife.AI.Services;

/// <summary>工具调用配置接口。由 ChatSetting 实现，供 ChatClientBuilder.UseTools 使用，避免每次新增配置项都需修改 UseTools 重载签名</summary>
public interface IToolSetting
{
    /// <summary>工具调用最大轮次。防止工具调用无限递归，默认10</summary>
    Int32 ToolMaxIterations { get; }

    /// <summary>单条消息Token总限额。工具调用累计Token（含input+output）超过此值时停止继续调用，0表示不限制，默认500万</summary>
    Int32 ToolMaxTotalTokens { get; }

    /// <summary>工具结果最大字符数。超过此长度时自动截断并追加省略提示，0表示不限制</summary>
    Int32 ToolResultMaxChars { get; }
}
