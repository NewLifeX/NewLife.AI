using NewLife.Collections;
using NewLife.Remoting;

namespace NewLife.AI.ModelContextProtocol;

internal class McpToolManager(IServiceProvider serviceProvider) : ApiManager(serviceProvider)
{
    public override void Add(ApiAction api)
    {
        api.Name = ToSnakeCase(api.Method.Name);

        base.Add(api);
    }

    private static String ToSnakeCase(String name)
    {
        if (String.IsNullOrEmpty(name)) return name;

        // 去掉 Async 后缀（对齐官方 SDK：异步方法工具名不带 _async 后缀）
        if (name.EndsWith("Async", StringComparison.Ordinal) && name.Length > 5)
            name = name[..^5];

        var sb = Pool.StringBuilder.Get();
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (Char.IsUpper(c))
            {
                // 大写字母前插入下划线（首字母除外）：
                // - 前一个字符是小写（PascalCase 边界：GetName → get_name）
                // - 前一个字符是大写、后一个字符是小写（缩写末尾：GetAPIKey → get_api_key，避免 get_apikey）
                if (sb.Length > 0 && i > 0 &&
                    (Char.IsLower(name[i - 1]) ||
                     (Char.IsUpper(name[i - 1]) && i + 1 < name.Length && Char.IsLower(name[i + 1]))))
                    sb.Append('_');
                sb.Append(Char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.Return(true);
    }
}
