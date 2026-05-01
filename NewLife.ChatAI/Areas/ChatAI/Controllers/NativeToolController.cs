using Microsoft.AspNetCore.Mvc;
using NewLife.AI.Tools;
using NewLife.Cube;
using NewLife.Log;
using NewLife.Web;
using XCode.Membership;

namespace NewLife.ChatAI.Areas.ChatAI.Controllers;

/// <summary>内置工具。系统内置的.NET工具函数，启动时自动扫描注册，管理员可在后台管理</summary>
[Menu(100, true, Icon = "fa-table")]
[ChatAIArea]
public class NativeToolController(ToolRegistry registry) : EntityController<NativeTool>
{
    static NativeToolController()
    {
        LogOnChange = true;

        ListFields.RemoveField("ClassName", "RoleIds", "DepartmentIds", "Providers", "Endpoint", "ApiKey");
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

    /// <summary>重新扫描并同步所有内置工具元数据到数据库。可在修改工具代码后手动触发，无需重启应用</summary>
    /// <returns></returns>
    [EntityAuthorize(PermissionFlags.Update)]
    public ActionResult Resync()
    {
        var count = registry.SyncNativeTools(NativeTool.FindByName, static e => e.Save(), XTrace.WriteException);
        XTrace.WriteLine("手动触发内置工具重新扫描，处理 {0} 个工具", count);
        return JsonRefresh($"重新扫描完成，处理 {count} 个工具");
    }

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