using System.Reflection;
using NewLife.AI.Tools;
using NewLife.Log;
using NewLife.Serialization;
using NewLife.ChatAI.Entity;

namespace NewLife.ChatAI.Services;

/// <summary>内置工具同步服务。ChatAI 启动时扫描所有带 ToolDescription 标注的工具方法，
/// 将工具名称、描述、参数 Schema 持久化到 NativeTool 表，以消除对 XML 文件的运行时依赖</summary>
/// <remarks>
/// 同步规则：
/// <list type="bullet">
/// <item>首次发现（Name 不存在）→ INSERT，Enable 默认 true，按内置 Seed 预填 Providers/Endpoint</item>
/// <item>已存在且 IsLocked=false → UPDATE Description、Parameters、ClassName、MethodName（不修改 Enable）</item>
/// <item>已存在且 IsLocked=true  → 只 UPDATE ClassName、MethodName（手工调整内容受保护）</item>
/// </list>
/// </remarks>
/// <remarks>实例化内置工具同步服务</remarks>
/// <param name="registry">工具注册表，包含所有已注册工具的类型信息</param>
public class NativeToolSyncService(ToolRegistry registry) : IHostedService
{
    #region 种子配置

    // 各工具的默认 Providers / Endpoint，首次插入时使用
    private static readonly Dictionary<String, (String Providers, String Endpoint, String DisplayName)> _seeds
        = new(StringComparer.OrdinalIgnoreCase)
        {
            ["get_current_time"] = ("", "", "当前时间"),
            ["calculate"] = ("", "", "数学计算"),
            ["query_date_info"] = ("", "", "日期节假日查询"),
            ["get_ip_location"] = ("pconline,ipapi,newlife", "https://ai.newlifex.com", "IP归属地"),
            ["get_weather"] = ("nmc,wttr,newlife", "https://ai.newlifex.com", "天气查询"),
            ["translate"] = ("mymemory,newlife", "https://ai.newlifex.com", "文本翻译"),
            ["web_search"] = ("bing,duckduckgo,newlife", "https://ai.newlifex.com", "网络搜索"),
            ["web_fetch"] = ("direct,newlife", "https://ai.newlifex.com", "网页抓取"),
            ["get_user_memories"] = ("", "", "用户记忆查询"),
            ["get_user_trust"] = ("", "", "用户信任查询"),
            ["fuse_knowledge"] = ("", "", "知识融合"),
            ["evolve_skill"] = ("", "", "技能演化"),
            ["review_knowledge"] = ("", "", "知识审核"),
            ["get_current_user"] = ("", "", "当前用户信息"),
        };

    #endregion

    #region IHostedService

    /// <summary>启动时同步工具信息到数据库</summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = Task.Run(() => SyncAll(), cancellationToken);

        return Task.CompletedTask;
    }

    /// <summary>停止时无需特殊处理</summary>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    #endregion

    #region 同步

    /// <summary>扫描所有内置工具类并同步到数据库</summary>
    private void SyncAll()
    {
        try
        {
            var count = 0;
            foreach (var type in registry.RegisteredTypes)
            {
                count += SyncType(type);
            }
            if (count > 0)
                XTrace.WriteLine("内置工具同步完成，处理 {0} 个工具", count);
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
        }
    }

    /// <summary>扫描指定类型中所有标注 ToolDescriptionAttribute 的方法，写入数据库</summary>
    /// <param name="type">工具服务类型</param>
    /// <returns>处理的工具数量</returns>
    private static Int32 SyncType(Type type)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetCustomAttribute<ToolDescriptionAttribute>() != null)
            .ToList();

        var count = 0;
        foreach (var method in methods)
        {
            try
            {
                SyncMethod(type, method);
                count++;
            }
            catch (Exception ex)
            {
                XTrace.WriteException(ex);
            }
        }
        return count;
    }

    /// <summary>将单个工具方法的信息同步到 NativeTool 表</summary>
    private static void SyncMethod(Type type, MethodInfo method)
    {
        // 通过 ToolSchemaBuilder 构建 ChatTool（会尝试读取 XML 注释）
        var chatTool = ToolSchemaBuilder.BuildFromMethod(method);
        var toolName = chatTool.Function!.Name;
        var description = chatTool.Function.Description;
        var parametersJson = chatTool.Function.Parameters?.ToJson();

        // 首次发现则新建，填充种子配置（DisplayName、Providers、远程地址）
        _seeds.TryGetValue(toolName, out var seed);
        var record = NativeTool.FindByName(toolName) ?? new NativeTool
        {
            Name = toolName,
            Enable = true,
            IsLocked = false,
            Providers = seed.Providers,
            Endpoint = seed.Endpoint,
        };

        if (!toolName.IsNullOrEmpty()) record.DisplayName = toolName;

        // 始终更新类/方法定位信息
        record.ClassName = type.FullName;
        record.MethodName = method.Name;

        // 未锁定时才更新描述和参数，保护手工调整的内容
        var attr = method.GetCustomAttribute<ToolDescriptionAttribute>()!;
        if (!record.IsLocked)
        {
            record.IsSystem = attr.IsSystem;
            record.Description = description;
            record.Parameters = parametersJson;
        }

        record.Save();
    }

    #endregion
}
