using Microsoft.AspNetCore.Mvc;
using NewLife.ChatAI.Entity;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
using NewLife.Log;
using NewLife.Web;
using XCode.Membership;
using static NewLife.ChatAI.Entity.UsageRecord;

namespace NewLife.ChatAI.Areas.ChatAI.Controllers;

/// <summary>用量记录。每次AI调用的Token消耗，支持按用户和AppKey双维度统计</summary>
[Menu(20, true, Icon = "fa-table")]
[ChatAIArea]
public class UsageRecordController : ReadOnlyEntityController<UsageRecord>
{
    static UsageRecordController()
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
        //    df.DataVisible = e => (e as UsageRecord).Devices > 0;
        //    df.Target = "_frame";
        //}
        //{
        //    var df = ListFields.GetField("Kind") as ListField;
        //    df.GetValue = e => ((Int32)(e as UsageRecord).Kind).ToString("X4");
        //}
        ListFields.TraceUrl("TraceId");
    }

    //private readonly ITracer _tracer;

    //public UsageRecordController(ITracer tracer)
    //{
    //    _tracer = tracer;
    //}

    /// <summary>高级搜索。列表页查询、导出Excel、导出Json、分享页等使用</summary>
    /// <param name="p">分页器。包含分页排序参数，以及Http请求参数</param>
    /// <returns></returns>
    protected override IEnumerable<UsageRecord> Search(Pager p)
    {
        var userId = p["userId"].ToInt(-1);
        var appKeyId = p["appKeyId"].ToInt(-1);
        var conversationId = p["conversationId"].ToLong(-1);
        var modelCode = p["modelCode"];

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return UsageRecord.Search(userId, appKeyId, conversationId, modelCode, start, end, p["Q"], p);
    }
}