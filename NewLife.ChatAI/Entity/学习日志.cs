using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using XCode;
using XCode.Cache;
using XCode.Configuration;
using XCode.DataAccessLayer;

namespace NewLife.ChatAI.Entity;

/// <summary>学习日志。记录自学习分析任务的执行历史及结果</summary>
[Serializable]
[DataObject]
[Description("学习日志。记录自学习分析任务的执行历史及结果")]
[BindIndex("IX_LearningLog_UserId_Id", false, "UserId,Id")]
[BindIndex("IX_LearningLog_ConversationId", false, "ConversationId")]
[BindIndex("IX_LearningLog_Status_Id", false, "Status,Id")]
[BindTable("LearningLog", Description = "学习日志。记录自学习分析任务的执行历史及结果", ConnName = "ChatAI", DbType = DatabaseType.None)]
public partial class LearningLog : IEntity<LearningLogModel>
{
    #region 属性
    private Int64 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, false, false, 0)]
    [BindColumn("Id", "编号", "", DataScale = "time")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int32 _UserId;
    /// <summary>用户</summary>
    [DisplayName("用户")]
    [Description("用户")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("UserId", "用户", "")]
    public Int32 UserId { get => _UserId; set { if (OnPropertyChanging("UserId", value)) { _UserId = value; OnPropertyChanged("UserId"); } } }

    private Int64 _ConversationId;
    /// <summary>触发会话</summary>
    [DisplayName("触发会话")]
    [Description("触发会话")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ConversationId", "触发会话", "")]
    public Int64 ConversationId { get => _ConversationId; set { if (OnPropertyChanging("ConversationId", value)) { _ConversationId = value; OnPropertyChanged("ConversationId"); } } }

    private String _TriggerReason;
    /// <summary>触发原因。Feedback=反馈触发/Scheduled=定时触发/Manual=手动触发</summary>
    [DisplayName("触发原因")]
    [Description("触发原因。Feedback=反馈触发/Scheduled=定时触发/Manual=手动触发")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("TriggerReason", "触发原因。Feedback=反馈触发/Scheduled=定时触发/Manual=手动触发", "")]
    public String TriggerReason { get => _TriggerReason; set { if (OnPropertyChanging("TriggerReason", value)) { _TriggerReason = value; OnPropertyChanged("TriggerReason"); } } }

    private Int32 _ExtractedCount;
    /// <summary>提取记忆数。本次分析新增/更新的记忆数量</summary>
    [DisplayName("提取记忆数")]
    [Description("提取记忆数。本次分析新增/更新的记忆数量")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ExtractedCount", "提取记忆数。本次分析新增/更新的记忆数量", "")]
    public Int32 ExtractedCount { get => _ExtractedCount; set { if (OnPropertyChanging("ExtractedCount", value)) { _ExtractedCount = value; OnPropertyChanged("ExtractedCount"); } } }

    private Int32 _FeedbackCount;
    /// <summary>处理反馈数。本次处理的反馈条数</summary>
    [DisplayName("处理反馈数")]
    [Description("处理反馈数。本次处理的反馈条数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("FeedbackCount", "处理反馈数。本次处理的反馈条数", "")]
    public Int32 FeedbackCount { get => _FeedbackCount; set { if (OnPropertyChanging("FeedbackCount", value)) { _FeedbackCount = value; OnPropertyChanged("FeedbackCount"); } } }

    private String _Status;
    /// <summary>状态。Success=成功/Failed=失败/Skipped=跳过</summary>
    [DisplayName("状态")]
    [Description("状态。Success=成功/Failed=失败/Skipped=跳过")]
    [DataObjectField(false, false, true, 20)]
    [BindColumn("Status", "状态。Success=成功/Failed=失败/Skipped=跳过", "")]
    public String Status { get => _Status; set { if (OnPropertyChanging("Status", value)) { _Status = value; OnPropertyChanged("Status"); } } }

    private Int32 _ElapsedMs;
    /// <summary>耗时毫秒</summary>
    [DisplayName("耗时毫秒")]
    [Description("耗时毫秒")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ElapsedMs", "耗时毫秒", "")]
    public Int32 ElapsedMs { get => _ElapsedMs; set { if (OnPropertyChanging("ElapsedMs", value)) { _ElapsedMs = value; OnPropertyChanged("ElapsedMs"); } } }

    private String _Remark;
    /// <summary>备注。错误信息或跳过原因</summary>
    [Category("扩展")]
    [DisplayName("备注")]
    [Description("备注。错误信息或跳过原因")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Remark", "备注。错误信息或跳过原因", "")]
    public String Remark { get => _Remark; set { if (OnPropertyChanging("Remark", value)) { _Remark = value; OnPropertyChanged("Remark"); } } }

    private DateTime _CreateTime;
    /// <summary>创建时间</summary>
    [Category("扩展")]
    [DisplayName("创建时间")]
    [Description("创建时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("CreateTime", "创建时间", "")]
    public DateTime CreateTime { get => _CreateTime; set { if (OnPropertyChanging("CreateTime", value)) { _CreateTime = value; OnPropertyChanged("CreateTime"); } } }

    private String _CreateIP;
    /// <summary>创建地址</summary>
    [Category("扩展")]
    [DisplayName("创建地址")]
    [Description("创建地址")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("CreateIP", "创建地址", "")]
    public String CreateIP { get => _CreateIP; set { if (OnPropertyChanging("CreateIP", value)) { _CreateIP = value; OnPropertyChanged("CreateIP"); } } }
    #endregion

    #region 拷贝
    /// <summary>拷贝模型对象</summary>
    /// <param name="model">模型</param>
    public void Copy(LearningLogModel model)
    {
        Id = model.Id;
        UserId = model.UserId;
        ConversationId = model.ConversationId;
        TriggerReason = model.TriggerReason;
        ExtractedCount = model.ExtractedCount;
        FeedbackCount = model.FeedbackCount;
        Status = model.Status;
        ElapsedMs = model.ElapsedMs;
        Remark = model.Remark;
        CreateTime = model.CreateTime;
        CreateIP = model.CreateIP;
    }
    #endregion

    #region 获取/设置 字段值
    /// <summary>获取/设置 字段值</summary>
    /// <param name="name">字段名</param>
    /// <returns></returns>
    public override Object this[String name]
    {
        get => name switch
        {
            "Id" => _Id,
            "UserId" => _UserId,
            "ConversationId" => _ConversationId,
            "TriggerReason" => _TriggerReason,
            "ExtractedCount" => _ExtractedCount,
            "FeedbackCount" => _FeedbackCount,
            "Status" => _Status,
            "ElapsedMs" => _ElapsedMs,
            "Remark" => _Remark,
            "CreateTime" => _CreateTime,
            "CreateIP" => _CreateIP,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "UserId": _UserId = value.ToInt(); break;
                case "ConversationId": _ConversationId = value.ToLong(); break;
                case "TriggerReason": _TriggerReason = Convert.ToString(value); break;
                case "ExtractedCount": _ExtractedCount = value.ToInt(); break;
                case "FeedbackCount": _FeedbackCount = value.ToInt(); break;
                case "Status": _Status = Convert.ToString(value); break;
                case "ElapsedMs": _ElapsedMs = value.ToInt(); break;
                case "Remark": _Remark = Convert.ToString(value); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                case "CreateIP": _CreateIP = Convert.ToString(value); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static LearningLog FindById(Int64 id)
    {
        if (id <= 0) return null;
        return Find(_.Id == id);
    }

    /// <summary>根据用户查找学习日志</summary>
    /// <param name="userId">用户</param>
    /// <param name="count">最大数量</param>
    /// <returns>实体列表</returns>
    public static IList<LearningLog> FindAllByUserId(Int32 userId, Int32 count = 20)
    {
        if (userId <= 0) return [];
        return FindAll(_.UserId == userId, _.Id.Desc(), null, 0, count);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="userId">用户</param>
    /// <param name="status">状态</param>
    /// <param name="triggerReason">触发原因</param>
    /// <param name="start">创建时间开始</param>
    /// <param name="end">创建时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数</param>
    /// <returns>实体列表</returns>
    public static IList<LearningLog> Search(Int32 userId, String status, String triggerReason, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();
        if (userId >= 0) exp &= _.UserId == userId;
        if (!status.IsNullOrEmpty()) exp &= _.Status == status;
        if (!triggerReason.IsNullOrEmpty()) exp &= _.TriggerReason == triggerReason;
        exp &= _.CreateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= _.Remark.Contains(key);
        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得学习日志字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");
        /// <summary>用户</summary>
        public static readonly Field UserId = FindByName("UserId");
        /// <summary>触发会话</summary>
        public static readonly Field ConversationId = FindByName("ConversationId");
        /// <summary>触发原因</summary>
        public static readonly Field TriggerReason = FindByName("TriggerReason");
        /// <summary>提取记忆数</summary>
        public static readonly Field ExtractedCount = FindByName("ExtractedCount");
        /// <summary>处理反馈数</summary>
        public static readonly Field FeedbackCount = FindByName("FeedbackCount");
        /// <summary>状态</summary>
        public static readonly Field Status = FindByName("Status");
        /// <summary>耗时毫秒</summary>
        public static readonly Field ElapsedMs = FindByName("ElapsedMs");
        /// <summary>备注</summary>
        public static readonly Field Remark = FindByName("Remark");
        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");
        /// <summary>创建地址</summary>
        public static readonly Field CreateIP = FindByName("CreateIP");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得学习日志字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";
        /// <summary>用户</summary>
        public const String UserId = "UserId";
        /// <summary>触发会话</summary>
        public const String ConversationId = "ConversationId";
        /// <summary>触发原因</summary>
        public const String TriggerReason = "TriggerReason";
        /// <summary>提取记忆数</summary>
        public const String ExtractedCount = "ExtractedCount";
        /// <summary>处理反馈数</summary>
        public const String FeedbackCount = "FeedbackCount";
        /// <summary>状态</summary>
        public const String Status = "Status";
        /// <summary>耗时毫秒</summary>
        public const String ElapsedMs = "ElapsedMs";
        /// <summary>备注</summary>
        public const String Remark = "Remark";
        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";
        /// <summary>创建地址</summary>
        public const String CreateIP = "CreateIP";
    }
    #endregion
}
