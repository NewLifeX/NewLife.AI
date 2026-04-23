using Microsoft.AspNetCore.Mvc.Filters;
using NewLife.AI.Models;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
using NewLife.ChatAI.Entity;
using NewLife.Web;
using XCode.Membership;

namespace NewLife.ChatAI.Areas.ChatAI.Controllers;

/// <summary>会话。一次完整的多轮对话上下文</summary>
[Menu(160, false, Icon = "fa-table")]
[ChatAIArea]
public class ConversationController : ChatEntityController<Conversation>
{
    static ConversationController()
    {
        //LogOnChange = true;

        ListFields.RemoveField("Id", "UserId", "ModelId", "SkillId", "IsPinned", "LastMessageTime");
        ListFields.RemoveCreateField().RemoveRemarkField();

        //{
        //    var df = ListFields.GetField("MessageCount") as ListField;
        //    df.Url = "/ChatAI/ChatMessage?conversationId={Id}&nav=1";
        //    //df.Target = "_blank";
        //}
        {
            var df = ListFields.GetField("Title") as ListField;
            df.Url = "/ChatAI/Conversation/Detail?Id={Id}&nav=1";
            df.Target = "_blank";
        }
        //{
        //    var df = ListFields.AddListField("devices", null, "Onlines");
        //    df.DisplayName = "查看设备";
        //    df.Url = "Device?groupId={Id}";
        //    df.DataVisible = e => (e as Conversation).Devices > 0;
        //    df.Target = "_frame";
        //}
        //{
        //    var df = ListFields.GetField("Kind") as ListField;
        //    df.GetValue = e => ((Int32)(e as Conversation).Kind).ToString("X4");
        //}
        ListFields.TraceUrl("TraceId");
    }

    /// <summary>已重载。</summary>
    /// <param name="filterContext"></param>
    public override void OnActionExecuting(ActionExecutingContext filterContext)
    {
        base.OnActionExecuting(filterContext);

        var p = new Pager(WebHelper.Params);
        var conversationId = p["Id"].ToLong(-1);
        if (conversationId > 0)
        {
            PageSetting.NavView = "_Conversation_Nav";
            PageSetting.EnableNavbar = false;
        }
    }

    /// <summary>高级搜索。列表页查询、导出Excel、导出Json、分享页等使用</summary>
    /// <param name="p">分页器。包含分页排序参数，以及Http请求参数</param>
    /// <returns></returns>
    protected override IEnumerable<Conversation> Search(Pager p)
    {
        var userId = p["userId"].ToInt(-1);
        var isPinned = p["isPinned"]?.ToBoolean();
        var thinkingMode = (ThinkingMode)p["thinkingMode"].ToInt(-1);
        var modelId = p["modelId"].ToInt(-1);
        var skillId = p["skillId"].ToInt(-1);

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return Conversation.Search(userId, -1, isPinned, p["source"], modelId, skillId, thinkingMode, start, end, p["Q"], p);
    }
}