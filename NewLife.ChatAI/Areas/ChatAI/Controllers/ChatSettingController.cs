using System.ComponentModel;
using Microsoft.AspNetCore.Mvc.Filters;
using NewLife.AI.ChatAI;
using NewLife.ChatAI.Entity;
using NewLife.ChatAI.Services;
using NewLife.Cube;
using NewLife.Cube.ViewModels;

namespace NewLife.ChatAI.Areas.ChatAI.Controllers;

/// <summary>对话设置控制器</summary>
[DisplayName("对话设置")]
[ChatAIArea]
[Menu(30, true, Icon = "fa-wrench")]
public class ChatSettingController : ConfigController<ChatSetting>
{
    private Boolean _initialized;

    /// <summary>已重载。初始化 DefaultModel 和 DefaultThinkingMode 下拉数据源</summary>
    /// <param name="filterContext"></param>
    public override void OnActionExecuting(ActionExecutingContext filterContext)
    {
        if (!_initialized)
        {
            var list = GetMembers(typeof(ChatSetting));

            var df = list.FirstOrDefault(e => e.Name == nameof(ChatSetting.DefaultModel));
            if (df != null)
                df.DataSource = _ =>
                {
                    var dic = new Dictionary<Int32, String> { [0] = "默认（第一个可用模型）" };
                    foreach (var m in ModelConfig.FindAll().Where(m => m.Enable).OrderBy(m => m.Sort))
                        dic[m.Id] = m.Name;
                    return dic;
                };

            df = list.FirstOrDefault(e => e.Name == nameof(ChatSetting.DefaultThinkingMode));
            if (df != null)
                df.DataSource = _ => EnumHelper.GetDescriptions(typeof(ThinkingMode));

            _initialized = true;
        }

        base.OnActionExecuting(filterContext);
    }
}
