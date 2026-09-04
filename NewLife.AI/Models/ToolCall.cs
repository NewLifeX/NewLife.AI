namespace NewLife.AI.Models;

/// <summary>工具调用</summary>
public class ToolCall
{
    /// <summary>流式下标。流式输出时用于标识属于哪个工具调用，非流式场景可忽略</summary>
    public Int32? Index { get; set; }

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
