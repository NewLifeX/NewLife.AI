using NewLife.Data;

namespace NewLife.AI.Tools;

/// <summary>工具结果默认实现。支持 String 隐式转换和 IExtend 元数据</summary>
/// <remarks>
/// <para>普通工具（99% 场景）：直接 return 字符串，隐式转换为 ToolResult，内容受众为 Both。</para>
/// <para>可视化工具（show_xxx）：显式构造 ToolResult，传入 ToolContent.ForLlm + ToolContent.ForUser/Svg。</para>
/// <para>失败场景：设置 IsError = true。</para>
/// </remarks>
public class ToolResult : IToolResult, IExtend
{
    #region 属性
    /// <summary>内容块列表</summary>
    public IList<ToolContent> Contents { get; } = [];

    /// <summary>是否失败</summary>
    public Boolean IsError { get; init; }

    /// <summary>扩展元数据。如执行耗时、来源等</summary>
    public IDictionary<String, Object?> Items { get; } = new Dictionary<String, Object?>();

    /// <summary>通过键读写扩展元数据</summary>
    /// <param name="key">键</param>
    public Object? this[String key]
    {
        get => Items.TryGetValue(key, out var v) ? v : null;
        set => Items[key] = value;
    }
    #endregion

    #region 构造
    /// <summary>实例化空结果</summary>
    public ToolResult() { }

    /// <summary>从内容块列表构造</summary>
    public ToolResult(params ToolContent[] contents)
    {
        foreach (var c in contents) Contents.Add(c);
    }

    /// <summary>从文本构造（Both 受众）</summary>
    public ToolResult(String content)
    {
        Contents.Add(ToolContent.ForBoth(content));
    }
    #endregion

    #region 转换
    /// <summary>String → ToolResult 隐式转换。现有 return "result" 代码零改动</summary>
    public static implicit operator ToolResult(String content) => new(content);
    #endregion
}
