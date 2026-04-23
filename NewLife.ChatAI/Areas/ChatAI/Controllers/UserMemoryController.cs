using Microsoft.AspNetCore.Mvc;
using NewLife.ChatAI.Entity;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
using NewLife.Log;
using NewLife.Web;
using XCode.Membership;
using static NewLife.ChatAI.Entity.UserMemory;

namespace NewLife.ChatAI.Areas.ChatAI.Controllers;

/// <summary>用户记忆。AI从对话和反馈中提取的用户信息碎片，是自学习系统的原始数据</summary>
[Menu(40, false, Icon = "fa-table", LastUpdate = "20260406")]
[ChatAIArea]
public class UserMemoryController : ChatEntityController<UserMemory>
{
    static UserMemoryController()
    {
        //LogOnChange = true;

        //ListFields.RemoveField("Id", "Creator");
        ListFields.RemoveCreateField().RemoveRemarkField();
        ListFields.AddListField("Value", null, "Key");

        //{
        //    var df = ListFields.GetField("Code") as ListField;
        //    df.Url = "?code={Code}";
        //    df.Target = "_blank";
        //}
        //{
        //    var df = ListFields.AddListField("devices", null, "Onlines");
        //    df.DisplayName = "查看设备";
        //    df.Url = "Device?groupId={Id}";
        //    df.DataVisible = e => (e as UserMemory).Devices > 0;
        //    df.Target = "_frame";
        //}
        //{
        //    var df = ListFields.GetField("Kind") as ListField;
        //    df.GetValue = e => ((Int32)(e as UserMemory).Kind).ToString("X4");
        //}
        ListFields.TraceUrl("TraceId");
    }

    //private readonly ITracer _tracer;

    //public UserMemoryController(ITracer tracer)
    //{
    //    _tracer = tracer;
    //}

    /// <summary>高级搜索。列表页查询、导出Excel、导出Json、分享页等使用</summary>
    /// <param name="p">分页器。包含分页排序参数，以及Http请求参数</param>
    /// <returns></returns>
    protected override IEnumerable<UserMemory> Search(Pager p)
    {
        var userId = p["userId"].ToInt(-1);
        var conversationId = p["conversationId"].ToLong(-1);
        var category = p["category"];
        var enable = p["enable"]?.ToBoolean();

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return UserMemory.Search(userId, -1, conversationId, category, enable, start, end, p["Q"], p);
    }
}