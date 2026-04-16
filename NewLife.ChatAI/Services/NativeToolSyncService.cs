using System.ComponentModel;
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
/// <item>首次发现（Name 不存在）→ INSERT，按 ToolDescriptionAttribute / DisplayName / XML 注释初始化</item>
/// <item>已存在且 IsLocked=false → UPDATE Description、Parameters、Triggers、ClassName、MethodName（不主动恢复 Enable）</item>
/// <item>已存在且 IsLocked=true  → 只 UPDATE ClassName、MethodName（手工调整内容受保护）</item>
/// <item>若 ToolDescriptionAttribute 显式禁用某工具 → 同步时强制关闭 Enable，避免误触发</item>
/// </list>
/// </remarks>
/// <remarks>实例化内置工具同步服务</remarks>
/// <param name="registry">工具注册表，包含所有已注册工具的类型信息</param>
public class NativeToolSyncService(ToolRegistry registry) : IHostedService
{
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
        // 通过 ToolSchemaBuilder 构建 ChatTool（方法/参数 [Description] 优先，无则读 XML 注释）
        var chatTool = ToolSchemaBuilder.BuildFromMethod(method);
        var toolName = chatTool.Function!.Name;
        var description = chatTool.Function.Description;
        var parametersJson = chatTool.Function.Parameters?.ToJson();
        var attr = method.GetCustomAttribute<ToolDescriptionAttribute>()!;

        // 首次发现则新建，填充特性声明的启用状态
        var existing = NativeTool.FindByName(toolName);
        var isNew = existing == null;
        var record = existing ?? new NativeTool
        {
            Name = toolName,
            Enable = attr.Enable,
            IsLocked = false,
        };

        if (!attr.Enable) record.Enable = false;

        // DisplayName 解析：[DisplayName] 标注 > XML 注释句号前中文 > 工具名
        var displayNameAttr = method.GetCustomAttribute<DisplayNameAttribute>();
        var resolvedDisplayName = displayNameAttr?.DisplayName;
        if (String.IsNullOrEmpty(resolvedDisplayName) && !String.IsNullOrEmpty(description))
        {
            var idx = description.IndexOf('。');
            if (idx > 0) resolvedDisplayName = description[..idx];
        }
        if (String.IsNullOrEmpty(resolvedDisplayName))
            resolvedDisplayName = toolName;
        // 新增记录时初始化 DisplayName；或存在明确的 [DisplayName] 标注且未锁定时更新
        if (isNew || (!record.IsLocked && displayNameAttr != null))
            record.DisplayName = resolvedDisplayName;

        // 始终更新类/方法定位信息
        record.ClassName = type.FullName;
        record.MethodName = method.Name;

        // 未锁定时才更新描述和参数，保护手工调整的内容
        if (!record.IsLocked)
        {
            record.IsSystem = attr.IsSystem;
            record.Description = description;
            record.Parameters = parametersJson;
            record.Triggers = NormalizeTriggers(attr.Triggers);
        }

        record.Save();
    }

    private static String? NormalizeTriggers(String? triggers)
    {
        if (String.IsNullOrWhiteSpace(triggers)) return null;

        var words = triggers.Split([',', '，'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(e => !String.IsNullOrWhiteSpace(e))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return words.Length == 0 ? null : String.Join(",", words);
    }

    #endregion
}
