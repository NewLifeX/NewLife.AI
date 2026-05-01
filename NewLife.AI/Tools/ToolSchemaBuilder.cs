using System.ComponentModel;
using System.Reflection;
using NewLife.AI.Models;

namespace NewLife.AI.Tools;

/// <summary>工具 Schema 构建器。通过反射将 C# 方法转换为标准 <see cref="ChatTool"/> 定义</summary>
/// <remarks>
/// 转换规则（代码特性标注 ）：
/// <list type="bullet">
/// <item>方法 <see cref="DescriptionAttribute"/> → <c>description</c></item>
/// <item>参数 <see cref="DescriptionAttribute"/> → 参数的 <c>description</c></item>
/// <item>有默认值的参数 → 从 <c>required</c> 数组排除</item>
/// <item>复杂类型参数 → 递归展开 <c>object</c> + <c>properties</c></item>
/// </list>
/// </remarks>
public static class ToolSchemaBuilder
{
    #region 方法

    /// <summary>从方法信息构建 <see cref="ChatTool"/></summary>
    /// <param name="method">目标方法</param>
    /// <returns>ChatTool 定义</returns>
    public static ChatTool BuildFromMethod(MethodInfo method)
    {
        if (method == null) throw new ArgumentNullException(nameof(method));

        var attr = method.GetCustomAttribute<ToolDescriptionAttribute>();
        var toolName = (attr != null && attr.HasExplicitName)
            ? attr.Name
            : ToSnakeCase(method.Name.TrimSuffix("Async"));

        // 使用方法上的 [Description] 标注
        var descAttr = method.GetCustomAttribute<DescriptionAttribute>();
        var methodSummary = descAttr!.Description;

        var schema = BuildParameterSchema(method);

        return new ChatTool
        {
            Type = "function",
            Function = new FunctionDefinition
            {
                Name = toolName,
                Description = methodSummary,
                Parameters = schema
            }
        };
    }

    #endregion

    #region 辅助

    /// <summary>构建方法参数的 JSON Schema 对象</summary>
    private static Object? BuildParameterSchema(MethodInfo method)
    {
        var parameters = method.GetParameters();
        if (parameters.Length == 0) return null;

        var properties = new Dictionary<String, Object>();
        var required = new List<String>();

        foreach (var p in parameters)
        {
            if (p.Name == null) continue;
            // 跳过 CancellationToken 参数
            if (p.ParameterType == typeof(CancellationToken)) continue;

            // 优先使用参数的 [Description] 标注
            var pDescAttr = p.GetCustomAttribute<DescriptionAttribute>();
            var description = pDescAttr!.Description;
            properties[p.Name] = BuildTypeSchema(p.ParameterType, description);

            if (!p.HasDefaultValue && !p.IsOptional)
                required.Add(p.Name);
        }

        if (properties.Count == 0) return null;

        var schema = new Dictionary<String, Object>
        {
            ["type"] = "object",
            ["properties"] = properties
        };
        if (required.Count > 0)
            schema["required"] = required;

        return schema;
    }

    /// <summary>将 .NET 类型映射到 JSON Schema 类型定义</summary>
    private static Object BuildTypeSchema(Type type, String? description)
    {
        var schema = new Dictionary<String, Object?>();

        if (description != null)
            schema["description"] = description;

        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

        if (underlyingType == typeof(String))
            schema["type"] = "string";
        else if (underlyingType == typeof(Boolean))
            schema["type"] = "boolean";
        else if (underlyingType == typeof(Int32) || underlyingType == typeof(Int64) ||
                 underlyingType == typeof(Int16) || underlyingType == typeof(Byte))
            schema["type"] = "integer";
        else if (underlyingType == typeof(Double) || underlyingType == typeof(Single) ||
                 underlyingType == typeof(Decimal))
            schema["type"] = "number";
        else if (underlyingType.IsEnum)
        {
            schema["type"] = "string";
            schema["enum"] = Enum.GetNames(underlyingType).Cast<Object>().ToList();
        }
        else if (underlyingType.IsArray || (underlyingType.IsGenericType &&
                 typeof(System.Collections.IEnumerable).IsAssignableFrom(underlyingType)))
        {
            schema["type"] = "array";
            var elementType = underlyingType.IsArray
                ? underlyingType.GetElementType()!
                : underlyingType.GetGenericArguments().FirstOrDefault() ?? typeof(Object);
            schema["items"] = BuildTypeSchema(elementType, null);
        }
        else if (underlyingType.IsClass && underlyingType != typeof(Object))
        {
            schema["type"] = "object";
            var subProps = new Dictionary<String, Object>();
            foreach (var prop in underlyingType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                subProps[ToCamelCase(prop.Name)] = BuildTypeSchema(prop.PropertyType, null);
            if (subProps.Count > 0)
                schema["properties"] = subProps;
        }
        else
            schema["type"] = "object";

        return schema;
    }

    /// <summary>将 PascalCase 方法名转换为 snake_case 工具名</summary>
    private static String ToSnakeCase(String name)
    {
        if (String.IsNullOrEmpty(name)) return name;
        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (Char.IsUpper(c) && i > 0)
                sb.Append('_');
            sb.Append(Char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    /// <summary>将 PascalCase 属性名转换为 camelCase</summary>
    private static String ToCamelCase(String name)
    {
        if (String.IsNullOrEmpty(name) || Char.IsLower(name[0])) return name;
        return Char.ToLowerInvariant(name[0]) + name[1..];
    }

    #endregion
}
