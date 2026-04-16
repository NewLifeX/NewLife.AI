using NewLife.AI.Models;
using NewLife.ChatData.Entity;
using NewLife.Cube;
using NewLife.Cube.ViewModels;
using NewLife.Web;
using XCode.Membership;

namespace NewLife.ChatAI.Areas.ChatAI.Controllers;

/// <summary>用户设置。用户的个性化配置</summary>
[Menu(100, true, Icon = "fa-table")]
[ChatAIArea]
public class UserSettingController : ChatEntityController<UserSetting>
{
    static UserSettingController()
    {
        //LogOnChange = true;

        //ListFields.RemoveField("Id", "Creator");
        ListFields.RemoveCreateField().RemoveRemarkField();

        {
            var df = ListFields.GetField("UserName") as ListField;
            df.Url = "/ChatAI/UserSetting?userId={UserId}";
            df.Target = "_blank";
        }
        //{
        //    var df = ListFields.AddListField("devices", null, "Onlines");
        //    df.DisplayName = "查看设备";
        //    df.Url = "Device?groupId={Id}";
        //    df.DataVisible = e => (e as UserSetting).Devices > 0;
        //    df.Target = "_frame";
        //}
        //{
        //    var df = ListFields.GetField("Kind") as ListField;
        //    df.GetValue = e => ((Int32)(e as UserSetting).Kind).ToString("X4");
        //}
        //ListFields.TraceUrl("TraceId");
    }

    //private readonly ITracer _tracer;

    //public UserSettingController(ITracer tracer)
    //{
    //    _tracer = tracer;
    //}

    /// <summary>高级搜索。列表页查询、导出Excel、导出Json、分享页等使用</summary>
    /// <param name="p">分页器。包含分页排序参数，以及Http请求参数</param>
    /// <returns></returns>
    protected override IEnumerable<UserSetting> Search(Pager p)
    {
        var userId = p["userId"].ToInt(-1);
        var defaultThinkingMode = (ThinkingMode)p["defaultThinkingMode"].ToInt(-1);
        var responseStyle = (ResponseStyle)p["responseStyle"].ToInt(-1);
        var allowTraining = p["allowTraining"]?.ToBoolean();
        var mcpEnabled = p["mcpEnabled"]?.ToBoolean();
        var showToolCalls = p["showToolCalls"]?.ToBoolean();
        var enableLearning = p["enableLearning"]?.ToBoolean();

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return UserSetting.Search(userId, defaultThinkingMode, responseStyle, allowTraining, mcpEnabled, showToolCalls, enableLearning, start, end, p["Q"], p);
    }
}