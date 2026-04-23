using NewLife.AI.Models;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
using NewLife.Web;
using XCode.Membership;
using ChatMessage = NewLife.ChatAI.Entity.ChatMessage;

namespace NewLife.ChatAI.Areas.ChatAI.Controllers;

/// <summary>对话消息。会话中的单条发言，包括用户消息和AI回复</summary>
[Menu(150, false, Icon = "fa-table")]
[ChatAIArea]
public class ChatMessageController : ChatEntityController<ChatMessage>
{
    static ChatMessageController()
    {
        //LogOnChange = true;

        //ListFields.RemoveField("Id", "Creator");
        ListFields.RemoveCreateField().RemoveRemarkField();

        //{
        //    var df = ListFields.GetField("Code") as ListField;
        //    df.Url = "?code={Code}";
        //    df.Target = "_blank";
        //}
        {
            var df = ListFields.AddListField("Content", null, "ThinkingMode");
            df.TextAlign = TextAligns.Nowrap;
        }
        //{
        //    var df = ListFields.GetField("Kind") as ListField;
        //    df.GetValue = e => ((Int32)(e as ChatMessage).Kind).ToString("X4");
        //}
        ListFields.TraceUrl("TraceId");
    }

    /// <summary>高级搜索。列表页查询、导出Excel、导出Json、分享页等使用</summary>
    /// <param name="p">分页器。包含分页排序参数，以及Http请求参数</param>
    /// <returns></returns>
    protected override IEnumerable<ChatMessage> Search(Pager p)
    {
        var conversationId = p["conversationId"].ToLong(-1);
        var parentId = p["parentId"].ToLong(-1);
        var thinkingMode = (ThinkingMode)p["thinkingMode"].ToInt(-1);

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return ChatMessage.Search(conversationId, parentId, thinkingMode, start, end, p["Q"], p);
    }
}