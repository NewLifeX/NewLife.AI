using Microsoft.AspNetCore.Mvc;
using NewLife.ChatAI.Entity;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
using NewLife.Log;
using NewLife.Web;
using XCode.Membership;
using static NewLife.ChatAI.Entity.MessageFeedback;
using NewLife.AI.Models;

namespace NewLife.ChatAI.Areas.ChatAI.Controllers;

/// <summary>消息反馈。用户对AI回复的点赞或点踩</summary>
[Menu(140, false, Icon = "fa-table", LastUpdate = "20260406")]
[ChatAIArea]
public class MessageFeedbackController : ChatEntityController<MessageFeedback>
{
    static MessageFeedbackController()
    {
        //LogOnChange = true;

        //ListFields.RemoveField("Id", "Creator");
        ListFields.RemoveCreateField().RemoveRemarkField();

        //{
        //    var df = ListFields.GetField("Code") as ListField;
        //    df.Url = "?code={Code}";
        //    df.Target = "_blank";
        //}
        //{
        //    var df = ListFields.AddListField("devices", null, "Onlines");
        //    df.DisplayName = "查看设备";
        //    df.Url = "Device?groupId={Id}";
        //    df.DataVisible = e => (e as MessageFeedback).Devices > 0;
        //    df.Target = "_frame";
        //}
        //{
        //    var df = ListFields.GetField("Kind") as ListField;
        //    df.GetValue = e => ((Int32)(e as MessageFeedback).Kind).ToString("X4");
        //}
        //ListFields.TraceUrl("TraceId");
    }

    /// <summary>高级搜索。列表页查询、导出Excel、导出Json、分享页等使用</summary>
    /// <param name="p">分页器。包含分页排序参数，以及Http请求参数</param>
    /// <returns></returns>
    protected override IEnumerable<MessageFeedback> Search(Pager p)
    {
        var messageId = p["messageId"].ToLong(-1);
        var userId = p["userId"].ToInt(-1);
        var feedbackType = (FeedbackType)p["feedbackType"].ToInt(-1);
        var allowTraining = p["allowTraining"]?.ToBoolean();
        var conversationId = p["conversationId"].ToLong(-1);

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return MessageFeedback.Search(conversationId, messageId, userId, feedbackType, allowTraining, start, end, p["Q"], p);
    }
}