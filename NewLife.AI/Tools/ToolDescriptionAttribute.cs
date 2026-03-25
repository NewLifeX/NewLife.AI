namespace NewLife.AI.Tools;

/// <summary>工具描述特性。标注在 C# 方法上，使其可被 <see cref="ToolRegistry"/> 注册为 AI 可调用工具</summary>
/// <remarks>
/// 方法的 XML 文档注释（<c>&lt;summary&gt;</c> 和 <c>&lt;param&gt;</c>）会被自动提取为工具描述。
/// <code>
/// [ToolDescription("get_weather")]
/// public async Task&lt;String&gt; GetWeatherAsync(String city)
/// {
///     // ...
/// }
/// </code>
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ToolDescriptionAttribute : Attribute
{
    /// <summary>工具名称。覆盖默认的方法名（自动转为 snake_case）</summary>
    public String Name { get; }

    /// <summary>初始化工具描述特性，使用指定工具名</summary>
    /// <param name="name">工具名称，供模型识别</param>
    public ToolDescriptionAttribute(String name)
    {
        if (String.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
        Name = name.Trim();
    }

    /// <summary>初始化工具描述特性，工具名称由 <see cref="ToolSchemaBuilder"/> 依据方法名自动生成</summary>
    public ToolDescriptionAttribute() => Name = String.Empty;

    /// <summary>是否通过构造函数显式指定了工具名称</summary>
    public Boolean HasExplicitName => Name.Length > 0;

    /// <summary>是否系统工具。true 时每次 LLM 请求自动携带，无需 @引用；false 时需在消息中 @工具名 显式引用</summary>
    public Boolean IsSystem { get; set; }
}
