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

public partial class UserProfile : Entity<UserProfile>
{
    #region 对象操作
    private static Int32 MaxCacheCount = 1000;

    static UserProfile()
    {
        Meta.Interceptors.Add<TimeInterceptor>();
    }

    /// <summary>验证并修补数据，返回验证结果，或者通过抛出异常的方式提示验证失败。</summary>
    /// <param name="method">添删改方法</param>
    public override Boolean Valid(DataMethod method)
    {
        if (!HasDirty) return true;
        if (!base.Valid(method)) return false;
        return true;
    }
    #endregion

    #region 扩展属性
    #endregion

    #region 业务操作
    /// <summary>获取或创建用户画像</summary>
    /// <param name="userId">用户ID</param>
    /// <returns>用户画像实体</returns>
    public static UserProfile GetOrCreate(Int32 userId)
    {
        var profile = FindByUserId(userId);
        if (profile != null) return profile;

        profile = new UserProfile { UserId = userId, AnalyzeCount = 0 };
        profile.Insert();
        return profile;
    }
    #endregion

    #region 日志
    private static NewLife.Log.ILog _log;
    /// <summary>日志对象</summary>
    public static NewLife.Log.ILog Log { get => _log ??= LogProvider.Provider?.AsLog(typeof(UserProfile).Name) ?? Logger.Null; set => _log = value; }

    /// <summary>写日志</summary>
    /// <param name="format">格式</param>
    /// <param name="args">参数</param>
    public static void WriteLog(String format, params Object[] args) => Log?.Info(format, args);
    #endregion
}
