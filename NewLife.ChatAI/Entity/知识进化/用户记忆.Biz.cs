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
        Meta.Interceptors.Add<TraceInterceptor>();
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
    /// <summary>来源会话</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public Conversation Conversation => Extends.Get(nameof(Conversation), k => Conversation.FindById(ConversationId));

    /// <summary>来源会话</summary>
    [Map(nameof(ConversationId), typeof(Conversation), "Id")]
    public String ConversationTitle => Conversation?.Title;
    #endregion

    #region 扩展查询
    /// <summary>获取用户有效记忆，按置信度降序</summary>
    /// <param name="userId">用户</param>
    /// <returns>实体列表</returns>
    public static IList<UserMemory> FindActiveByUserId(Int32 userId)
    {
        if (userId <= 0) return [];

        return FindAll(_.UserId == userId & _.IsActive == true, _.Confidence.Desc(), null, 0, 0);
    }

    /// <summary>根据用户和分类查找记忆</summary>
    /// <param name="userId">用户</param>
    /// <param name="category">分类</param>
    /// <returns>实体列表</returns>
    public static IList<UserMemory> FindAllByUserIdAndCategory(Int32 userId, String category)
    {
        if (userId <= 0) return [];

        var exp = _.UserId == userId & _.IsActive == true;
        if (!category.IsNullOrEmpty()) exp &= _.Category == category;

        return FindAll(exp, _.Confidence.Desc(), null, 0, 0);
    }

    /// <summary>获取指定范围的有效记忆</summary>
    /// <param name="userId">用户</param>
    /// <param name="scope">范围。user/team/global</param>
    /// <returns>实体列表</returns>
    public static IList<UserMemory> FindActiveByScope(Int32 userId, String scope)
    {
        var exp = _.IsActive == true & _.Status == 1;
        if (userId > 0) exp &= _.UserId == userId;
        if (!scope.IsNullOrEmpty()) exp &= _.Scope == scope;

        return FindAll(exp, _.Confidence.Desc(), null, 0, 0);
    }

    /// <summary>获取待审核的记忆</summary>
    /// <param name="count">最大返回数</param>
    /// <returns></returns>
    public static IList<UserMemory> FindPendingReview(Int32 count = 50) => FindAll(_.Status == 0 & _.IsActive == true, _.Id.Asc(), null, 0, count);
    #endregion

    #region 业务操作
    /// <summary>将记忆标记为无效</summary>
    public void Deactivate()
    {
        IsActive = false;
        Update();
    }

    /// <summary>批准记忆</summary>
    /// <param name="reviewUserId">审核人</param>
    public void Approve(Int32 reviewUserId)
    {
        Status = 1;
        ReviewUserId = reviewUserId;
        ReviewTime = DateTime.Now;
        Update();
    }

    /// <summary>拒绝记忆</summary>
    /// <param name="reviewUserId">审核人</param>
    public void Reject(Int32 reviewUserId)
    {
        Status = 2;
        ReviewUserId = reviewUserId;
        ReviewTime = DateTime.Now;
        Update();
    }

    /// <summary>标记为已弃用</summary>
    public void Deprecate()
    {
        Status = 3;
        IsActive = false;
        Update();
    }
    #endregion

    #region 日志
    private static Log.ILog _log;
    /// <summary>日志对象</summary>
    public static Log.ILog Log { get => _log ??= LogProvider.Provider?.AsLog(typeof(UserMemory).Name) ?? Logger.Null; set => _log = value; }

    /// <summary>写日志</summary>
    /// <param name="format">格式</param>
    /// <param name="args">参数</param>
    public static void WriteLog(String format, params Object[] args) => Log?.Info(format, args);
    #endregion
}