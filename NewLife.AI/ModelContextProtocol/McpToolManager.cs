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

        var sb = Pool.StringBuilder.Get();
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (Char.IsUpper(c))
            {
                if (sb.Length > 0 && i > 0 && !Char.IsUpper(name[i - 1])) sb.Append('_');
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
