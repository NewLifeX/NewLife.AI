using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace NewLife.ChatAI.Entity;

/// <summary>对话附件。上传的文件，关联到消息</summary>
public partial class ChatAttachmentModel
{
    #region 属性
    /// <summary>编号</summary>
    public Int64 Id { get; set; }

    /// <summary>消息。所属消息，发送前为0</summary>
    public Int64 MessageId { get; set; }

    /// <summary>用户。上传用户</summary>
    public Int32 UserId { get; set; }

    /// <summary>文件名</summary>
    public String FileName { get; set; }

    /// <summary>文件路径。服务端存储路径</summary>
    public String FilePath { get; set; }

    /// <summary>内容类型。MIME类型，如image/png</summary>
    public String ContentType { get; set; }

    /// <summary>文件大小。字节数</summary>
    public Int64 FileSize { get; set; }

    /// <summary>创建用户</summary>
    public Int32 CreateUserID { get; set; }

    /// <summary>创建地址</summary>
    public String CreateIP { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; }

    /// <summary>更新用户</summary>
    public Int32 UpdateUserID { get; set; }

    /// <summary>更新地址</summary>
    public String UpdateIP { get; set; }

    /// <summary>更新时间</summary>
    public DateTime UpdateTime { get; set; }
    #endregion

    #region 拷贝
    /// <summary>拷贝模型对象</summary>
    /// <param name="model">模型</param>
    public void Copy(ChatAttachmentModel model)
    {
        Id = model.Id;
        MessageId = model.MessageId;
        UserId = model.UserId;
        FileName = model.FileName;
        FilePath = model.FilePath;
        ContentType = model.ContentType;
        FileSize = model.FileSize;
        CreateUserID = model.CreateUserID;
        CreateIP = model.CreateIP;
        CreateTime = model.CreateTime;
        UpdateUserID = model.UpdateUserID;
        UpdateIP = model.UpdateIP;
        UpdateTime = model.UpdateTime;
    }
    #endregion
}
