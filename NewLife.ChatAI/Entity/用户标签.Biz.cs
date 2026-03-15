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

public partial class UserTag : Entity<UserTag>
{
    #region 对象操作
    private static Int32 MaxCacheCount = 1000;

    static UserTag()
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
    /// <summary>保存标签（已存在则更新权重，不存在则新增）</summary>
    /// <param name="userId">用户ID</param>
    /// <param name="tagName">标签名</param>
    /// <param name="category">分类</param>
    /// <param name="weight">权重</param>
    /// <returns>实体对象</returns>
    public static UserTag Upsert(Int32 userId, String tagName, String category, Int32 weight = 50)
    {
        var tag = Find(_.UserId == userId & _.Name == tagName);
        if (tag != null)
        {
            tag.Weight = weight;
            tag.Category = category;
            tag.Update();
            return tag;
        }

        tag = new UserTag { UserId = userId, Name = tagName, Category = category, Weight = weight };
        tag.Insert();
        return tag;
    }
    #endregion

    #region 日志
    private static NewLife.Log.ILog _log;
    /// <summary>日志对象</summary>
    public static NewLife.Log.ILog Log { get => _log ??= LogProvider.Provider?.AsLog(typeof(UserTag).Name) ?? Logger.Null; set => _log = value; }

    /// <summary>写日志</summary>
    /// <param name="format">格式</param>
    /// <param name="args">参数</param>
    public static void WriteLog(String format, params Object[] args) => Log?.Info(format, args);
    #endregion
}
