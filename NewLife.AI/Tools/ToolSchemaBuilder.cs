using System.Collections.Concurrent;
using System.Reflection;
using System.Xml;
using NewLife.AI.Models;

namespace NewLife.AI.Tools;

/// <summary>工具 Schema 构建器。通过反射和 XML 文档注释将 C# 方法转换为标准 <see cref="ChatTool"/> 定义</summary>
/// <remarks>
/// 转换规则：
/// <list type="bullet">
/// <item>方法 <c>&lt;summary&gt;</c> → <c>description</c></item>
/// <item>参数 <c>&lt;param name="x"&gt;</c> → 参数的 <c>description</c></item>
/// <item>有默认值的参数 → 从 <c>required</c> 数组排除</item>
/// <item>复杂类型参数 → 递归展开 <c>object</c> + <c>properties</c></item>
/// </list>
/// </remarks>
public static class ToolSchemaBuilder
{
    #region 静态

    /// <summary>XML 文档缓存。键为程序集位置路径</summary>
    private static readonly ConcurrentDictionary<String, XmlDocument?> _xmlCache = new(StringComparer.OrdinalIgnoreCase);

    #endregion

    #region 方法

    /// <summary>从方法信息构建 <see cref="ChatTool"/>，提取 XML 注释作为描述</summary>
    /// <param name="method">目标方法</param>
    /// <returns>ChatTool 定义</returns>
    public static ChatTool BuildFromMethod(MethodInfo method)
    {
        if (method == null) throw new ArgumentNullException(nameof(method));

        var attr = method.GetCustomAttribute<ToolDescriptionAttribute>();
        var toolName = (attr != null && attr.HasExplicitName)
            ? attr.Name
            : ToSnakeCase(method.Name.EndsWith("Async", StringComparison.Ordinal)
                ? method.Name[..^5]
                : method.Name);

        var xmlDoc = LoadXmlDoc(method.DeclaringType?.Assembly);
        var memberKey = GetMemberKey(method);
        var methodSummary = xmlDoc != null ? GetSummary(xmlDoc, memberKey) : null;
        var paramDocs = xmlDoc != null ? GetParamDocs(xmlDoc, memberKey) : null;

        var schema = BuildParameterSchema(method, paramDocs);

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
    private static Object? BuildParameterSchema(MethodInfo method, IDictionary<String, String>? paramDocs)
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

            var description = paramDocs != null && paramDocs.TryGetValue(p.Name, out var d) ? d : null;
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

    /// <summary>加载程序集对应的 XML 文档（同目录下同名 .xml 文件）</summary>
    private static XmlDocument? LoadXmlDoc(Assembly? assembly)
    {
        if (assembly == null) return null;
        var location = assembly.Location;
        if (String.IsNullOrEmpty(location)) return null;

        return _xmlCache.GetOrAdd(location, path =>
        {
            var xmlPath = Path.ChangeExtension(path, ".xml");
            if (!File.Exists(xmlPath)) return null;

            var doc = new XmlDocument();
            try { doc.Load(xmlPath); return doc; }
            catch { return null; }
        });
    }

    /// <summary>从 XML 文档提取成员的 <c>&lt;summary&gt;</c> 文本</summary>
    private static String? GetSummary(XmlDocument xmlDoc, String memberKey)
    {
        var node = xmlDoc.SelectSingleNode($"//member[@name='{memberKey}']");
        var summary = node?.SelectSingleNode("summary")?.InnerText;
        return summary?.Trim();
    }

    /// <summary>从 XML 文档提取成员所有 <c>&lt;param&gt;</c> 注释，返回参数名→描述字典</summary>
    private static IDictionary<String, String>? GetParamDocs(XmlDocument xmlDoc, String memberKey)
    {
        var node = xmlDoc.SelectSingleNode($"//member[@name='{memberKey}']");
        if (node == null) return null;

        var paramNodes = node.SelectNodes("param");
        if (paramNodes == null || paramNodes.Count == 0) return null;

        var dict = new Dictionary<String, String>(StringComparer.Ordinal);
        foreach (XmlNode p in paramNodes)
        {
            var name = p.Attributes?["name"]?.Value;
            var text = p.InnerText?.Trim();
            if (!String.IsNullOrEmpty(name) && !String.IsNullOrEmpty(text))
                dict[name] = text!;
        }
        return dict.Count > 0 ? dict : null;
    }

    /// <summary>生成方法的 XML 文档成员键，格式为 <c>M:Namespace.Class.Method(ParamType)</c></summary>
    private static String GetMemberKey(MethodInfo method)
    {
        var typeName = method.DeclaringType?.FullName ?? String.Empty;
        var paramList = String.Join(",", method.GetParameters()
            .Select(p => p.ParameterType.FullName ?? p.ParameterType.Name));
        return String.IsNullOrEmpty(paramList)
            ? $"M:{typeName}.{method.Name}"
            : $"M:{typeName}.{method.Name}({paramList})";
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
