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
    [Description("根据关键字搜索匹配的数据库表结构，返回表名、注释、字段信息、所属连接和数据库类型")]
    public String SearchTable(
        [Description("搜索关键字，多个用逗号或空格分隔")] String keywords,
        [Description("限定连接名，为空则搜索所有连接")] String? connName = null,
        ToolCallContext? context = null)
    {
        if (keywords.IsNullOrEmpty()) throw new ArgumentNullException(nameof(keywords), "keywords 不能为空");

        log.Info("[SearchTable] keywords={0}, connName={1}", keywords, connName);

        var roleIds = GetRoleIds(context);
        var tables = schemaService.SearchTables(keywords, connName);

        // 访问控制过滤（白名单+黑名单均支持通配符 * 和 ?，黑名单在任意情况下优先拦截）
        var allowedTables = new List<IDataTable>();
        foreach (var table in tables)
        {
            var conn = table.ConnName ?? "";
            var name = table.TableName ?? "";

            if (!DbAccessConfig.IsTableAllowed(conn, name, roleIds))
                continue;

            allowedTables.Add(table);
        }

        log.Info("[SearchTable] 匹配 {0} 个表（过滤后 {1} 个）", tables.Count, allowedTables.Count);

        // 按连接分组输出：每组先写连接头，再跟该连接下的表 XML
        return BuildGroupedResult(allowedTables, connName, roleIds);
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
    [Description("在指定数据库连接上执行SQL查询，返回结果集。仅允许SELECT和安全的INSERT/UPDATE操作，禁止DDL和DELETE")]
    public String QuerySql(
        [Description("数据库连接名")] String connName,
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
    /// <param name="allowedTables">已过滤的表列表</param>
    /// <param name="connName">限定连接名</param>
    /// <param name="roleIds">角色ID列表</param>
    /// <returns>分组后的搜索结果文本</returns>
    private static String BuildGroupedResult(List<IDataTable> allowedTables, String? connName, Int32[] roleIds)
    {
        if (allowedTables.Count == 0)
        {
            var availableConns = GetAvailableConnNames(connName, roleIds);
            var connList = availableConns.Count > 0
                ? "可用连接: " + String.Join(", ", availableConns)
                : "无可用连接";
            return $"未找到匹配表。{connList}";
        }

        // 按连接名分组
        var groups = allowedTables
            .GroupBy(t => t.ConnName ?? "")
            .OrderBy(g => g.Key)
            .ToList();

        var sb = new StringBuilder();
        foreach (var group in groups)
        {
            var header = BuildConnectionHeader(group.Key);
            sb.AppendLine(header);

            var groupTables = group.ToList();
            var xml = DAL.Export(groupTables);
            if (!xml.IsNullOrEmpty())
                sb.AppendLine(xml);
        }

        return sb.ToString();
    }

    #endregion

    #region 日志

    private ILog Log { get; } = log;

    #endregion
}
