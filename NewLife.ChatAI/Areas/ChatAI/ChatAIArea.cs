using System.ComponentModel;
using Microsoft.AspNetCore.Mvc.Filters;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.ViewModels;
using XCode;

namespace NewLife.ChatAI.Areas.ChatAI;

[DisplayName("AI对话")]
public class ChatAIArea : AreaBase
{
    public ChatAIArea() : base(nameof(ChatAIArea).TrimEnd("Area")) { }
}

/// <summary>对话控制器基类</summary>
/// <typeparam name="T"></typeparam>
public abstract class ChatEntityController<T> : EntityController<T> where T : Entity<T>, new()
{
    /// <summary>已重载。</summary>
    /// <param name="filterContext"></param>
    public override void OnActionExecuting(ActionExecutingContext filterContext)
    {
        base.OnActionExecuting(filterContext);

        var conversationId = GetRequest("conversationId").ToLong(-1);
        if (conversationId > 0)
        {
            PageSetting.NavView = "_Conversation_Nav";
            PageSetting.EnableNavbar = false;
        }
    }

    protected override FieldCollection OnGetFields(ViewKinds kind, Object model)
    {
        var fields = base.OnGetFields(kind, model);

        if (kind == ViewKinds.List)
        {
            var conversationId = GetRequest("conversationId").ToLong(-1);
            if (conversationId > 0) fields.RemoveField("ConversationId", "ConversationTitle");
        }

        return fields;
    }
}