using Microsoft.AspNetCore.Mvc;
using NewLife.ChatAI.Entity;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
using NewLife.Log;
using NewLife.Web;
using XCode.Membership;
using static NewLife.ChatAI.Entity.ChatPreset;

namespace NewLife.ChatAI.Areas.ChatAI.Controllers;

/// <summary>对话预设。保存模型+技能+SystemPrompt组合为预设模板</summary>
[Menu(60, true, Icon = "fa-table")]
[ChatAIArea]
public class ChatPresetController : ChatEntityController<ChatPreset>
{
    static ChatPresetController()
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
        //    df.DataVisible = e => (e as ChatPreset).Devices > 0;
        //    df.Target = "_frame";
        //}
        //{
        //    var df = ListFields.GetField("Kind") as ListField;
        //    df.GetValue = e => ((Int32)(e as ChatPreset).Kind).ToString("X4");
        //}
        //ListFields.TraceUrl("TraceId");
    }

    //private readonly ITracer _tracer;

    //public ChatPresetController(ITracer tracer)
    //{
    //    _tracer = tracer;
    //}

    /// <summary>高级搜索。列表页查询、导出Excel、导出Json、分享页等使用</summary>
    /// <param name="p">分页器。包含分页排序参数，以及Http请求参数</param>
    /// <returns></returns>
    protected override IEnumerable<ChatPreset> Search(Pager p)
    {
        var userId = p["userId"].ToInt(-1);
        var modelId = p["modelId"].ToInt(-1);
        var thinkingMode = (NewLife.AI.Models.ThinkingMode)p["thinkingMode"].ToInt(-1);
        var isDefault = p["isDefault"]?.ToBoolean();
        var enable = p["enable"]?.ToBoolean();

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return ChatPreset.Search(userId, modelId, thinkingMode, isDefault, enable, start, end, p["Q"], p);
    }
}