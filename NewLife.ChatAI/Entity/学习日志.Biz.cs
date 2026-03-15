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

public partial class LearningLog : Entity<LearningLog>
{
    #region 对象操作
    static LearningLog()
    {
        Meta.Table.DataTable.InsertOnly = true;
        Meta.Interceptors.Add<TimeInterceptor>();
        Meta.Interceptors.Add<IPInterceptor>();
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
    /// <summary>创建一条成功的学习日志</summary>
    /// <param name="userId">用户ID</param>
    /// <param name="conversationId">触发会话ID</param>
    /// <param name="triggerReason">触发原因</param>
    /// <param name="extractedCount">提取记忆数</param>
    /// <param name="elapsedMs">耗时毫秒</param>
    /// <returns>实体对象</returns>
    public static LearningLog CreateSuccess(Int32 userId, Int64 conversationId, String triggerReason, Int32 extractedCount, Int32 elapsedMs)
    {
        var log = new LearningLog
        {
            UserId = userId,
            ConversationId = conversationId,
            TriggerReason = triggerReason,
            ExtractedCount = extractedCount,
            Status = "Success",
            ElapsedMs = elapsedMs,
        };
        log.Insert();
        return log;
    }

    /// <summary>创建一条失败的学习日志</summary>
    /// <param name="userId">用户ID</param>
    /// <param name="conversationId">触发会话ID</param>
    /// <param name="triggerReason">触发原因</param>
    /// <param name="remark">错误信息</param>
    /// <param name="elapsedMs">耗时毫秒</param>
    /// <returns>实体对象</returns>
    public static LearningLog CreateFailed(Int32 userId, Int64 conversationId, String triggerReason, String remark, Int32 elapsedMs)
    {
        var log = new LearningLog
        {
            UserId = userId,
            ConversationId = conversationId,
            TriggerReason = triggerReason,
            Status = "Failed",
            Remark = remark,
            ElapsedMs = elapsedMs,
        };
        log.Insert();
        return log;
    }
    #endregion

    #region 日志
    private static NewLife.Log.ILog _log;
    /// <summary>日志对象</summary>
    public static NewLife.Log.ILog Log { get => _log ??= LogProvider.Provider?.AsLog(typeof(LearningLog).Name) ?? Logger.Null; set => _log = value; }

    /// <summary>写日志</summary>
    /// <param name="format">格式</param>
    /// <param name="args">参数</param>
    public static void WriteLog(String format, params Object[] args) => Log?.Info(format, args);
    #endregion
}
