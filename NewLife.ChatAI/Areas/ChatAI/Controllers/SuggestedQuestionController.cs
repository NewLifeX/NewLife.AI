using Microsoft.AspNetCore.Mvc;
using NewLife.ChatData.Entity;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
using NewLife.Log;
using NewLife.Web;
using XCode.Membership;
using static NewLife.ChatData.Entity.SuggestedQuestion;

namespace NewLife.ChatAI.Areas.ChatAI.Controllers;

/// <summary>推荐问题。欢迎页展示的推荐问题，支持缓存响应以加速体验</summary>
[Menu(180, true, Icon = "fa-table")]
[ChatAIArea]
public class SuggestedQuestionController : EntityController<SuggestedQuestion>
{
    static SuggestedQuestionController()
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
        //    df.DataVisible = e => (e as SuggestedQuestion).Devices > 0;
        //    df.Target = "_frame";
        //}
        //{
        //    var df = ListFields.GetField("Kind") as ListField;
        //    df.GetValue = e => ((Int32)(e as SuggestedQuestion).Kind).ToString("X4");
        //}
        //ListFields.TraceUrl("TraceId");
    }

    //private readonly ITracer _tracer;

    //public SuggestedQuestionController(ITracer tracer)
    //{
    //    _tracer = tracer;
    //}

    /// <summary>高级搜索。列表页查询、导出Excel、导出Json、分享页等使用</summary>
    /// <param name="p">分页器。包含分页排序参数，以及Http请求参数</param>
    /// <returns></returns>
    protected override IEnumerable<SuggestedQuestion> Search(Pager p)
    {
        var sort = p["sort"].ToInt(-1);
        var enable = p["enable"]?.ToBoolean();
        var modelId = p["modelId"].ToInt(-1);

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return SuggestedQuestion.Search(sort, enable, modelId, start, end, p["Q"], p);
    }
}