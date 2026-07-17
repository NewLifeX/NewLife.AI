using System.ComponentModel;
using System.Data;
using System.Text;
using System.Text.RegularExpressions;
using NewLife;
using NewLife.Data;
using NewLife.AI.Tools;
using NewLife.Log;
using NewLife.Serialization;
using XCode;
using XCode.DataAccessLayer;

namespace NewLife.ChatAI.Tools;

/// <summary>数据库查询工具服务。提供 search_table 和 query_sql 两个 AI 工具</summary>
/// <remarks>
/// 工具1 search_table：输入关键字搜索匹配的数据库表结构
/// 工具2 query_sql：传入连接名和SQL，执行查询后返回 JSON 结果集
/// </remarks>
/// <param name="schemaService">架构信息服务</param>
/// <param name="log">日志</param>
public class DbQueryToolService(DbSchemaService schemaService, ILog log)
{
    #region 工具方法

    /// <summary>根据关键字搜索匹配的数据库表结构</summary>
    /// <param name="keywords">搜索关键字</param>
    /// <param name="connName">限定连接名</param>
    /// <param name="context">工具调用上下文</param>
    /// <returns>匹配的表结构列表</returns>
    [ToolDescription("search_table", IsSystem = false,
        Triggers = "表结构,数据库表,表字段,数据库有哪些表,有什么表",
        AssistantTriggers = "数据库表,表结构,search_table,数据库连接,连接名")]
    [DisplayName("搜索数据库表")]
    [Description("根据关键字搜索匹配的数据库表结构。返回包含两部分：① 表→连接映射表（Markdown表格，明确每个表属于哪个连接，同名表在不同连接下独立列出）；② 各连接下的表结构 XML 详情。后续 query_sql 须使用映射表中指定的连接名")]
    public String SearchTable(
        [Description("搜索关键字，多个用逗号或空格分隔")] String keywords,
        [Description("限定连接名，为空则搜索所有连接")] String? connName = null,
        ToolCallContext? context = null)
    {
        if (keywords.IsNullOrEmpty()) throw new ArgumentNullException(nameof(keywords), "keywords 不能为空");

        log.Info("[SearchTable] keywords={0}, connName={1}", keywords, connName);

        var roleIds = GetRoleIds(context);
        var groupedTables = schemaService.SearchTables(keywords, connName);

        // 访问控制过滤：对每个连接下的表列表逐表检查
        var filtered = new Dictionary<String, IList<IDataTable>>(StringComparer.OrdinalIgnoreCase);
        var totalMatched = 0;
        foreach (var kv in groupedTables)
        {
            totalMatched += kv.Value.Count;

            var allowed = new List<IDataTable>();
            foreach (var table in kv.Value)
            {
                if (DbAccessConfig.IsTableAllowed(kv.Key, table.TableName ?? "", roleIds))
                    allowed.Add(table);
            }
            if (allowed.Count > 0)
                filtered[kv.Key] = allowed;
        }

        log.Info("[SearchTable] 匹配 {0} 个表（过滤后 {1} 个）", totalMatched, filtered.Sum(kv => kv.Value.Count));

        return BuildGroupedResult(filtered, connName, roleIds);
    }

    /// <summary>在指定数据库连接上执行SQL查询，返回结果集</summary>
    /// <param name="connName">数据库连接名</param>
    /// <param name="sql">SQL语句（仅允许SELECT/INSERT/UPDATE）</param>
    /// <param name="context">工具调用上下文</param>
    /// <returns>查询结果（JSON格式）</returns>
    [ToolDescription("query_sql", IsSystem = false,
        Triggers = "执行SQL,运行SQL,SQL查询,SQL语句",
        AssistantTriggers = "SQL查询,执行查询,query_sql,SQL语句")]
    [DisplayName("查询数据库")]
    [Description("在指定数据库连接上执行SQL查询，返回结果集。仅允许SELECT和安全的INSERT/UPDATE操作，禁止DDL和DELETE。connName 必须与 search_table 返回的映射表中'所属连接'列一致")]
    public String QuerySql(
        [Description("数据库连接名（必须与 search_table 返回的映射表中'所属连接'列一致）")] String connName,
        [Description("要执行的SQL语句（仅允许SELECT/INSERT/UPDATE）")] String sql,
        ToolCallContext? context = null)
    {
        if (connName.IsNullOrEmpty()) throw new ArgumentNullException(nameof(connName), "connName 不能为空");
        if (sql.IsNullOrEmpty()) throw new ArgumentNullException(nameof(sql), "sql 不能为空");

        log.Info("[QuerySql] connName={0}, sql.length={1}", connName, sql.Length);

        // 1. SQL 安全检查
        ValidateSql(sql);

        // 2. 表权限校验
        var tableNames = ExtractTableNames(sql);
        var roleIds = GetRoleIds(context);
        foreach (var tableName in tableNames)
        {
            if (!DbAccessConfig.IsTableAllowed(connName, tableName, roleIds))
                throw new InvalidOperationException($"无权访问表 [{tableName}]，连接 {connName}");
        }

        // 3. 获取 DAL
        var dal = GetDal(connName);
        if (dal == null)
        {
            var available = GetAvailableConnNames(null, roleIds);
            var hint = available.Count > 0 ? $"，可用连接: {String.Join(", ", available)}" : "";
            throw new InvalidOperationException($"无法获取数据库连接 [{connName}]，请检查连接配置{hint}");
        }

        // 4. 执行SQL
        try
        {
            var table = dal.Query(sql);

            if (table == null || table.Rows == null || table.Rows.Count == 0)
                return new { rows = Array.Empty<Object>(), columns = Array.Empty<Object>(), total = 0 }.ToJson();

            const Int32 maxRows = 1000;
            var rowCount = table.Rows.Count;
            var displayRows = Math.Min(rowCount, maxRows);

            // 构建列信息
            var columns = new List<Object>();
            for (var i = 0; i < table.Columns.Length; i++)
            {
                var dataType = table.Types?[i].Name ?? "String";
                columns.Add(new { name = table.Columns[i], dataType });
            }

            // 构建行数据
            var rows = new List<Object>();
            for (var i = 0; i < displayRows; i++)
            {
                var row = new Dictionary<String, Object?>();
                var values = table.Rows[i];
                for (var j = 0; j < table.Columns.Length; j++)
                {
                    var val = values[j];
                    row[table.Columns[j]] = val == DBNull.Value ? null : val;
                }
                rows.Add(row);
            }

            if (rowCount > maxRows)
                log.Warn("[QuerySql] 结果 {0} 行，截断至 {1} 行", rowCount, maxRows);

            log.Info("[QuerySql] 返回 {0} 行 {1} 列", displayRows, table.Columns.Length);

            return new { rows = rows.ToArray(), columns = columns.ToArray(), total = rowCount }.ToJson();
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            log.Error("[QuerySql] 执行SQL失败：{0}", ex.Message);
            throw new InvalidOperationException($"SQL执行失败：{ex.Message}", ex);
        }
    }

    #endregion

    #region SQL安全

    private static readonly HashSet<String> _forbiddenKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "DROP", "ALTER", "TRUNCATE", "CREATE",
        "EXEC", "EXECUTE", "GRANT", "REVOKE", "DENY",
        "BACKUP", "RESTORE", "KILL", "SHUTDOWN",
    };

    /// <summary>验证SQL安全性</summary>
    private static void ValidateSql(String sql)
    {
        var cleaned = RemoveStringLiterals(sql);
        cleaned = RemoveComments(cleaned);
        var upperSql = cleaned.ToUpperInvariant();

        foreach (var kw in _forbiddenKeywords)
        {
            if (Regex.IsMatch(upperSql, $@"\b{kw}\b", RegexOptions.None, TimeSpan.FromSeconds(1)))
                throw new InvalidOperationException($"SQL中包含禁止的操作 [{kw}]，仅允许 SELECT/INSERT/UPDATE");
        }
    }

    private static String RemoveStringLiterals(String sql)
        => Regex.Replace(sql, @"'[^']*'", "''", RegexOptions.None, TimeSpan.FromSeconds(1));

    private static String RemoveComments(String sql)
    {
        sql = Regex.Replace(sql, @"--[^\r\n]*", "", RegexOptions.None, TimeSpan.FromSeconds(1));
        sql = Regex.Replace(sql, @"/\*[\s\S]*?\*/", "", RegexOptions.None, TimeSpan.FromSeconds(1));
        return sql;
    }

    /// <summary>提取SQL中的表名</summary>
    private static HashSet<String> ExtractTableNames(String sql)
    {
        var tables = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
        var matches = Regex.Matches(sql, @"\b(?:FROM|JOIN|INTO|UPDATE)\s+[\[`""]?(\w+)",
            RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));

        foreach (Match m in matches)
        {
            var name = m.Groups[1].Value;
            if (!name.IsNullOrEmpty() && !IsSqlKeyword(name))
                tables.Add(name);
        }

        return tables;
    }

    private static Boolean IsSqlKeyword(String word)
    {
        return word.Equals("SELECT", StringComparison.OrdinalIgnoreCase) ||
               word.Equals("SET", StringComparison.OrdinalIgnoreCase) ||
               word.Equals("WHERE", StringComparison.OrdinalIgnoreCase) ||
               word.Equals("VALUES", StringComparison.OrdinalIgnoreCase) ||
               word.Equals("AS", StringComparison.OrdinalIgnoreCase) ||
               word.Equals("ON", StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region 数据库连接

    /// <summary>获取指定连接名的 DAL，支持从 DbAccessConfig 动态添加连接</summary>
    /// <param name="connName">连接名</param>
    /// <returns>DAL 实例，若无法找到则返回 null</returns>
    private static DAL? GetDal(String connName)
    {
        // 优先查 DbAccessConfig 数据表（运行时可能修改连接字符串）
        var cfg = DbAccessConfig.FindEnabledByConnName(connName);
        if (cfg != null)
        {
            // 有自定义连接字符串时动态注册
            if (!cfg.ConnString.IsNullOrEmpty())
            {
                var dbType = !cfg.DbType.IsNullOrEmpty() ? cfg.DbType : "SQLite";
                DAL.AddConnStr(connName, cfg.ConnString, null, dbType);
            }
            return DAL.Create(connName);
        }

        // 回退检查 DAL.ConnStrs
        if (DAL.ConnStrs != null && DAL.ConnStrs.ContainsKey(connName))
            return DAL.Create(connName);

        return null;
    }

    #endregion

    #region 辅助

    /// <summary>获取用户角色ID列表</summary>
    private static Int32[] GetRoleIds(ToolCallContext? context)
    {
        var userId = context?.Request?.UserId.ToInt() ?? 0;
        if (userId <= 0) return [];

        try
        {
            var user = XCode.Membership.ManageProvider.Provider.FindByID(userId);
            if (user == null) return [];

            // 反射获取角色ID字段，兼容不同版本的 IManageUser
            var roleProp = user.GetType().GetProperty("RoleIDs")
                           ?? user.GetType().GetProperty("RoleIds");
            if (roleProp == null) return [];

            var val = roleProp.GetValue(user) as String;
            if (val.IsNullOrEmpty()) return [];

            return val.Split(',').Select(s => s.ToInt()).Where(id => id > 0).ToArray();
        }
        catch
        {
            return [];
        }
    }

    /// <summary>收集可用连接名列表</summary>
    /// <param name="connName">限定连接名，为空则收集所有</param>
    /// <param name="roleIds">角色ID列表</param>
    /// <returns>去重排序后的连接名列表</returns>
    private static List<String> GetAvailableConnNames(String? connName, Int32[] roleIds)
    {
        var names = new HashSet<String>(StringComparer.OrdinalIgnoreCase);

        if (!connName.IsNullOrEmpty())
        {
            names.Add(connName);
            return [.. names.OrderBy(n => n)];
        }

        // 从 DbAccessConfig 获取（按角色过滤）
        var configs = DbAccessConfig.FindAllByRole(roleIds);
        foreach (var cfg in configs)
        {
            if (!cfg.ConnName.IsNullOrEmpty())
                names.Add(cfg.ConnName);
        }

        // 补充 DAL.ConnStrs 中未在配置中出现的连接
        if (DAL.ConnStrs != null)
        {
            foreach (var kv in DAL.ConnStrs)
            {
                if (!kv.Key.IsNullOrEmpty())
                    names.Add(kv.Key);
            }
        }

        return [.. names.OrderBy(n => n)];
    }

    /// <summary>构建单个连接的头信息</summary>
    /// <param name="connName">连接名</param>
    /// <returns>连接头文本</returns>
    private static String BuildConnectionHeader(String connName)
    {
        try
        {
            var dal = DAL.Create(connName);
            var dbType = dal.DbType + "";
            var version = dal.Db?.ServerVersion ?? "-";
            return $"## 连接: {connName} ({dbType}, {version})";
        }
        catch
        {
            return $"## 连接: {connName} (不可用)";
        }
    }

    /// <summary>按连接分组构建表结构搜索结果</summary>
    /// <param name="groupedTables">已按连接名分组的表字典（key=连接名, value=该连接下的表列表）</param>
    /// <param name="connName">限定连接名</param>
    /// <param name="roleIds">角色ID列表</param>
    /// <returns>分组后的搜索结果文本</returns>
    private static String BuildGroupedResult(IDictionary<String, IList<IDataTable>> groupedTables, String? connName, Int32[] roleIds)
    {
        if (groupedTables.Count == 0)
        {
            var availableConns = GetAvailableConnNames(connName, roleIds);
            var connList = availableConns.Count > 0
                ? "可用连接: " + String.Join(", ", availableConns)
                : "无可用连接";
            return $"未找到匹配表。{connList}";
        }

        // 保持 SearchTables 的全局排序（精确匹配的连接在前，模糊匹配在后）
        var groups = groupedTables.ToList();

        var sb = new StringBuilder();

        // 1. 表→连接映射摘要（AI 后续调用 query_sql 时据此选择正确连接）
        // 表顺序与 SearchTables 返回一致：精确匹配优先，同级按相关度降序
        sb.AppendLine("## 表→连接映射");
        sb.AppendLine("| 表名 | 所属连接 | 数据库类型 | 说明 |");
        sb.AppendLine("|------|---------|-----------|------|");
        foreach (var kv in groups)
        {
            var cn = kv.Key;
            var dbType = GetConnectionDbType(cn);
            foreach (var table in kv.Value)
            {
                var tableName = table.TableName ?? "";
                var desc = table.Description ?? "";
                sb.AppendLine($"| {tableName} | {cn} | {dbType} | {desc} |");
            }
        }
        sb.AppendLine();

        // 2. 分隔：详细表结构 XML
        sb.AppendLine("---");

        // 3. 每个连接的表结构详情
        foreach (var kv in groups)
        {
            var header = BuildConnectionHeader(kv.Key);
            sb.AppendLine(header);

            var xml = DAL.Export(kv.Value);
            if (!xml.IsNullOrEmpty())
                sb.AppendLine(xml);
        }

        return sb.ToString();
    }

    /// <summary>获取连接的数据库类型（仅名称，不含版本）</summary>
    private static String GetConnectionDbType(String connName)
    {
        try
        {
            var dal = DAL.Create(connName);
            return dal.DbType + "";
        }
        catch
        {
            return "-";
        }
    }

    #endregion

    #region 日志

    private ILog Log { get; } = log;

    #endregion
}
