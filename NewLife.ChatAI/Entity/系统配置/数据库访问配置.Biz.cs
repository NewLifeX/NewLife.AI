using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using NewLife.Model;
using NewLife.Reflection;
using NewLife.Threading;
using NewLife.Web;
using XCode;
using XCode.Cache;
using XCode.Configuration;
using XCode.DataAccessLayer;
using XCode.Membership;
using XCode.Shards;

namespace NewLife.ChatAI.Entity;

public partial class DbAccessConfig : Entity<DbAccessConfig>
{
    #region 对象操作
    // 控制最大缓存数量，Find/FindAll查询方法在表行数小于该值时走实体缓存
    private static Int32 MaxCacheCount = 1000;

    static DbAccessConfig()
    {
        // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
        //var df = Meta.Factory.AdditionalFields;
        //df.Add(nameof(Sort));

        // 拦截器 UserInterceptor、TimeInterceptor、IPInterceptor
        Meta.Interceptors.Add(new UserInterceptor { AllowEmpty = false });
        Meta.Interceptors.Add<TimeInterceptor>();
        Meta.Interceptors.Add(new IPInterceptor { AllowEmpty = false });

        // 实体缓存
        // var ec = Meta.Cache;
        // ec.Expire = 60;

        // 单对象缓存
        var sc = Meta.SingleCache;
        // sc.Expire = 60;
        sc.FindSlaveKeyMethod = k => Find(_.ConnName == k);
        sc.GetSlaveKeyMethod = e => e.ConnName;
    }

    /// <summary>验证并修补数据，返回验证结果，或者通过抛出异常的方式提示验证失败。</summary>
    /// <param name="method">添删改方法</param>
    public override Boolean Valid(DataMethod method)
    {
        //if (method == DataMethod.Delete) return true;
        // 如果没有脏数据，则不需要进行任何处理
        if (!HasDirty) return true;

        // 建议先调用基类方法，基类方法会做一些统一处理
        if (!base.Valid(method)) return false;

        // 在新插入数据或者修改了指定字段时进行修正

        // 处理当前已登录用户信息，可以由UserInterceptor拦截器代劳
        /*var user = ManageProvider.User;
        if (user != null)
        {
            if (method == DataMethod.Insert && !Dirtys[nameof(CreateUserID)]) CreateUserID = user.ID;
            if (!Dirtys[nameof(UpdateUserID)]) UpdateUserID = user.ID;
        }*/
        //if (method == DataMethod.Insert && !Dirtys[nameof(CreateTime)]) CreateTime = DateTime.Now;
        //if (!Dirtys[nameof(UpdateTime)]) UpdateTime = DateTime.Now;
        //if (method == DataMethod.Insert && !Dirtys[nameof(CreateIP)]) CreateIP = ManageProvider.UserHost;
        //if (!Dirtys[nameof(UpdateIP)]) UpdateIP = ManageProvider.UserHost;

        // 检查唯一索引
        // CheckExist(method == DataMethod.Insert, nameof(ConnName));

        return true;
    }

    ///// <summary>首次连接数据库时初始化数据，仅用于实体类重载，用户不应该调用该方法</summary>
    //[EditorBrowsable(EditorBrowsableState.Never)]
    //protected override void InitData()
    //{
    //    // InitData一般用于当数据表没有数据时添加一些默认数据，该实体类的任何第一次数据库操作都会触发该方法，默认异步调用
    //    if (Meta.Session.Count > 0) return;

    //    if (XTrace.Debug) XTrace.WriteLine("开始初始化DbAccessConfig[数据库访问配置]数据……");

    //    var entity = new DbAccessConfig();
    //    entity.ConnName = "abc";
    //    entity.DbType = "abc";
    //    entity.ConnString = "abc";
    //    entity.WhiteTables = "abc";
    //    entity.BlackTables = "abc";
    //    entity.RoleIds = "abc";
    //    entity.Enable = true;
    //    entity.Sort = 0;
    //    entity.Insert();

    //    if (XTrace.Debug) XTrace.WriteLine("完成初始化DbAccessConfig[数据库访问配置]数据！");
    //}

    ///// <summary>已重载。基类先调用Valid(true)验证数据，然后在事务保护内调用OnInsert</summary>
    ///// <returns></returns>
    //public override Int32 Insert()
    //{
    //    return base.Insert();
    //}

    ///// <summary>已重载。在事务保护范围内处理业务，位于Valid之后</summary>
    ///// <returns></returns>
    //protected override Int32 OnDelete()
    //{
    //    return base.OnDelete();
    //}
    #endregion

    #region 扩展属性
    #endregion

    #region 高级查询

    // Select Count(Id) as Id,Category From DbAccessConfig Where CreateTime>'2020-01-24 00:00:00' Group By Category Order By Id Desc limit 20
    //static readonly FieldCache<DbAccessConfig> _CategoryCache = new(nameof(Category))
    //{
    //Where = _.CreateTime > DateTime.Today.AddDays(-30) & Expression.Empty
    //};

    ///// <summary>获取类别列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>
    ///// <returns></returns>
    //public static IDictionary<String, String> GetCategoryList() => _CategoryCache.FindAllName();
    #endregion

    #region 业务操作
    /// <summary>按连接名查找启用的配置</summary>
    /// <param name="connName">连接名</param>
    /// <returns></returns>
    public static DbAccessConfig? FindEnabledByConnName(String connName)
    {
        var cfg = FindByConnName(connName);
        return cfg is { Enable: true } ? cfg : null;
    }

    /// <summary>获取所有启用的配置列表</summary>
    /// <returns></returns>
    public static IList<DbAccessConfig> FindAllEnabled() => FindAllWithCache().Where(e => e.Enable).ToList();

    /// <summary>按角色获取可访问的配置列表。RoleIds为空表示不限制角色</summary>
    /// <param name="roleIds">用户角色ID集合</param>
    /// <returns></returns>
    public static IList<DbAccessConfig> FindAllByRole(Int32[] roleIds)
    {
        var all = FindAllEnabled();
        if (roleIds == null || roleIds.Length == 0) return all.Where(e => e.RoleIds.IsNullOrEmpty()).ToList();

        var roleSet = new HashSet<Int32>(roleIds);
        return all.Where(e =>
        {
            if (e.RoleIds.IsNullOrEmpty()) return true;

            foreach (var id in e.RoleIds.Split(','))
            {
                if (id.ToInt() is var rid && rid > 0 && roleSet.Contains(rid)) return true;
            }
            return false;
        }).ToList();
    }

    /// <summary>获取指定连接名下允许访问的表名集合。白名单优先，无白名单时黑名单排除</summary>
    /// <param name="connName">连接名</param>
    /// <param name="roleIds">用户角色ID集合</param>
    /// <returns>允许访问的表名集合，若连接不存在或无权限返回空集合。白名单支持 * 和 ? 通配符模式，由调用方使用 StringHelper.IsMatch 匹配</returns>
    public static HashSet<String> GetAllowedTables(String connName, Int32[] roleIds)
    {
        var cfg = FindEnabledByConnName(connName);
        if (cfg == null) return [];

        // 角色检查
        if (!cfg.RoleIds.IsNullOrEmpty() && roleIds != null && roleIds.Length > 0)
        {
            var roleSet = new HashSet<Int32>(roleIds);
            var hasRole = false;
            foreach (var id in cfg.RoleIds.Split(','))
            {
                if (id.ToInt() is var rid && rid > 0 && roleSet.Contains(rid))
                {
                    hasRole = true;
                    break;
                }
            }
            if (!hasRole) return [];
        }

        // 白名单优先。可能包含通配符模式（如 "User*"），调用方需使用 IsMatch 匹配
        if (!cfg.WhiteTables.IsNullOrEmpty())
        {
            var set = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in cfg.WhiteTables.Split(','))
            {
                var name = t.Trim();
                if (!name.IsNullOrEmpty()) set.Add(name);
            }
            return set;
        }

        // 黑名单。无白名单时由 IsTableAllowed 做 IsMatch 匹配
        // 此处返回空表示"无白名单"，由调用方通过 IsTableAllowed 单独判断
        return [];
    }

    /// <summary>判断指定表是否允许访问。白名单和黑名单均支持 * 和 ? 通配符模式</summary>
    /// <param name="connName">连接名</param>
    /// <param name="tableName">表名</param>
    /// <param name="roleIds">用户角色ID集合</param>
    /// <returns></returns>
    public static Boolean IsTableAllowed(String connName, String tableName, Int32[] roleIds)
    {
        var cfg = FindEnabledByConnName(connName);
        if (cfg == null) return false;

        // 角色检查
        if (!cfg.RoleIds.IsNullOrEmpty() && roleIds != null && roleIds.Length > 0)
        {
            var roleSet = new HashSet<Int32>(roleIds);
            var hasRole = false;
            foreach (var id in cfg.RoleIds.Split(','))
            {
                if (id.ToInt() is var rid && rid > 0 && roleSet.Contains(rid))
                {
                    hasRole = true;
                    break;
                }
            }
            if (!hasRole) return false;
        }

        // ── 黑名单优先 ──
        // 匹配任意黑名单模式 → 直接拒绝，不受白名单影响
        if (!cfg.BlackTables.IsNullOrEmpty())
        {
            foreach (var t in cfg.BlackTables.Split(','))
            {
                var pattern = t.Trim();
                if (!pattern.IsNullOrEmpty() && pattern.IsMatch(tableName, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
        }

        // ── 白名单其次 ──
        if (!cfg.WhiteTables.IsNullOrEmpty())
        {
            // 匹配任意白名单模式 → 通过
            foreach (var t in cfg.WhiteTables.Split(','))
            {
                var pattern = t.Trim();
                if (!pattern.IsNullOrEmpty() && pattern.IsMatch(tableName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            // 有白名单但都不匹配 → 拒绝
            return false;
        }

        // ── 无白名单 → 默认允许（黑名单已检查通过）──
        return true;
    }

    /// <summary>获取连接配置（含动态连接字符串）。优先从DAL获取，其次从配置</summary>
    /// <param name="connName">连接名</param>
    /// <returns></returns>
    public static DbAccessConfig? GetEffectiveConfig(String connName)
    {
        var cfg = FindEnabledByConnName(connName);
        if (cfg != null) return cfg;

        // DAL中有该连接但没有配置记录时，尝试从DAL获取基础信息
        var dal = DAL.Create(connName);
        if (dal != null && dal.DbType != DatabaseType.None)
            return new DbAccessConfig { ConnName = connName, DbType = dal.DbType + "", Enable = true };

        return null;
    }
    #endregion
}
