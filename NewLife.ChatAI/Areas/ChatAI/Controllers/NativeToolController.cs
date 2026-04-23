using Microsoft.AspNetCore.Mvc;
using NewLife.ChatAI.Entity;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
using NewLife.Log;
using NewLife.Web;
using XCode.Membership;
using static NewLife.ChatAI.Entity.NativeTool;

namespace NewLife.ChatAI.Areas.ChatAI.Controllers;

/// <summary>内置工具。系统内置的.NET工具函数，启动时自动扫描注册，管理员可在后台管理</summary>
[Menu(100, true, Icon = "fa-table")]
[ChatAIArea]
public class NativeToolController : EntityController<NativeTool>
{
    static NativeToolController()
    {
        LogOnChange = true;

        ListFields.RemoveField("ClassName");
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
        //    df.DataVisible = e => (e as NativeTool).Devices > 0;
        //    df.Target = "_frame";
        //}
        //{
        //    var df = ListFields.GetField("Kind") as ListField;
        //    df.GetValue = e => ((Int32)(e as NativeTool).Kind).ToString("X4");
        //}
        //ListFields.TraceUrl("TraceId");
    }

    //private readonly ITracer _tracer;

    //public NativeToolController(ITracer tracer)
    //{
    //    _tracer = tracer;
    //}

    /// <summary>高级搜索。列表页查询、导出Excel、导出Json、分享页等使用</summary>
    /// <param name="p">分页器。包含分页排序参数，以及Http请求参数</param>
    /// <returns></returns>
    protected override IEnumerable<NativeTool> Search(Pager p)
    {
        var enable = p["enable"]?.ToBoolean();
        var isSystem = p["isSystem"]?.ToBoolean();
        var isLocked = p["isLocked"]?.ToBoolean();

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return NativeTool.Search(enable, isSystem, isLocked, start, end, p["Q"], p);
    }
}