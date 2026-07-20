namespace NewLife.AI.Tools;

/// <summary>参数别名特性。标注在工具方法的参数上，声明该参数的备选名称。
/// 当 LLM 使用别名而非参数原名传参时，<see cref="ToolRegistry"/> 的参解析逻辑会用别名匹配。</summary>
/// <remarks>
/// 别名仅用于服务端参数解析 fallback，不会出现在发送给 LLM 的 JSON Schema 中，不增加请求体积。
/// <code>
/// [ToolDescription("query_sql")]
/// public String QuerySql(
///     String connName,
///     [Description("SQL语句")] [ParameterAlias("sql", "sqlStatement")] String query)
/// </code>
/// </remarks>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class ParameterAliasAttribute : Attribute
{
    /// <summary>参数备选名称列表（不包含 C# 参数本身的名称）</summary>
    public String[] Aliases { get; }

    /// <summary>初始化参数别名特性</summary>
    /// <param name="aliases">备选名称，不包含 C# 参数本身的名称</param>
    public ParameterAliasAttribute(params String[] aliases) => Aliases = aliases;
}
