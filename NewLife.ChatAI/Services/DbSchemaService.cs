using NewLife.Caching;
using NewLife.Log;
using XCode;
using XCode.DataAccessLayer;

namespace NewLife.ChatAI.Services;

/// <summary>数据库架构信息服务。从 XCode DAL 获取已注册的数据库表架构信息</summary>
/// <remarks>
/// 架构获取优先级（合并）：
/// 1. 已映射实体类 → 使用 EntityFactory.GetTables 获取字段/索引信息（含 Model.xml 注释），永久缓存
/// 2. 数据库原始表 → 使用 DAL.Tables 读取，缓存 10 分钟
/// 3. 同表名时优先使用实体类的元数据（经过人工修缮整理）
/// </remarks>
/// <param name="cacheProvider">缓存提供者，用于 DAL 表缓存</param>
/// <param name="log">日志</param>
public class DbSchemaService(ICacheProvider cacheProvider, ILog log)
{
    #region 缓存

    /// <summary>DAL 表缓存（进程内缓存），10 分钟过期</summary>
    private ICache DalTableCache => cacheProvider.InnerCache;

    /// <summary>实体表缓存（连接名→表列表），永久有效</summary>
    private Dictionary<String, IList<IDataTable>> _entityTableCache = new(StringComparer.OrdinalIgnoreCase);

    #endregion

    #region 表架构搜索

    /// <summary>搜索匹配关键字的数据库表，按连接名分组返回</summary>
    /// <param name="keywords">搜索关键字，多个用逗号或空格分隔，不能为空</param>
    /// <param name="connName">限定连接名，为空则搜索所有连接</param>
    /// <param name="maxResults">最大返回条数，默认 10</param>
    /// <returns>匹配的表字典（key=连接名, value=该连接下匹配的表列表）</returns>
    public IDictionary<String, IList<IDataTable>> SearchTables(String keywords, String? connName = null, Int32 maxResults = 10, IList<String>? failures = null)
    {
        if (keywords.IsNullOrEmpty()) throw new ArgumentNullException(nameof(keywords));

        // 解析关键字
        var kwList = new List<String>();
        foreach (var part in keywords.Split([',', '，', ' '], StringSplitOptions.RemoveEmptyEntries))
        {
            var kw = part.Trim();
            if (!kw.IsNullOrEmpty()) kwList.Add(kw);
        }
        if (kwList.Count == 0) throw new ArgumentException("关键字不能为空", nameof(keywords));

        // 获取所有连接的表（DAL 表 + 实体表合并，实体表优先）
        // 连接枚举：DAL.ConnStrs ∪ 启用的 DbAccessConfig（支持运行时动态连接，动态连接串先注册再扫描）
        var allTables = new List<IDataTable>();
        if (!connName.IsNullOrEmpty())
        {
            TryLoadTables(connName, allTables, failures);
        }
        else
        {
            foreach (var cn in GetSearchableConnNames())
            {
                TryLoadTables(cn, allTables, failures);
            }
        }

        // 四级 Tier 排序，取前 maxResults 条，按连接名分组返回
        // Tier 3=表名精确匹配, Tier 2=表名前缀匹配, Tier 1=表名包含匹配, Tier 0=注释匹配；同 Tier 按分数降序
        return allTables
            .Select(table =>
            {
                var bestTier = -1;
                var totalScore = 0;
                foreach (var kw in kwList)
                {
                    var (tier, score) = CalculateMatchLevel(kw, table.TableName ?? "", table.Description ?? "");
                    if (tier > bestTier) bestTier = tier;
                    totalScore += score;
                }
                return (Table: table, Score: totalScore, Tier: bestTier);
            })
            .Where(x => x.Tier >= 0)
            .OrderByDescending(x => x.Tier)
            .ThenByDescending(x => x.Score)
            .Take(maxResults)
            .Select(x => x.Table)
            .GroupBy(t => t.ConnName ?? "")
            .ToDictionary(g => g.Key, g => (IList<IDataTable>)g.ToList(), StringComparer.OrdinalIgnoreCase);
    }
    #endregion

    #region 辅助方法

    /// <summary>收集可搜索连接的名称列表。优先 DAL.ConnStrs，补充启用的 DbAccessConfig（动态连接串先注册再并入）</summary>
    /// <returns>去重后的连接名列表</returns>
    private List<String> GetSearchableConnNames()
    {
        var names = new List<String>();

        if (DAL.ConnStrs != null)
        {
            foreach (var cn in DAL.ConnStrs.Keys)
            {
                if (!cn.IsNullOrEmpty() && !names.Contains(cn, StringComparer.OrdinalIgnoreCase))
                    names.Add(cn);
            }
        }

        foreach (var cfg in DbAccessConfig.FindAllEnabled())
        {
            var cn = cfg.ConnName;
            if (cn.IsNullOrEmpty() || names.Contains(cn, StringComparer.OrdinalIgnoreCase)) continue;

            // 仅存在于配置表且带连接串的动态连接，先注册到 DAL 再扫描
            if (!cfg.ConnString.IsNullOrEmpty() && (DAL.ConnStrs == null || !DAL.ConnStrs.ContainsKey(cn)))
            {
                try
                {
                    var dbType = !cfg.DbType.IsNullOrEmpty() ? cfg.DbType : "SQLite";
                    DAL.AddConnStr(cn, cfg.ConnString, null, dbType);
                }
                catch (Exception ex)
                {
                    Log.Warn("[SearchTables] 连接 [{0}] 动态注册失败: {1}", cn, ex.Message);
                    continue;
                }
            }

            names.Add(cn);
        }

        return names;
    }

    /// <summary>尝试加载指定连接的全部表结构，失败时记录原因并继续</summary>
    /// <param name="connName">连接名</param>
    /// <param name="target">表列表收集目标</param>
    /// <param name="failures">失败原因收集列表（可为 null）</param>
    private void TryLoadTables(String connName, List<IDataTable> target, IList<String>? failures)
    {
        try
        {
            var tables = GetMergedTables(connName);

            // DAL 物理表可能未填充所属连接名，补齐后分组与权限判断（IsTableAllowed）才正确
            foreach (var t in tables)
            {
                if (t.ConnName.IsNullOrEmpty())
                    t.ConnName = connName;
            }

            target.AddRange(tables);
        }
        catch (Exception ex)
        {
            failures?.Add($"连接 [{connName}] 表结构获取失败: {ex.Message}");
            Log.Warn("[SearchTables] 获取连接 [{0}] 表结构失败: {1}", connName, ex.Message);
        }
    }

    /// <summary>获取指定连接的合并表列表。</summary>
    /// <remarks>
    /// 先从 DAL.Tables 获取数据库原始表，再从 EntityFactory.GetTables 获取实体映射表。
    /// 同表名时优先使用实体表的元数据（字段注释更准确），其次使用 DAL 表。
    /// 先精确匹配表名，再不区分大小写匹配，最后添加 DAL 中不存在的实体表。
    /// </remarks>
    private IList<IDataTable> GetMergedTables(String connName)
    {
        var dalTables = GetDalTables(connName);
        var entityTables = GetEntityTables(connName);

        if (entityTables.Count == 0) return dalTables;

        // 构建实体表查找（先精确，再忽略大小写）
        var exactLookup = new Dictionary<String, IDataTable>(StringComparer.Ordinal);
        var ignoreCaseLookup = new Dictionary<String, IDataTable>(StringComparer.OrdinalIgnoreCase);
        foreach (var et in entityTables)
        {
            var name = et.TableName;
            if (name.IsNullOrEmpty()) continue;

            exactLookup[name] = et;
            if (!ignoreCaseLookup.ContainsKey(name))
                ignoreCaseLookup[name] = et;
        }

        // 合并：实体表覆盖 DAL 表
        var merged = new List<IDataTable>();
        foreach (var dt in dalTables)
        {
            var name = dt.TableName;
            if (name.IsNullOrEmpty())
            {
                merged.Add(dt);
                continue;
            }

            // 优先精确匹配，其次忽略大小写
            if (exactLookup.TryGetValue(name, out var entityTable))
            {
                merged.Add(entityTable);
                exactLookup.Remove(name);
                ignoreCaseLookup.Remove(name);
            }
            else if (ignoreCaseLookup.TryGetValue(name, out entityTable))
            {
                merged.Add(entityTable);
                ignoreCaseLookup.Remove(name);
            }
            else
            {
                merged.Add(dt);
            }
        }

        // 添加 DAL 中不存在的实体表
        merged.AddRange(ignoreCaseLookup.Values);

        return merged;
    }

    /// <summary>获取指定连接的 DAL 表（物理表），缓存 10 分钟</summary>
    private IList<IDataTable> GetDalTables(String connName)
    {
        var key = $"DbSchema:DalTables:{connName}";
        var tables = DalTableCache.Get<IList<IDataTable>>(key);
        if (tables != null) return tables;

        var dal = DAL.Create(connName);
        tables = dal.Tables;

        DalTableCache.Set(key, tables, 600);
        return tables;
    }

    /// <summary>获取指定连接的实体表（实体映射），永久缓存</summary>
    private IList<IDataTable> GetEntityTables(String connName)
    {
        if (_entityTableCache.TryGetValue(connName, out var cached))
            return cached;

        var tables = EntityFactory.GetTables(connName, false);
        _entityTableCache[connName] = tables;
        return tables;
    }

    /// <summary>计算单个关键字与表名/注释的匹配层级和分数</summary>
    /// <remarks>
    /// 层级（Tier）用于排序优先级，分数用于同层级内排序：
    /// Tier 3 = 表名精确匹配，Tier 2 = 表名前缀匹配，Tier 1 = 表名包含匹配，Tier 0 = 注释匹配。
    /// 返回 (-1, 0) 表示无匹配。
    /// </remarks>
    private static (Int32 Tier, Int32 Score) CalculateMatchLevel(String keyword, String tableName, String description)
    {
        if (tableName.Equals(keyword, StringComparison.OrdinalIgnoreCase))
            return (3, 100);
        if (tableName.StartsWith(keyword, StringComparison.OrdinalIgnoreCase))
            return (2, 50);
        if (tableName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            return (1, 20);
        if (description.Equals(keyword, StringComparison.OrdinalIgnoreCase))
            return (0, 30);
        if (description.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            return (0, 10);

        return (-1, 0);
    }

    /// <summary>将 IDataTable 转换为 TableSchema（向后兼容）</summary>
    private static TableSchema ConvertToTableSchema(IDataTable table)
    {
        var schema = new TableSchema
        {
            TableName = table.TableName ?? table.Name ?? "",
            Description = table.Description ?? "",
            ConnName = table.ConnName ?? "",
            DbType = table.DbType + "",
            Columns = [],
        };

        foreach (var col in table.Columns)
        {
            schema.Columns.Add(new ColumnSchema
            {
                Name = col.Name ?? col.ColumnName ?? "",
                ColumnName = col.ColumnName ?? col.Name ?? "",
                DataType = col.DataType?.Name ?? "String",
                Description = col.Description ?? "",
                Length = col.Length,
                IsPrimaryKey = col.PrimaryKey,
                IsNullable = col.Nullable,
            });
        }

        return schema;
    }

    #endregion

    #region 日志

    private ILog Log { get; } = log;

    #endregion
}

/// <summary>表架构信息（对外输出）</summary>
public class TableSchema
{
    /// <summary>表名</summary>
    public String TableName { get; set; } = "";

    /// <summary>表注释</summary>
    public String Description { get; set; } = "";

    /// <summary>所属连接名</summary>
    public String ConnName { get; set; } = "";

    /// <summary>数据库类型</summary>
    public String DbType { get; set; } = "";

    /// <summary>字段列表</summary>
    public IList<ColumnSchema> Columns { get; set; } = [];
}

/// <summary>字段架构信息（对外输出）</summary>
public class ColumnSchema
{
    /// <summary>字段名（C#属性名）</summary>
    public String Name { get; set; } = "";

    /// <summary>数据库列名</summary>
    public String ColumnName { get; set; } = "";

    /// <summary>数据类型</summary>
    public String DataType { get; set; } = "";

    /// <summary>字段注释</summary>
    public String Description { get; set; } = "";

    /// <summary>长度</summary>
    public Int32 Length { get; set; }

    /// <summary>是否主键</summary>
    public Boolean IsPrimaryKey { get; set; }

    /// <summary>是否允许空</summary>
    public Boolean IsNullable { get; set; }
}
