using NewLife.ChatAI.Entity;
using NewLife.Log;
using XCode;

namespace NewLife.ChatAI.Services;

/// <summary>数据迁移工具。用于从旧表结构迁移到新表结构</summary>
public class DataMigrationService
{
    /// <summary>检查是否需要数据迁移</summary>
    /// <returns>true表示需要迁移，false表示无需迁移</returns>
    public static Boolean NeedMigration()
    {
        try
        {
            var oldTable = ModelConfig.Meta.Table.DataTable;
            var hasParentId = oldTable.Columns.Any(c => c.ColumnName.EqualIgnoreCase("ParentId"));
            return hasParentId;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>获取迁移提示信息</summary>
    /// <returns></returns>
    public static String GetMigrationTip()
    {
        return @"检测到旧的 ModelConfig 表结构（包含 ParentId 字段）。
新版本已将配置拆分为两张表：
1. ProviderConfig：存储提供商连接信息（Endpoint/ApiKey）
2. ModelConfig：存储模型配置，通过 ProviderId 关联提供商

请按以下步骤手动迁移数据：
1. 备份当前 ModelConfig 表数据
2. 提取 ParentId=0 的记录作为 ProviderConfig
3. 提取 ParentId>0 的记录作为 ModelConfig，设置 ProviderId
4. 对于没有子记录的顶级配置，也需创建对应的 ModelConfig 记录

或者联系开发团队获取自动迁移脚本。";
    }
}
