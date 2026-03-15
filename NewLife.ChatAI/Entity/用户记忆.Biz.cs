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

public partial class UserMemory : Entity<UserMemory>
{
    #region 对象操作
    private static Int32 MaxCacheCount = 1000;

    static UserMemory()
    {
        Meta.Interceptors.Add<TimeInterceptor>();
    }

    /// <summary>验证并修补数据，返回验证结果，或者通过抛出异常的方式提示验证失败。</summary>
    /// <param name="method">添删改方法</param>
    public override Boolean Valid(DataMethod method)
    {
        if (!HasDirty) return true;
        if (!base.Valid(method)) return false;

        // 新插入时默认激活
        if (method == DataMethod.Insert && !Dirtys[nameof(IsActive)]) IsActive = true;

        return true;
    }
    #endregion

    #region 扩展属性
    #endregion

    #region 扩展查询
    #endregion

    #region 业务操作
    /// <summary>将记忆标记为无效</summary>
    public void Deactivate()
    {
        IsActive = false;
        Update();
    }
    #endregion

    #region 日志
    private static NewLife.Log.ILog _log;
    /// <summary>日志对象</summary>
    public static NewLife.Log.ILog Log { get => _log ??= LogProvider.Provider?.AsLog(typeof(UserMemory).Name) ?? Logger.Null; set => _log = value; }

    /// <summary>写日志</summary>
    /// <param name="format">格式</param>
    /// <param name="args">参数</param>
    public static void WriteLog(String format, params Object[] args) => Log?.Info(format, args);
    #endregion
}
