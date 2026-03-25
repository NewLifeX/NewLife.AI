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

public partial class AppKey : Entity<AppKey>
{
    #region 对象操作
    // 控制最大缓存数量，Find/FindAll查询方法在表行数小于该值时走实体缓存
    private static Int32 MaxCacheCount = 1000;

    static AppKey()
    {
        // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
        //var df = Meta.Factory.AdditionalFields;
        //df.Add(nameof(UserId));

        // 拦截器 UserInterceptor、TimeInterceptor、IPInterceptor
        Meta.Interceptors.Add(new UserInterceptor { AllowEmpty = false });
        Meta.Interceptors.Add<TimeInterceptor>();
        Meta.Interceptors.Add(new IPInterceptor { AllowEmpty = false });

        // 实体缓存
        // var ec = Meta.Cache;
        // ec.Expire = 60;
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

        // 模型列表有变化时清空缓存
        if (Dirtys[nameof(Models)]) _allowedModels = null;

        return true;
    }

    ///// <summary>首次连接数据库时初始化数据，仅用于实体类重载，用户不应该调用该方法</summary>
    //[EditorBrowsable(EditorBrowsableState.Never)]
    //protected override void InitData()
    //{
    //    // InitData一般用于当数据表没有数据时添加一些默认数据，该实体类的任何第一次数据库操作都会触发该方法，默认异步调用
    //    if (Meta.Session.Count > 0) return;

    //    if (XTrace.Debug) XTrace.WriteLine("开始初始化AppKey[应用密钥]数据……");

    //    var entity = new AppKey();
    //    entity.UserId = 0;
    //    entity.Name = "abc";
    //    entity.Secret = "abc";
    //    entity.Enable = true;
    //    entity.ExpireTime = DateTime.Now;
    //    entity.LastCallTime = DateTime.Now;
    //    entity.Calls = 0;
    //    entity.TotalTokens = 0;
    //    entity.Insert();

    //    if (XTrace.Debug) XTrace.WriteLine("完成初始化AppKey[应用密钥]数据！");
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

    // Select Count(Id) as Id,Category From AppKey Where CreateTime>'2020-01-24 00:00:00' Group By Category Order By Id Desc limit 20
    //static readonly FieldCache<AppKey> _CategoryCache = new(nameof(Category))
    //{
    //Where = _.CreateTime > DateTime.Today.AddDays(-30) & Expression.Empty
    //};

    ///// <summary>获取类别列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>
    ///// <returns></returns>
    //public static IDictionary<String, String> GetCategoryList() => _CategoryCache.FindAllName();
    #endregion

    #region 业务操作
    /// <summary>归一化模型限制字符串。支持逗号、中文逗号、空白和换行分隔，返回逗号拼接结果</summary>
    /// <param name="models">原始模型限制输入</param>
    /// <returns></returns>
    public static String NormalizeModels(String? models)
    {
        if (String.IsNullOrWhiteSpace(models)) return String.Empty;

        var set = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
        var items = models.Split([',', '，', ';', '；', '\r', '\n', '\t', ' '], StringSplitOptions.RemoveEmptyEntries);
        foreach (var item in items)
        {
            var value = item.Trim();
            if (value.Length > 0) set.Add(value);
        }

        return set.Count > 0 ? String.Join(",", set) : String.Empty;
    }

    private HashSet<String>? _allowedModels;

    /// <summary>获取当前密钥允许的模型集合（模型名称/编码），结果已缓存，仅在Models字段变更时重新计算</summary>
    /// <returns></returns>
    public HashSet<String> GetAllowedModels()
    {
        if (_allowedModels != null) return _allowedModels;

        var set = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
        var text = NormalizeModels(Models);
        if (!text.IsNullOrEmpty())
        {
            foreach (var item in text.Split(','))
            {
                var value = item.Trim();
                if (value.Length > 0) set.Add(value);
            }
        }

        return _allowedModels = set;
    }
    #endregion
}
