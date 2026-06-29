namespace NewLife.AI.Services;

/// <summary>工具调用配置的可变实现。供 SubAgent、CodingAgent、单元测试等需要动态覆盖默认值的场景使用</summary>
public class ToolSetting : IToolSetting
{
    /// <summary>工具调用最大轮次。默认10</summary>
    public Int32 ToolMaxIterations { get; set; } = 10;

    /// <summary>单条消息Token总限额。0表示不限制</summary>
    public Int32 ToolMaxTotalTokens { get; set; }

    /// <summary>工具结果最大字符数。0表示不限制</summary>
    public Int32 ToolResultMaxChars { get; set; }
}
